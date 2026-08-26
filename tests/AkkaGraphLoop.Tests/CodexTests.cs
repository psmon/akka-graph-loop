using System.Text;
using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// Codex(GPT 구독) OAuth 경로 검증: JWT(exp/account_id) 파싱, auth.json 읽기/재기록,
/// Responses API SSE 파싱. 모두 순수/주입형이라 실제 Codex 계정 없이 격리 검증된다.
/// </summary>
public class CodexTests
{
    // {"exp":<exp>,"https://api.openai.com/auth":{"chatgpt_account_id":"<acct>"}} 를 담은 가짜 JWT.
    private static string FakeJwt(long exp, string? acct)
    {
        var auth = acct is null ? "" : $",\"https://api.openai.com/auth\":{{\"chatgpt_account_id\":\"{acct}\"}}";
        var payload = $"{{\"exp\":{exp}{auth}}}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "hdr." + b64 + ".sig";
    }

    [Fact]
    public void Jwt_exp_and_account_id_are_extracted()
    {
        var jwt = FakeJwt(1_700_000_000, "acct-XYZ");
        Assert.Equal(1_700_000_000, Codex.ExpiresAtUnix(jwt));
        Assert.Equal("acct-XYZ", Codex.AccountIdFromJwt(jwt));
    }

    [Theory]
    [InlineData(2000, 1000, false)]   // exp 여유 → 아직 유효
    [InlineData(1000, 1000, true)]    // 이미 만료 시각 → 갱신 필요
    [InlineData(1100, 1000, true)]    // skew(120) 안에 듦 → 갱신
    public void IsExpiring_respects_skew(long exp, long now, bool expected)
        => Assert.Equal(expected, Codex.IsExpiring(FakeJwt(exp, "a"), now));

    [Fact]
    public void IsExpiring_true_when_exp_unknown()
        => Assert.True(Codex.IsExpiring("not-a-jwt", 1000));

    [Fact]
    public void Load_reads_tokens_and_falls_back_to_jwt_account_id()
    {
        var path = TempFile();
        var jwt = FakeJwt(1_900_000_000, "acct-from-jwt");
        File.WriteAllText(path, $$"""{ "tokens": { "access_token": "{{jwt}}", "refresh_token": "r1", "id_token": "i1" } }""");
        try
        {
            var t = Codex.Load(path)!;
            Assert.Equal(jwt, t.AccessToken);
            Assert.Equal("r1", t.RefreshToken);
            Assert.Equal("acct-from-jwt", t.AccountId);   // tokens.account_id 없으면 JWT 에서
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Load_prefers_explicit_account_id_field()
    {
        var path = TempFile();
        File.WriteAllText(path, $$"""{ "tokens": { "access_token": "{{FakeJwt(1_900_000_000, "jwt-acct")}}", "refresh_token": "r", "account_id": "explicit-acct" } }""");
        try { Assert.Equal("explicit-acct", Codex.Load(path)!.AccountId); }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Load_returns_null_when_missing_required_fields()
    {
        var path = TempFile();
        File.WriteAllText(path, """{ "tokens": { "access_token": "a" } }""");   // refresh_token 없음
        try { Assert.Null(Codex.Load(path)); }
        finally { TryDelete(path); }
    }

    [Fact]
    public void Persist_updates_tokens_and_preserves_other_keys()
    {
        var path = TempFile();
        File.WriteAllText(path, """{ "auth_mode": "chatgpt", "OPENAI_API_KEY": "keep", "tokens": { "access_token": "old", "refresh_token": "oldr" } }""");
        try
        {
            Codex.Persist(new CodexTokens("newAccess", "newRefresh", "idtok", "acct"), path);
            var reread = Codex.Load(path)!;
            Assert.Equal("newAccess", reread.AccessToken);
            Assert.Equal("newRefresh", reread.RefreshToken);
            // 다른 최상위 키 보존
            var raw = File.ReadAllText(path);
            Assert.Contains("\"OPENAI_API_KEY\"", raw);
            Assert.Contains("keep", raw);
        }
        finally { TryDelete(path); }
    }

    // ── Responses API SSE 파싱 ──
    [Fact]
    public async Task Sse_accumulates_output_text_deltas()
    {
        var sse =
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello\"}\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\" world\"}\n" +
            "data: [DONE]\n";
        Assert.Equal("Hello world", await CodexClient.ParseSseAsync(Stream(sse), default));
    }

    [Fact]
    public async Task Sse_completed_event_finalizes_from_response_output()
    {
        var sse =
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"final answer\"}]}]}}\n";
        Assert.Equal("final answer", await CodexClient.ParseSseAsync(Stream(sse), default));
    }

    [Fact]
    public async Task Sse_ignores_non_data_and_malformed_lines()
    {
        var sse =
            ": comment\n" +
            "event: ping\n" +
            "data: not-json\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"ok\"}\n";
        Assert.Equal("ok", await CodexClient.ParseSseAsync(Stream(sse), default));
    }

    private static Stream Stream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"pdsa-codex-{Guid.NewGuid():N}.json");
    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
}
