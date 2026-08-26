using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// pdsa-cli 인자 파서(<see cref="ArgUtil"/>) 순수 로직 검증:
/// <c>--key value</c> 옵션, <c>--flag</c> 플래그, invariant 수치 파싱, 위치 인자 결합.
/// </summary>
public class ArgUtilTests
{
    [Fact]
    public void Option_returns_value_when_present()
    {
        var args = new[] { "plan", "--project", "myproj", "--limit", "5" };
        Assert.Equal("myproj", ArgUtil.Option(args, "--project"));
        Assert.Equal("5", ArgUtil.Option(args, "--limit"));
    }

    [Fact]
    public void Option_returns_null_when_absent()
        => Assert.Null(ArgUtil.Option(new[] { "plan", "--project", "x" }, "--nope"));

    [Fact]
    public void Option_returns_null_when_name_is_last_token_without_value()
        => Assert.Null(ArgUtil.Option(new[] { "plan", "--project" }, "--project"));

    [Theory]
    [InlineData(new[] { "--force" }, "--force", true)]
    [InlineData(new[] { "plan", "--force", "x" }, "--force", true)]
    [InlineData(new[] { "plan", "x" }, "--force", false)]
    public void Flag_detects_presence(string[] args, string name, bool expected)
        => Assert.Equal(expected, ArgUtil.Flag(args, name));

    [Fact]
    public void Double_parses_invariant_and_falls_back()
    {
        var args = new[] { "run", "--start", "3.14" };
        Assert.Equal(3.14, ArgUtil.Double(args, "--start", 0.0), 5);
        Assert.Equal(90.0, ArgUtil.Double(args, "--target", 90.0), 5);   // 미지정 → fallback
        Assert.Equal(1.0, ArgUtil.Double(new[] { "--x", "abc" }, "--x", 1.0), 5); // 파싱 실패 → fallback
    }

    [Fact]
    public void Int_parses_and_falls_back()
    {
        Assert.Equal(7, ArgUtil.Int(new[] { "status", "--limit", "7" }, "--limit", 5));
        Assert.Equal(5, ArgUtil.Int(new[] { "status" }, "--limit", 5));            // 미지정
        Assert.Equal(5, ArgUtil.Int(new[] { "--limit", "x" }, "--limit", 5));      // 파싱 실패
    }

    [Fact]
    public void Positional_joins_non_option_tokens()
    {
        // 옵션(-로 시작)만 제외하고 나머지를 공백으로 결합.
        Assert.Equal("hello world", ArgUtil.Positional(new[] { "hello", "world" }));
        Assert.Equal("a b", ArgUtil.Positional(new[] { "--flag", "a", "-x", "b" }));
        Assert.Equal("", ArgUtil.Positional(new[] { "--only", "-flags" }));
    }
}
