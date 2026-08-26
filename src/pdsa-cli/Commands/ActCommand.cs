using System.Collections.Generic;
using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// PDSA 의 Act: 이번 사이클(판정 포함)을 바탕으로 다음 개선 액션을 코칭하고, 즉시 보강이 필요한지
/// 판단해 기록한다(사이클 종료). 보강이 필요하면 다음 <c>pdsa plan</c> 이 자동으로 보강 사이클로 이어진다.
/// </summary>
public sealed class ActCommand : ICliCommand
{
    public string Name => "act";
    public string Summary => "학습 정리 + 즉시 보강 판단(사이클 종료)";
    public string Usage => "pdsa act [--note \"<메모>\"] [--reinforce \"<보강할 것>\"] [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        using var s = PdsaSession.Open(args);
        var cur = s.Workflow.CurrentCycle();
        if (cur is null) { Console.Error.WriteLine("진행 중인 사이클이 없습니다. 먼저 `pdsa plan` 으로 시작하세요."); return 3; }

        var cid = cur.Value.Id;
        var plan = s.Workflow.GetPhase(cid, PdsaWorkflow.PlanKind)?.Input ?? "";
        var done = s.Workflow.GetPhase(cid, PdsaWorkflow.DoKind)?.Input ?? "";
        var studyPhase = s.Workflow.GetPhase(cid, PdsaWorkflow.StudyKind);
        var study = studyPhase?.Input ?? "";
        var verdict = studyPhase?.Verdict ?? "";
        var note = ArgUtil.Option(args, "--note") ?? "";

        var coaching = await s.Coach.NextActionAsync(plan, done, study, verdict, ct);

        // 보강 여부: 명시 --reinforce 우선, 없으면 코치 판단.
        var manual = ArgUtil.Flag(args, "--reinforce");
        var manualWhat = ArgUtil.Option(args, "--reinforce");
        if (manualWhat is not null && manualWhat.StartsWith('-')) manualWhat = null; // 옵션값 오인 방지
        var reinforce = manual || coaching.Reinforce;
        var what = manualWhat ?? coaching.What;
        var reinforceValue = reinforce ? "yes:" + what : "no";

        s.Workflow.RecordPhase(cid, PdsaWorkflow.ActKind, note, coaching.Narrative,
            new Dictionary<string, string> { ["reinforce"] = reinforceValue });

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} — Act 기록됨(사이클 종료). 누적 사이클 {s.Workflow.CycleCount()}개.");
        var (met, total) = s.Workflow.HitRate();
        if (total > 0)
            Console.WriteLine($"  기대 충족률: {met}/{total} ({100 * met / total}%)");

        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 다음 개선 액션 ──────────────────────────");
            Console.WriteLine(coaching.Narrative);
        }
        else Console.WriteLine(PlanCommand.Note(s));

        Console.WriteLine();
        if (reinforce)
        {
            Console.WriteLine($"▶ 보강 필요{(what.Length > 0 ? $": {what}" : "")}");
            Console.WriteLine($"   다음 `pdsa plan \"<보강 계획>\"` 은 자동으로 #{cid} 의 보강 사이클로 이어집니다(원치 않으면 --fresh).");
        }
        else
        {
            Console.WriteLine("▶ 다음: 이 개선을 반영해 `pdsa plan \"<개선된 계획>\"` 로 다음 사이클을 시작하세요.");
        }
        Console.WriteLine("   (누적 그래프 보기: pdsa view)");
        return 0;
    }
}
