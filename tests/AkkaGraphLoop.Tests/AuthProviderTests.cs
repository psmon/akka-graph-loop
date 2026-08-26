using System.Net.Http.Headers;
using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 인증 전략(<see cref="IAuthProvider"/>) 검증: ApiKey=Bearer, None=무헤더, OAuth 스텁 계약,
/// 그리고 무인증 자동허용의 사설대역 판정(<see cref="AuthProviders.IsPrivateEndpoint"/>).
/// 순수 로직이라 env/파일에 의존하지 않는다.
/// </summary>
public class AuthProviderTests
{
    [Fact]
    public async Task ApiKeyAuth_emits_bearer_header()
    {
        var h = await new ApiKeyAuth("sk-test-123").GetHeaderAsync();
        Assert.NotNull(h);
        Assert.Equal("Bearer", h!.Scheme);
        Assert.Equal("sk-test-123", h.Parameter);
    }

    [Fact]
    public async Task NoAuth_emits_null_header()
        => Assert.Null(await new NoAuth().GetHeaderAsync());

    [Fact]
    public async Task OAuthAuth_uses_existing_access_token()
    {
        var h = await new OAuthAuth(new OAuthOptions(AccessToken: "tok-abc")).GetHeaderAsync();
        Assert.Equal("Bearer", h!.Scheme);
        Assert.Equal("tok-abc", h.Parameter);
    }

    [Fact]
    public async Task OAuthAuth_without_token_fails_clearly()
    {
        // 계약: 토큰 없으면 명확히 실패(사이클 C 예정). ApiKey/None 경로엔 영향 없음.
        await Assert.ThrowsAsync<NotSupportedException>(
            () => new OAuthAuth(new OAuthOptions()).GetHeaderAsync());
    }

    [Fact]
    public void Factory_maps_authmode_to_provider()
    {
        Assert.IsType<ApiKeyAuth>(AuthProviders.Create(Opt(AuthMode.ApiKey)));
        Assert.IsType<NoAuth>(AuthProviders.Create(Opt(AuthMode.None)));
        Assert.IsType<OAuthAuth>(AuthProviders.Create(Opt(AuthMode.OAuth, oauth: new OAuthOptions(AccessToken: "t"))));
    }

    [Theory]
    [InlineData("http://localhost:11434/v1", true)]
    [InlineData("http://127.0.0.1:1234/v1", true)]
    [InlineData("http://[::1]:8080/v1", true)]
    [InlineData("http://192.168.1.50:11434/v1", true)]
    [InlineData("http://10.0.0.5/v1", true)]
    [InlineData("http://172.16.4.4/v1", true)]
    [InlineData("http://172.32.4.4/v1", false)]         // 172.32 은 사설 범위 밖
    [InlineData("http://ollama.local/v1", true)]        // mDNS
    [InlineData("https://api.openai.com/v1", false)]    // 원격 공인
    [InlineData("https://my-remote-host/v1", false)]    // DNS 이름 → 재해석 위험, 자동허용 안 함
    public void IsPrivateEndpoint_allows_only_loopback_and_private(string url, bool expected)
        => Assert.Equal(expected, AuthProviders.IsPrivateEndpoint(url));

    private static LlmOptions Opt(AuthMode auth, OAuthOptions? oauth = null)
        => new("http://localhost/v1", "sk-x", "m", null, auth, oauth);
}
