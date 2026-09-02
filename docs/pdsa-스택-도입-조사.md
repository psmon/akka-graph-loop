# pdsa 스택 도입 조사 — Akka.NET Streams 미사용 스펙 + 주변 스택

> 조사일: 2026-09-03 · 대상: `pdsa` CLI v0.0.7 (Akka.Streams 1.5.70 / .NET 10 / Native AOT)
> 관점: **안전성**(실패 격리·데이터 무결성) · **효율성**(지연·크기·메모리) · **도입 비용** · **도입 후 추가 체크**
> 이 문서의 모든 "사용/미사용", "존재/부재" 판정은 아래 §1 의 실측 근거에 기반한다(기억이 아닌 검증).

> 📌 **도입 결과(2026-09-03, PR #4)**: **N1·N2·N3 구현 완료**.
> 실패 주입 20회 → 고아 사이클 0개(기준선 100%), 259 테스트 통과, 바이너리·지연 회귀 없음.
> B1(Kùzu 트랜잭션)은 "미검증"에서 **검증·채택**으로 바뀌었다 — C API 바인딩에서 정상 동작해
> 보상 삭제 폴백이 불필요했고, N1 이 그 위에 구현됐다.
> 남은 항목: N4(`pdsa doctor`), N5(`--json` 골든 테스트), Akka 스펙(§3 재평가 트리거 대기).

---

## 0. 요약 — 판정표

| # | 후보 | 판정 | 한 줄 근거 |
|---|---|---|---|
| N1 | **쓰기 순서 교정 + 고아 사이클 정리** | **now** | LLM 실패 1회 = 고아 사이클 1개 생성을 실측 확인(재현율 2/2). 의존성 0 |
| N2 | **단계별 계측치를 Phase 노드에 기록** | **now** | Study 판정을 뒷받침할 데이터가 그래프에 전혀 없음. 의존성 0 |
| N3 | **HTTP 일시 실패 제한적 재시도(지수 백오프+지터)** | **now** | 재시도 코드 0줄, 60초 단일 시도. 의존성 0(약 25줄) |
| N4 | `pdsa doctor` 진단·복구 명령 | next | N1/N2 도입 후 기존 DB 의 고아·결측을 정리할 수단 필요 |
| N5 | `--json` 계약 골든 테스트 | next | 에이전트가 의존하는 계약인데 회귀 방어 장치 없음 |
| A1 | Akka **RestartFlow/RestartSource** (백오프) | later | API 존재 확인. 단 명령 경로에 ActorSystem 도입 비용이 이득보다 큼 |
| A2 | Akka **KillSwitches** | later | `run`/`view` 의 Ctrl+C 정리에만 유효. 현재 실피해 미확인 |
| A3 | Akka **Supervision(Decider)** | later | 사이클 배치 실행 기능이 생길 때 유효 |
| A4 | Akka **Throttle** | later | 다중 프로젝트 동시 실행이 실제 레이트리밋에 걸릴 때 |
| A5 | Akka **AlsoTo/DivertTo/GroupedWithin/Conflate** | later | N2 가 배치 기록으로 확장될 때의 구현 수단 |
| A6 | Akka **Streams TestKit** | **이미 사용 중** | `tests` 에 참조·사용 중 |
| A7 | Akka **RetryFlow** | **reject(부재)** | Akka.NET 1.5.70 에 **존재하지 않음** — JVM 전용 API |
| A8 | Akka.Persistence / Streams.Kafka / Cluster | reject | 단일 사용자 로컬 CLI. AOT 크기·기동 비용만 증가 |
| B1 | Kùzu 명시적 트랜잭션 | **채택(검증 완료)** | C API 바인딩에서 정상 동작 확인 → N1 이 이 위에 구현됨 |
| B2 | Polly / Microsoft.Extensions.Http.Resilience | reject | N3(25줄)로 충분. AOT 39MB 바이너리에 DI 스택 추가는 과함 |
| B3 | OpenTelemetry | reject(CLI) / later(Viewer) | 수명 200~300ms 프로세스에 익스포터 부적합. N2 가 대체 |
| B4 | System.Threading.RateLimiting | reject | A4 와 중복. 현재 병목 근거 없음 |

---

## 1. 실측 근거 (조사의 출발점)

### 1.1 현재 코드가 실제로 쓰는 것 / 안 쓰는 것

| 항목 | 확인 방법 | 결과 |
|---|---|---|
| 패키지 참조 | 전 `*.csproj` grep | `Akka.Streams` = Core·Samples·Tests 만. **`src/pdsa-cli` 는 Akka 패키지 직접 참조 없음**(Core 경유) |
| Akka 사용 지점 | `IPdsaEngine` 참조 검색 | `RunCommand.cs:13` **단 1곳** |
| 실제 PDSA 명령 경로 | `PdsaWorkflow.cs` / `PdsaGraphStore.cs` 의 `using` | **Akka 없음** — `plan/do/study/act` 는 순수 동기 Kùzu 호출 |
| 트랜잭션 | `BEGIN/COMMIT/ROLLBACK` grep in `Kuzu/*.cs` | **0건** |
| 고아 정리 로직 | `DELETE\|abort\|orphan\|Cleanup` grep in `PdsaWorkflow.cs` | **0건** |
| 재시도 | `Retry\|Task.Delay` grep in `OpenAiClient.cs` | **0건**. `HttpClient.Timeout = 60s` 단일 시도 |

> **가장 중요한 구조적 사실**: 이 저장소의 간판인 Akka.Streams 피드백 사이클은
> **데모(`pdsa run`)에만** 쓰이고, 사용자가 실제로 도는 PDSA 사이클(`plan→do→study→act`)에는
> 쓰이지 않는다. 따라서 "Akka 스펙을 더 영입한다"는 질문은 대부분
> **"명령 경로에 ActorSystem 을 새로 들일 가치가 있는가"** 라는 질문으로 환원된다. (→ §3 결론: 현시점 아니오)

### 1.2 Akka.NET Streams 1.5.70 API 존재 여부 (XML 문서 실검색)

`~/.nuget/packages/akka.streams/1.5.70/lib/net6.0/Akka.Streams.xml` 검색 결과:

| API | 히트 | 판정 |
|---|---|---|
| `RestartFlow` / `RestartSource` / `RestartSink` | 7 / 7 / 4 | ✔ 존재 |
| `RestartSettings` (`Create(min,max,randomFactor)`, `WithMaxRestarts`) | ✔ | 백오프·지터·최대횟수 모두 지원 |
| `KillSwitches` / `SharedKillSwitch` | 12 / 28 | ✔ 존재 |
| `Supervision` | 113 | ✔ 존재 |
| `Throttle` / `AlsoTo` / `DivertTo` / `Conflate` / `Aggregate` / `GroupedWithin` / `RecoverWithRetries` / `WatchTermination` | 36 / 26 / 12 / 26 / 47 / 6 / 7 / 4 | ✔ 모두 존재 |
| **`RetryFlow`** | **0** | ✘ **부재** |

> ⚠️ 계획 단계의 코칭 가설에 `RetryFlow` 가 포함돼 있었으나, **Akka.NET 에는 포팅되지 않았다.**
> 스트림 기반 재시도가 필요하면 `RestartFlow.WithBackoff` + `RestartSettings.WithMaxRestarts` 를 써야 한다.
> (JVM Akka 문서를 근거로 삼으면 안 되는 대표 사례 — .NET 포트는 API 커버리지가 다르다.)

### 1.3 실패 주입 실험 — 안전성 결함 재현 (핵심 발견)

폐기용 프로젝트에 `OPENAI_BASE_URL=http://127.0.0.1:9/v1` 을 그 호출에만 주입해 LLM 실패를 강제:

```
$ OPENAI_BASE_URL=http://127.0.0.1:9/v1 pdsa plan "probe" --project pdsa-probe
오류/Error: 대상 컴퓨터에서 연결을 거부했으므로 연결하지 못했습니다. (127.0.0.1:9)
$ echo $?
1                                    # ✔ 종료코드는 정상적으로 실패를 알림

$ pdsa status --project pdsa-probe
누적 사이클: 1개
최근 사이클:
  #1  [planning]  2026-09-02 15:53:30   # ✘ Phase 0개짜리 고아 사이클이 남음
```

원인은 `PlanCommand.cs:28` 의 **쓰기 순서**다.

```csharp
var cid = s.Workflow.StartCycle(reinforceOf);          // ① 그래프에 Cycle 노드 + HAS_CYCLE/NEXT 엣지 먼저 커밋
var coaching = await Spinner.RunAsync(..., ct);        // ② 여기서 던지면 ①이 롤백되지 않음
s.Workflow.RecordPhase(cid, PlanKind, plan, ...);      // ③ 도달 못 함
```

`StartCycle` 은 `CREATE (:Cycle)` → `CREATE (p)-[:HAS_CYCLE]->` → `CREATE (:NEXT)` 3개 문을
**트랜잭션 없이** 순차 실행하므로, 중간 실패 시 부분 그래프도 남을 수 있다.

**2차 피해까지 실측** — 고아 사이클은 조용히 다음 단계를 흡수한다:

```
$ pdsa do "probe do" --project pdsa-probe
■ [pdsa-probe] 사이클 #2 — Do 기록됨
── Plan→Do 정리 ──
- `Plan`: 입력 없음          # ← Plan 없는 사이클에 Do 가 붙고, LLM 은 그대로 진행
```

**연쇄 결과**: Plan 없는 Do → `Expected` 공백 → Study 가 판정 불가 →
`hitRate` 분모(`판정된 사이클`)와 `NEXT` 체인이 유령 사이클로 오염 → `recall` 이 되먹이는 학습 품질 저하.
즉 **일시적 네트워크 오류 하나가 장기 메모리의 무결성을 훼손**한다. 재현율 2/2 (100%).

### 1.4 효율성 기준선 (도입 비용 판단용)

| 지표 | 실측값 | 측정 방법 |
|---|---|---|
| 배포 바이너리 | **`pdsa.exe` 39MB + `kuzu_shared.dll` 13MB ≈ 52MB / 플랫폼** | 설치본 `ls -lh` |
| CLI 콜드 스타트(LLM 미사용, `status`) | **221 ~ 321 ms** | exe 직접 실행, 3회 반복 |
| LLM 왕복(`check`, 최소 프롬프트) | **2,346 ms** | `pdsa check` 출력 |

> ⚠️ **측정 프로토콜(뒤늦게 바로잡음)**: 콜드 스타트는 반드시 **exe 를 직접 실행**해 잰다.
> 최초 측정은 `pdsa`(npm 이 만든 셸/Node 래퍼)를 거쳐 424 / 422 / 496 ms 가 나왔는데,
> 그중 **약 190ms 는 래퍼 오버헤드**였다. 도입 후 A/B 에서 존재하지 않는 "45% 개선"이 나와
> 원인을 추적하다 발견했다. 위 표는 래퍼를 제외한 값이다 — 측정 도구가 측정 대상에 개입한 사례.

이 기준선이 판정을 좌우한다: 프로세스 수명이 **0.2~3초**인 CLI 에서
ActorSystem 기동이나 OTel 익스포터 플러시는 **고정비가 이득을 초과**한다.

---

## 2. now 후보 — 상세 (지금 도입 권고)

### N1. 쓰기 순서 교정 + 고아 사이클 정리 [now]

- **무엇**: `StartCycle` 을 LLM 호출 **이후**로 옮긴다(성공한 결과만 그래프에 커밋).
  `plan` 시작 시 직전 사이클이 `planning` 상태 + Phase 0개면 **재사용**하거나 정리한다.
- **안전성**: §1.3 의 고아 사이클 생성 경로를 원천 차단. 되먹임 메모리 오염 방지.
- **효율성**: 영향 없음(호출 수 동일). 오히려 실패 시 불필요한 3회 쓰기 제거.
- **도입 비용**: 의존성 0. `PlanCommand` + `PdsaWorkflow` 소폭 수정. `cid` 를 로그에 미리 못 쓰는 정도의 제약.
- **도입 후 추가 체크**:
  - 실패 주입 20회 → 고아 사이클 **0개** (현재 기준선: 20/20 생성)
  - Ctrl+C 취소(`OperationCanceledException`) 경로도 동일하게 0개인지 별도 확인
  - `--json` 의 `cycle` 필드가 성공 시에만 채워지는지(에이전트 계약 변화 여부) 확인
  - 기존 DB 의 과거 고아 사이클은 이 수정으로 **사라지지 않음** → N4 필요

### N2. 단계별 계측치를 Phase 노드에 기록 [now]

- **무엇**: `Phase` 노드에 `latencyMs`, `attempts`, `model`, `promptTokens`, `completionTokens`,
  `llmEnabled` 컬럼을 추가하고 `--json` 에 노출한다.
  (기존 `expected/verdict/actual/reinforce` 화이트리스트 확장 방식과 동일 패턴)
- **안전성**: 중립. 단, **토큰 수·모델명만** 저장하고 프롬프트 원문은 저장하지 않아 유출면을 넓히지 않는다.
- **효율성**: 쓰기 1회당 컬럼 몇 개. 측정 오버헤드 무시 가능.
- **도입 비용**: 의존성 0. `OpenAiClient` 가 `usage` 를 파싱해 반환하도록 확장(응답에 이미 포함됨).
  기존 DB 는 스키마 변경 필요 → `ALTER TABLE ADD` 의 Kùzu 지원 여부 확인 필요.
- **도입 후 추가 체크**:
  - 구 버전 DB 를 새 바이너리로 열었을 때 **깨지지 않는지**(하위 호환) — 가장 중요한 회귀
  - Study 판정 시 계측 데이터가 프롬프트에 실제로 주입되는지
  - `pdsa view` 그래프에 새 속성이 렌더되는지
- **효과 가설**: 근거 데이터 없이 판정되는 사이클 비율 **100% → 0%**.

### N3. HTTP 일시 실패 제한적 재시도 [now]

- **무엇**: 429 / 5xx / `TaskCanceledException`(타임아웃) / 소켓 오류에 한해
  최대 2회 재시도, 지수 백오프 + 지터(예: 0.5s → 1.5s ±30%). `Retry-After` 헤더 존중.
  4xx(인증·잘못된 모델)는 **재시도 금지**. 취소 토큰은 즉시 전파.
- **안전성**: N1 과 결합해야 의미 있음(재시도 중 실패해도 그래프가 더러워지지 않아야 함).
  멱등하지 않은 쓰기는 재시도 대상이 아니므로 중복 기록 위험 없음.
- **효율성**: 성공 경로에 0 오버헤드. 실패 경로만 최대 +2s.
- **도입 비용**: 약 25줄, 의존성 0. **Polly 대비** DI·AOT 트리밍 리스크가 없다.
- **도입 후 추가 체크**:
  - 429 주입 시 자동 복구율 **0% → ≥80%**, 성공 경로 p50 지연 증가 **≤ 0ms**
  - 재시도 횟수가 `attempts` 로 N2 에 기록되는지
  - `check` 명령이 재시도로 인해 장애를 **가려버리지 않는지**(진단 명령은 재시도 1회 제한 권장)
  - 60초 고정 타임아웃이 reasoning 모델에서 부족한 문제는 **별건**(`config timeout` 필요)

---

## 3. Akka.NET Streams 스펙별 판정 — 왜 대부분 later 인가

핵심 논거: 이 스펙들은 **긴 수명의 스트림**에서 값을 낸다. 현재 명령 경로는
프로세스당 LLM 호출 1회 + 그래프 쓰기 몇 회로 끝나는 **0.2~3초짜리 단발 실행**이고,
여기에 ActorSystem 을 세우면 기동 고정비를 매 명령마다 지불한다.

| 스펙 | 존재 | 도입 시 얻는 것 | 왜 지금은 아닌가 | 판정 |
|---|---|---|---|---|
| `RestartFlow.WithBackoff` + `RestartSettings` | ✔ | 백오프·지터·최대횟수를 검증된 구현으로 | N3(25줄)이 같은 효과를 의존성·기동비 0으로 달성 | later |
| `KillSwitches` / `SharedKillSwitch` | ✔ | `run`/`view` 의 협조적 종료 | 현재 `CancellationToken` 으로 충분, 실피해 미확인 | later |
| `Supervision`(Decider: Resume/Restart/Stop) | ✔ | 원소 단위 실패 격리 | 원소가 사이클 1개뿐이라 격리할 대상이 없음 | later |
| `Throttle` | ✔ | LLM 레이트리밋 준수 | 다중 프로젝트 동시 실행이 실제로 429 를 맞을 때 재평가 | later |
| `AlsoTo` / `DivertTo` | ✔ | 본류를 건드리지 않는 계측 사이드채널 | N2 를 스트림으로 확장할 때의 구현 수단 | later |
| `GroupedWithin` / `Conflate` / `Aggregate` | ✔ | 그래프 쓰기 배치화 | 쓰기량이 회당 수 건이라 배치 이득 없음 | later |
| `WatchTermination` | ✔ | 사이클 완료·실패 신호 | 동기 코드에서 `try/finally` 로 충분 | later |
| `RecoverWithRetries` | ✔ | 스트림 대체 소스 폴백 | 폴백 LLM 프로바이더 기능이 생기면 재평가 | later |
| **`RetryFlow`** | **✘ 부재** | — | **Akka.NET 에 없음** | reject |
| `Akka.Persistence` / `Streams.Kafka` / `Cluster` | (별도 패키지) | 분산·영속 | 단일 사용자 로컬 CLI. 52MB 바이너리에 추가 부담 | reject |
| `Akka.Streams.TestKit` | ✔ | 스트림 단위 검증 | **이미 `tests` 에서 사용 중** | 유지 |

**Akka 를 명령 경로에 들일 조건(재평가 트리거)** — 아래 중 하나가 참이 되면 A1~A5 를 다시 검토한다:
1. 한 프로세스에서 **여러 사이클을 배치**로 돌리는 기능(`pdsa batch`)이 생긴다
2. **다중 프로젝트 동시 실행**이 실제 레이트리밋/스로틀링에 걸린다
3. `view` 가 장기 실행 스트림(실시간 갱신/워치)을 갖는다

이 중 무엇도 참이 아닌 지금, 이득은 가설이고 비용(ActorSystem 기동 + AOT 표면 + 복잡도)은 확정이다.

---

## 4. next / reject — 주변 스택

### B1. Kùzu 명시적 트랜잭션 [next — 검증 선행]

N1(순서 교정)은 증상을 없애지만, `StartCycle` 내부의 3문 시퀀스와
`RecordPhase` 의 다중 쓰기는 여전히 **원자적이지 않다**.
Kùzu 는 Cypher 수준의 `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK` 을 문서상 지원하지만,
**이 저장소가 쓰는 C API 바인딩(`KuzuNative.cs`)에서 실제로 동작하는지는 미검증**이다.

- **선행 검증(PoC)**: `kuzu_connection_query("BEGIN TRANSACTION")` → 실패 유도 → `ROLLBACK` 후
  노드가 남지 않는지 확인. 30분 규모.
- **주의**: 트랜잭션 열린 채 프로세스가 죽는 경우의 동작, 그리고 `view` 가 동시에 DB 를 열 때의
  락 충돌(§ `update` 의 `kuzu_shared.dll` 잠금 이슈와 같은 계열)을 반드시 같이 확인.

### N4. `pdsa doctor` [next]

N1 이후에도 **기존 DB 에 이미 쌓인** 고아 사이클은 남는다.
`pdsa doctor` 로 (a) Phase 0개 사이클, (b) Plan 없는 Do, (c) `Expected` 공백인 판정 대상,
(d) 끊긴 `NEXT` 체인을 진단하고 `--repair` 로 정리한다.
**도입 후 체크**: 복구가 `hitRate` 를 조작하지 않는지(삭제 전후 분모 변화를 명시적으로 보고).

### N5. `--json` 골든 테스트 [next]

`--json` 은 에이전트가 파싱하는 **공개 계약**인데 회귀 방어가 없다.
스키마 검증 라이브러리(의존성 추가)보다 **골든 파일 비교 테스트**가 AOT CLI 에 맞다:
필드 추가는 통과, **필드 삭제·타입 변경은 실패**하도록 한다.

### B2. Polly / Http.Resilience [reject]
표준적이고 검증됐지만, 얻는 것이 N3 의 25줄과 거의 같다.
AOT + 단일 파일 배포에서 DI/옵션 스택은 트리밍 경고와 크기 증가를 부른다. **비용 > 이득**.

### B3. OpenTelemetry [reject(CLI) / later(Viewer)]
수명 200~300ms 프로세스는 익스포터 플러시 시간이 작업 시간과 맞먹는다.
게다가 원격 전송은 **프롬프트 내용 유출면**을 새로 만든다.
지금 필요한 건 원격 텔레메트리가 아니라 **Study 가 읽을 로컬 근거 데이터** → **N2 가 정답**.
`view` 가 장기 실행 서버로 발전하면 그때 재검토.

### B4. System.Threading.RateLimiting [reject]
A4(Throttle)와 목적 중복이고, 현재 레이트리밋에 걸린다는 근거가 없다.
멀티프로젝트 동시 실행은 **프로세스가 분리**돼 있어 in-process 리미터가 애초에 효력이 없다.

---

## 5. 도입 순서 제안

```
N1 (원자성)  →  N3 (재시도)  →  N2 (계측)  →  N4 (doctor)  →  N5 (골든 테스트)
   └ 먼저 해야 함: 재시도가 실패해도 그래프가 더러워지지 않는 상태를 먼저 만든다
                     └ N2 의 attempts 필드는 N3 이 있어야 의미가 생긴다
```

Akka 스펙(A1~A5)은 **§3 의 재평가 트리거 3개 중 하나가 참이 될 때** 다시 연다.

## 6. 이 조사가 남기는 검증 가능한 지표

| 지표 | 현재(실측) | 목표 |
|---|---|---|
| LLM 실패 시 고아 사이클 생성률 | **100%** (2/2) | **0%** (20회 주입) |
| 근거 데이터 없이 판정되는 사이클 비율 | **100%** | **0%** |
| 일시적 실패(429/5xx) 자동 복구율 | **0%** | **≥80%** |
| 성공 경로 p50 지연 증가 | — | **≤ 0ms** |
| 배포 바이너리 크기 | 39MB + 13MB | **증가 ≤ 0MB** (N1~N3 모두 의존성 0) |
