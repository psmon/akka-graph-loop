using System.Net;

namespace PdsaCli.Llm;

/// <summary>
/// LLM 호출의 <b>일시적</b> 실패만 제한적으로 재시도하는 최소 정책(의존성 없음).
///
/// <para>도입 순서가 중요하다 — 재시도는 실패 <i>시도</i> 횟수를 늘린다. 사이클 생성이 원자적이지
/// 않던 시절(PlanCommand 가 LLM 호출 전에 StartCycle 을 커밋하던 때)에 이걸 먼저 넣었다면
/// 고아 사이클만 늘렸을 것이다. 원자성(<c>StartCycleWithPlan</c>) 뒤에 오는 이유다.</para>
///
/// <para>재시도해도 결과가 달라지지 않는 실패(인증·모델명·잘못된 요청)는 즉시 던진다.
/// 호출자의 취소도 재시도 없이 즉시 전파한다.</para>
/// </summary>
public static class RetryPolicy
{
    /// <summary>기본 재시도 횟수(총 시도 = 1 + 이 값).</summary>
    public const int DefaultMaxRetries = 2;

    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);
    private const double JitterFactor = 0.3;   // ±30%

    /// <summary>재시도 대상 HTTP 상태(레이트리밋 + 일시적 서버 장애).</summary>
    public static bool IsRetryableStatus(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests            // 429
        || status == HttpStatusCode.InternalServerError      // 500
        || status == HttpStatusCode.BadGateway               // 502
        || status == HttpStatusCode.ServiceUnavailable       // 503
        || status == HttpStatusCode.GatewayTimeout;          // 504

    /// <summary>
    /// 재시도 대상 예외인지. 연결 실패(<see cref="HttpRequestException"/>)와
    /// <b>호출자가 취소하지 않은</b> 타임아웃만 해당한다.
    /// </summary>
    public static bool IsRetryableException(Exception ex, CancellationToken ct) => ex switch
    {
        _ when ct.IsCancellationRequested => false,          // 사용자 Ctrl+C — 재시도 금지
        HttpRequestException => true,                        // 연결 거부/DNS/소켓
        TaskCanceledException => true,                       // HttpClient.Timeout 만료
        TimeoutException => true,
        _ => false,
    };

    /// <summary>지수 백오프 + 지터. <paramref name="retryAfter"/> 가 있으면 그 값을 우선한다(상한 적용).</summary>
    public static TimeSpan Backoff(int attempt, TimeSpan? retryAfter = null, Random? rng = null)
    {
        if (retryAfter is { } ra && ra > TimeSpan.Zero)
            return ra > MaxDelay ? MaxDelay : ra;

        var raw = BaseDelay * Math.Pow(3, attempt - 1);      // 500ms → 1.5s → 4.5s
        if (raw > MaxDelay) raw = MaxDelay;

        var jitter = 1.0 + ((rng ?? Random.Shared).NextDouble() * 2 - 1) * JitterFactor;
        var delayed = raw * jitter;
        return delayed > MaxDelay ? MaxDelay : delayed;
    }

    /// <summary>
    /// <paramref name="action"/> 을 실행하고, 일시적 실패면 백오프 후 재시도한다.
    /// <paramref name="action"/> 은 시도 번호(1부터)를 받는다.
    /// </summary>
    /// <param name="maxRetries">0 이면 재시도하지 않는다(진단 명령은 장애를 가리면 안 되므로 0).</param>
    public static async Task<T> ExecuteAsync<T>(
        Func<int, Task<T>> action, int maxRetries, CancellationToken ct, Func<TimeSpan, Task>? sleep = null)
    {
        sleep ??= d => Task.Delay(d, ct);
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action(attempt);
            }
            catch (Exception ex) when (attempt <= maxRetries && ShouldRetry(ex, ct))
            {
                await sleep(Backoff(attempt, RetryAfterOf(ex)));
            }
        }
    }

    /// <summary>예외가 재시도 대상인지(HTTP 상태를 담은 <see cref="LlmTransientException"/> 포함).</summary>
    private static bool ShouldRetry(Exception ex, CancellationToken ct) =>
        ex is LlmTransientException || IsRetryableException(ex, ct);

    private static TimeSpan? RetryAfterOf(Exception ex) =>
        ex is LlmTransientException { RetryAfter: { } ra } ? ra : null;
}

/// <summary>
/// 재시도해 볼 가치가 있는 LLM 응답(429/5xx)을 나타내는 예외.
/// 최종 실패 시 그대로 사용자에게 전달되므로 메시지는 원래 오류 문구를 유지한다.
/// </summary>
public sealed class LlmTransientException(string message, HttpStatusCode status, TimeSpan? retryAfter = null)
    : InvalidOperationException(message)
{
    public HttpStatusCode Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
