using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// OAuth 인증(<see cref="OAuthAuth"/>)의 토큰 라이프사이클 검증: 유효토큰 사용, 만료→refresh→Bearer,
/// persist 콜백, 실패경로. now 와 <see cref="ITokenRefresher"/> 를 주입해 sleep·네트워크 없이 격리한다.
/// </summary>
public class OAuthAuthTests
{
    private const long Now = 1_000;

    private sealed class FakeRefresher : ITokenRefresher
    {
        public int Calls;
        public OAuthToken? Next;
        public Exception? Throw;
        public Task<OAuthToken> RefreshAsync(OAuthOptions o, CancellationToken ct = default)
        {
            Calls++;
            if (Throw is not null) throw Throw;
            return Task.FromResult(Next!);
        }
    }

    [Fact]
    public async Task Valid_access_token_is_used_without_refresh()
    {
        var refr = new FakeRefresher();
        var auth = new OAuthAuth(
            new OAuthOptions(AccessToken: "valid-tok", RefreshToken: "r", ExpiresAtUnix: Now + 600),
            refr, nowUnix: () => Now);

        var h = await auth.GetHeaderAsync();

        Assert.Equal("valid-tok", h!.Parameter);
        Assert.Equal(0, refr.Calls);            // 갱신 안 함
    }

    [Fact]
    public async Task Unknown_expiry_token_is_used_as_is()
    {
        var refr = new FakeRefresher();
        var auth = new OAuthAuth(new OAuthOptions(AccessToken: "direct", ExpiresAtUnix: 0), refr, nowUnix: () => Now);
        var h = await auth.GetHeaderAsync();
        Assert.Equal("direct", h!.Parameter);
        Assert.Equal(0, refr.Calls);
    }

    [Fact]
    public async Task Expired_token_triggers_refresh_and_persist()
    {
        var refr = new FakeRefresher { Next = new OAuthToken("new-access", "new-refresh", Now + 3600) };
        OAuthToken? persisted = null;
        var auth = new OAuthAuth(
            new OAuthOptions(AccessToken: "old", RefreshToken: "old-refresh", ExpiresAtUnix: Now - 1),
            refr, onRefreshed: t => persisted = t, nowUnix: () => Now);

        var h = await auth.GetHeaderAsync();

        Assert.Equal("new-access", h!.Parameter);   // 새 토큰으로 Bearer
        Assert.Equal(1, refr.Calls);
        Assert.Equal("new-access", persisted!.AccessToken);
        Assert.Equal("new-refresh", persisted.RefreshToken);
    }

    [Fact]
    public async Task Refresh_without_new_refresh_token_keeps_old_one()
    {
        var refr = new FakeRefresher { Next = new OAuthToken("new-access", null, Now + 3600) };
        OAuthToken? persisted = null;
        var auth = new OAuthAuth(
            new OAuthOptions(AccessToken: "old", RefreshToken: "keep-me", ExpiresAtUnix: Now - 1),
            refr, onRefreshed: t => persisted = t, nowUnix: () => Now);

        await auth.GetHeaderAsync();

        Assert.Equal("keep-me", persisted!.RefreshToken);   // 응답에 refresh 없으면 기존 유지
    }

    [Fact]
    public async Task Second_call_after_refresh_reuses_cached_token()
    {
        var refr = new FakeRefresher { Next = new OAuthToken("fresh", "r2", Now + 3600) };
        var auth = new OAuthAuth(
            new OAuthOptions(AccessToken: "old", RefreshToken: "r", ExpiresAtUnix: Now - 1),
            refr, nowUnix: () => Now);

        await auth.GetHeaderAsync();
        var h2 = await auth.GetHeaderAsync();

        Assert.Equal("fresh", h2!.Parameter);
        Assert.Equal(1, refr.Calls);            // 캐시 재사용 — 재갱신 안 함
    }

    [Fact]
    public async Task No_token_and_no_refresh_fails_clearly()
    {
        var auth = new OAuthAuth(new OAuthOptions(), new FakeRefresher(), nowUnix: () => Now);
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => auth.GetHeaderAsync());
        Assert.Contains("pdsa config login", ex.Message);
    }

    [Fact]
    public async Task Refresh_failure_propagates()
    {
        var refr = new FakeRefresher { Throw = new InvalidOperationException("토큰 갱신 실패(401)") };
        var auth = new OAuthAuth(
            new OAuthOptions(AccessToken: "old", RefreshToken: "bad", ExpiresAtUnix: Now - 1),
            refr, nowUnix: () => Now);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => auth.GetHeaderAsync());
        Assert.Contains("갱신 실패", ex.Message);
    }

    [Fact]
    public void ParseToken_reads_fields_and_computes_expiry()
    {
        var tok = HttpTokenRefresher.ParseToken(
            """{ "access_token": "A", "refresh_token": "R", "expires_in": 3600 }""", () => Now);
        Assert.Equal("A", tok.AccessToken);
        Assert.Equal("R", tok.RefreshToken);
        Assert.Equal(Now + 3600, tok.ExpiresAtUnix);
    }

    [Fact]
    public void ParseToken_without_access_token_throws()
        => Assert.Throws<InvalidOperationException>(() => HttpTokenRefresher.ParseToken("""{ "error": "x" }""", () => Now));
}
