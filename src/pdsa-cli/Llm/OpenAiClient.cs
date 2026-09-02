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
    [property: JsonPropertyName("messages")] ChatMessage[] Messages,
    [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChatRequest))]
internal partial class OpenAiJsonContext : JsonSerializerContext;

/// <summary>
/// OpenAI Chat Completions 기반 <see cref="ILlmClient"/> 기본 구현(Native AOT 호환).
/// (초기 구성: 인터페이스 + 동작하는 기본 구현. PDSA 전용 가이드 프롬프트 로직은 이후 지침에서 확장.)
/// </summary>
public sealed class OpenAiClient : ILlmClient, ILlmUsageReporter, IDisposable
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly IAuthProvider _auth;
    private readonly int _maxRetries;

    /// <inheritdoc/>
    public LlmCallStats? LastCall { get; private set; }

    public OpenAiClient(LlmOptions options) : this(options, AuthProviders.Create(options)) { }

    /// <summary>인증 전략을 직접 주입(테스트용 fake <see cref="IAuthProvider"/> 등).</summary>
    /// <param name="maxRetries">
    /// 일시적 실패(429/5xx/연결/타임아웃) 재시도 횟수. <b>진단용 <c>check</c> 는 0</b> —
    /// 재시도가 장애를 가려 "잘 되는 것처럼" 보이면 안 된다.
    /// </param>
    public OpenAiClient(LlmOptions options, IAuthProvider auth, int maxRetries = RetryPolicy.DefaultMaxRetries)
    {
        _options = options;
        _auth = auth;
        _maxRetries = maxRetries;
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        LastCall = null;
        return RetryPolicy.ExecuteAsync(
            attempt => SendOnceAsync(systemPrompt, userPrompt, attempt, ct), _maxRetries, ct);
    }

    /// <summary>한 번의 요청·응답. 재시도 판단은 <see cref="RetryPolicy"/> 가 예외 타입으로 한다.</summary>
    private async Task<string> SendOnceAsync(string systemPrompt, string userPrompt, int attempt, CancellationToken ct)
    {
        var request = new ChatRequest(_options.Model, new[]
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt),
        }, _options.ReasoningEffort);

        var json = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.ChatRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = content };
        msg.Headers.Authorization = await _auth.GetHeaderAsync(ct);
        using var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var message = $"OpenAI 요청 실패({(int)resp.StatusCode}): {Truncate(body, 500)}";
            // 429/5xx 만 재시도 대상. 4xx(키·모델명·요청 오류)는 재시도해도 결과가 같으므로 즉시 실패.
            throw RetryPolicy.IsRetryableStatus(resp.StatusCode)
                ? new LlmTransientException(message, resp.StatusCode, resp.Headers.RetryAfter?.Delta)
                : new InvalidOperationException(message);
        }

        // 응답 파싱은 JsonDocument(리플렉션 미사용, AOT-safe)
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var text = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        LastCall = new LlmCallStats(attempt, TokenCount(root, "prompt_tokens"), TokenCount(root, "completion_tokens"),
            _options.Model);

        return text?.Trim() ?? "";
    }

    /// <summary>응답의 <c>usage</c> 에서 토큰 수를 읽는다(없는 엔드포인트도 있으므로 없으면 0).</summary>
    private static int TokenCount(JsonElement root, string field) =>
        root.TryGetProperty("usage", out var usage) && usage.TryGetProperty(field, out var v) &&
        v.TryGetInt32(out var n)
            ? n : 0;

    /// <summary>지원 모델 id 목록을 조회한다(GET /models).</summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, "models");
        msg.Headers.Authorization = await _auth.GetHeaderAsync(ct);
        using var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"모델 목록 조회 실패({(int)resp.StatusCode}): {Truncate(body, 500)}");

        using var doc = JsonDocument.Parse(body);
        var ids = new List<string>();
        foreach (var m in doc.RootElement.GetProperty("data").EnumerateArray())
            if (m.TryGetProperty("id", out var id) && id.GetString() is { } s)
                ids.Add(s);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    public void Dispose() => _http.Dispose();
}
