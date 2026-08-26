using System.Text.Json;

namespace PdsaCli.Llm;

/// <summary>OAuth 토큰 한 벌(access + 선택적 refresh + 만료 유닉스초). ExpiresAtUnix=0 이면 만료 불명.</summary>
public sealed record OAuthToken(string AccessToken, string? RefreshToken, long ExpiresAtUnix);

/// <summary>refresh_token 으로 새 access token 을 받는 전략(주입형 — 테스트에서 fake 로 대체).</summary>
public interface ITokenRefresher
{
    Task<OAuthToken> RefreshAsync(OAuthOptions options, CancellationToken ct = default);
}

/// <summary>
/// 표준 OAuth2 <c>grant_type=refresh_token</c> 로 토큰을 갱신한다(form-urlencoded POST).
/// 응답 파싱은 <see cref="JsonDocument"/>(리플렉션 미사용, AOT-safe). HttpMessageHandler 주입으로 테스트 가능.
/// </summary>
public sealed class HttpTokenRefresher : ITokenRefresher, IDisposable
{
    private readonly HttpClient _http;
    private readonly Func<long> _nowUnix;

    public HttpTokenRefresher(HttpMessageHandler? handler = null, Func<long>? nowUnix = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _nowUnix = nowUnix ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async Task<OAuthToken> RefreshAsync(OAuthOptions o, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(o.TokenEndpoint))
            throw new InvalidOperationException("oauth token_endpoint 가 설정되지 않았습니다. pdsa config oauth endpoint <URL>");
        if (string.IsNullOrWhiteSpace(o.RefreshToken))
            throw new InvalidOperationException("refresh_token 이 없습니다. pdsa config login 으로 로그인하세요.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = o.RefreshToken!,
        };
        if (!string.IsNullOrWhiteSpace(o.ClientId)) form["client_id"] = o.ClientId!;

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(o.TokenEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"토큰 갱신 실패({(int)resp.StatusCode}): {Truncate(body, 300)}");

        return ParseToken(body, _nowUnix);
    }

    /// <summary>토큰 응답 JSON 을 파싱한다(access_token 필수, refresh_token/expires_in 선택).</summary>
    internal static OAuthToken ParseToken(string json, Func<long> nowUnix)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        if (string.IsNullOrWhiteSpace(access))
            throw new InvalidOperationException("응답에 access_token 이 없습니다.");
        string? refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        long expiresAt = 0;
        if (root.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var secs) && secs > 0)
            expiresAt = nowUnix() + secs;
        return new OAuthToken(access!, refresh, expiresAt);
    }

    internal static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    public void Dispose() => _http.Dispose();
}

// ── device-code 흐름 ─────────────────────────────────────────────────────────

/// <summary>device authorization 시작 응답(사용자 코드/검증 URL/폴링 간격).</summary>
public sealed record DeviceCodeStart(
    string DeviceCode, string UserCode, string VerificationUri,
    string? VerificationUriComplete, int IntervalSeconds, int ExpiresInSeconds);

public enum DevicePollStatus { Pending, SlowDown, Success, Denied, Expired, Error }

/// <summary>토큰 폴링 1회 결과.</summary>
public sealed record DevicePoll(DevicePollStatus Status, OAuthToken? Token, string? Error);

/// <summary>device-code 흐름의 시작·폴링 I/O(주입형 — 테스트에서 fake 로 대체).</summary>
public interface IDeviceCodeClient
{
    Task<DeviceCodeStart> StartAsync(string deviceAuthEndpoint, string? clientId, string? scope, CancellationToken ct = default);
    Task<DevicePoll> PollAsync(string tokenEndpoint, string? clientId, string deviceCode, CancellationToken ct = default);
}

