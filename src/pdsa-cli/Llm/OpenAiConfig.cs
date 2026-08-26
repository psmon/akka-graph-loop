using System.Text.Json;
using System.Text.Json.Nodes;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Llm;

/// <summary>
/// OpenAI 설정 로더/저장기. 키 설정과 모델 설정을 분리하고, 키를 <b>파일 위치</b>로 지정할 수 있어
/// 키를 설정에 노출하지 않고도 동작한다.
///
/// 로드 우선순위(높을수록 우선): 환경변수 &gt; 전역 설정(<c>{LocalAppData}/pdsa-cli/openai.json</c>) &gt; 레포 <c>.secret/openai.json</c>.
/// 전역 설정의 <c>api_key_file</c> 이 있으면 그 파일에서 키를 읽는다(그 파일이 여기 포맷의 JSON 이면 그대로,
/// 아니면 파일 내용을 원시 키로 사용).
/// </summary>
public static class OpenAiConfig
{
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-5.6-terra";
    private const string PlaceholderPrefix = "sk-여기에";

    private static readonly JsonDocumentOptions Lenient =
        new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static bool TryLoad(out LlmOptions options, out string error)
    {
        options = Resolve();

        // 무인증(None): 키리스 로컬 오픈웨이트. 사설대역만 자동 허용, 원격은 명시적 opt-in 필요.
        if (options.Auth == AuthMode.None)
        {
            if (AuthProviders.IsPrivateEndpoint(options.BaseUrl) || ReadGlobalBool("allow_insecure_no_auth"))
            {
                error = "";
                return true;
            }
            error =
                $"무인증(auth_mode=none) 은 로컬/사설대역 엔드포인트에만 자동 허용됩니다.\n" +
                $"  현재 base_url: {options.BaseUrl} (원격으로 판단)\n" +
                "  원격에 무인증으로 접속하려면 명시적 opt-in 이 필요합니다:\n" +
                "    pdsa config allow-insecure-no-auth true   (경고: 인증 없이 원격 전송)\n" +
                "  또는 로컬 프리셋: pdsa config provider local";
            return false;
        }

        // Claude Code CLI(claude -p): 이미 로그인된 Claude 를 서브프로세스로 사용. 키/토큰 설정 불필요.
        if (options.Auth == AuthMode.ClaudeCli)
        {
            if (ClaudeCli.IsAvailable()) { error = ""; return true; }
            error =
                "Claude Code CLI(`claude`)를 찾을 수 없습니다.\n" +
                "  공식 Claude Code 를 설치·로그인한 뒤 사용하세요(별도 토큰 설정 불필요).\n" +
                "  경로 지정: pdsa config claude-cli-path <경로>   또는 env PDSA_CLAUDE_CLI";
            return false;
        }

        // Codex(GPT 구독): ~/.codex/auth.json 의 토큰을 재사용. 키 불필요.
        if (options.Auth == AuthMode.Codex)
        {
            if (Codex.Load() is not null) { error = ""; return true; }
            error =
                $"Codex 인증을 찾을 수 없습니다: {Codex.AuthPath()}\n" +
                "  공식 Codex CLI 로 로그인하세요: codex login   (ChatGPT Plus/Pro/Team/Enterprise 구독)\n" +
                "  또는 API 키 사용: pdsa config auth apikey";
            return false;
        }

        // OAuth: 키 요구 대신 access token 존재만 확인.
        if (options.Auth == AuthMode.OAuth)
        {
            if (!string.IsNullOrWhiteSpace(options.OAuth?.AccessToken)) { error = ""; return true; }
            error =
                "auth_mode=oauth 이지만 사용할 access_token 이 없습니다.\n" +
                "  OAuth 로그인/갱신은 아직 미구현(사이클 C)입니다. 현재는 access_token 이 있어야 동작합니다.\n" +
                "  API 키로 전환: pdsa config auth apikey";
            return false;
        }

        // ApiKey(기본): 기존 동작 그대로.
        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.StartsWith(PlaceholderPrefix))
        {
            error =
                "OpenAI API 키가 설정되지 않았습니다.\n" +
                "  키 직접:   pdsa config key <키>\n" +
                "  키 파일:   pdsa config key-file <파일경로>   (키를 설정에 노출하지 않음)\n" +
                "  모델:      pdsa config model <모델>\n" +
                "  키리스 로컬: pdsa config provider local\n" +
                $"  또는 환경변수 OPENAI_API_KEY / 파일 {GlobalPath()}";
            return false;
        }
        error = "";
        return true;
    }

    // ── 설정(분리): 키 / 키파일 / 모델 / base-url ──────────────────────────────
    public static string SetKey(string apiKey) => Update(o => { o["api_key"] = apiKey; o.Remove("api_key_file"); o["auth_mode"] = "apikey"; });
    public static string SetKeyFile(string path) => Update(o => { o["api_key_file"] = Path.GetFullPath(path); o.Remove("api_key"); o["auth_mode"] = "apikey"; });
    public static string SetModel(string model) => Update(o => o["model"] = model);
    public static string SetBaseUrl(string url) => Update(o => o["base_url"] = url);
    public static string SetReasoning(string effort) => Update(o => o["reasoning_effort"] = effort);
    public static string SetAuthMode(AuthMode mode) => Update(o => o["auth_mode"] = mode.ToString().ToLowerInvariant());
    /// <summary>표시/기록 언어. "auto"(또는 미상)면 설정을 지워 OS 로케일 자동 감지로 되돌린다.</summary>
    public static string SetLang(string lang) => Update(o =>
    {
        var v = lang.Trim().ToLowerInvariant();
        if (v is "auto" or "") o.Remove("lang"); else o["lang"] = v;
    });
    public static string? ReadLang() => ReadGlobalString("lang");
    public static string SetAllowInsecureNoAuth(bool allow) => Update(o => o["allow_insecure_no_auth"] = allow);

    /// <summary>provider 프리셋. 베이스값만 제공하고 사용자 개별설정(base_url/model)이 이후 재정의 가능.</summary>
    public static string SetProvider(string provider, string? url)
    {
        return provider.ToLowerInvariant() switch
        {
            "local" => Update(o =>
            {
                o["provider"] = "local";
                o["base_url"] = url ?? "http://localhost:11434/v1";
                o["auth_mode"] = "none";
            }),
            "openai-compat" => Update(o =>
            {
                o["provider"] = "openai-compat";
                if (!string.IsNullOrWhiteSpace(url)) o["base_url"] = url;
                o["auth_mode"] = "none";
            }),
            "openai" => Update(o =>
            {
                o["provider"] = "openai";
                o["base_url"] = DefaultBaseUrl;
                o["auth_mode"] = "apikey";
            }),
            _ => throw new ArgumentException($"알 수 없는 provider: {provider} (local|openai-compat|openai)"),
        };
    }

    // ── OAuth 설정/영속 ───────────────────────────────────────────────────────
    public static string SetOAuthEndpoint(string url) => Update(o => { o["oauth_token_endpoint"] = url; o["auth_mode"] = "oauth"; });
    public static string SetOAuthDeviceEndpoint(string url) => Update(o => o["oauth_device_endpoint"] = url);
    public static string SetOAuthClient(string clientId) => Update(o => o["oauth_client_id"] = clientId);
    public static string SetOAuthRefreshToken(string token) => Update(o => { o["oauth_refresh_token"] = token; o.Remove("oauth_refresh_token_file"); o["auth_mode"] = "oauth"; });
    public static string SetOAuthRefreshTokenFile(string path) => Update(o => { o["oauth_refresh_token_file"] = Path.GetFullPath(path); o.Remove("oauth_refresh_token"); o["auth_mode"] = "oauth"; });

    /// <summary>
    /// 갱신/로그인으로 얻은 토큰을 영속화한다. access_token/expires_at 는 전역 설정에,
    /// refresh_token 은 파일 참조(<c>oauth_refresh_token_file</c>)가 있으면 그 파일에(설정 미노출), 없으면 전역 설정에.
    /// </summary>
    public static void PersistOAuthToken(OAuthToken tok)
    {
        var refreshFile = ReadGlobalString("oauth_refresh_token_file");
        if (refreshFile is not null && tok.RefreshToken is { Length: > 0 })
        {
            try { File.WriteAllText(refreshFile, tok.RefreshToken); } catch { /* 파일 쓰기 실패는 무시(설정은 계속) */ }
        }
        Update(o =>
        {
            o["oauth_access_token"] = tok.AccessToken;
            o["oauth_expires_at"] = tok.ExpiresAtUnix;
            if (refreshFile is null && tok.RefreshToken is { Length: > 0 })
                o["oauth_refresh_token"] = tok.RefreshToken;
        });
    }

    /// <summary>device-code 로그인에 필요한 엔드포인트/클라이언트(전역 설정).</summary>
    public static (string? DeviceEndpoint, string? TokenEndpoint, string? ClientId, string? Scope) OAuthLoginConfig()
        => (ReadGlobalString("oauth_device_endpoint"), ReadGlobalString("oauth_token_endpoint"),
            ReadGlobalString("oauth_client_id"), ReadGlobalString("oauth_scope"));

    /// <summary>표시용 현재 설정(키는 마스킹, 출처 표기).</summary>
    public static (string BaseUrl, string MaskedKey, string Model, string Reasoning, string Auth, string KeySource, bool Configured) Describe()
    {
        var o = Resolve();
        var configured = o.Auth switch
        {
            AuthMode.None => AuthProviders.IsPrivateEndpoint(o.BaseUrl) || ReadGlobalBool("allow_insecure_no_auth"),
            // access token 이 있거나, refresh_token + token_endpoint 로 갱신 가능하면 설정된 것으로 본다.
            AuthMode.OAuth => !string.IsNullOrWhiteSpace(o.OAuth?.AccessToken)
                || (!string.IsNullOrWhiteSpace(o.OAuth?.RefreshToken) && !string.IsNullOrWhiteSpace(o.OAuth?.TokenEndpoint)),
            AuthMode.Codex => Codex.Load() is not null,
            AuthMode.ClaudeCli => ClaudeCli.IsAvailable(),
            _ => !string.IsNullOrWhiteSpace(o.ApiKey) && !o.ApiKey.StartsWith(PlaceholderPrefix),
        };
        var authLabel = o.Auth switch
        {
            AuthMode.None => "none(키리스)",
            AuthMode.OAuth => "oauth",
            AuthMode.Codex => "codex(GPT 구독)",
            AuthMode.ClaudeCli => "claude-cli(claude -p)",
            _ => "apikey",
        };
        return (o.BaseUrl, Mask(o.ApiKey), o.Model, o.ReasoningEffort ?? "(모델 기본)", authLabel, KeySource(), configured);
    }

    // ── 경로 seam(테스트 격리용) ─────────────────────────────────────────────
    // 미설정 시 기존 프로덕션 경로를 그대로 쓴다(하위호환). 테스트에서만 override 후 반드시 복원.
    internal static string? GlobalPathOverride;
    internal static string? RepoPathOverride;
    // GlobalPathOverride(테스트) > PDSA_GLOBAL_CONFIG(env, 스크립트 격리용) > 기본 경로.
    // GetFolderPath 는 LOCALAPPDATA env 를 따르지 않으므로, 전역설정을 다른 곳으로 돌리려면 이 env 를 쓴다.
    private static string GlobalPath() =>
        GlobalPathOverride
        ?? Environment.GetEnvironmentVariable("PDSA_GLOBAL_CONFIG")
        ?? PdsaProjectPaths.GlobalConfigFile;
    private static string? RepoConfigPath() => RepoPathOverride ?? FindUp(".secret/openai.json");

    // ── 내부 구현 ───────────────────────────────────────────────────────────
    private static LlmOptions Resolve()
    {
        string baseUrl = DefaultBaseUrl, apiKey = "", model = DefaultModel;

        // 낮은 우선순위(레포 .secret) → 높은 우선순위(전역)
        var repo = RepoConfigPath();
        if (repo is not null) MergeFile(repo, ref baseUrl, ref apiKey, ref model);
        MergeGlobal(ref baseUrl, ref apiKey, ref model);

        // reasoning_effort: 전역 설정 + 환경변수(미설정 시 모델 기본)
        string? reasoning = ReadGlobalString("reasoning_effort");

        // 환경변수 최우선
        baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? baseUrl;
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? apiKey;
        model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? model;
        reasoning = Environment.GetEnvironmentVariable("OPENAI_REASONING_EFFORT") ?? reasoning;

        // auth_mode: repo < global < env (필드 단위). 미지정 시 ApiKey(하위호환).
        string? authRaw = ReadRepoString(repo, "auth_mode") ?? null;
        authRaw = ReadGlobalString("auth_mode") ?? authRaw;
        authRaw = Environment.GetEnvironmentVariable("OPENAI_AUTH_MODE") ?? authRaw;
        var auth = ParseAuthMode(authRaw);

        // OAuth: access token(env 우선) + refresh_token(파일 참조로 미노출 가능) + 만료.
        OAuthOptions? oauth = null;
        if (auth == AuthMode.OAuth)
        {
            var access = Environment.GetEnvironmentVariable("OPENAI_ACCESS_TOKEN") ?? ReadGlobalString("oauth_access_token");
            var refresh = ReadGlobalString("oauth_refresh_token");
            if (ReadGlobalString("oauth_refresh_token_file") is { } rf && File.Exists(rf))
                refresh = File.ReadAllText(rf).Trim();          // 파일에서 읽기(설정에 미노출)
            long expiresAt = ReadGlobalLong("oauth_expires_at");
            oauth = new OAuthOptions(
                TokenEndpoint: ReadGlobalString("oauth_token_endpoint"),
                ClientId: ReadGlobalString("oauth_client_id"),
                RefreshToken: refresh,
                AccessToken: access,
                ExpiresAtUnix: expiresAt);
        }

        return new LlmOptions(baseUrl, apiKey, model, reasoning, auth, oauth);
    }

    private static AuthMode ParseAuthMode(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "none" => AuthMode.None,
        "oauth" => AuthMode.OAuth,
        "codex" => AuthMode.Codex,
        "claudecli" => AuthMode.ClaudeCli,
        _ => AuthMode.ApiKey, // "apikey"/null/미상 → 기본
    };

    /// <summary>Codex(GPT 구독) 모드로 전환: auth_mode=codex + Codex base_url/model 기본값.</summary>
    public static string SetCodex() => Update(o =>
    {
        o["auth_mode"] = "codex";
        o["base_url"] = Codex.DefaultBaseUrl;
        o["model"] = Codex.DefaultModel;
    });

    /// <summary>Claude Code CLI(claude -p) 모드로 전환: auth_mode=claudecli(모델 미강제 — claude 기본 사용).</summary>
    public static string SetClaudeCli() => Update(o => o["auth_mode"] = "claudecli");
    public static string? ReadClaudeCliPath() => ReadGlobalString("claude_cli_path");
    public static string SetClaudeCliPath(string path) => Update(o => o["claude_cli_path"] = Path.GetFullPath(path));

    private static string? ReadRepoString(string? repoPath, string name)
    {
        if (repoPath is null || !File.Exists(repoPath)) return null;
        try { using var doc = JsonDocument.Parse(File.ReadAllText(repoPath), Lenient); return Str(doc.RootElement, name); }
        catch { return null; }
    }

    private static long ReadGlobalLong(string name)
    {
        var path = GlobalPath();
        if (!File.Exists(path)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient);
            if (doc.RootElement.TryGetProperty(name, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
                if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s;
            }
        }
        catch { }
        return 0;
    }

    private static bool ReadGlobalBool(string name)
    {
        var path = GlobalPath();
        if (!File.Exists(path)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient);
            return doc.RootElement.TryGetProperty(name, out var v)
                && (v.ValueKind == JsonValueKind.True
                    || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b));
        }
        catch { return false; }
    }

    private static void MergeGlobal(ref string baseUrl, ref string apiKey, ref string model)
    {
        var path = GlobalPath();
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient);
            var root = doc.RootElement;
            if (Str(root, "base_url") is { } b) baseUrl = b;
            if (Str(root, "model") is { } m) model = m;
            if (Str(root, "api_key") is { } k) apiKey = k;
            // 키 파일 참조가 있으면 그 파일에서 키(및 있으면 base_url/model)를 읽는다.
            if (Str(root, "api_key_file") is { } file)
                ReadKeyFile(file, ref baseUrl, ref apiKey, ref model);
        }
        catch { /* 손상 파일 무시 */ }
    }

    private static void MergeFile(string path, ref string baseUrl, ref string apiKey, ref string model)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient);
            var root = doc.RootElement;
            if (Str(root, "base_url") is { } b) baseUrl = b;
            if (Str(root, "model") is { } m) model = m;
            if (Str(root, "api_key") is { } k) apiKey = k;
        }
        catch { }
    }

    /// <summary>키 파일을 읽는다: 여기 포맷의 JSON 이면 필드 사용, 아니면 파일 내용 전체를 원시 키로.</summary>
    private static void ReadKeyFile(string file, ref string baseUrl, ref string apiKey, ref string model)
    {
        if (!File.Exists(file)) return;
        var text = File.ReadAllText(file).Trim();
        try
        {
            using var doc = JsonDocument.Parse(text, Lenient);
            var root = doc.RootElement;
            if (Str(root, "api_key") is { } k) apiKey = k;
            if (Str(root, "base_url") is { } b) baseUrl = b;
            if (Str(root, "model") is { } m) model = m;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(text)) apiKey = text; // 원시 키 파일
        }
    }

    private static string Update(Action<JsonObject> mutate)
    {
        var path = GlobalPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject root;
        try
        {
            root = (File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path), null, new JsonDocumentOptions { AllowTrailingCommas = true })
                : null) as JsonObject ?? new JsonObject();
        }
        catch { root = new JsonObject(); }

        mutate(root);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string KeySource()
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null) return "환경변수";
        var g = GlobalPath();
        if (File.Exists(g))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(g), Lenient);
                if (Str(doc.RootElement, "api_key_file") is { } f) return $"키파일: {f}";
                if (Str(doc.RootElement, "api_key") is not null) return $"전역설정: {g}";
            }
            catch { }
        }
        var repo = RepoConfigPath();
        return repo is not null && File.Exists(repo) ? $"레포: {repo}" : "(없음)";
    }

    private static string? ReadGlobalString(string name)
    {
        var path = GlobalPath();
        if (!File.Exists(path)) return null;
        try { using var doc = JsonDocument.Parse(File.ReadAllText(path), Lenient); return Str(doc.RootElement, name); }
        catch { return null; }
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private static string? FindUp(string relative)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }

    internal static string Mask(string key)
    {
        if (string.IsNullOrEmpty(key)) return "(미설정)";
        if (key.Length <= 8) return "****";
        return key[..4] + new string('*', Math.Min(8, key.Length - 8)) + key[^4..];
    }
}
