using System.Net;
using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 실제 HTTP transport(<see cref="HttpTokenRefresher"/>/<see cref="HttpDeviceCodeClient"/>) 를
/// 스텁 <see cref="HttpMessageHandler"/> 로 검증한다(네트워크 없이 요청/응답 계약 고정).
/// </summary>
public class OAuthHttpTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastBody = req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct);
            var (code, body) = respond(req, LastBody);
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }
    }

    [Fact]
    public async Task Refresher_posts_refresh_grant_and_parses_token()
    {
        var stub = new StubHandler((_, _) => (HttpStatusCode.OK,
            """{ "access_token": "AA", "refresh_token": "RR", "expires_in": 100 }"""));
        using var refr = new HttpTokenRefresher(stub, nowUnix: () => 1000);

        var tok = await refr.RefreshAsync(new OAuthOptions(
            TokenEndpoint: "https://auth/token", ClientId: "cid", RefreshToken: "old-refresh"));

        Assert.Equal("AA", tok.AccessToken);
        Assert.Equal("RR", tok.RefreshToken);
        Assert.Equal(1100, tok.ExpiresAtUnix);
        Assert.Contains("grant_type=refresh_token", stub.LastBody);
        Assert.Contains("refresh_token=old-refresh", stub.LastBody);
        Assert.Contains("client_id=cid", stub.LastBody);
    }

    [Fact]
    public async Task Refresher_maps_http_error_to_exception()
    {
        var stub = new StubHandler((_, _) => (HttpStatusCode.Unauthorized, """{ "error": "invalid_grant" }"""));
        using var refr = new HttpTokenRefresher(stub);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => refr.RefreshAsync(new OAuthOptions(TokenEndpoint: "https://auth/token", RefreshToken: "x")));
        Assert.Contains("갱신 실패", ex.Message);
    }

    [Fact]
    public async Task Refresher_requires_endpoint_and_refresh_token()
    {
        using var refr = new HttpTokenRefresher(new StubHandler((_, _) => (HttpStatusCode.OK, "{}")));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => refr.RefreshAsync(new OAuthOptions(RefreshToken: "x")));           // endpoint 없음
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => refr.RefreshAsync(new OAuthOptions(TokenEndpoint: "https://t")));   // refresh 없음
    }

    [Fact]
    public async Task DeviceClient_start_parses_user_code_and_interval()
    {
        var stub = new StubHandler((_, _) => (HttpStatusCode.OK,
            """{ "device_code": "DC", "user_code": "WXYZ", "verification_uri": "https://v", "interval": 7, "expires_in": 600 }"""));
        using var client = new HttpDeviceCodeClient(stub);

        var start = await client.StartAsync("https://auth/device", "cid", "openid", default);

        Assert.Equal("DC", start.DeviceCode);
        Assert.Equal("WXYZ", start.UserCode);
        Assert.Equal(7, start.IntervalSeconds);
        Assert.Equal(600, start.ExpiresInSeconds);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "authorization_pending" }""", DevicePollStatus.Pending)]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "slow_down" }""", DevicePollStatus.SlowDown)]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "access_denied" }""", DevicePollStatus.Denied)]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "expired_token" }""", DevicePollStatus.Expired)]
    public async Task DeviceClient_poll_maps_error_codes(HttpStatusCode code, string body, DevicePollStatus expected)
    {
        using var client = new HttpDeviceCodeClient(new StubHandler((_, _) => (code, body)));
        var poll = await client.PollAsync("https://auth/token", "cid", "DC", default);
        Assert.Equal(expected, poll.Status);
    }

    [Fact]
    public async Task DeviceClient_poll_success_returns_token()
    {
        var stub = new StubHandler((_, _) => (HttpStatusCode.OK,
            """{ "access_token": "ACC", "refresh_token": "REF", "expires_in": 3600 }"""));
        using var client = new HttpDeviceCodeClient(stub, nowUnix: () => 2000);

        var poll = await client.PollAsync("https://auth/token", "cid", "DC", default);

        Assert.Equal(DevicePollStatus.Success, poll.Status);
        Assert.Equal("ACC", poll.Token!.AccessToken);
        Assert.Equal(5600, poll.Token.ExpiresAtUnix);
        Assert.Contains("grant_type=urn", stub.LastBody);
    }
}
