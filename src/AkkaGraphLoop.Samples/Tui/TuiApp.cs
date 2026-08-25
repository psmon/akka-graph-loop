using Akka.Streams;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>
/// TUI 튜토리얼 오케스트레이터. 장면을 순차로 하나씩 실행하며 실제 그래프를 돌리고,
/// 렌더 루프가 그 상태를 애니메이션처럼 갱신한다.
/// - ESC: 현재 장면 일시정지/재개
/// - Ctrl+C (또는 Q): 전체 종료
/// </summary>
public static class TuiApp
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromSeconds(5);
    private static volatile Pacer? _current;

    public static async Task Run()
    {
        Term.EnableVirtualTerminal();
        Term.HideCursor();
        Term.ClearScreen();

        using var appCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // 즉시 죽지 않고 정리 후 종료
            appCts.Cancel();
        };

        var inputThread = StartInputThread(appCts);

        using var host = new DemoHost("graph-tui");
        var scenes = Scenes.All;

        try
        {
            for (var i = 0; i < scenes.Count; i++)
            {
                await RunScene(scenes[i], i + 1, scenes.Count, host.Materializer, appCts.Token);
                await DelayCancellable(TimeSpan.FromSeconds(2), appCts.Token); // 장면 전환 간격
            }

            FinalScreen("✔ 모든 그래프 장면을 마쳤습니다. 수고하셨습니다!");
        }
        catch (OperationCanceledException)
        {
            FinalScreen("■ 사용자 요청으로 종료했습니다. (Ctrl+C)");
        }
        finally
        {
            _current = null;
            Term.ShowCursor();
            Console.Out.Write(Term.Reset);
            inputThread.Join(200);
        }
    }

    private static async Task RunScene(Scene scene, int index, int total, IMaterializer mat, CancellationToken appToken)
    {
        appToken.ThrowIfCancellationRequested();

        var pacer = new Pacer(StepDelay, appToken);
        _current = pacer;

        using var sceneStop = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        var render = Task.Run(async () =>
        {
            try
            {
                while (!sceneStop.IsCancellationRequested)
                {
                    Renderer.Draw(scene, index, total, pacer);
                    await Task.Delay(120, sceneStop.Token);
                }
            }
            catch (OperationCanceledException) { }
        });

        try
        {
            await scene.Run(pacer, mat);
        }
        catch (Exception) when (appToken.IsCancellationRequested)
        {
            // Ctrl+C 로 스트림이 취소되며 발생한 예외 — 정상 종료 경로
        }
        finally
        {
            sceneStop.Cancel();
            try { await render; } catch (OperationCanceledException) { }
        }

        appToken.ThrowIfCancellationRequested();
        Renderer.Draw(scene, index, total, pacer); // 완료된 최종 프레임 한 번 더
    }

    private static Thread StartInputThread(CancellationTokenSource appCts)
    {
        var t = new Thread(() =>
        {
            if (Console.IsInputRedirected) return; // 파이프/리다이렉트 환경에서는 키 입력 비활성
            while (!appCts.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        switch (key.Key)
                        {
                            case ConsoleKey.Escape:
                                _current?.TogglePause();
                                break;
                            case ConsoleKey.Q:
                                appCts.Cancel();
                                break;
                        }
                    }
                    else
                    {
                        Thread.Sleep(25);
                    }
                }
                catch (InvalidOperationException)
                {
                    return; // 콘솔 입력 불가
                }
            }
        })
        { IsBackground = true, Name = "tui-input" };
        t.Start();
        return t;
    }

    private static async Task DelayCancellable(TimeSpan delay, CancellationToken token)
    {
        try { await Task.Delay(delay, token); }
        catch (OperationCanceledException) { throw; }
    }

    private static void FinalScreen(string message)
    {
        Term.ClearScreen();
        Term.ShowCursor();
        Console.WriteLine();
        Console.WriteLine("  " + Term.Bold + Term.Cyan + "Akka.NET Streams Graph — TUI 튜토리얼" + Term.Reset);
        Console.WriteLine();
        Console.WriteLine("  " + message);
        Console.WriteLine();
    }
}
