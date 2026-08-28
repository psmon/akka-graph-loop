using System.Diagnostics;

namespace PdsaCli.Cli;

/// <summary>
/// 대화형 터미널에서 LLM 대기 중 진행 스피너를 <b>stderr</b> 에 표시한다.
/// 출력이 리다이렉트/파이프(캡처)되면(<see cref="Console.IsErrorRedirected"/>) 아무것도 그리지 않고
/// 작업만 await 한다 — stdout(기록·파싱 대상)은 절대 건드리지 않는다.
/// </summary>
public static class Spinner
{
    private static readonly string[] Frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private const int FrameMs = 120;

    /// <summary>스피너를 돌리며 <paramref name="op"/> 를 실행하고 결과를 반환한다(예외는 그대로 전파).</summary>
    public static async Task<T> RunAsync<T>(string label, Func<CancellationToken, Task<T>> op, CancellationToken ct)
    {
        // 비대화형(캡처/파이프): 애니메이션 없이 그대로 실행.
        if (Console.IsErrorRedirected)
            return await op(ct);

        var task = op(ct);
        var sw = Stopwatch.StartNew();
        var i = 0;
        try
        {
            while (true)
            {
                var winner = await Task.WhenAny(task, Task.Delay(FrameMs, ct));
                if (winner == task) break;
                Console.Error.Write($"\r{Frames[i++ % Frames.Length]} {label}… {sw.Elapsed.TotalSeconds:0.0}s ");
            }
        }
        catch (OperationCanceledException)
        {
            // Task.Delay 취소(사용자 Ctrl+C) — 아래 `await task` 로 op 의 결과/취소를 마무리한다.
        }
        finally
        {
            ClearLine(label);
        }
        return await task;
    }

    private static void ClearLine(string label)
        => Console.Error.Write("\r" + new string(' ', label.Length + 24) + "\r");
}
