using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// 한 사이클의 전체 내용을 보여준다: 4단계 입력·코칭, 기대/판정/실제, 단계별 계측, 보강(REINFORCES) 링크.
/// <c>status</c> 가 여러 사이클을 얕게 훑는다면 <c>show</c> 는 한 사이클을 끝까지 파고든다.
/// </summary>
public sealed class ShowCommand : ICliCommand
{
    public string Name => "show";
    public string Summary => "한 사이클 상세(기대/판정/실제·계측·보강 링크)";
    public string Usage => "pdsa show [<사이클>] [--project <이름>] [--full] [--json]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        using var s = PdsaSession.Open(args);

        // 인자 없으면 가장 최근 사이클.
        var arg = ArgUtil.Positional(args);
        long id;
        if (string.IsNullOrWhiteSpace(arg))
        {
            var cur = s.Workflow.CurrentCycle();
            if (cur is null) { Console.Error.WriteLine("아직 기록이 없습니다. `pdsa plan \"<계획>\"` 으로 시작하세요."); return Task.FromResult(3); }
            id = cur.Value.Id;
        }
        else if (!long.TryParse(arg.Trim().TrimStart('#'), out id))
        {
            Console.Error.WriteLine($"사이클 번호가 아닙니다: {arg}\n사용법: {Usage}");
            return Task.FromResult(2);
        }

        var cycle = s.Workflow.Cycle(id);
        if (cycle is null)
        {
            Console.Error.WriteLine($"사이클 #{id} 가 없습니다. 범위: 1 ~ {s.Workflow.CycleCount()} (`pdsa history` 로 목록)");
            return Task.FromResult(3);
        }

        var (reinforces, reinforcedBy) = s.Workflow.ReinforceLinks(id);

        if (ArgUtil.Flag(args, "--json"))
        {
            JsonOut.Write(new ShowJson(s.Project, CycleMap.From(cycle), reinforces, reinforcedBy),
                PdsaJson.Default.ShowJson);
            return Task.FromResult(0);
        }

        var full = ArgUtil.Flag(args, "--full");
        var badge = cycle.Verdict.Length > 0 ? $"  판정:{cycle.Verdict}" : "";
        Console.WriteLine($"■ [{s.Project}] 사이클 #{cycle.Id}  [{cycle.Status}]{badge}  {cycle.Started}");
        if (reinforces > 0) Console.WriteLine($"  ↳ #{reinforces} 를 보강하는 사이클");
        if (reinforcedBy.Count > 0)
            Console.WriteLine($"  ↳ 이 사이클을 보강함: {string.Join(", ", reinforcedBy.Select(r => "#" + r))}");

        foreach (var p in cycle.Phases)
        {
            Console.WriteLine();
            Console.WriteLine($"── {p.Kind.ToUpperInvariant()} ─────────────────────────────");
            if (p.Input.Length > 0) Console.WriteLine(Body(p.Input, full));
            if (p.Expected.Length > 0) Console.WriteLine($"  기대 평가: {Body(p.Expected, full)}");
            if (p.Verdict.Length > 0) Console.WriteLine($"  판정: {p.Verdict}");
            if (p.Actual.Length > 0) Console.WriteLine($"  실제: {Body(p.Actual, full)}");
            if (p.Reinforce.Length > 0) Console.WriteLine($"  보강: {p.Reinforce}");
            var metrics = p.MetricsLine();
            if (metrics.Length > 0) Console.WriteLine($"  계측: {metrics}");
            if (full && p.Llm.Length > 0)
            {
                Console.WriteLine("  ── 코칭 ──");
                Console.WriteLine(p.Llm);
            }
        }

        if (!full) Console.WriteLine("\n(전체 코칭 서술은 `--full`, 기계 판독은 `--json`)");
        return Task.FromResult(0);
    }

    private static string Body(string s, bool full)
    {
        var t = (s ?? "").Trim();
        return full || t.Length <= 400 ? t : t[..400] + "…";
    }
}