/// <summary>실제 HTTP 기반 device-code I/O(RFC 8628). HttpMessageHandler 주입으로 테스트 가능.</summary>
public sealed class HttpDeviceCodeClient : IDeviceCodeClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly Func<long> _nowUnix;

    public HttpDeviceCodeClient(HttpMessageHandler? handler = null, Func<long>? nowUnix = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _nowUnix = nowUnix ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async Task<DeviceCodeStart> StartAsync(string deviceAuthEndpoint, string? clientId, string? scope, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(clientId)) form["client_id"] = clientId!;
        if (!string.IsNullOrWhiteSpace(scope)) form["scope"] = scope!;
        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(deviceAuthEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"device 인증 시작 실패({(int)resp.StatusCode}): {HttpTokenRefresher.Truncate(body, 300)}");

        using var doc = JsonDocument.Parse(body);
        var r = doc.RootElement;
        string S(string n) => r.TryGetProperty(n, out var v) ? v.GetString() ?? "" : "";
        int I(string n, int d) => r.TryGetProperty(n, out var v) && v.TryGetInt32(out var i) ? i : d;
        return new DeviceCodeStart(S("device_code"), S("user_code"), S("verification_uri"),
            r.TryGetProperty("verification_uri_complete", out var vc) ? vc.GetString() : null,
            I("interval", 5), I("expires_in", 900));
    }

    public async Task<DevicePoll> PollAsync(string tokenEndpoint, string? clientId, string deviceCode, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = deviceCode,
        };
        if (!string.IsNullOrWhiteSpace(clientId)) form["client_id"] = clientId!;
        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(tokenEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
            return new DevicePoll(DevicePollStatus.Success, HttpTokenRefresher.ParseToken(body, _nowUnix), null);

        // 오류 응답: error 코드로 상태 판별(RFC 8628 §3.5).
        string? error = null;
        try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("error", out var e)) error = e.GetString(); }
        catch { }
        return error switch
        {
            "authorization_pending" => new DevicePoll(DevicePollStatus.Pending, null, error),
            "slow_down" => new DevicePoll(DevicePollStatus.SlowDown, null, error),
            "access_denied" => new DevicePoll(DevicePollStatus.Denied, null, error),
            "expired_token" => new DevicePoll(DevicePollStatus.Expired, null, error),
            _ => new DevicePoll(DevicePollStatus.Error, null, error ?? HttpTokenRefresher.Truncate(body, 200)),
        };
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// device-code 로그인 드라이버: 시작 → 사용자 코드 안내 → 성공/거부/만료/타임아웃까지 폴링.
/// 지연(<paramref name="delay"/>)과 <see cref="IDeviceCodeClient"/> 를 주입해 상태머신을 sleep 없이 테스트한다.
/// </summary>
public static class DeviceCodeLogin
{
    public static async Task<OAuthToken> RunAsync(
        IDeviceCodeClient client, string deviceAuthEndpoint, string tokenEndpoint,
        string? clientId, string? scope,
        Action<DeviceCodeStart> prompt,
        Func<int, CancellationToken, Task> delay,
        Func<long> nowUnix,
        CancellationToken ct = default)
    {
        var start = await client.StartAsync(deviceAuthEndpoint, clientId, scope, ct);
        prompt(start);

        var interval = Math.Max(1, start.IntervalSeconds);
        var deadline = nowUnix() + (start.ExpiresInSeconds > 0 ? start.ExpiresInSeconds : 900);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (nowUnix() >= deadline)
                throw new TimeoutException("device-code 인증이 제한 시간 내에 완료되지 않았습니다.");

            await delay(interval, ct);
            var poll = await client.PollAsync(tokenEndpoint, clientId, start.DeviceCode, ct);
            switch (poll.Status)
            {
                case DevicePollStatus.Success when poll.Token is not null:
                    return poll.Token;
                case DevicePollStatus.Pending:
                    break;                               // 계속 폴링
                case DevicePollStatus.SlowDown:
                    interval += 5;                       // RFC 8628: 간격 증가
                    break;
                case DevicePollStatus.Denied:
                    throw new InvalidOperationException("사용자가 인증을 거부했습니다.");
                case DevicePollStatus.Expired:
                    throw new TimeoutException("device_code 가 만료되었습니다. 다시 시도하세요.");
                default:
                    throw new InvalidOperationException($"device-code 폴링 오류: {poll.Error ?? "알 수 없음"}");
            }
        }
    }
}
