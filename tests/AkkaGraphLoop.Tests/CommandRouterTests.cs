using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 최상위 디스패처(<see cref="CommandRouter"/>) 검증: 도움말/미지정, 알 수 없는 명령,
/// 정상 위임(부분 인자 전달), 예외·취소 종료코드.
/// </summary>
public class CommandRouterTests
{
    /// <summary>테스트용 스텁 명령: 전달된 args 를 캡처하고 지정 동작을 수행.</summary>
    private sealed class StubCommand : ICliCommand
    {
        private readonly Func<string[], CancellationToken, Task<int>> _run;
        public StubCommand(string name, Func<string[], CancellationToken, Task<int>> run) { Name = name; _run = run; }
        public string Name { get; }
        public string Summary => "stub";
        public string Usage => "stub";
        public string[]? Received { get; private set; }
        public Task<int> RunAsync(string[] args, CancellationToken ct) { Received = args; return _run(args, ct); }
    }

    private static async Task<int> DispatchSilently(CommandRouter router, string[] args, CancellationToken ct = default)
    {
        var (o, e) = (Console.Out, Console.Error);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try { return await router.DispatchAsync(args, ct); }
        finally { Console.SetOut(o); Console.SetError(e); }
    }

    [Fact]
    public async Task Help_or_empty_returns_zero()
    {
        var router = new CommandRouter(new ICliCommand[] { new StubCommand("plan", (_, _) => Task.FromResult(0)) });
        foreach (var args in new[] { Array.Empty<string>(), new[] { "-h" }, new[] { "--help" }, new[] { "help" } })
            Assert.Equal(0, await DispatchSilently(router, args));
    }

    [Fact]
    public async Task Unknown_command_returns_two()
    {
        var router = new CommandRouter(new ICliCommand[] { new StubCommand("plan", (_, _) => Task.FromResult(0)) });
        Assert.Equal(2, await DispatchSilently(router, new[] { "nope" }));
    }

    [Fact]
    public async Task Known_command_is_invoked_with_remaining_args()
    {
        var stub = new StubCommand("plan", (_, _) => Task.FromResult(42));
        var router = new CommandRouter(new ICliCommand[] { stub });

        var code = await DispatchSilently(router, new[] { "plan", "--project", "x", "hello" });

        Assert.Equal(42, code);                                        // 명령의 종료코드가 그대로 전달
        Assert.Equal(new[] { "--project", "x", "hello" }, stub.Received); // 첫 토큰(명령명) 제외한 나머지
    }

    [Fact]
    public async Task Command_name_match_is_case_insensitive()
    {
        var router = new CommandRouter(new ICliCommand[] { new StubCommand("plan", (_, _) => Task.FromResult(7)) });
        Assert.Equal(7, await DispatchSilently(router, new[] { "PLAN" }));
    }

    [Fact]
    public async Task Command_exception_maps_to_one()
    {
        var router = new CommandRouter(new ICliCommand[]
        {
            new StubCommand("boom", (_, _) => throw new InvalidOperationException("kaboom")),
        });
        Assert.Equal(1, await DispatchSilently(router, new[] { "boom" }));
    }

    [Fact]
    public async Task Cancellation_maps_to_130()
    {
        var router = new CommandRouter(new ICliCommand[]
        {
            new StubCommand("wait", (_, _) => throw new OperationCanceledException()),
        });
        Assert.Equal(130, await DispatchSilently(router, new[] { "wait" }));
    }
}
