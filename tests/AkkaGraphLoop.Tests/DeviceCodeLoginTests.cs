using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// device-code 로그인 폴링 상태머신(<see cref="DeviceCodeLogin"/>) 검증:
/// pending→성공, slow_down 시 간격 증가, 거부/만료/타임아웃. delay·now·client 를 주입해 sleep 없이 격리한다.
/// </summary>
public class DeviceCodeLoginTests
{
    private sealed class FakeDevice(DeviceCodeStart start, Queue<DevicePoll> polls) : IDeviceCodeClient
    {
        public int StartCalls, PollCalls;
        public Task<DeviceCodeStart> StartAsync(string e, string? c, string? s, CancellationToken ct = default)
        { StartCalls++; return Task.FromResult(start); }
        public Task<DevicePoll> PollAsync(string e, string? c, string dc, CancellationToken ct = default)
        { PollCalls++; return Task.FromResult(polls.Dequeue()); }
    }

    private static DeviceCodeStart Start(int interval = 5, int expiresIn = 900)
        => new("dev-code", "USER-CODE", "https://verify", null, interval, expiresIn);

    // delay 로 전달된 간격을 수집(간격 증가 관찰용). now 는 고정.
    private static (Func<int, CancellationToken, Task> delay, List<int> seen) CaptureDelay()
    {
        var seen = new List<int>();
        return ((sec, ct) => { seen.Add(sec); return Task.CompletedTask; }, seen);
    }

    [Fact]
    public async Task Pending_then_success_returns_token()
    {
        var polls = new Queue<DevicePoll>(new[]
        {
            new DevicePoll(DevicePollStatus.Pending, null, "authorization_pending"),
            new DevicePoll(DevicePollStatus.Pending, null, "authorization_pending"),
            new DevicePoll(DevicePollStatus.Success, new OAuthToken("ACCESS", "REFRESH", 5000), null),
        });
        var dev = new FakeDevice(Start(), polls);
        var (delay, _) = CaptureDelay();

        var tok = await DeviceCodeLogin.RunAsync(dev, "d", "t", "cid", null, _ => { }, delay, () => 1000);

        Assert.Equal("ACCESS", tok.AccessToken);
        Assert.Equal(3, dev.PollCalls);
        Assert.Equal(1, dev.StartCalls);
    }

    [Fact]
    public async Task Slow_down_increases_interval_by_five()
    {
        var polls = new Queue<DevicePoll>(new[]
        {
            new DevicePoll(DevicePollStatus.SlowDown, null, "slow_down"),
            new DevicePoll(DevicePollStatus.Success, new OAuthToken("A", null, 0), null),
        });
        var dev = new FakeDevice(Start(interval: 5), polls);
        var (delay, seen) = CaptureDelay();

        await DeviceCodeLogin.RunAsync(dev, "d", "t", null, null, _ => { }, delay, () => 1000);

        // 첫 폴 전 간격 5, slow_down 후 다음 폴 전 간격 10.
        Assert.Equal(new[] { 5, 10 }, seen);
    }

    [Fact]
    public async Task Denied_throws()
    {
        var polls = new Queue<DevicePoll>(new[] { new DevicePoll(DevicePollStatus.Denied, null, "access_denied") });
        var dev = new FakeDevice(Start(), polls);
        var (delay, _) = CaptureDelay();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DeviceCodeLogin.RunAsync(dev, "d", "t", null, null, _ => { }, delay, () => 1000));
    }

    [Fact]
    public async Task Expired_device_code_throws_timeout()
    {
        var polls = new Queue<DevicePoll>(new[] { new DevicePoll(DevicePollStatus.Expired, null, "expired_token") });
        var dev = new FakeDevice(Start(), polls);
        var (delay, _) = CaptureDelay();

        await Assert.ThrowsAsync<TimeoutException>(
            () => DeviceCodeLogin.RunAsync(dev, "d", "t", null, null, _ => { }, delay, () => 1000));
    }

    [Fact]
    public async Task Deadline_exceeded_throws_timeout()
    {
        // now 가 시작 즉시 만료시각을 넘도록: expiresIn=1, now 는 매 호출 +10.
        var polls = new Queue<DevicePoll>();  // 폴 도달 전에 타임아웃
        var dev = new FakeDevice(Start(interval: 1, expiresIn: 1), polls);
        var (delay, _) = CaptureDelay();
        long t = 1000;

        await Assert.ThrowsAsync<TimeoutException>(
            () => DeviceCodeLogin.RunAsync(dev, "d", "t", null, null, _ => { }, delay, () => (t += 10)));
    }

    [Fact]
    public async Task Prompt_receives_user_code()
    {
        var polls = new Queue<DevicePoll>(new[] { new DevicePoll(DevicePollStatus.Success, new OAuthToken("A", null, 0), null) });
        var dev = new FakeDevice(Start(), polls);
        var (delay, _) = CaptureDelay();
        DeviceCodeStart? shown = null;

        await DeviceCodeLogin.RunAsync(dev, "d", "t", null, null, s => shown = s, delay, () => 1000);

        Assert.Equal("USER-CODE", shown!.UserCode);
    }
}
