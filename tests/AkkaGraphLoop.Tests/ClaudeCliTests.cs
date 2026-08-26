using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// Claude Code CLI 프로바이더(<see cref="ClaudeCliClient"/>) 검증: claude -p JSON 의 result 파싱,
/// 오류/거부 처리, --model 판정, 그리고 주입형 runner 로 인자 조립·성공 경로. 실제 claude 호출 없이 격리.
/// </summary>
public class ClaudeCliTests
{
    [Fact]
    public void ParseResult_extracts_result_text()
    {
        var json = """{"type":"result","subtype":"success","is_error":false,"result":"기대평가: x\n코칭"}""";
        Assert.Equal("기대평가: x\n코칭", ClaudeCliClient.ParseResult(json));
    }

    [Fact]
    public void ParseResult_throws_on_is_error()
    {
        var json = """{"type":"result","is_error":true,"result":"boom"}""";
        var ex = Assert.Throws<InvalidOperationException>(() => ClaudeCliClient.ParseResult(json));
        Assert.Contains("오류", ex.Message);
    }

    [Fact]
    public void ParseResult_throws_on_non_success_subtype()
        => Assert.Throws<InvalidOperationException>(
            () => ClaudeCliClient.ParseResult("""{"subtype":"error_max_turns","is_error":false}"""));

    [Fact]
    public void ParseResult_throws_on_empty_or_missing_result()
    {
        Assert.Throws<InvalidOperationException>(() => ClaudeCliClient.ParseResult("   "));
        Assert.Throws<InvalidOperationException>(() => ClaudeCliClient.ParseResult("""{"subtype":"success"}"""));
    }

    [Fact]
    public void ParseResult_throws_on_bad_json()
        => Assert.Throws<InvalidOperationException>(() => ClaudeCliClient.ParseResult("not json"));

    [Theory]
    [InlineData("claude-opus-4-8", true)]
    [InlineData("claude-sonnet-5", true)]
    [InlineData("gpt-5.6-terra", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UsesClaudeModel_only_for_claude_ids(string? model, bool expected)
        => Assert.Equal(expected, ClaudeCliClient.UsesClaudeModel(model));

    [Fact]
    public async Task CompleteAsync_passes_system_and_user_and_returns_result()
    {
        IReadOnlyList<string>? capturedArgs = null;
        string? capturedStdin = null;
        var client = new ClaudeCliClient(
            model: "claude-opus-4-8",
            runner: (exe, args, stdin, ct) =>
            {
                capturedArgs = args; capturedStdin = stdin;
                return Task.FromResult((0, """{"subtype":"success","is_error":false,"result":"판정: met"}""", ""));
            },
            exeResolver: () => "claude");

        var text = await client.CompleteAsync("SYSTEM", "USER");

        Assert.Equal("판정: met", text);
        Assert.Equal("USER", capturedStdin);                      // user 는 stdin 으로
        Assert.Contains("--append-system-prompt", capturedArgs!); // system 은 플래그로
        Assert.Contains("SYSTEM", capturedArgs!);
        Assert.Contains("--model", capturedArgs!);                // claude 모델이면 --model 전달
        Assert.Contains("claude-opus-4-8", capturedArgs!);
    }

    [Fact]
    public async Task CompleteAsync_omits_model_flag_for_non_claude_model()
    {
        IReadOnlyList<string>? args = null;
        var client = new ClaudeCliClient(
            model: "gpt-5.6-terra",
            runner: (e, a, s, ct) => { args = a; return Task.FromResult((0, """{"subtype":"success","result":"ok"}""", "")); },
            exeResolver: () => "claude");

        await client.CompleteAsync("s", "u");
        Assert.DoesNotContain("--model", args!);                  // 비-claude 모델명은 넘기지 않음
    }

    [Fact]
    public async Task CompleteAsync_throws_when_cli_missing()
    {
        var client = new ClaudeCliClient(exeResolver: () => null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteAsync("s", "u"));
        Assert.Contains("claude", ex.Message);
    }

    // claude -p 는 에이전트 서두/잡음을 앞에 붙일 수 있다 — PdsaCoach 태그 파서가 그래도 태그를 추출해야 한다.
    [Fact]
    public void Coach_tag_parser_survives_agent_preamble_noise()
    {
        var noisy =
            "네, 이 계획을 도와드리겠습니다.\n\n" +
            "기대평가: 기존 테스트가 모두 통과하면 성공\n" +
            "코칭: 가설을 먼저 검증하세요.";
        Assert.Equal("기존 테스트가 모두 통과하면 성공",
            PdsaCoach.ParseTag(noisy, "기대평가", "expected"));
        Assert.DoesNotContain("기대평가:", PdsaCoach.StripTags(noisy));
    }
}
