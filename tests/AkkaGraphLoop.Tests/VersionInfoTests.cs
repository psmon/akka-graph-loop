using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>버전 비교 로직 검증(순수). 네트워크/캐시 I/O 는 대상 아님.</summary>
public class VersionInfoTests
{
    [Theory]
    [InlineData("0.0.8", "0.0.7", 1)]
    [InlineData("0.0.7", "0.0.7", 0)]
    [InlineData("0.0.7", "0.0.8", -1)]
    [InlineData("0.1.0", "0.0.9", 1)]
    [InlineData("1.0.0", "0.9.9", 1)]
    [InlineData("0.0.7", "0.0.7.0", 0)]   // 4-part 는 뒤가 잘려 x.y.z 로 비교
    public void Compare_orders_by_x_y_z(string a, string b, int sign)
        => Assert.Equal(sign, Math.Sign(VersionInfo.Compare(a, b)));

    [Theory]
    [InlineData("0.0.7", "0.0.8", true)]
    [InlineData("0.0.8", "0.0.8", false)]
    [InlineData("0.0.9", "0.0.8", false)]
    public void IsOutdated_true_only_when_latest_is_higher(string current, string latest, bool expected)
        => Assert.Equal(expected, VersionInfo.IsOutdated(current, latest));

    [Fact]
    public void IsOutdated_false_when_latest_missing()
    {
        Assert.False(VersionInfo.IsOutdated("0.0.7", null));
        Assert.False(VersionInfo.IsOutdated("0.0.7", ""));
    }

    [Fact]
    public void Current_is_three_part_numeric()
    {
        var parts = VersionInfo.Current().Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _)));
    }
}
