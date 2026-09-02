using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>현재 프로젝트의 PDSA 진행 상태와 최근 사이클/단계를 보여준다.</summary>
public sealed class StatusCommand : ICliCommand
{
    public string Name => "status";
    public string Summary => "현재 프로젝트의 PDSA 진행/누적 상태";
    public string Usage => "pdsa status [--project <이름>] [--limit 5] [--full] [--json]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        using var s = PdsaSession.Open(args);
        var limit = ArgUtil.Int(args, "--limit", 5);

        if (ArgUtil.Flag(args, "--json"))
        {
            var (m, t) = s.Workflow.HitRate();
            var cyclesJson = s.Workflow.Recent(limit).Select(CycleMap.From).ToList();
            JsonOut.Write(
                new StatusJson(s.Project, s.DbPath, s.LlmConfigured, s.Workflow.CycleCount(),
                    new HitRateJson(m, t), cyclesJson),
                PdsaJson.Default.StatusJson);
            return Task.FromResult(0);
        }

        Console.WriteLine($"프로젝트 : {s.Project}");
        Console.WriteLine($"그래프DB : {s.DbPath}");
        Console.WriteLine($"LLM      : {(s.LlmConfigured ? "설정됨" : "미설정")}");
        Console.WriteLine($"누적 사이클: {s.Workflow.CycleCount()}개");
        var (met, total) = s.Workflow.HitRate();
        Console.WriteLine(total > 0
            ? $"기대 충족률: {met}/{total} ({100 * met / total}%)   (재현율 = 기대대로 된 사이클 비율)"
            : "기대 충족률: (판정된 사이클 없음)");

        var recent = s.Workflow.Recent(limit);
        if (recent.Count == 0)
        {
            Console.WriteLine("\n아직 기록이 없습니다. `pdsa plan \"<계획>\"` 으로 시작하세요.");
            return Task.FromResult(0);
        }

        var full = ArgUtil.Flag(args, "--full");
        Console.WriteLine("\n최근 사이클:");
        foreach (var c in recent)
        {
            var badge = c.Verdict.Length > 0 ? $"  판정:{c.Verdict}" : "";
            Console.WriteLine($"  #{c.Id}  [{c.Status}]{badge}  {c.Started}");
            foreach (var p in c.Phases)
                Console.WriteLine($"     - {p.Kind,-5}: {OneLine(p.Input, full)}");
        }
        return Task.FromResult(0);
    }

    private static string OneLine(string s, bool full)
    {
        var t = (s ?? "").ReplaceLineEndings(" ").Trim();
        return full || t.Length <= 70 ? t : t[..70] + "…";
    }
}
