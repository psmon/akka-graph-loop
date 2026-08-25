namespace PdsaCli.Llm;

/// <summary>LLM 옵션(엔드포인트/키/모델).</summary>
public sealed record LlmOptions(string BaseUrl, string ApiKey, string Model);

/// <summary>LLM 채팅 완성 인터페이스. 구현체를 갈아끼울 수 있게 추상화.</summary>
public interface ILlmClient
{
    /// <summary>시스템/사용자 프롬프트로 한 번의 완성을 받아 텍스트를 반환한다.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
