# akka-graph-loop

**Akka.NET Streams의 Graph 기능을 조사하고, 각 개념을 실제로 돌려보며 배우는 학습용 프로젝트.**

선형 `Source → Flow → Sink` 파이프라인으로는 표현할 수 없는 **fan-out(1→N 분기)** · **fan-in(N→1 합류)**,
재사용 가능한 **partial graph**, 그리고 이 저장소 이름처럼 가장 까다로운 **사이클/피드백 loop(데드락 vs liveness)** 까지
— Akka.NET Graph DSL의 핵심을 개념·예제·테스트·시각화로 한 번에 정리했다.

## 이 프로젝트가 제공하는 것

- 📄 **조사 문서** — [`docs/akka-net-graph-조사.md`](docs/akka-net-graph-조사.md)
  Graph 개념, junction 카탈로그, partial graph, cycle 데드락 처리 4패턴을 한글로 정리(버전별 API 주의점 포함).
- 🧩 **실행 샘플** — `src/AkkaGraphLoop.Samples`
  GraphDSL 기본 · fan-in/out junction 전체 · partial graph · 사이클 3가지 해법을 실제로 돌려보는 예제.
- 🎬 **TUI 튜토리얼** — 각 그래프의 흐름을 **실제 스트림에 연결된** ASCII 애니메이션으로 단계별 관찰(스텝당 ~5초, ESC 일시정지, Ctrl+C 종료).
- 🔁 **PDSA 루프** — 데밍의 Plan–Do–Study–Act 지속개선 사이클을 실제 피드백 그래프로 구현한 별도 실행 샘플(`-- pdsa`). 각 회차는 스트림 진행 중 **Kùzu 임베디드 그래프 DB**에 실시간 기록되고 Cypher 로 되읽는다.
- 🖥️ **그래프 뷰어** — 기록된 Kùzu 그래프를 **별도 웹 프로젝트**로 시각화(로컬 포트, 외부 CDN 없는 SVG 포스 레이아웃).
- 🛠️ **pdsa-cli** — AI 에이전트가 PDSA 루프를 실행/기록·조회하고 LLM 조언을 받는 공식 CLI. **Native AOT 단일 실행 파일**(Akka+Kùzu 포함, 검증 완료).
- 🧪 **테스트** — `tests/AkkaGraphLoop.Tests`
  xUnit + Akka TestKit 으로 각 junction의 동작과 **사이클의 liveness(데드락 없음)** 를 검증(총 22개).

## 무엇을 배우나

- `GraphDsl.Builder` 로 junction을 연결해 비선형 그래프를 조립하는 법
- fan-out(`Broadcast`/`Balance`/`UnZip`)과 fan-in(`Merge`/`MergePreferred`/`Zip`/`ZipWith`/`Concat`)의 차이와 용법
- `UniformFanInShape` · `Source/Flow.FromGraph` 로 재사용 가능한 컴포넌트를 만드는 법
- 유한 버퍼(bounded) 때문에 사이클이 **데드락**하는 이유와, `MergePreferred` / `Buffer(DropHead)` / `ZipWith 균형+초기주입` 으로 **liveness** 를 확보하는 법

**기술 스택:** `Akka.Streams` 1.5.70 · .NET 10 · xUnit / Akka.TestKit

## 빌드 & 테스트

```bash
dotnet build
dotnet test
```

## TUI 튜토리얼 모드 (기본)

인자 없이 실행하면 **TUI 튜토리얼**이 시작된다. 각 그래프 샘플을 순차로 하나씩 보여주며,
ASCII 다이어그램 위에서 원소가 스테이지를 지날 때마다 활성 노드가 하이라이트된다.

```bash
dotnet run --project src/AkkaGraphLoop.Samples
# 또는
dotnet run --project src/AkkaGraphLoop.Samples -- tui
```

- **실제 스트림에 연결**: 각 junction 사이에 계측 스테이지(`SelectAsync`)를 끼워 넣었고,
  출력은 **실제 `Sink.ForEach`** 가 받은 값이다. 화면의 흐름은 진짜 그래프의 흐름이다.
- **스텝당 ~5초**: 계측 스테이지가 원소를 5초간 붙잡아(=Akka backpressure) 그래프가 스텝 단위로 진행된다.
- **조작키**
  - `ESC` — 현재 작업 **일시정지 / 재개** (backpressure 로 그래프가 그 자리에서 멈춘다)
  - `Ctrl+C` (또는 `Q`) — **종료**

장면 순서: Broadcast+Merge → Balance → UnZip → Zip → ZipWith → Concat → PickMaxOfThree
→ Cycle(MergePreferred) → Cycle(Buffer DropHead) → Cycle(Balanced ZipWith).

## 개별 데모 단발 실행

번호를 주면 해당 그래프를 한 번에(빠르게) 실행하고 결과만 출력한다.

```bash
dotnet run --project src/AkkaGraphLoop.Samples -- <데모번호>
```

