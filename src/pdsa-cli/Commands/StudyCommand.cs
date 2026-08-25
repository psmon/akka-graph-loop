using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>PDSA 의 Study: 결과를 학습 관점으로 분석하고 개선점을 도출한다.</summary>
public sealed class StudyCommand : ICliCommand
{
    public string Name => "study";
    public string Summary => "결과 보고 → 학습·개선점 도출(Check 아님)";
    public string Usage => "pdsa study \"<결과/관찰>\" [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }
        var study = ArgUtil.Positional(args);
        if (string.IsNullOrWhiteSpace(study)) { Console.Error.WriteLine($"사용법: {Usage}"); return 2; }

        using var s = PdsaSession.Open(args);
        var cur = s.Workflow.CurrentCycle();
        if (cur is null) { Console.Error.WriteLine("진행 중인 사이클이 없습니다. 먼저 `pdsa plan` 으로 시작하세요."); return 3; }

        var cid = cur.Value.Id;
        var plan = s.Workflow.GetPhase(cid, PdsaWorkflow.PlanKind)?.Input ?? "";
        var done = s.Workflow.GetPhase(cid, PdsaWorkflow.DoKind)?.Input ?? "";
        var analysis = await s.Coach.StudyAsync(plan, done, study, ct);
        s.Workflow.RecordPhase(cid, PdsaWorkflow.StudyKind, study, analysis);

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} — Study 기록됨");
        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 학습 & 개선점 ───────────────────────────");
            Console.WriteLine(analysis);
        }
        else Console.WriteLine(PlanCommand.Note(s));

        Console.WriteLine();
        Console.WriteLine("▶ 다음: `pdsa act` 로 다음에 수행할 개선 액션을 확인하세요.");
        return 0;
    }
}
