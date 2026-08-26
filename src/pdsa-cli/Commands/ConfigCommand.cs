using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>
/// LLM 설정. 키와 모델을 <b>분리</b>해서 설정한다(키 설정 후 모델만 갈아끼우기 가능).
/// 키는 직접 입력하거나 <b>파일 위치</b>로 지정할 수 있다(키 미노출).
/// </summary>
public sealed class ConfigCommand : ICliCommand
{
    public string Name => "config";
    public string Summary => "LLM 키/모델 설정(분리) — 키 직접 또는 파일 위치";
    public string Usage =>
        "pdsa config key <키> | key-file <파일> | model <모델> | reasoning <none|low|medium|high|xhigh|max> | base-url <URL> "
        + "| provider <local|openai-compat [URL]|openai> | auth <apikey|oauth|none> | allow-insecure-no-auth <true|false> "
        + "| oauth <endpoint|device-endpoint|client|refresh-token|refresh-token-file> <값> | login | lang <en|ko|auto> | show";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help") || args.Length == 0 || args[0] is "show")
        {
            if (args.Length == 0 || args.ElementAtOrDefault(0) is "show" or "--help" or null)
                Show();
            return 0;
        }

        var sub = args[0];
        var value = string.Join(' ', args.Skip(1)).Trim();

