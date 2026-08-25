# Akka.NET Streams Graph 기능 조사

Akka.NET Streams(v1.5.70 기준)의 **Graph** 기능을 개념부터 사이클(loop) 처리까지 정리한 문서다.
각 절 끝에 이 저장소의 **실행 가능한 예제 코드**와 **테스트** 경로를 연결해 두었다.

- 대상 버전: `Akka.Streams` **1.5.70** (net6.0 / netstandard2.0, net10 호환)
- 실행 환경: .NET 10 SDK

---

## 1. Graph 란 무엇인가

선형 `Source → Flow → Sink` 파이프라인은 입력 1개·출력 1개의 **선형 변환**만 표현할 수 있다.
반면 **fan-out(1→N 다중 출력)**, **fan-in(N→1 다중 입력)** 같은 위상은 선형 DSL로 표현할 수 없다.
Graph 는 이런 **분기·합류(junction)** 를 그림 그리듯 코드로 작성하기 위한 DSL이다.

핵심 성질:

- `GraphDsl` 로 구성한 그래프는 완성 후 **immutable · thread-safe** 하며 여러 위치에서 **재사용** 가능하다.
- **junction 참조 동일성 = 그래프 노드 동일성** — 같은 junction 인스턴스는 그래프 상의 같은 지점을 가리킨다.

---

## 2. GraphDSL 기본 문법

```csharp
var graph = Source.FromGraph(GraphDsl.Create(b =>
{
    var broadcast = b.Add(new Broadcast<int>(2));  // junction 추가
    var merge     = b.Add(new Merge<int>(2));
    // ...
    b.From(source).Via(f1).Via(broadcast).Via(f2).Via(merge).Via(f3).To(last.Inlet);
    b.From(broadcast).Via(f4).To(merge);           // 남은 포트 연결
    return new SourceShape<int>(last.Outlet);      // 열린 포트를 Shape 로 노출
}));
```

- `b.Add(junction)` : junction 을 그래프에 추가하고 그 **Shape**(포트 묶음)를 돌려준다.
- `b.From(x).Via(flow).To(y)` : 포트를 연결한다. `.Via(broadcast)` 는 broadcast 의 `In` 과 `Out(0)` 을 소비하고,
  이어지는 `b.From(broadcast)` 는 남은 `Out(1)` 을 사용한다.
- 반환값이 결정하는 그래프 종류:
  - `ClosedShape.Instance` → 실행 가능한 완결 그래프(**RunnableGraph**)
  - `SourceShape`/`FlowShape`/`SinkShape`/`UniformFanInShape` 등 → **partial graph**(부분 그래프)

> 📁 예제: [`Basics/GraphDslBasics.cs`](../src/AkkaGraphLoop.Samples/Basics/GraphDslBasics.cs) · 실행: `dotnet run -- 0`

---

## 3. Junction 카탈로그

### Fan-out (1 입력 → N 출력)

| Junction | 동작 | 비고 |
|---|---|---|
| `Broadcast<T>(n)` | 입력 원소를 **모든** 출력으로 복제 | 기본은 모든 출력이 준비돼야 방출 |
| `Balance<T>(n)` | 가용한 출력 **하나로만** 흘려 부하 분산 | 분배는 비결정적 |
| `UnZip<T1,T2>()` | `KeyValuePair<T1,T2>` 스트림을 두 스트림으로 분리 | 입력 타입이 KeyValuePair |
| `UnzipWith<...>` | 함수로 하나의 입력을 여러 출력으로 분리 | 최대 20 출력 |

### Fan-in (N 입력 → 1 출력)

| Junction | 동작 | 비고 |
|---|---|---|
| `Merge<T>(n)` | 도착하는 대로 공정하게 합침 | 순서 비결정적 |
| `MergePreferred<T>(k)` | **우선 포트**에 원소가 있으면 항상 먼저 소비 | secondary k개 + preferred 1개 |
| `MergePrioritized<T>(weights)` | 가중치에 비례해 확률적으로 선택 | |
| `Zip<T1,T2>()` | 두 스트림을 `(a,b)` **ValueTuple** 로 쌍맞춤 | 두 입력이 모두 있어야 방출 |
| `ZipWith.Apply<...>(fn)` | 여러 입력을 함수로 결합 | 최대 20 입력 |
| `Concat<TIn,TOut>()` | 첫 스트림을 모두 흘린 뒤 다음을 이어붙임 | 순차 결합 |

> ⚠️ **버전 주의(1.5.70)**: `Zip<T1,T2>` 의 출력은 `System.ValueTuple`(`t.Item1`, `t.Item2`)이고,
> `UnZip<T1,T2>` 의 입력은 `KeyValuePair<T1,T2>` 다. (구버전 문서의 `System.Tuple` 과 다름)

> 📁 예제: [`FanOut/FanOutSamples.cs`](../src/AkkaGraphLoop.Samples/FanOut/FanOutSamples.cs) · [`FanIn/FanInSamples.cs`](../src/AkkaGraphLoop.Samples/FanIn/FanInSamples.cs)
> 실행: Balance `-- 1`, UnZip `-- 2`, Zip `-- 3`, ZipWith `-- 4`, Concat `-- 5`, MergePrioritized `-- 6`

---

## 4. Partial Graph 와 Source/Flow/Sink 변환

열린 포트를 `Shape` 로 노출하면 **재사용 가능한 컴포넌트**가 된다.

- `UniformFanInShape<TIn,TOut>(outlet, in0, in1, ...)` : 동일 타입 N 입력 1 출력 부분 그래프.
- `Source.FromGraph(...)` : `SourceShape` 를 반환하는 그래프를 **Source** 로.
- `Flow.FromGraph(...)` : `FlowShape` 를 반환하는 그래프를 **Flow** 로.
- `Sink.FromGraph(...)` : `SinkShape` 를 반환하는 그래프를 **Sink** 로.

