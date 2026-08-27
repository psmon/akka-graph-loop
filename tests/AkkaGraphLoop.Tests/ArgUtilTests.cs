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

    [Fact]
    public void Positional_excludes_value_option_values()
    {
        // 값-옵션(--project 등)의 '값' 토큰은 본문에 섞이지 않아야 한다(멀티프로젝트 핵심).
        Assert.Equal("my plan",
            ArgUtil.Positional(new[] { "my", "plan", "--project", "akka-graph-loop" }));
        // 옵션이 본문 앞에 와도 값만 정확히 건너뛴다.
        Assert.Equal("do report",
            ArgUtil.Positional(new[] { "--project", "svc-a", "do", "report" }));
        // 여러 값-옵션이 섞여도 각 값만 제외.
        Assert.Equal("result text",
            ArgUtil.Positional(new[] { "result", "--lang", "ko", "text", "--expect", "met" }));
        // Option() 은 여전히 값을 정확히 반환(본문 제외와 별개).
        var args = new[] { "my", "plan", "--project", "akka-graph-loop" };
        Assert.Equal("akka-graph-loop", ArgUtil.Option(args, "--project"));
    }

    [Fact]
    public void Positional_keeps_value_of_non_whitelisted_flag_neighbor()
    {
        // 플래그(--fresh)는 값이 없으므로 바로 뒤 토큰은 본문으로 유지되어야 한다.
        Assert.Equal("plan body",
            ArgUtil.Positional(new[] { "plan", "--fresh", "body" }));
    }
}
