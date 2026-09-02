using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// 전 사이클을 <b>시간순(오름차순)</b> 타임라인으로 보여준다 — 회차마다 기대 → 판정 → 실제 → 배운점.
///
/// <para><c>status</c> 와 역할이 다르다: <c>status</c> 는 최신순으로 "지금 어디까지 왔나",
/// <c>history</c> 는 오름차순으로 "어떻게 여기까지 왔나"를 읽는다. 그래서 정렬 기본값이 반대다.</para>
/// </summary>
public sealed class HistoryCommand : ICliCommand
{
    public string Name => "history";
    public string Summary => "전 사이클 타임라인(오름차순): 기대→판정→실제→배운점";
    public string Usage =>
        "pdsa history [--from <n>] [--to <n>] [--limit <n>] [--desc] [--full] [--json] [--project <이름>]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        using var s = PdsaSession.Open(args);
        var from = ArgUtil.Int(args, "--from", 0);
        var to = ArgUtil.Int(args, "--to", 0);
        var limit = ArgUtil.Int(args, "--limit", 0);          // 0 = 전체(서사를 끊지 않는 게 기본)
        var ascending = !ArgUtil.Flag(args, "--desc");

        if (from > 0 && to > 0 && from > to)
        {
            Console.Error.WriteLine($"--from({from}) 이 --to({to}) 보다 큽니다.");
            return Task.FromResult(2);
        }

        var cycles = s.Workflow.Range(from, to, ascending, limit);
        var (met, total) = s.Workflow.HitRate();

        if (ArgUtil.Flag(args, "--json"))
        {
            JsonOut.Write(
                new HistoryJson(s.Project, s.Workflow.CycleCount(), new HitRateJson(met, total),
                    cycles.Select(CycleMap.From).ToList()),
                PdsaJson.Default.HistoryJson);
            return Task.FromResult(0);
        }

        if (cycles.Count == 0)
        {
            Console.WriteLine(from > 0 || to > 0
                ? $"해당 범위에 사이클이 없습니다(누적 {s.Workflow.CycleCount()}개)."
                : "아직 기록이 없습니다. `pdsa plan \"<계획>\"` 으로 시작하세요.");
            return Task.FromResult(0);
        }

        var full = ArgUtil.Flag(args, "--full");
        Console.WriteLine($"■ [{s.Project}] 사이클 {cycles.Count}개" +
                          (total > 0 ? $"   기대 충족률 {met}/{total} ({100 * met / total}%)" : ""));
        Console.WriteLine();

        foreach (var c in cycles)
        {
            var plan = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.PlanKind);
            var study = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.StudyKind);
            var act = c.Phases.FirstOrDefault(p => p.Kind == PdsaWorkflow.ActKind);

            Console.WriteLine($"#{c.Id}  {c.Started[..10]}  {Verdict(c.Verdict)}  {Line(plan?.Input, full, 90)}");
            if (plan is { Expected.Length: > 0 }) Console.WriteLine($"    기대 : {Line(plan.Expected, full)}");
            if (study is { Actual.Length: > 0 }) Console.WriteLine($"    실제 : {Line(study.Actual, full)}");
            if (study is { Llm.Length: > 0 }) Console.WriteLine($"    배움 : {Line(study.Llm, full)}");
            if (act is { Reinforce.Length: > 0 } && act.Reinforce.StartsWith("yes", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"    보강 : {Line(act.Reinforce[4..], full)}");
            Console.WriteLine();
        }

        if (!full) Console.WriteLine("(절삭 없이 보려면 `--full`, 기계 판독은 `--json`, 한 회차 상세는 `pdsa show <n>`)");
        return Task.FromResult(0);
    }

    private static string Verdict(string v) => v switch
    {
        "met" => "met    ",
        "partial" => "partial",
        "unmet" => "unmet  ",
        _ => "-      ",
    };

    private static string Line(string? s, bool full, int width = 110)
    {
        var t = (s ?? "").ReplaceLineEndings(" ").Replace("  ", " ").Trim();
        return full || t.Length <= width ? t : t[..width] + "…";
    }
}