| 번호 | 데모 | 번호 | 데모 |
|---|---|---|---|
| 0 | GraphDSL 기본(Broadcast+Merge) | 8 | Partial - Source.FromGraph(홀/짝 페어) |
| 1 | FanOut - Balance | 9 | Partial - Flow.FromGraph |
| 2 | FanOut - UnZip | 10 | Cycle - MergePreferred (해법 1) |
| 3 | FanIn - Zip | 11 | Cycle - Buffer DropHead (해법 2) |
| 4 | FanIn - ZipWith(Max) | 12 | Cycle - Balanced ZipWith (해법 3) |
| 5 | FanIn - Concat | 99 | Cycle - 순진한 데드락 시연(2초 가드) |
| 6 | FanIn - MergePrioritized | | |
| 7 | Partial - PickMaxOfThree | | |

예:

```bash
dotnet run --project src/AkkaGraphLoop.Samples -- 10
# [MergePreferredCycle] => [1, 2, 3, ... , 20]
```

## PDSA 루프 (Deming) — 별도 실행

데밍(W. Edwards Deming)의 **PDSA(Plan–Do–Study–Act)** 지속개선 루프를 **실제 Akka.Streams 피드백 사이클**로
구현한 독립 샘플이다. `Act` 의 결과가 다음 `Plan` 으로 되먹여지며, 품질이 목표에 도달하면 수렴하고 종료한다.
그리고 **각 회차는 스트림 진행 '중' Kùzu 임베디드 그래프 DB에 실시간 기록**되고, 종료 후 Cypher 로 되읽어 보여준다.

```bash
dotnet run --project src/AkkaGraphLoop.Samples -- pdsa
```

```
 seed ─▶ (MergePreferred) ─▶ Plan ─▶ Do ─▶ Study ─▶ Act ─▶ Record(Kùzu) ─▶ TakeWhile(미달) ─▶ Broadcast ─▶ Sink
              ▲                                                                              │
              └───────────────────── (다음 회차 준비) ◀── Feedback ◀────────────────────────┘
```

- MergePreferred 의 우선 포트로 피드백을 넣어 데드락 없이 루프가 흐른다(liveness).
- 목표 품질 도달 시 `TakeWhile(inclusive)` 이 수렴 원소까지 방출하고 루프를 종료(수렴).
- **Record 스테이지**가 각 회차를 `(:Run)-[:HAS_CYCLE]->(:Cycle)` 및 `(:Cycle)-[:NEXT]->(:Cycle)` 그래프로 실시간 기록.
- **데밍 포인트**: 3단계는 PDCA 의 'Check(잘 됐나?)'가 아니라 **'Study(무엇을 배웠나?)'** — 분석적 학습이 루프를 굴린다.

실행 예(그래프 되읽기):

```
■ Kùzu 그래프 되읽기 (경로 길이 NEXT×4):  DB=…\pdsa_kuzu_xxxx
    (:Cycle #1)  품질=65.2  수렴=False
    ...
    (:Cycle #5)  품질=90.0  수렴=True
```

### 내장 그래프 DB: Kùzu

기록에는 **Kùzu**(*"graph DB계의 SQLite/DuckDB"* — 인프로세스 임베디드 그래프 DB, Cypher)를 사용한다.
공식 NuGet 이 없어, C API 를 P/Invoke(`Kuzu/KuzuNative.cs`)하고 네이티브 `libkuzu`(약 12MB)는
**빌드 시 자동 다운로드**한다(`native/Kuzu.targets`, 버전 고정 v0.11.3, 현재 OS/아키텍처에 맞춰 출력 폴더로 복사).
git 에는 바이너리를 커밋하지 않으며 첫 빌드에만 네트워크가 필요하다.

> 참고: "애플이 인수해 오픈소스화한" DB는 **FoundationDB**(2015 인수 → 2018 오픈소스)이지만, 이는 분산 KV 스토어라
> 인프로세스 임베디드 그래프 용도에는 맞지 않아 Kùzu 를 채택했다.

### 그래프 뷰어 (별도 프로젝트, 로컬 포트)

기록된 Kùzu 그래프를 **분리된 웹 프로젝트**(`AkkaGraphLoop.Viewer`)에서 시각화한다.
ASP.NET Core 최소 API 가 DB 를 읽어(읽기 전용) `/api/graph` JSON 으로 제공하고, 외부 CDN 없는
자체 포함 HTML(vanilla JS + SVG 포스 레이아웃)로 그래프를 그린다.

```bash
# 1) 데이터 생성(고정 경로에 기록)
dotnet run --project src/AkkaGraphLoop.Samples -- pdsa
# 2) 뷰어 실행 → 브라우저에서 http://localhost:5099
dotnet run --project src/AkkaGraphLoop.Viewer
```