        switch (sub)
        {
            case "key":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config key <키>");
                Console.WriteLine($"저장됨(키): {OpenAiConfig.SetKey(value)}");
                break;
            case "key-file":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config key-file <파일경로>");
                if (!File.Exists(value)) Console.WriteLine($"주의: 파일이 아직 없습니다: {value}");
                Console.WriteLine($"저장됨(키파일 참조): {OpenAiConfig.SetKeyFile(value)}");
                break;
            case "model":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config model <모델>");
                Console.WriteLine($"저장됨(모델={value}): {OpenAiConfig.SetModel(value)}");
                break;
            case "reasoning":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config reasoning <none|low|medium|high|xhigh|max>");
                Console.WriteLine($"저장됨(reasoning={value}): {OpenAiConfig.SetReasoning(value)}");
                break;
            case "base-url":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config base-url <URL>");
                Console.WriteLine($"저장됨(base-url={value}): {OpenAiConfig.SetBaseUrl(value)}");
                break;
            case "provider":
            {
                var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) return Fail("사용법: pdsa config provider <local|openai-compat [URL]|openai>");
                var name = parts[0];
                var url = parts.Length > 1 ? parts[1] : null;
                try
                {
                    var saved = OpenAiConfig.SetProvider(name, url);
                    Console.WriteLine($"저장됨(provider={name}): {saved}");
                    if (name is "openai-compat" or "local")
                        Console.WriteLine("  참고: auth_mode=none 로 설정됨. 원격 URL 무인증 사용은 `allow-insecure-no-auth true` opt-in 필요.");
                }
                catch (ArgumentException ex) { return Fail(ex.Message); }
                break;
            }
            case "auth":
                if (value is not ("apikey" or "oauth" or "none"))
                    return Fail("사용법: pdsa config auth <apikey|oauth|none>");
                Console.WriteLine($"저장됨(auth={value}): {OpenAiConfig.SetAuthMode(ParseAuth(value))}");
                break;
            case "lang":
                if (value is not ("en" or "ko" or "auto"))
                    return Fail("사용법: pdsa config lang <en|ko|auto>   (auto=OS 로케일 자동)");
                Console.WriteLine($"저장됨(lang={value}): {OpenAiConfig.SetLang(value)}");
                break;
            case "allow-insecure-no-auth":
                if (!bool.TryParse(value, out var allow)) return Fail("사용법: pdsa config allow-insecure-no-auth <true|false>");
                if (allow) Console.WriteLine("경고: 원격 엔드포인트에 인증 없이 요청을 보낼 수 있습니다.");
                Console.WriteLine($"저장됨(allow-insecure-no-auth={allow}): {OpenAiConfig.SetAllowInsecureNoAuth(allow)}");
                break;
            case "oauth":
            {
                var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var oarg = parts.Length > 1 ? parts[1] : "";
                switch (parts.ElementAtOrDefault(0))
                {
                    case "endpoint" when oarg.Length > 0: Console.WriteLine($"저장됨(oauth token endpoint): {OpenAiConfig.SetOAuthEndpoint(oarg)}"); break;
                    case "device-endpoint" when oarg.Length > 0: Console.WriteLine($"저장됨(oauth device endpoint): {OpenAiConfig.SetOAuthDeviceEndpoint(oarg)}"); break;
                    case "client" when oarg.Length > 0: Console.WriteLine($"저장됨(oauth client): {OpenAiConfig.SetOAuthClient(oarg)}"); break;
                    case "refresh-token" when oarg.Length > 0: Console.WriteLine($"저장됨(oauth refresh-token): {OpenAiConfig.SetOAuthRefreshToken(oarg)}"); break;
                    case "refresh-token-file" when oarg.Length > 0: Console.WriteLine($"저장됨(oauth refresh-token-file): {OpenAiConfig.SetOAuthRefreshTokenFile(oarg)}"); break;
                    default: return Fail("사용법: pdsa config oauth <endpoint|device-endpoint|client|refresh-token|refresh-token-file> <값>");
                }
                break;
            }
            case "login":
                return await LoginAsync(ct);
            default:
                return Fail($"알 수 없는 하위 명령: {sub}\n{Usage}");
        }

        Console.WriteLine();
        Show();
        return 0;
    }

    /// <summary>device-code 흐름으로 OAuth 로그인 후 토큰을 영속화한다.</summary>
    private static async Task<int> LoginAsync(CancellationToken ct)
    {
        var (deviceEndpoint, tokenEndpoint, clientId, scope) = OpenAiConfig.OAuthLoginConfig();
        if (string.IsNullOrWhiteSpace(deviceEndpoint) || string.IsNullOrWhiteSpace(tokenEndpoint))
            return Fail("OAuth 로그인 설정이 필요합니다:\n" +
                        "  pdsa config oauth device-endpoint <URL>\n" +
                        "  pdsa config oauth endpoint <token URL>\n" +
                        "  pdsa config oauth client <client_id>   (필요 시)");

        using var client = new HttpDeviceCodeClient();
        try
        {
            var token = await DeviceCodeLogin.RunAsync(
                client, deviceEndpoint!, tokenEndpoint!, clientId, scope,
                prompt: s =>
                {
                    Console.WriteLine("\n브라우저에서 아래 주소를 열고 코드를 입력해 인증하세요:");
                    Console.WriteLine($"  URL : {(string.IsNullOrEmpty(s.VerificationUriComplete) ? s.VerificationUri : s.VerificationUriComplete)}");
                    Console.WriteLine($"  코드: {s.UserCode}");
                    Console.WriteLine("승인 대기 중…");
                },
                delay: (sec, c) => Task.Delay(TimeSpan.FromSeconds(sec), c),
                nowUnix: () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ct: ct);

            OpenAiConfig.SetAuthMode(AuthMode.OAuth);
            OpenAiConfig.PersistOAuthToken(token);
            Console.WriteLine("✔ 로그인 성공 — access token 저장됨.");
            Console.WriteLine();
            Show();
            return 0;
        }
        catch (Exception ex)
        {
            return Fail($"✘ 로그인 실패: {ex.Message}");
        }
    }

    private static void Show()
    {
        var (url, masked, model, reasoning, auth, source, ok) = OpenAiConfig.Describe();
        var langCfg = OpenAiConfig.ReadLang() ?? "auto";
        Console.WriteLine($"base_url  : {url}");
        Console.WriteLine($"model     : {model}");
        Console.WriteLine($"auth_mode : {auth}");
        Console.WriteLine($"lang      : {langCfg}  (effective: {PdsaLang.Resolve(System.Array.Empty<string>())})");
        Console.WriteLine($"reasoning : {reasoning}");
        Console.WriteLine($"api_key   : {masked}  ({source})");
        Console.WriteLine($"상태      : {(ok ? "설정됨 — `pdsa check` 로 호출 확인" : "미설정")}");
    }

    private static AuthMode ParseAuth(string v) => v switch
    {
        "oauth" => AuthMode.OAuth,
        "none" => AuthMode.None,
        _ => AuthMode.ApiKey,
    };

    private static int Fail(string msg) { Console.Error.WriteLine(msg); return 2; }
}
