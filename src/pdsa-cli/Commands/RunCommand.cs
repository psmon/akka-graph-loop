using PdsaCli.Cli;
using PdsaCli.Engine;

namespace PdsaCli.Commands;

/// <summary>PDSA 루프를 Akka.Streams 로 실행하고 Kùzu 그래프 DB에 실시간 기록한다.</summary>
public sealed class RunCommand : ICliCommand
{
    public string Name => "run";
    public string Summary => "PDSA 루프 실행(Akka 스트림) + 그래프 DB 기록";
    public string Usage => "pdsa run [--start 45] [--target 90]";

    private readonly IPdsaEngine _engine = new AkkaPdsaEngine();

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        var start = ArgUtil.Double(args, "--start", 45);
        var target = ArgUtil.Double(args, "--target", 90);

        Console.WriteLine($"■ PDSA 실행: 시작 품질 {start:0.0} → 목표 {target:0.0}  (Akka 스트림 · Kùzu 기록)");
        Console.WriteLine();

        var result = await _engine.RunAsync(start, target, Console.WriteLine, ct);

        var final = result.History[^1];
        Console.WriteLine();
        Console.WriteLine($"■ 수렴 완료: 총 {result.History.Count}회, 최종 품질 {final.Quality:0.0}");
        Console.WriteLine($"■ 그래프 기록(NEXT×{result.NextEdges}):  {result.DbPath}");
        foreach (var line in result.GraphProgress)
            Console.WriteLine($"    {line}");
        Console.WriteLine();
        Console.WriteLine("▶ 그래프 보기: pdsa view");
        return 0;
    }
}
