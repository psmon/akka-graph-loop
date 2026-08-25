using System.Collections.Immutable;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.Cycles;

/// <summary>
/// 그래프 사이클(feedback loop)과 liveness/deadlock.
///
/// Akka.Streams 는 유한 버퍼(bounded)로 동작하므로, 순진한 사이클은 루프 안에 원소가 계속
/// 쌓여 모든 버퍼가 차고 영구 backpressure(=deadlock)에 빠질 수 있다.
/// 아래 세 가지 방법으로 liveness 를 확보한다.
///  1) <see cref="MergePreferred{T}"/> : 피드백을 우선 포트로 넣어 항상 흐르게 함(단 starvation 가능).
///  2) <c>Buffer(n, OverflowStrategy.DropHead)</c> : 피드백 arc 에서 원소를 버려 fair + live.
///  3) <see cref="ZipWith"/> 균형 사이클 : 입출력 1:1 균형 + <c>Source.Single</c> 초기 원소 주입.
///
/// 모든 예제는 방출 지점 앞에 <c>Take(take)</c> 를 두어 정해진 개수 처리 후 정상 종료(=데드락 없음)
/// 하도록 만들어, 테스트에서 타임아웃 내 완료로 liveness 를 입증할 수 있게 했다.
/// </summary>
public static class CycleSamples
{
    /// <summary>해법 1: MergePreferred. 피드백(+1)을 우선 포트로 되먹여 항상 흐르게 한다. 결과: [1..take].</summary>
    public static Task<IImmutableList<int>> MergePreferredCycle(IMaterializer mat, int take = 20)
    {
        var seed = Source.Single(1);

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var merge = b.Add(new MergePreferred<int>(1)); // secondary 1개 + preferred 1개
            var broadcast = b.Add(new Broadcast<int>(2));
            var limit = Flow.Create<int>().Take(take);
            var feedback = Flow.Create<int>().Select(x => x + 1);

            b.From(seed).To(merge.In(0));                     // 초기값 -> secondary
            b.From(merge.Out).Via(limit).To(broadcast.In);
            b.From(broadcast.Out(1)).Via(feedback).To(merge.Preferred); // 피드백 -> preferred

            return new SourceShape<int>(broadcast.Out(0));
        }));

        return graph.RunWith(Sink.Seq<int>(), mat);
    }

    /// <summary>해법 2: Merge + 피드백 arc 의 DropHead 버퍼. 버퍼가 넘치면 오래된 원소를 버려 데드락을 피한다.</summary>
    public static Task<IImmutableList<int>> BufferDropHeadCycle(IMaterializer mat, int take = 20)
    {
        var seed = Source.Single(1);

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var merge = b.Add(new Merge<int>(2));
            var broadcast = b.Add(new Broadcast<int>(2));
            var limit = Flow.Create<int>().Take(take);
            var feedback = Flow.Create<int>()
                .Select(x => x + 1)
                .Buffer(10, OverflowStrategy.DropHead);

            b.From(seed).To(merge.In(0));
            b.From(merge.Out).Via(limit).To(broadcast.In);
            b.From(broadcast.Out(1)).Via(feedback).To(merge.In(1));

            return new SourceShape<int>(broadcast.Out(0));
        }));

        return graph.RunWith(Sink.Seq<int>(), mat);
    }

    /// <summary>
    /// 해법 3: ZipWith 균형 사이클. Zip 은 두 입력이 모두 있어야 1 개를 방출하므로 입출력이 1:1 로 균형 잡힌다.
    /// <c>Source.Single(0)</c> 를 Concat 앞에 두어 초기 원소를 주입(닭-달걀 문제 해결). 결과: [0..take-1].
    /// </summary>
    public static Task<IImmutableList<int>> BalancedZipWithCycle(IMaterializer mat, int take = 20)
    {
        var driver = Source.From(Enumerable.Range(1, take)); // 사이클 페이싱용(한 번에 하나씩 진행)
        var start = Source.Single(0);                        // 초기 주입 원소

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var zip = b.Add(ZipWith.Apply<int, int, int>((_, fed) => fed)); // Keep.Right: 되먹인 값을 방출
            var broadcast = b.Add(new Broadcast<int>(2));
            // .Async() 로 비동기 경계(=버퍼)를 두어 사이클 부트스트랩 시 데드락을 방지한다.
            var concat = b.Add(new Concat<int, int>().Async());
            var limit = Flow.Create<int>().Take(take);
            var feedback = Flow.Create<int>().Select(x => x + 1);

            b.From(driver).To(zip.In0);
            b.From(zip.Out).Via(limit).To(broadcast.In);

            b.From(start).To(concat.In(0));                     // 초기 원소가 먼저
            b.From(broadcast.Out(1)).Via(feedback).To(concat.In(1)); // 그 다음부터 피드백
            b.From(concat.Out).To(zip.In1);

            return new SourceShape<int>(broadcast.Out(0));
        }));

        return graph.RunWith(Sink.Seq<int>(), mat);
    }

    /// <summary>
    /// (참고용) 순진한 Merge+Broadcast 사이클 — 실제로 데드락된다.
    /// print 가 사이클마다 원소를 하나 더 넣어 버퍼가 차고 영구 backpressure 에 빠진다.
    /// 반환 Task 는 완료되지 않으므로, 호출부에서 반드시 타임아웃으로 감싸 실행할 것.
    /// </summary>
    public static Task RunNaiveDeadlock(IMaterializer mat)
    {
        var source = Source.From(Enumerable.Range(1, 5));

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var merge = b.Add(new Merge<int>(2));
            var broadcast = b.Add(new Broadcast<int>(2));
            var print = Flow.Create<int>().Select(x =>
            {
                Console.WriteLine($"[deadlock-demo] {x}");
                return x;
            });

            b.From(source).Via(merge).Via(print).To(broadcast.In);
            b.From(broadcast.Out(1)).To(merge.In(1)); // 피드백: 균형을 맞추는 장치가 없어 데드락

            return new SourceShape<int>(broadcast.Out(0));
        }));

        return graph.RunWith(Sink.Ignore<int>(), mat);
    }
}
