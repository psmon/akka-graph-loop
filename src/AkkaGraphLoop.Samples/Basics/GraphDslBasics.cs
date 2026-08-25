using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.Basics;

/// <summary>
/// GraphDSL 기본: 선형 Flow 로는 표현할 수 없는 fan-out/fan-in 위상을 만든다.
///
///                     +----> f2 ----+
/// source -> f1 -> broadcast          merge -> f3 -> sink
///                     +----> f4 ----+
///
/// - <see cref="ClosedShape"/> 를 반환하면 실행 가능한 완결 그래프(RunnableGraph)가 된다.
/// - <c>b.From(x).Via(f).To(y)</c> 로 포트를 연결한다.
/// - <c>.Via(broadcast)</c> 는 broadcast 의 In 과 Out(0) 을 소비하고,
///   이어지는 <c>b.From(broadcast)</c> 는 남은 Out(1) 을 사용한다(참조 동일성 = 노드 동일성).
/// </summary>
public static class GraphDslBasics
{
    public static Task Run(IMaterializer mat)
    {
        var source = Source.From(Enumerable.Range(1, 10));

        // 마지막 f3 의 출력을 SourceShape 로 노출한 뒤, 출력 Sink 로 실행한다.
        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var broadcast = b.Add(new Broadcast<int>(2));
            var merge = b.Add(new Merge<int>(2));

            var f1 = Flow.Create<int>().Select(x => x + 10);
            var f2 = Flow.Create<int>().Select(x => x + 10);
            var f3 = Flow.Create<int>().Select(x => x + 10);
            var f4 = Flow.Create<int>().Select(x => x + 10);

            // f3 이후의 출력을 SourceShape 로 노출하기 위한 identity 스테이지
            var last = b.Add(Flow.Create<int>());
            b.From(source).Via(f1).Via(broadcast).Via(f2).Via(merge).Via(f3).To(last.Inlet);
            b.From(broadcast).Via(f4).To(merge);

            return new SourceShape<int>(last.Outlet);
        }));

        return graph.RunForeach(x => Console.WriteLine($"[basics] {x}"), mat);
    }
}