예: 3 입력 최댓값을 고르는 부분 그래프 (`ZipWith(Max)` 두 개 연결)

```csharp
GraphDsl.Create(b =>
{
    var zip1 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
    var zip2 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
    b.From(zip1.Out).To(zip2.In0);
    return new UniformFanInShape<int, int>(zip2.Out, zip1.In0, zip1.In1, zip2.In1);
});
```

> 📁 예제: [`Partial/PartialGraphSamples.cs`](../src/AkkaGraphLoop.Samples/Partial/PartialGraphSamples.cs)
> 실행: PickMaxOfThree `-- 7`, Source.FromGraph `-- 8`, Flow.FromGraph `-- 9`

---

## 5. Graph 사이클(loop)과 liveness / deadlock ★

Akka.Streams 는 **유한 버퍼(bounded)** 로 동작한다. 피드백 사이클에서 루프로 들어가는 원소가
나가는 원소보다 많으면, 내부 버퍼가 모두 차고 소스가 **영구 backpressure(=deadlock)** 상태가 된다.

### 5.0 순진한 사이클 — 데드락

```csharp
b.From(source).Via(merge).Via(print).To(broadcast.In);
b.From(broadcast.Out(1)).To(merge.In(1)); // 피드백 — 균형 장치가 없음
```

`broadcast` 가 원소를 복제해 되먹이므로 사이클마다 원소 수가 늘어 결국 멈춘다.
(참고: `-- 99` 데모는 이 그래프를 2초 타임아웃으로 감싸 데드락을 시연한다.)

### 5.1 해법 1 — `MergePreferred`

피드백을 **우선 포트**로 넣으면, 루프 안의 원소가 항상 먼저 소비되어 흐름이 끊기지 않는다.

```csharp
var merge = b.Add(new MergePreferred<int>(1));      // secondary 1 + preferred 1
b.From(seed).To(merge.In(0));                        // 초기값 → secondary
b.From(merge.Out).Via(limit).To(broadcast.In);
b.From(broadcast.Out(1)).Via(feedback).To(merge.Preferred); // 피드백 → preferred
```

- 장점: 데드락 회피(liveness). 단점: 우선 포트가 계속 차면 secondary 입력이 **starvation** 될 수 있다.

### 5.2 해법 2 — 피드백 arc 의 `Buffer(n, DropHead)`

피드백 경로에 **드롭 버퍼**를 두면, 버퍼가 넘칠 때 오래된 원소를 버려 루프가 계속 살아있다(fair + live).

```csharp
var feedback = Flow.Create<int>().Select(x => x + 1)
                   .Buffer(10, OverflowStrategy.DropHead);
b.From(broadcast.Out(1)).Via(feedback).To(merge.In(1));
```

- 트레이드오프: **boundedness ↔ 완전성** — liveness 를 위해 일부 원소를 희생.

### 5.3 해법 3 — `ZipWith` 균형 사이클 + 초기 원소 주입

`Zip`/`ZipWith` 는 두 입력이 **모두** 있어야 1 개를 방출하므로 입출력이 자동으로 **1:1 균형**을 이룬다.
단, 처음엔 피드백 값이 없으니 `Source.Single(0)` 을 `Concat` 앞에 두어 **초기 원소를 주입**한다(닭-달걀 문제).

```csharp
var zip    = b.Add(ZipWith.Apply<int, int, int>((_, fed) => fed)); // Keep.Right
var concat = b.Add(new Concat<int, int>().Async());                // 비동기 경계=버퍼로 부트스트랩
b.From(driver).To(zip.In0);                       // 사이클 페이싱
b.From(zip.Out).Via(limit).To(broadcast.In);
b.From(start).To(concat.In(0));                   // 초기 원소가 먼저
b.From(broadcast.Out(1)).Via(feedback).To(concat.In(1));
b.From(concat.Out).To(zip.In1);
```

- `Concat` 에 `.Async()` 를 붙여 비동기 경계(버퍼)를 두지 않으면 부트스트랩 시점에 데드락한다.

> 📁 예제: [`Cycles/CycleSamples.cs`](../src/AkkaGraphLoop.Samples/Cycles/CycleSamples.cs)
> 실행: MergePreferred `-- 10`, Buffer DropHead `-- 11`, Balanced ZipWith `-- 12`, 데드락 시연 `-- 99`
> 🧪 검증: [`CycleTests.cs`](../tests/AkkaGraphLoop.Tests/CycleTests.cs) — 세 해법이 타임아웃 내 완료되어 liveness 를 입증

---

## 6. 요약

- Graph 는 fan-in/fan-out 위상을 표현하는 DSL이며, 완성 후 immutable·재사용 가능하다.
- fan-out: `Broadcast`(복제) / `Balance`(분산) / `UnZip`·`UnzipWith`(분리).
- fan-in: `Merge`/`MergePreferred`/`MergePrioritized`(합류) / `Zip`·`ZipWith`(결합) / `Concat`(순차).
- partial graph + `FromGraph` 로 Source/Flow/Sink 컴포넌트를 조립한다.
- **사이클은 boundedness 때문에 데드락 위험**이 있으며, `MergePreferred` / `Buffer(DropHead)` / `ZipWith 균형+초기주입` 으로 liveness 를 확보한다.

---

## 출처

- [Working with Graphs — Akka.NET 공식 문서](https://getakka.net/articles/streams/workingwithgraphs.html)
- [workingwithgraphs.md (GitHub, dev 브랜치)](https://github.com/akkadotnet/akka.net/blob/dev/docs/articles/streams/workingwithgraphs.md)
- [Akka.Streams NuGet 패키지](https://www.nuget.org/packages/Akka.Streams)
