using System.Collections.Immutable;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.FanOut;

/// <summary>
/// Fan-out junction (1 입력 -> N 출력).
/// - <see cref="Broadcast{T}"/> : 모든 출력 포트로 같은 원소를 복제.
/// - <see cref="Balance{T}"/>   : 가용한 출력 포트 하나로 흘려 부하를 분산(순서/분배는 비결정적).
/// - <see cref="UnZip{T1,T2}"/> : KeyValuePair 스트림을 두 스트림으로 분리.
/// </summary>
public static class FanOutSamples
{
    /// <summary>
    /// Balance 로 원소를 N 개의 워커 Flow 에 분산한 뒤 Merge 로 다시 합친다.
    /// 결과 총 개수는 입력과 동일하지만, 어떤 워커가 처리했는지는 실행마다 달라질 수 있다.
    /// </summary>
    public static Task<IImmutableList<string>> BalanceDemo(IMaterializer mat, int workerCount = 3, int count = 9)
    {
        var source = Source.From(Enumerable.Range(1, count));

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var balance = b.Add(new Balance<int>(workerCount));
            var merge = b.Add(new Merge<string>(workerCount));

            for (var i = 0; i < workerCount; i++)
            {
                var worker = i;
                var work = Flow.Create<int>().Select(x => $"worker{worker}:{x}");
                b.From(balance.Out(i)).Via(work).To(merge.In(i));
            }

            b.From(source).To(balance.In);
            return new SourceShape<string>(merge.Out);
        }));

        return graph.RunWith(Sink.Seq<string>(), mat);
    }

    /// <summary>
    /// UnZip 은 <see cref="KeyValuePair{TKey,TValue}"/> 스트림을 key 스트림과 value 스트림으로 나눈다.
    /// 여기서는 나눈 두 스트림을 각각 가공한 뒤 하나의 문자열 스트림으로 다시 합쳐 반환한다.
    /// </summary>
    public static Task<IImmutableList<string>> UnzipDemo(IMaterializer mat)
    {
        var pairs = Source.From(new[]
        {
            new KeyValuePair<int, string>(1, "a"),
            new KeyValuePair<int, string>(2, "b"),
            new KeyValuePair<int, string>(3, "c"),
        });

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var unzip = b.Add(new UnZip<int, string>());
            var merge = b.Add(new Merge<string>(2));

            b.From(pairs).To(unzip.In);
            b.From(unzip.Out0).Via(Flow.Create<int>().Select(x => $"num:{x}")).To(merge.In(0));
            b.From(unzip.Out1).Via(Flow.Create<string>().Select(s => $"str:{s}")).To(merge.In(1));

            return new SourceShape<string>(merge.Out);
        }));

        return graph.RunWith(Sink.Seq<string>(), mat);
    }
}
