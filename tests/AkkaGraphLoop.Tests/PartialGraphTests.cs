using AkkaGraphLoop.Samples.Partial;

namespace AkkaGraphLoop.Tests;

public class PartialGraphTests : GraphTestBase
{
    [Fact]
    public void PickMaxOfThree_returns_the_maximum()
    {
        var result = Await(PartialGraphSamples.PickMaxOfThreeDemo(Materializer));
        Assert.Equal(3, result);
    }

    [Fact]
    public void OddEvenPairs_zips_odd_and_even_numbers()
    {
        var result = Await(PartialGraphSamples.OddEvenPairsDemo(Materializer, take: 3));
        Assert.Equal(new[] { (1, 2), (3, 4), (5, 6) }, result);
    }

    [Fact]
    public void PairUpWithToString_broadcasts_then_zips_with_string()
    {
        var result = Await(PartialGraphSamples.PairUpWithToStringDemo(Materializer));
        Assert.Equal(new[] { (1, "1"), (2, "2"), (3, "3") }, result);
    }
}