- 옵션: `--port <번호>`(기본 5099), `--db <경로>`(기본 = 샘플과 공유하는 고정 경로).
- Run(파란 사각형) · Cycle(원, 수렴 시 초록) 노드, `HAS_CYCLE`(점선) · `NEXT`(실선 화살표) 엣지.
- 노드 클릭 시 우측 패널에 속성(품질/수렴 등) 표시, 드래그 이동, 새로고침·재배치 버튼.
- 뷰어는 매 요청마다 DB 를 읽기 전용으로 열고 닫으므로, `-- pdsa` 를 다시 돌린 뒤 새로고침하면 최신 그래프가 보인다.

## pdsa-cli — 공식 CLI 툴 (Native AOT)

AI 에이전트가 PDSA 루프를 실행·기록하고, 그래프를 보고, LLM 조언을 받도록 지원하는 공식 CLI(`pdsa`).
재사용 코어는 별도 라이브러리 **`AkkaGraphLoop.Core`**(PDSA 엔진 + Kùzu)로 추출했고, Samples·Viewer·CLI 가 공유한다.

```bash
pdsa run [--start 45] [--target 90]   # PDSA 루프 실행(Akka 스트림) + Kùzu 그래프 기록
pdsa guide "<질문/상황>"               # OpenAI 로 PDSA 조언(기본 구현)
pdsa view [--port 5099] [--no-open]    # 그래프 DB 뷰어 구동
pdsa version                           # 버전/런타임 정보
```

### Native AOT 빌드 (검증 완료)

```bash
dotnet publish src/pdsa-cli -c Release -r win-x64
# → bin/Release/net10.0/win-x64/publish/pdsa.exe (네이티브 단일 실행 + kuzu_shared.dll)
```

- **Akka.NET + Native AOT 동작 확인**: Akka 는 기본 `ActorSystem.Create(name)` 시 `System.Configuration.ConfigurationManager`(app.config)를
  통해 설정을 읽는데, 이 경로가 AOT/single-file 에서 크래시한다(akka.net #4876/#7246). **명시적 Config(`ConfigurationFactory.Default()`)를
  전달**해 그 경로를 우회하면 AOT 에서 정상 동작한다(`AkkaPdsaEngine` 참고).
- 프리빌트 **Kùzu(C++) 는 P/Invoke** 로 이용(AOT 친화적), OpenAI 는 HttpClient + **소스젠 JSON**(AOT-safe).
- `TrimmerRootAssembly` 로 Akka 어셈블리를 빌드에 통째로 포함.
- 참고: 이 환경에서 AOT 링크에는 VS Build Tools(C++) 가 필요하며, `vswhere.exe` 경로(VS Installer 폴더)가 PATH 에 있어야 한다.

### LLM(OpenAI) 설정

`.secret/openai.json.tmp` 를 `.secret/openai.json` 으로 복사 후 `api_key` 를 채우거나, 환경변수
`OPENAI_API_KEY`(+ `OPENAI_BASE_URL`, `OPENAI_MODEL`)를 설정한다. 실제 키 파일은 git 에 커밋되지 않는다.

## 구조

```
src/AkkaGraphLoop.Core/        # 공유 라이브러리(Samples·Viewer·CLI 가 참조)
  Pdsa/                        #   데밍 PDSA 지속개선 루프(피드백 사이클)
    PdsaLoop.cs                #     Plan/Do/Study/Act 사이클 + 실시간 그래프 기록 스테이지
    PdsaGraphStore.cs          #     IPdsaGraphStore + Kùzu 구현(Cypher 기록)
    PdsaGraphReader.cs         #     그래프 되읽기(뷰어용 노드/엣지 모델)
    PdsaPaths.cs               #     공유 DB 고정 경로
  Kuzu/                        #   Kùzu 임베디드 그래프 DB 인터롭
    KuzuNative.cs / KuzuGraph.cs  #   C API P/Invoke · 얇은 관리형 래퍼
src/AkkaGraphLoop.Samples/     # 그래프 학습 샘플 + TUI 튜토리얼(-- pdsa 콘솔 포함)
  Basics · FanOut · FanIn · Partial · Cycles · Tui/
src/AkkaGraphLoop.Viewer/      # 그래프 뷰어(별도 웹 프로젝트, 로컬 포트)
  Program.cs / ViewerHtml.cs   #   ASP.NET Core 최소 API + 자체 포함 SVG 뷰어
src/pdsa-cli/                  # 공식 CLI 툴 pdsa (Native AOT)
  Program.cs / Cli/            #   진입점 · 명령 라우터/인자 파서
  Commands/                    #   run · guide · view · version
  Engine/                      #   IPdsaEngine + AkkaPdsaEngine(Akka+Kùzu, AOT용 config 우회)
  Llm/                         #   ILlmClient + OpenAiClient(소스젠 JSON) + 설정 로드
  Viewer/ViewerLauncher.cs     #   뷰어 프로세스 구동 장치
native/Kuzu.targets            # libkuzu 네이티브 라이브러리 빌드/게시시 자동 다운로드·포함
tests/AkkaGraphLoop.Tests/
  FanInOutTests.cs / PartialGraphTests.cs / CycleTests.cs
  TuiSceneTests.cs / PdsaTests.cs / PdsaGraphStoreTests.cs
```
