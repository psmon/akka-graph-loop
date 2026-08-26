using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdsaCli.Llm;

// Codex Responses API 요청(AOT-safe 소스생성 직렬화). input 은 단문 문자열로 전달.
internal sealed record CodexRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("instructions")] string Instructions,
    [property: JsonPropertyName("input")] string Input,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("store")] bool Store);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CodexRequest))]
internal partial class CodexJsonContext : JsonSerializerContext;

/// <summary>
/// Codex(ChatGPT 구독) OAuth 로 <c>{base}/responses</c>(표준 Responses API)를 호출하는 <see cref="ILlmClient"/>.
/// 요청 전 토큰 만료를 검사해 refresh(단회성 토큰 → auth.json 재기록)하고, 필수 헤더로 SSE 스트림을 파싱한다.
/// </summary>
public sealed class CodexClient : ILlmClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ITokenRefresher _refresher;
    private readonly Func<long> _nowUnix;
    private readonly string? _authPath;      // 테스트에서 auth.json 경로 주입

    public CodexClient(string baseUrl, string model,
        HttpMessageHandler? handler = null, ITokenRefresher? refresher = null,
        Func<long>? nowUnix = null, string? authPath = null)
    {
        _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? Codex.DefaultBaseUrl : baseUrl).TrimEnd('/');
        _model = string.IsNullOrWhiteSpace(model) ? Codex.DefaultModel : model;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(120);
        _refresher = refresher ?? new HttpTokenRefresher();
        _nowUnix = nowUnix ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _authPath = authPath;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var tokens = await EnsureFreshTokenAsync(ct);

        var request = new CodexRequest(_model, systemPrompt, userPrompt, Stream: true, Store: false);
        var json = JsonSerializer.Serialize(request, CodexJsonContext.Default.CodexRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/responses") { Content = content };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        if (tokens.AccountId is { Length: > 0 } acct) msg.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", acct);
        msg.Headers.TryAddWithoutValidation("originator", Codex.Originator);          // Cloudflare 우회(1st-party)
        msg.Headers.TryAddWithoutValidation("User-Agent", Codex.UserAgent);
        msg.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

        using var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(DescribeError((int)resp.StatusCode, err));
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await ParseSseAsync(stream, ct);
    }

    /// <summary>토큰 로드 → 만료 임박이면 refresh 후 auth.json 재기록 → 사용할 토큰 반환.</summary>
    private async Task<CodexTokens> EnsureFreshTokenAsync(CancellationToken ct)
    {
        var tokens = Codex.Load(_authPath)
            ?? throw new InvalidOperationException(
                $"Codex 인증을 찾을 수 없습니다: {Codex.AuthPath()}\n  공식 Codex CLI 로 로그인하세요: codex login");

        if (!Codex.IsExpiring(tokens.AccessToken, _nowUnix())) return tokens;

        // auth.openai.com 로 refresh(grant_type=refresh_token, client_id=Codex). 단회성 토큰 → 재기록.
        var refreshed = await _refresher.RefreshAsync(
            new OAuthOptions(TokenEndpoint: Codex.TokenUrl, ClientId: Codex.ClientId, RefreshToken: tokens.RefreshToken), ct);
        var updated = tokens with
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken ?? tokens.RefreshToken,
            AccountId = tokens.AccountId ?? Codex.AccountIdFromJwt(refreshed.AccessToken),
        };
        Codex.Persist(updated, _authPath);
        return updated;
    }

    /// <summary>Responses API SSE 스트림에서 output_text 를 누적한다(delta 이벤트 합산). AOT-safe(JsonDocument).</summary>
    internal static async Task<string> ParseSseAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line["data:".Length..].Trim();
            if (data is "[DONE]") break;
            if (data.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                switch (type)
                {
                    case "response.output_text.delta":
                        if (root.TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.String)
                            sb.Append(d.GetString());
                        break;
                    case "response.completed":
                        return Finalize(sb, root);   // 완료 이벤트에 전체 응답이 실려오면 그것으로 대체
                }
            }
            catch (JsonException) { /* 부분/비JSON 데이터 라인 무시 */ }
        }
        return sb.ToString().Trim();
    }

    /// <summary>completed 이벤트의 response.output 에서 최종 텍스트를 뽑되, 없으면 누적 델타 사용.</summary>
    private static string Finalize(StringBuilder deltas, JsonElement completed)
    {
        if (completed.TryGetProperty("response", out var response)
            && response.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in output.EnumerateArray())
                if (item.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                    foreach (var c in contentArr.EnumerateArray())
                        if (c.TryGetProperty("type", out var ct) && ct.GetString() is "output_text"
                            && c.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
                            sb.Append(txt.GetString());
            if (sb.Length > 0) return sb.ToString().Trim();
        }
        return deltas.ToString().Trim();
    }

    private static string DescribeError(int status, string body) => status switch
    {
        401 => "Codex 토큰이 만료/무효입니다(401). 공식 CLI 로 재로그인하세요: codex login\n  " + Truncate(body, 200),
        403 => "Codex 접근이 차단되었습니다(403, Cloudflare). 잠시 후 재시도하거나 codex login 재확인.\n  " + Truncate(body, 200),
        429 => "Codex 사용량 한도 초과(429). 구독 한도 리셋 후 재시도하세요.\n  " + Truncate(body, 200),
        _ => $"Codex Responses 요청 실패({status}): {Truncate(body, 300)}",
    };

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    public void Dispose() => _http.Dispose();
}
