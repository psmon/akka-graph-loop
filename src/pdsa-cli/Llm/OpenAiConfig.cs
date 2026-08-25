using System.Text.Json;

namespace PdsaCli.Llm;

/// <summary>
/// OpenAI 설정 로더. 우선순위: 환경변수 → <c>.secret/openai.json</c>(레포에서 상위로 탐색).
/// 실제 키 파일(.secret/*.json)은 git 에 커밋되지 않으며, 템플릿은 <c>.secret/openai.json.tmp</c> 참고.
/// </summary>
public static class OpenAiConfig
{
    private const string PlaceholderPrefix = "sk-여기에"; // 템플릿 placeholder 감지

    public static bool TryLoad(out LlmOptions options, out string error)
    {
        options = new LlmOptions("https://api.openai.com/v1", "", "gpt-4o-mini");
        error = "";

        string baseUrl = options.BaseUrl, apiKey = "", model = options.Model;

        // 1) 시크릿 파일(.secret/openai.json)
        var file = FindSecretFile();
        if (file is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (root.TryGetProperty("base_url", out var b) && b.ValueKind == JsonValueKind.String) baseUrl = b.GetString()!;
                if (root.TryGetProperty("api_key", out var k) && k.ValueKind == JsonValueKind.String) apiKey = k.GetString()!;
                if (root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String) model = m.GetString()!;
            }
            catch (Exception ex)
            {
                error = $"시크릿 파일을 읽지 못했습니다({file}): {ex.Message}";
                return false;
            }
        }

        // 2) 환경변수 오버라이드
        baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? baseUrl;
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? apiKey;
        model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? model;

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith(PlaceholderPrefix))
        {
            error =
                "OpenAI API 키가 설정되지 않았습니다.\n" +
                "  방법 1) .secret/openai.json.tmp 를 .secret/openai.json 으로 복사 후 api_key 를 실제 키로 채우기\n" +
                "  방법 2) 환경변수 OPENAI_API_KEY (필요시 OPENAI_BASE_URL, OPENAI_MODEL) 설정";
            return false;
        }

        options = new LlmOptions(baseUrl, apiKey, model);
        return true;
    }

    /// <summary>실행 파일 위치와 현재 작업 디렉터리에서 상위로 올라가며 .secret/openai.json 을 찾는다.</summary>
    private static string? FindSecretFile()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, ".secret", "openai.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }
}
