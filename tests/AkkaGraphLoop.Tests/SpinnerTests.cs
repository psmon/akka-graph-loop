using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 대기 스피너(<see cref="Spinner"/>) 계약 검증: 결과 그대로 반환, 예외 전파, 그리고
/// <b>stdout(Console.Out)</b> 은 절대 건드리지 않음(기록·파싱 안전). 스피너는 stderr 전용.
/// </summary>
public class SpinnerTests
{
    [Fact]
    public async Task RunAsync_returns_operation_result()
    {
        var result = await Spinner.RunAsync("작업", _ => Task.FromResult(42), CancellationToken.None);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_propagates_exceptions()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Spinner.RunAsync<int>("작업", _ => throw new InvalidOperationException("boom"), CancellationToken.None));

    [Fact]
    public async Task RunAsync_never_writes_to_stdout()
    {
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            await Spinner.RunAsync("작업", _ => Task.FromResult("ok"), CancellationToken.None);
        }
        finally
        {
            Console.SetOut(original);
        }
        Assert.Equal("", captured.ToString());   // stdout 무오염
    }
}
