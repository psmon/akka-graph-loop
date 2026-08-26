namespace PdsaCli.Llm;

/// <summary>인증 방식에 맞는 <see cref="ILlmClient"/> 를 만든다: Codex(GPT 구독)는 Responses API, 그 외는 Chat Completions.</summary>
public static class LlmClientFactory
{
    public static ILlmClient Create(LlmOptions options) => options.Auth switch
    {
        AuthMode.Codex => new CodexClient(options.BaseUrl, options.Model),
        AuthMode.ClaudeCli => new ClaudeCliClient(options.Model),
        _ => new OpenAiClient(options),
    };
}
