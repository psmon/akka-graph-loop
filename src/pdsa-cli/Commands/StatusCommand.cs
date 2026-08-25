using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>현재 프로젝트의 PDSA 진행 상태와 최근 사이클/단계를 보여준다.</summary>
public sealed class StatusCommand : ICliCommand
{
    public string Name => "status";
    public string Summary => "현재 프로젝트의 PDSA 진행/누적 상태";
    public string Usage => "pdsa status [--project <이름>] [--limit 5]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        using var s = PdsaSession.Open(args);
        var limit = ArgUtil.Int(args, "--limit", 5);

        Console.WriteLine($"프로젝트 : {s.Project}");
        Console.WriteLine($"그래프DB : {s.DbPath}");
        Console.WriteLine($"LLM      : {(s.LlmConfigured ? "설정됨" : "미설정")}");
        Console.WriteLine($"누적 사이클: {s.Workflow.CycleCount()}개");

        var recent = s.Workflow.Recent(limit);
        if (recent.Count == 0)
        {
            Console.WriteLine("\n아직 기록이 없습니다. `pdsa plan \"<계획>\"` 으로 시작하세요.");
            return Task.FromResult(0);
        }

        Console.WriteLine("\n최근 사이클:");
        foreach (var c in recent)
        {
            Console.WriteLine($"  #{c.Id}  [{c.Status}]  {c.Started}");
            foreach (var p in c.Phases)
                Console.WriteLine($"     - {p.Kind,-5}: {OneLine(p.Input)}");
        }
        return Task.FromResult(0);
    }

    private static string OneLine(string s)
    {
        var t = (s ?? "").ReplaceLineEndings(" ").Trim();
        return t.Length <= 70 ? t : t[..70] + "…";
    }
}
