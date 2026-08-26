namespace PdsaCli.Llm;

/// <summary>인증 방식. 미지정 시 <see cref="ApiKey"/>(기존 정적 키) — 하위호환 기본값.</summary>
public enum AuthMode
{
    /// <summary>정적 Bearer API 키(기존 동작).</summary>
    ApiKey,
    /// <summary>OAuth access token(만료 시 갱신).</summary>
    OAuth,
    /// <summary>Codex(ChatGPT 구독) OAuth. ~/.codex/auth.json 재사용 + Responses API.</summary>
    Codex,
    /// <summary>공식 Claude Code CLI(claude -p) 서브프로세스. 별도 토큰 설정 없이 로그인된 Claude 를 그대로 사용.</summary>
    ClaudeCli,
    /// <summary>무인증. 키리스 로컬 오픈웨이트(ollama/vLLM/LM Studio)용. 사설대역만 자동 허용.</summary>
    None,
}

/// <summary>OAuth 설정(토큰 엔드포인트/클라이언트/캐시된 토큰). 사이클 C에서 실제 갱신 구현.</summary>
public sealed record OAuthOptions(
    string? TokenEndpoint = null,
    string? ClientId = null,
    string? RefreshToken = null,
    string? AccessToken = null,
    long ExpiresAtUnix = 0);

/// <summary>
/// LLM 옵션(엔드포인트/키/모델/추론강도/인증). ReasoningEffort 는 GPT-5.x 추론 모델용(none~max), 미설정 시 모델 기본.
/// 새 필드(<see cref="Auth"/>/<see cref="OAuth"/>)는 뒤에 덧붙여 기존 positional 생성부와 하위호환을 유지한다.
/// </summary>
public sealed record LlmOptions(
    string BaseUrl,
    string ApiKey,
    string Model,
    string? ReasoningEffort = null,
    AuthMode Auth = AuthMode.ApiKey,
    OAuthOptions? OAuth = null);

/// <summary>LLM 채팅 완성 인터페이스. 구현체를 갈아끼울 수 있게 추상화.</summary>
public interface ILlmClient
{
    /// <summary>시스템/사용자 프롬프트로 한 번의 완성을 받아 텍스트를 반환한다.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
