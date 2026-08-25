using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>PDSA 의 Act: 이번 사이클을 바탕으로 다음 개선 액션을 코칭하고 사이클을 닫는다.</summary>
public sealed class ActCommand : ICliCommand
{
    public string Name => "act";
    public string Summary => "다음 개선 액션 코칭(사이클 종료)";
    public string Usage => "pdsa act [--note \"<메모>\"] [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        using var s = PdsaSession.Open(args);
        var cur = s.Workflow.CurrentCycle();
        if (cur is null) { Console.Error.WriteLine("진행 중인 사이클이 없습니다. 먼저 `pdsa plan` 으로 시작하세요."); return 3; }

        var cid = cur.Value.Id;
        var plan = s.Workflow.GetPhase(cid, PdsaWorkflow.PlanKind)?.Input ?? "";
        var done = s.Workflow.GetPhase(cid, PdsaWorkflow.DoKind)?.Input ?? "";
        var study = s.Workflow.GetPhase(cid, PdsaWorkflow.StudyKind)?.Input ?? "";
        var note = ArgUtil.Option(args, "--note") ?? "";

        var nextAction = await s.Coach.NextActionAsync(plan, done, study, ct);
        s.Workflow.RecordPhase(cid, PdsaWorkflow.ActKind, note, nextAction);

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} — Act 기록됨(사이클 종료). 누적 사이클 {s.Workflow.CycleCount()}개.");
        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 다음 개선 액션 ──────────────────────────");
            Console.WriteLine(nextAction);
        }
        else Console.WriteLine(PlanCommand.Note(s));

        Console.WriteLine();
        Console.WriteLine("▶ 다음: 이 개선을 반영해 `pdsa plan \"<개선된 계획>\"` 로 다음 사이클을 시작하세요.");
        Console.WriteLine("   (누적 그래프 보기: pdsa view)");
        return 0;
    }
}
