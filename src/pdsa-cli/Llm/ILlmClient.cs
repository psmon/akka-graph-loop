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

/// <summary>직전 LLM 호출의 계측치. 값을 모르는 항목은 0/빈 문자열.</summary>
/// <param name="Attempts">실제 시도 횟수(재시도 포함, 최소 1).</param>
public sealed record LlmCallStats(int Attempts, int PromptTokens, int CompletionTokens, string Model);

/// <summary>
/// 직전 호출의 계측치를 보고할 수 있는 <see cref="ILlmClient"/> 의 <b>선택적</b> 확장.
///
/// <para><see cref="ILlmClient.CompleteAsync"/> 의 시그니처를 바꾸면 구현체 4종과 테스트 페이크가
/// 모두 깨지므로, 계측을 보고할 수 있는 구현체만 이 인터페이스를 추가로 구현한다.
/// 호출자는 <c>is ILlmUsageReporter</c> 로 확인하고, 아니면 지연 시간만 기록한다.</para>
/// </summary>
public interface ILlmUsageReporter
{
    /// <summary>직전 <see cref="ILlmClient.CompleteAsync"/> 호출의 계측치(호출 전이면 null).</summary>
    LlmCallStats? LastCall { get; }
}
