using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>PDSA 의 Do: 수행한 내용을 보고받아 Plan→Do 를 그래프로 정리해 기록한다.</summary>
public sealed class DoCommand : ICliCommand
{
    public string Name => "do";
    public string Summary => "수행한 것 보고 → Plan→Do 그래프 정리";
    public string Usage => "pdsa do \"<수행한 것>\" [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }
        var done = ArgUtil.Positional(args);
        if (string.IsNullOrWhiteSpace(done)) { Console.Error.WriteLine($"사용법: {Usage}"); return 2; }

        using var s = PdsaSession.Open(args);
        var cur = s.Workflow.CurrentCycle();
        if (cur is null) { Console.Error.WriteLine("진행 중인 사이클이 없습니다. 먼저 `pdsa plan` 으로 계획을 입력하세요."); return 3; }

        var cid = cur.Value.Id;
        var plan = s.Workflow.GetPhase(cid, PdsaWorkflow.PlanKind)?.Input ?? "";
        var organized = await Spinner.RunAsync("코칭 중", c => s.Coach.OrganizeDoAsync(plan, done, c), ct);
        s.Workflow.RecordPhase(cid, PdsaWorkflow.DoKind, done, organized);

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} — Do 기록됨");
        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── Plan→Do 정리 ────────────────────────────");
            Console.WriteLine(organized);
        }
        else Console.WriteLine(PlanCommand.Note(s));

        Console.WriteLine();
        Console.WriteLine("▶ 다음: 작업이 완료되면 `pdsa study \"<결과/관찰>\"` 로 결과를 알려주세요.");
        return 0;
    }
}
