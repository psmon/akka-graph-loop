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

## 구조

```
src/AkkaGraphLoop.Samples/
  Basics/GraphDslBasics.cs     # GraphDSL 기본 폐그래프
  FanOut/FanOutSamples.cs      # Broadcast, Balance, UnZip
  FanIn/FanInSamples.cs        # Merge*, Zip, ZipWith, Concat
  Partial/PartialGraphSamples.cs  # UniformFanInShape, Source/Flow.FromGraph
  Cycles/CycleSamples.cs       # 사이클 데드락과 3가지 해법
  Tui/                         # TUI 튜토리얼 모드
    Pacer.cs                   #   흐름 속도 제어(5초/스텝)·일시정지·취소·노드 상태
    Term.cs / Renderer.cs      #   ANSI 터미널 제어 · 프레임 렌더링
    Scene.cs / Scenes.cs       #   장면 추상화 · 계측된 실제 그래프 + ASCII 다이어그램
    TuiApp.cs                  #   장면 순차 실행 · 입력(ESC/Ctrl+C) 처리
tests/AkkaGraphLoop.Tests/
  FanInOutTests.cs / PartialGraphTests.cs / CycleTests.cs / TuiSceneTests.cs
```
