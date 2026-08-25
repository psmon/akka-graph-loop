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

## 구조

```
src/AkkaGraphLoop.Samples/
  Basics/GraphDslBasics.cs     # GraphDSL 기본 폐그래프
  FanOut/FanOutSamples.cs      # Broadcast, Balance, UnZip
  FanIn/FanInSamples.cs        # Merge*, Zip, ZipWith, Concat
  Partial/PartialGraphSamples.cs  # UniformFanInShape, Source/Flow.FromGraph
  Cycles/CycleSamples.cs       # 사이클 데드락과 3가지 해법
  Pdsa/                        # 데밍 PDSA 지속개선 루프(피드백 사이클, -- pdsa)
    PdsaLoop.cs                #   Plan/Do/Study/Act 사이클 + 실시간 그래프 기록 스테이지
    PdsaGraphStore.cs          #   IPdsaGraphStore + Kùzu 구현(Cypher 기록/되읽기)
  Kuzu/                        # Kùzu 임베디드 그래프 DB 인터롭
    KuzuNative.cs / KuzuGraph.cs  #   C API P/Invoke · 얇은 관리형 래퍼
  Tui/                         # TUI 튜토리얼 모드
native/Kuzu.targets            # libkuzu 네이티브 라이브러리 빌드시 자동 다운로드
    Pacer.cs                   #   흐름 속도 제어(5초/스텝)·일시정지·취소·노드 상태
    Term.cs / Renderer.cs      #   ANSI 터미널 제어 · 프레임 렌더링
    Scene.cs / Scenes.cs       #   장면 추상화 · 계측된 실제 그래프 + ASCII 다이어그램
    TuiApp.cs                  #   장면 순차 실행 · 입력(ESC/Ctrl+C) 처리
tests/AkkaGraphLoop.Tests/
  FanInOutTests.cs / PartialGraphTests.cs / CycleTests.cs
  TuiSceneTests.cs / PdsaTests.cs / PdsaGraphStoreTests.cs
```
