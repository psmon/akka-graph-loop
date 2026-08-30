using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// PDSA 폐루프 평가 요약: 사이클별 기대 평가 → 판정 → 실제 를 나열하고, 전체 기대 충족률(재현율)을 보여준다.
/// </summary>
public sealed class EvalCommand : ICliCommand
{
    public string Name => "eval";
    public string Summary => "기대 충족률(재현율) + 사이클별 기대/판정/실제";
    public string Usage => "pdsa eval [--project <이름>] [--limit 10] [--full] [--json]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        using var s = PdsaSession.Open(args);
        var limit = ArgUtil.Int(args, "--limit", 10);

        if (ArgUtil.Flag(args, "--json"))
        {
            var (jm, jt) = s.Workflow.HitRate();
            var cyclesJson = s.Workflow.Recent(limit).Select(c =>
            {
                var plan = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.PlanKind);
                var studyP = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.StudyKind);
                var act = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.ActKind);
                var reinforce = act is not null && act.Reinforce.StartsWith("yes", StringComparison.OrdinalIgnoreCase)
                    ? (act.Reinforce.Length > 4 ? act.Reinforce[4..] : "") : "";
                return new EvalCycleJson(c.Id, c.Status, c.Verdict, plan?.Expected ?? "", studyP?.Actual ?? "", reinforce);
            }).ToList();
            JsonOut.Write(new EvalJson(s.Project, new HitRateJson(jm, jt), cyclesJson), PdsaJson.Default.EvalJson);
            return Task.FromResult(0);
        }

        var (met, total) = s.Workflow.HitRate();
        Console.WriteLine($"프로젝트 : {s.Project}");
        Console.WriteLine(total > 0
            ? $"기대 충족률(재현율): {met}/{total} ({100 * met / total}%)"
            : "기대 충족률(재현율): (판정된 사이클 없음)");

        var recent = s.Workflow.Recent(limit);
        if (recent.Count == 0)
        {
            Console.WriteLine("\n아직 기록이 없습니다. `pdsa plan \"<계획>\"` 으로 시작하세요.");
            return Task.FromResult(0);
        }

        var full = ArgUtil.Flag(args, "--full");
        Console.WriteLine("\n사이클별 폐루프:");
        foreach (var c in recent)
        {
            var plan = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.PlanKind);
            var studyP = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.StudyKind);
            var act = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.ActKind);
            var verdict = c.Verdict.Length > 0 ? c.Verdict : "-";

            Console.WriteLine($"  #{c.Id}  [{c.Status}]  판정:{verdict}");
            Console.WriteLine($"     기대: {OneLine(plan?.Expected, full)}");
            Console.WriteLine($"     실제: {OneLine(studyP?.Actual, full)}");
            if (act is not null && act.Reinforce.StartsWith("yes", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"     보강: {OneLine(act.Reinforce.Length > 4 ? act.Reinforce[4..] : "", full)}");
        }
        return Task.FromResult(0);
    }

    private static string OneLine(string? s, bool full)
    {
        var t = (s ?? "").ReplaceLineEndings(" ").Trim();
        if (t.Length == 0) return "-";
        return full || t.Length <= 90 ? t : t[..90] + "…";
    }
}
