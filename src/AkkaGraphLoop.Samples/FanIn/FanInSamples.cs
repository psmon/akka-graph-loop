using System.Collections.Immutable;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.FanIn;

/// <summary>
/// Fan-in junction (N 입력 -> 1 출력).
/// - <see cref="Merge{T}"/>            : 도착하는 대로 공정하게 합친다(순서 비결정적).
/// - <see cref="MergePrioritized{T}"/> : 가중치에 비례해 확률적으로 선택.
/// - <see cref="Zip{T1,T2}"/>          : 두 스트림을 (a,b) ValueTuple 로 쌍 맞춤(둘 다 있어야 진행).
/// - <c>ZipWith.Apply</c>              : 여러 입력을 함수로 결합.
/// - <see cref="Concat{TIn,TOut}"/>    : 첫 스트림을 모두 흘린 뒤 다음 스트림을 이어붙인다.
/// </summary>
public static class FanInSamples
{
    /// <summary>Zip: 정수 스트림과 문자열 스트림을 쌍으로 결합.</summary>
    public static Task<IImmutableList<string>> ZipDemo(IMaterializer mat)
    {
        var numbers = Source.From(Enumerable.Range(1, 3));
        var letters = Source.From(new[] { "a", "b", "c" });

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var zip = b.Add(new Zip<int, string>());
            b.From(numbers).To(zip.In0);
            b.From(letters).To(zip.In1);
            return new SourceShape<(int, string)>(zip.Out);
        }));

        return graph.Select(t => $"{t.Item1}{t.Item2}").RunWith(Sink.Seq<string>(), mat);
    }

    /// <summary>ZipWith: 두 정수 스트림에서 원소별 최댓값을 취한다.</summary>
    public static Task<IImmutableList<int>> ZipWithMaxDemo(IMaterializer mat)
    {
        var left = Source.From(new[] { 1, 9, 3 });
        var right = Source.From(new[] { 5, 2, 8 });

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var zipWith = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
            b.From(left).To(zipWith.In0);
            b.From(right).To(zipWith.In1);
            return new SourceShape<int>(zipWith.Out);
        }));

        return graph.RunWith(Sink.Seq<int>(), mat); // 기대: [5, 9, 8]
    }

    /// <summary>Concat: 첫 스트림(1,2,3)을 모두 방출한 뒤 둘째 스트림(10,20)을 이어붙인다.</summary>
    public static Task<IImmutableList<int>> ConcatDemo(IMaterializer mat)
    {
        var first = Source.From(new[] { 1, 2, 3 });
        var second = Source.From(new[] { 10, 20 });

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var concat = b.Add(new Concat<int, int>());
            b.From(first).To(concat.In(0));
            b.From(second).To(concat.In(1));
            return new SourceShape<int>(concat.Out);
        }));

        return graph.RunWith(Sink.Seq<int>(), mat); // 기대: [1,2,3,10,20]
    }

    /// <summary>MergePrioritized: 가중치가 높은 입력이 확률적으로 더 자주 선택된다.</summary>
    public static Task<IImmutableList<int>> MergePrioritizedDemo(IMaterializer mat)
    {
        var high = Source.From(Enumerable.Repeat(1, 10)); // priority 10
        var low = Source.From(Enumerable.Repeat(2, 10));  // priority 1

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var merge = b.Add(new MergePrioritized<int>(new[] { 10, 1 }));
            b.From(high).To(merge.In(0));
            b.From(low).To(merge.In(1));
            return new SourceShape<int>(merge.Out);
        }));

        return graph.RunWith(Sink.Seq<int>(), mat); // 총 20개, 앞쪽에 1(고우선)이 더 많이 섞임
    }
}
