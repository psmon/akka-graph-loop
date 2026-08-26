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
        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.StartsWith(PlaceholderPrefix))
        {
            error =
                "OpenAI API 키가 설정되지 않았습니다.\n" +
                "  키 직접:   pdsa config key <키>\n" +
                "  키 파일:   pdsa config key-file <파일경로>   (키를 설정에 노출하지 않음)\n" +
                "  모델:      pdsa config model <모델>\n" +
                $"  또는 환경변수 OPENAI_API_KEY / 파일 {PdsaProjectPaths.GlobalConfigFile}";
            return false;
        }
        error = "";
        return true;
    }

    // ── 설정(분리): 키 / 키파일 / 모델 / base-url ──────────────────────────────
    public static string SetKey(string apiKey) => Update(o => { o["api_key"] = apiKey; o.Remove("api_key_file"); });
    public static string SetKeyFile(string path) => Update(o => { o["api_key_file"] = Path.GetFullPath(path); o.Remove("api_key"); });
    public static string SetModel(string model) => Update(o => o["model"] = model);
    public static string SetBaseUrl(string url) => Update(o => o["base_url"] = url);
    public static string SetReasoning(string effort) => Update(o => o["reasoning_effort"] = effort);

    /// <summary>표시용 현재 설정(키는 마스킹, 출처 표기).</summary>
    public static (string BaseUrl, string MaskedKey, string Model, string Reasoning, string KeySource, bool Configured) Describe()
    {
        var o = Resolve();
        var configured = !string.IsNullOrWhiteSpace(o.ApiKey) && !o.ApiKey.StartsWith(PlaceholderPrefix);
        return (o.BaseUrl, Mask(o.ApiKey), o.Model, o.ReasoningEffort ?? "(모델 기본)", KeySource(), configured);
    }

    // ── 내부 구현 ───────────────────────────────────────────────────────────
    private static LlmOptions Resolve()
    {
        string baseUrl = DefaultBaseUrl, apiKey = "", model = DefaultModel;

        // 낮은 우선순위(레포 .secret) → 높은 우선순위(전역)
        var repo = FindUp(".secret/openai.json");
        if (repo is not null) MergeFile(repo, ref baseUrl, ref apiKey, ref model);
        MergeGlobal(ref baseUrl, ref apiKey, ref model);

        // reasoning_effort: 전역 설정 + 환경변수(미설정 시 모델 기본)
        string? reasoning = ReadGlobalString("reasoning_effort");

        // 환경변수 최우선
        baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? baseUrl;
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? apiKey;
        model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? model;
        reasoning = Environment.GetEnvironmentVariable("OPENAI_REASONING_EFFORT") ?? reasoning;

        return new LlmOptions(baseUrl, apiKey, model, reasoning);
    }

    private static void MergeGlobal(ref string baseUrl, ref string apiKey, ref string model)
    {
        var path = PdsaProjectPaths.GlobalConfigFile;
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
        var path = PdsaProjectPaths.GlobalConfigFile;
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
        var g = PdsaProjectPaths.GlobalConfigFile;
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
        var repo = FindUp(".secret/openai.json");
        return repo is not null ? $"레포: {repo}" : "(없음)";
    }

    private static string? ReadGlobalString(string name)
    {
        var path = PdsaProjectPaths.GlobalConfigFile;
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
