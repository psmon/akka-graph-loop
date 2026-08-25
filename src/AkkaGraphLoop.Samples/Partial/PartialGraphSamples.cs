using System.Collections.Immutable;
using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.Partial;

/// <summary>
/// Partial graph: 열린 포트를 <see cref="Shape"/> 로 노출해 재사용 가능한 컴포넌트를 만든다.
/// - <see cref="UniformFanInShape{TIn,TOut}"/> 등으로 부분 그래프를 캡슐화.
/// - <c>Source.FromGraph</c> / <c>Flow.FromGraph</c> 로 그래프를 Source/Flow 로 변환.
/// </summary>
public static class PartialGraphSamples
{
    /// <summary>
    /// 3 입력의 최댓값을 고르는 부분 그래프. ZipWith(Max) 두 개를 연결해
    /// 3 개의 입력(In0,In1,In2)과 1 개의 출력(Out)을 갖는 UniformFanInShape 로 노출한다.
    /// </summary>
    public static IGraph<UniformFanInShape<int, int>, NotUsed> PickMaxOfThree()
    {
        return GraphDsl.Create(b =>
        {
            var zip1 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
            var zip2 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
            b.From(zip1.Out).To(zip2.In0);
            return new UniformFanInShape<int, int>(zip2.Out, zip1.In0, zip1.In1, zip2.In1);
        });
    }

    /// <summary>PickMaxOfThree 부분 그래프에 세 상수 스트림(1,2,3)을 연결해 최댓값 3 을 낸다.</summary>
    public static Task<int> PickMaxOfThreeDemo(IMaterializer mat)
    {
        var source = Source.FromGraph(GraphDsl.Create(b =>
        {
            var pick = b.Add(PickMaxOfThree());
            b.From(Source.Single(1)).To(pick.In(0));
            b.From(Source.Single(2)).To(pick.In(1));
            b.From(Source.Single(3)).To(pick.In(2));
            return new SourceShape<int>(pick.Out);
        }));

        return source.RunWith(Sink.First<int>(), mat);
    }

    /// <summary>
    /// <c>Source.FromGraph</c>: 내부에서 Zip 으로 홀수/짝수를 쌍맞춰 (odd, even) 스트림을 만든다.
    /// </summary>
    public static Task<IImmutableList<(int, int)>> OddEvenPairsDemo(IMaterializer mat, int take = 3)
    {
        var pairs = Source.FromGraph(GraphDsl.Create(b =>
        {
            var zip = b.Add(new Zip<int, int>());
            var ints = Source.From(Enumerable.Range(1, 100));
            b.From(ints.Where(x => x % 2 != 0)).To(zip.In0);
            b.From(ints.Where(x => x % 2 == 0)).To(zip.In1);
            return new SourceShape<(int, int)>(zip.Out);
        }));

        return pairs.Take(take).RunWith(Sink.Seq<(int, int)>(), mat); // [(1,2),(3,4),(5,6)]
    }

    /// <summary>
    /// <c>Flow.FromGraph</c>: 입력 int 를 Broadcast 로 복제해 한쪽은 그대로, 한쪽은 문자열로 만든 뒤
    /// Zip 으로 (int, string) 쌍을 만드는 재사용 가능한 Flow.
    /// </summary>
    public static Task<IImmutableList<(int, string)>> PairUpWithToStringDemo(IMaterializer mat)
    {
        var flow = Flow.FromGraph(GraphDsl.Create(b =>
        {
            var broadcast = b.Add(new Broadcast<int>(2));
            var zip = b.Add(new Zip<int, string>());

            b.From(broadcast.Out(0)).Via(Flow.Create<int>().Select(x => x)).To(zip.In0);
            b.From(broadcast.Out(1)).Via(Flow.Create<int>().Select(x => x.ToString())).To(zip.In1);

            return new FlowShape<int, (int, string)>(broadcast.In, zip.Out);
        }));

        return Source.From(new[] { 1, 2, 3 })
            .Via(flow)
            .RunWith(Sink.Seq<(int, string)>(), mat); // [(1,"1"),(2,"2"),(3,"3")]
    }
}
