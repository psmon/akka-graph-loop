using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace PdsaCli.Llm;

/// <summary>
/// 요청별 Authorization 헤더를 만드는 인증 전략. <see cref="OpenAiClient"/> 가 요청마다 호출한다.
/// ctor 1회 고정이 아니라 요청 시 주입이므로 OAuth 토큰 갱신을 수용할 수 있다.
/// </summary>
public interface IAuthProvider
{
    /// <summary>이 요청에 쓸 인증 헤더. <c>null</c> 이면 무인증(헤더 없음).</summary>
    Task<AuthenticationHeaderValue?> GetHeaderAsync(CancellationToken ct = default);
}

/// <summary>정적 Bearer API 키(기존 동작 그대로).</summary>
public sealed class ApiKeyAuth(string apiKey) : IAuthProvider
{
    private readonly AuthenticationHeaderValue _header = new("Bearer", apiKey);
    public Task<AuthenticationHeaderValue?> GetHeaderAsync(CancellationToken ct = default)
        => Task.FromResult<AuthenticationHeaderValue?>(_header);
}

/// <summary>무인증. 키리스 로컬 엔드포인트용.</summary>
public sealed class NoAuth : IAuthProvider
{
    public Task<AuthenticationHeaderValue?> GetHeaderAsync(CancellationToken ct = default)
        => Task.FromResult<AuthenticationHeaderValue?>(null);
}

/// <summary>
/// OAuth 인증: 유효한 access token 이면 그대로 Bearer, 만료/부재면 refresh_token 으로 갱신 후 Bearer.
/// 갱신 성공 시 <paramref name="onRefreshed"/> 로 새 토큰을 영속화한다.
/// <paramref name="refresher"/>/<paramref name="nowUnix"/> 주입으로 sleep·네트워크 없이 테스트 가능.
/// </summary>
public sealed class OAuthAuth(
    OAuthOptions options,
    ITokenRefresher? refresher = null,
    Action<OAuthToken>? onRefreshed = null,
    Func<long>? nowUnix = null) : IAuthProvider
{
    private const int SkewSeconds = 30;   // 만료 직전 여유
    private OAuthOptions _options = options;
    private readonly ITokenRefresher _refresher = refresher ?? new HttpTokenRefresher();
    private readonly Func<long> _nowUnix = nowUnix ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public async Task<AuthenticationHeaderValue?> GetHeaderAsync(CancellationToken ct = default)
    {
        if (HasValidAccessToken())
            return new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        if (string.IsNullOrWhiteSpace(_options.RefreshToken))
            throw new NotSupportedException(
                "OAuth access token 이 만료/부재이고 refresh_token 도 없습니다. " +
                "로그인: pdsa config login   또는 API 키 사용: pdsa config auth apikey");

        var tok = await _refresher.RefreshAsync(_options, ct);
        // refresh 응답이 새 refresh_token 을 주지 않으면 기존 것을 유지.
        var merged = tok with { RefreshToken = tok.RefreshToken ?? _options.RefreshToken };
        _options = _options with
        {
            AccessToken = merged.AccessToken,
            RefreshToken = merged.RefreshToken,
            ExpiresAtUnix = merged.ExpiresAtUnix,
        };
        onRefreshed?.Invoke(merged);
        return new AuthenticationHeaderValue("Bearer", merged.AccessToken);
    }

    /// <summary>access token 이 있고, 만료 불명(0)이거나 skew 여유를 두고 아직 유효하면 true.</summary>
    private bool HasValidAccessToken()
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken)) return false;
        if (_options.ExpiresAtUnix <= 0) return true;                 // 만료 불명 → 있는 토큰 사용(직접 제공 호환)
        return _nowUnix() + SkewSeconds < _options.ExpiresAtUnix;     // 유효기간 내
    }
}

/// <summary>인증 전략 팩토리와 무인증 허용 규칙(사설대역 판정).</summary>
public static class AuthProviders
{
    /// <summary>옵션의 <see cref="AuthMode"/> 에 맞는 전략을 만든다.</summary>
    public static IAuthProvider Create(LlmOptions options) => options.Auth switch
    {
        AuthMode.None => new NoAuth(),
        // 갱신된 토큰을 전역 설정에 영속화해 매 호출 재갱신을 피한다.
        AuthMode.OAuth => new OAuthAuth(options.OAuth ?? new OAuthOptions(), onRefreshed: OpenAiConfig.PersistOAuthToken),
        _ => new ApiKeyAuth(options.ApiKey),
    };

    /// <summary>
    /// 무인증(None)이 자동 허용되는 호스트인지: loopback / 사설대역(RFC1918, ULA fc00::/7) / *.local.
    /// 원격 공인 호스트는 명시적 opt-in(<c>allow_insecure_no_auth</c>) 없이는 false.
    /// </summary>
    public static bool IsPrivateEndpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        if (string.IsNullOrEmpty(host)) return false;

        if (uri.IsLoopback) return true;                              // localhost, 127.*, ::1
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;  // mDNS

        if (IPAddress.TryParse(host, out var ip)) return IsPrivateIp(ip);
        return false; // DNS 이름은 재해석 위험 → 자동 허용하지 않음(원격으로 간주)
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                10 => true,                              // 10.0.0.0/8
                172 => b[1] >= 16 && b[1] <= 31,         // 172.16.0.0/12
                192 => b[1] == 168,                      // 192.168.0.0/16
                169 => b[1] == 254,                      // 169.254.0.0/16 link-local
                _ => false,
            };
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal) return true;
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;                // fc00::/7 unique local
        }
        return false;
    }
}
