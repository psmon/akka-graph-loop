using AkkaGraphLoop.Samples.FanIn;
using AkkaGraphLoop.Samples.FanOut;

namespace AkkaGraphLoop.Tests;

public class FanInOutTests : GraphTestBase
{
    [Fact]
    public void Balance_distributes_all_elements_exactly_once()
    {
        var result = Await(FanOutSamples.BalanceDemo(Materializer, workerCount: 3, count: 9));

        // 분배(어느 워커가 처리하는지)는 비결정적이지만, 모든 원소가 정확히 한 번씩 처리되어야 한다.
        Assert.Equal(9, result.Count);
        var numbers = result.Select(s => int.Parse(s.Split(':')[1])).OrderBy(x => x);
        Assert.Equal(Enumerable.Range(1, 9), numbers);
    }

    [Fact]
    public void Unzip_splits_pairs_into_two_streams()
    {
        var result = Await(FanOutSamples.UnzipDemo(Materializer));

        Assert.Equal(6, result.Count);
        Assert.Contains("num:1", result);
        Assert.Contains("num:3", result);
        Assert.Contains("str:a", result);
        Assert.Contains("str:c", result);
    }

    [Fact]
    public void Zip_pairs_elements_positionally()
    {
        var result = Await(FanInSamples.ZipDemo(Materializer));
        Assert.Equal(new[] { "1a", "2b", "3c" }, result);
    }

    [Fact]
    public void ZipWith_takes_elementwise_max()
    {
        var result = Await(FanInSamples.ZipWithMaxDemo(Materializer));
        Assert.Equal(new[] { 5, 9, 8 }, result);
    }

    [Fact]
    public void Concat_appends_second_stream_after_first()
    {
        var result = Await(FanInSamples.ConcatDemo(Materializer));
        Assert.Equal(new[] { 1, 2, 3, 10, 20 }, result);
    }

    [Fact]
    public void MergePrioritized_emits_all_elements_from_both_inputs()
    {
        var result = Await(FanInSamples.MergePrioritizedDemo(Materializer));
        Assert.Equal(20, result.Count);
        Assert.Equal(10, result.Count(x => x == 1)); // 고우선 입력
        Assert.Equal(10, result.Count(x => x == 2)); // 저우선 입력
    }
}
