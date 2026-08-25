using AkkaGraphLoop.Samples;
using AkkaGraphLoop.Samples.Basics;
using AkkaGraphLoop.Samples.Cycles;
using AkkaGraphLoop.Samples.FanIn;
using AkkaGraphLoop.Samples.FanOut;
using AkkaGraphLoop.Samples.Partial;
using AkkaGraphLoop.Samples.Tui;

// 인자가 없거나 "tui" 이면 TUI 튜토리얼 모드로 실행한다.
//   dotnet run                → TUI 튜토리얼(각 그래프를 순차로, 스텝당 ~5초, ESC 일시정지, Ctrl+C 종료)
//   dotnet run -- tui         → 동일
if (args.Length == 0 || string.Equals(args[0], "tui", StringComparison.OrdinalIgnoreCase))
{
    await TuiApp.Run();
    return;
}

// "selftest": 렌더링 없이 모든 TUI 장면의 실제 그래프를 빠른 스텝으로 돌려
// 각 장면이 데드락 없이 완료되고 Sink 출력이 나오는지 검증한다(CI/스모크용).
if (string.Equals(args[0], "selftest", StringComparison.OrdinalIgnoreCase))
{
    using var testHost = new DemoHost("graph-tui-selftest");
    var scenes = Scenes.All;
    var failed = 0;
    for (var i = 0; i < scenes.Count; i++)
    {
        var scene = scenes[i];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pacer = new Pacer(TimeSpan.FromMilliseconds(120), cts.Token);
        try
        {
            await scene.Run(pacer, testHost.Materializer).WaitAsync(TimeSpan.FromSeconds(20));
            Console.WriteLine($"[selftest] OK   {i + 1,2}/{scenes.Count}  {scene.Title,-28} sink={pacer.Log.Count}");
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[selftest] FAIL {i + 1,2}/{scenes.Count}  {scene.Title,-28} {ex.GetType().Name}: {ex.Message}");
        }
    }
    Console.WriteLine(failed == 0 ? "[selftest] 전체 통과" : $"[selftest] 실패 {failed}건");
    Environment.ExitCode = failed == 0 ? 0 : 1;
    return;
}

// 아래는 개별 데모 단발 실행 모드.
// 사용법: dotnet run -- <데모번호>
//   0: GraphDSL 기본(Broadcast+Merge 폐그래프)
//   1: FanOut - Balance (워커 분산)
//   2: FanOut - UnZip
//   3: FanIn  - Zip
//   4: FanIn  - ZipWith (원소별 Max)
//   5: FanIn  - Concat
//   6: FanIn  - MergePrioritized
//   7: Partial - PickMaxOfThree
//   8: Partial - Source.FromGraph (홀/짝 페어)
//   9: Partial - Flow.FromGraph (pairUpWithToString)
//  10: Cycle  - MergePreferred (해법 1)
//  11: Cycle  - Buffer DropHead (해법 2)
//  12: Cycle  - Balanced ZipWith (해법 3)
//  99: Cycle  - 순진한 데드락 데모 (2초 타임아웃 가드)

var demo = args[0];
using var host = new DemoHost();
var mat = host.Materializer;

static void Print<T>(string title, IEnumerable<T> items)
    => Console.WriteLine($"[{title}] => [{string.Join(", ", items)}]");

switch (demo)
{
    case "0":
        await GraphDslBasics.Run(mat);
        break;
    case "1":
        Print("Balance", await FanOutSamples.BalanceDemo(mat));
        break;
    case "2":
        Print("UnZip", await FanOutSamples.UnzipDemo(mat));
        break;
    case "3":
        Print("Zip", await FanInSamples.ZipDemo(mat));
        break;
    case "4":
        Print("ZipWith-Max", await FanInSamples.ZipWithMaxDemo(mat));
        break;
    case "5":
        Print("Concat", await FanInSamples.ConcatDemo(mat));
        break;
    case "6":
        Print("MergePrioritized", await FanInSamples.MergePrioritizedDemo(mat));
        break;
    case "7":
        Console.WriteLine($"[PickMaxOfThree] => {await PartialGraphSamples.PickMaxOfThreeDemo(mat)}");
        break;
    case "8":
        Print("OddEvenPairs", await PartialGraphSamples.OddEvenPairsDemo(mat));
        break;
    case "9":
        Print("PairUpWithToString", await PartialGraphSamples.PairUpWithToStringDemo(mat));
        break;
    case "10":
        Print("MergePreferredCycle", await CycleSamples.MergePreferredCycle(mat));
        break;
    case "11":
        Print("BufferDropHeadCycle", await CycleSamples.BufferDropHeadCycle(mat));
        break;
    case "12":
        Print("BalancedZipWithCycle", await CycleSamples.BalancedZipWithCycle(mat));
        break;
    case "99":
        var deadlock = CycleSamples.RunNaiveDeadlock(mat);
        var finished = await Task.WhenAny(deadlock, Task.Delay(2000));
        Console.WriteLine(finished == deadlock
            ? "[deadlock-demo] 완료됨(예상과 다름)"
            : "[deadlock-demo] 예상대로 2초 내 완료되지 않음 → 데드락 확인. 데모 종료.");
        break;
    default:
        Console.WriteLine($"알 수 없는 데모 번호: {demo}. Program.cs 상단 주석의 목록을 참고하세요.");
        break;
}
