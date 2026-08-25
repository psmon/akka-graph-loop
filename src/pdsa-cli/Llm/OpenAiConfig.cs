using System.Text.Json;
using System.Text.Json.Nodes;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Llm;

/// <summary>
/// OpenAI 설정 로더/저장기.
/// 우선순위(로드): 환경변수 → 전역 설정(<c>{LocalAppData}/pdsa-cli/openai.json</c>) → 레포 <c>.secret/openai.json</c>.
/// 키/모델은 <c>pdsa config</c> 로 전역 설정에 저장한다(개인 단위, 모든 프로젝트 공용).
/// </summary>
public static class OpenAiConfig
{
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-5.6-tera";
    private const string PlaceholderPrefix = "sk-여기에";

    public static bool TryLoad(out LlmOptions options, out string error)
    {
        string baseUrl = DefaultBaseUrl, apiKey = "", model = DefaultModel;

        // 1) 파일(전역 → 레포). 뒤에서 읽은 값이 앞을 덮지 않도록 전역을 먼저, 레포를 나중에 병합.
        foreach (var file in ConfigFilesLowToHigh())
            MergeFromFile(file, ref baseUrl, ref apiKey, ref model);

        // 2) 환경변수(최우선)
        baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? baseUrl;
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? apiKey;
        model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? model;

        options = new LlmOptions(baseUrl, apiKey, model);
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith(PlaceholderPrefix))
        {
            error =
                "OpenAI API 키가 설정되지 않았습니다.\n" +
                "  설정: pdsa config --api-key <키> [--model <모델>] [--base-url <URL>]\n" +
                $"  또는 환경변수 OPENAI_API_KEY / 파일 {PdsaProjectPaths.GlobalConfigFile}";
            return false;
        }
        error = "";
        return true;
    }

    /// <summary>전역 설정 파일에 저장(부분 갱신). 반환값은 저장된 파일 경로.</summary>
    public static string Save(string? apiKey, string? baseUrl, string? model)
    {
        var path = PdsaProjectPaths.GlobalConfigFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        JsonObject root;
        try { root = (JsonNode.Parse(File.Exists(path) ? File.ReadAllText(path) : "{}") as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }

        if (apiKey is not null) root["api_key"] = apiKey;
        if (baseUrl is not null) root["base_url"] = baseUrl;
        if (model is not null) root["model"] = model;

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    /// <summary>표시용 현재 설정(키는 마스킹).</summary>
    public static (string BaseUrl, string MaskedKey, string Model, bool Configured) Describe()
    {
        var ok = TryLoad(out var o, out _);
        return (o.BaseUrl, Mask(o.ApiKey), o.Model, ok);
    }

    private static IEnumerable<string> ConfigFilesLowToHigh()
    {
        // 낮은 우선순위(전역) → 높은 우선순위(레포)
        yield return PdsaProjectPaths.GlobalConfigFile;
        var repo = FindUp(".secret/openai.json");
        if (repo is not null) yield return repo;
    }

    private static void MergeFromFile(string file, ref string baseUrl, ref string apiKey, ref string model)
    {
        if (!File.Exists(file)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            if (root.TryGetProperty("base_url", out var b) && b.ValueKind == JsonValueKind.String) baseUrl = b.GetString()!;
            if (root.TryGetProperty("api_key", out var k) && k.ValueKind == JsonValueKind.String) apiKey = k.GetString()!;
            if (root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String) model = m.GetString()!;
        }
        catch { /* 손상된 파일은 무시 */ }
    }

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

    private static string Mask(string key)
    {
        if (string.IsNullOrEmpty(key)) return "(미설정)";
        if (key.Length <= 8) return "****";
        return key[..4] + new string('*', Math.Min(8, key.Length - 8)) + key[^4..];
    }
}
