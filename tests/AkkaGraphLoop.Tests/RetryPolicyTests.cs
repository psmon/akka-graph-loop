using System.Net;
using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 일시적 LLM 실패 재시도 정책 검증. 핵심은 "무엇을 재시도하지 <b>않는가</b>" 다 —
/// 인증·모델명 오류를 재시도하면 사용자를 기다리게만 하고, 사용자의 Ctrl+C 를 재시도하면
/// 취소가 먹지 않는다.
/// </summary>
public class RetryPolicyTests
{
    /// <summary>실제로 기다리지 않도록 sleep 을 가로채고 호출된 지연을 기록한다.</summary>
    private sealed class SleepSpy
    {
        public List<TimeSpan> Delays { get; } = new();
        public Task Sleep(TimeSpan d) { Delays.Add(d); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Transient_status_is_retried_then_succeeds()
    {
        var spy = new SleepSpy();
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(attempt =>
        {
            attempts = attempt;
            if (attempt < 3) throw new LlmTransientException("429", HttpStatusCode.TooManyRequests);
            return Task.FromResult("ok");
        }, maxRetries: 2, CancellationToken.None, spy.Sleep);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);          // 1회 + 재시도 2회
        Assert.Equal(2, spy.Delays.Count);
    }

    [Fact]
    public async Task Auth_error_is_not_retried()
    {
        var spy = new SleepSpy();
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.ExecuteAsync<string>(attempt =>
            {
                attempts = attempt;
                throw new InvalidOperationException("OpenAI 요청 실패(401): invalid api key");
            }, maxRetries: 2, CancellationToken.None, spy.Sleep));

        Assert.Equal(1, attempts);          // 재시도하지 않는다
        Assert.Empty(spy.Delays);
    }

    [Fact]
    public async Task Connection_failure_is_retried_and_finally_rethrown()
    {
        var spy = new SleepSpy();
        var attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            RetryPolicy.ExecuteAsync<string>(attempt =>
            {
                attempts = attempt;
                throw new HttpRequestException("connection refused");
            }, maxRetries: 2, CancellationToken.None, spy.Sleep));

        Assert.Equal(3, attempts);          // 소진 후 원래 예외를 그대로 던진다
    }

    [Fact]
    public async Task Caller_cancellation_is_not_retried()
    {
        var spy = new SleepSpy();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RetryPolicy.ExecuteAsync<string>(attempt =>
            {
                attempts = attempt;
                throw new TaskCanceledException();
            }, maxRetries: 2, cts.Token, spy.Sleep));

        Assert.Equal(0, attempts);          // 진입 전에 취소를 확인한다
        Assert.Empty(spy.Delays);
    }

    [Fact]
    public async Task Zero_max_retries_makes_a_single_attempt()
    {
        var spy = new SleepSpy();
        var attempts = 0;

        await Assert.ThrowsAsync<LlmTransientException>(() =>
            RetryPolicy.ExecuteAsync<string>(attempt =>
            {
                attempts = attempt;
                throw new LlmTransientException("503", HttpStatusCode.ServiceUnavailable);
            }, maxRetries: 0, CancellationToken.None, spy.Sleep));

        Assert.Equal(1, attempts);          // check 명령이 장애를 가리지 않는 근거
        Assert.Empty(spy.Delays);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void Backoff_stays_within_bounds(int attempt)
    {
        var delay = RetryPolicy.Backoff(attempt);
        Assert.True(delay > TimeSpan.Zero, $"지연이 0 이하: {delay}");
        Assert.True(delay <= TimeSpan.FromSeconds(5), $"상한 초과: {delay}");
    }

    [Fact]
    public void Retry_after_header_wins_over_computed_backoff()
    {
        var delay = RetryPolicy.Backoff(1, TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void Retry_after_is_capped_at_the_maximum()
    {
        var delay = RetryPolicy.Backoff(1, TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void Retryable_statuses_are_limited_to_rate_limit_and_server_errors(HttpStatusCode status, bool expected) =>
        Assert.Equal(expected, RetryPolicy.IsRetryableStatus(status));
}
