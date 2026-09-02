namespace PdsaCli.Llm;

/// <summary>인증 방식에 맞는 <see cref="ILlmClient"/> 를 만든다: Codex(GPT 구독)는 Responses API, 그 외는 Chat Completions.</summary>
public static class LlmClientFactory
{
    /// <param name="maxRetries">
    /// 일시적 실패 재시도 횟수(Chat Completions 경로에만 적용). 진단 명령(<c>check</c>)은
    /// <b>0</b> 을 넘겨 장애를 재시도로 가리지 않는다.
    /// </param>
    public static ILlmClient Create(LlmOptions options, int maxRetries = RetryPolicy.DefaultMaxRetries) =>
        options.Auth switch
        {
            AuthMode.Codex => new CodexClient(options.BaseUrl, options.Model),
            AuthMode.ClaudeCli => new ClaudeCliClient(options.Model),
            _ => new OpenAiClient(options, AuthProviders.Create(options), maxRetries),
        };
}
