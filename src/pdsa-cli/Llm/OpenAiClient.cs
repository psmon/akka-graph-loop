using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdsaCli.Llm;

// AOT-safe JSON: 소스 생성기(JsonSerializerContext)로 직렬화(리플렉션 사용 안 함).
internal sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] ChatMessage[] Messages);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ChatRequest))]
internal partial class OpenAiJsonContext : JsonSerializerContext;

/// <summary>
/// OpenAI Chat Completions 기반 <see cref="ILlmClient"/> 기본 구현(Native AOT 호환).
/// (초기 구성: 인터페이스 + 동작하는 기본 구현. PDSA 전용 가이드 프롬프트 로직은 이후 지침에서 확장.)
/// </summary>
public sealed class OpenAiClient : ILlmClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public OpenAiClient(LlmOptions options)
    {
        _options = options;
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new ChatRequest(_options.Model, new[]
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt),
        });

        var json = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.ChatRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("chat/completions", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI 요청 실패({(int)resp.StatusCode}): {Truncate(body, 500)}");

        // 응답 파싱은 JsonDocument(리플렉션 미사용, AOT-safe)
        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return text?.Trim() ?? "";
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    public void Dispose() => _http.Dispose();
}
