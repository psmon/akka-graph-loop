using System.Text.Json;
using System.Text.Json.Nodes;

namespace PdsaCli.Llm;

/// <summary>Codex(ChatGPT 구독) OAuth 토큰 한 벌. account_id 는 tokens 필드 또는 access_token(JWT) 클레임에서.</summary>
public sealed record CodexTokens(string AccessToken, string RefreshToken, string? IdToken, string? AccountId);

/// <summary>
/// Codex(GPT) OAuth 상수/도우미. 공식 <c>codex login</c> 이 만든 <c>~/.codex/auth.json</c> 을 재사용한다.
/// (Hermes 에이전트의 방식을 참조: auth.openai.com 로 refresh, chatgpt.com/backend-api/codex 로 추론.)
/// </summary>
public static class Codex
{
    public const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    public const string TokenUrl = "https://auth.openai.com/oauth/token";
    public const string DefaultBaseUrl = "https://chatgpt.com/backend-api/codex";
    public const string DefaultModel = "gpt-5-codex";
    public const string Originator = "codex_cli_rs";
    public const string UserAgent = "codex_cli_rs/0.0.0 (pdsa)";
    private const int RefreshSkewSeconds = 120;

    private static readonly JsonDocumentOptions Lenient =
        new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary><c>CODEX_HOME</c> &gt; <c>~/.codex</c> 기준 auth.json 경로.</summary>
    public static string AuthPath()
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME");
        var dir = string.IsNullOrWhiteSpace(home)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
            : home;
        return Path.Combine(dir, "auth.json");
    }

    /// <summary>auth.json 에서 토큰을 읽는다(없거나 손상/미완이면 null).</summary>
    public static CodexTokens? Load(string? path = null)
    {
        path ??= AuthPath();
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient);
            if (!doc.RootElement.TryGetProperty("tokens", out var t) || t.ValueKind != JsonValueKind.Object) return null;
            var access = Str(t, "access_token");
            var refresh = Str(t, "refresh_token");
            if (access is null || refresh is null) return null;
            return new CodexTokens(access, refresh, Str(t, "id_token"), Str(t, "account_id") ?? AccountIdFromJwt(access));
        }
        catch { return null; }
    }

    /// <summary>갱신된 토큰을 auth.json 에 원자적으로 다시 쓴다(다른 최상위 키 보존). codex CLI 가 계속 동작하도록.</summary>
    public static void Persist(CodexTokens tokens, string? path = null)
    {
        path = path ?? AuthPath();
        JsonObject root;
        try
        {
            root = (File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null) as JsonObject ?? new JsonObject();
        }
        catch { root = new JsonObject(); }

        var t = root["tokens"] as JsonObject ?? new JsonObject();
        t["access_token"] = tokens.AccessToken;
        t["refresh_token"] = tokens.RefreshToken;
        if (tokens.IdToken is not null) t["id_token"] = tokens.IdToken;
        if (tokens.AccountId is not null) t["account_id"] = tokens.AccountId;
        root["tokens"] = t;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);   // 원자적 교체
    }

    // ── JWT 도우미(순수) ──────────────────────────────────────────────────────

    /// <summary>access_token(JWT) 의 만료 유닉스초(<c>exp</c>). 파싱 불가면 0.</summary>
    public static long ExpiresAtUnix(string accessToken)
    {
        var payload = JwtPayload(accessToken);
        if (payload is null) return 0;
        return payload.Value.TryGetProperty("exp", out var e) && e.TryGetInt64(out var exp) ? exp : 0;
    }

    /// <summary>account_id 를 JWT 클레임 <c>["https://api.openai.com/auth"].chatgpt_account_id</c> 에서 추출.</summary>
    public static string? AccountIdFromJwt(string accessToken)
    {
        var payload = JwtPayload(accessToken);
        if (payload is null) return null;
        if (payload.Value.TryGetProperty("https://api.openai.com/auth", out var auth) && auth.ValueKind == JsonValueKind.Object)
            return Str(auth, "chatgpt_account_id");
        return null;
    }

    /// <summary>만료 임박(skew 포함) 또는 exp 불명이면 true → 갱신 필요.</summary>
    public static bool IsExpiring(string accessToken, long nowUnix, int skewSeconds = RefreshSkewSeconds)
    {
        var exp = ExpiresAtUnix(accessToken);
        return exp <= 0 || nowUnix + skewSeconds >= exp;
    }

    private static JsonElement? JwtPayload(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var json = System.Text.Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch { return null; }
    }

    internal static byte[] Base64UrlDecode(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b.PadRight(b.Length + (4 - b.Length % 4) % 4, '='));
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
}
