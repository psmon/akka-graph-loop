using System.Collections.Generic;
using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// PDSA 의 Study: 결과를 학습 관점으로 분석하고, Plan 의 '기대 평가' 대비 달성 여부를 LLM 이 판정한다.
/// </summary>
public sealed class StudyCommand : ICliCommand
{
    public string Name => "study";
    public string Summary => "결과 보고 → 기대 대비 판정·학습(Check 아님)";
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
        var planPhase = s.Workflow.GetPhase(cid, PdsaWorkflow.PlanKind);
        var expected = planPhase?.Expected ?? "";
        var plan = planPhase?.Input ?? "";
        var done = s.Workflow.GetPhase(cid, PdsaWorkflow.DoKind)?.Input ?? "";

        var judgment = await Spinner.RunAsync("판정 중", c => s.Coach.JudgeAsync(expected, plan, done, study, c), ct);
        s.Workflow.RecordPhase(cid, PdsaWorkflow.StudyKind, study, judgment.Narrative,
            new Dictionary<string, string> { ["verdict"] = judgment.Verdict, ["actual"] = judgment.Actual });

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} — Study 기록됨");
        if (expected.Length > 0)
            Console.WriteLine($"  기대 평가: {expected}");
        if (judgment.Verdict.Length > 0)
            Console.WriteLine($"  판정: {VerdictLabel(judgment.Verdict)}" +
                              (judgment.Actual.Length > 0 ? $"   (실제: {judgment.Actual})" : ""));

        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 학습 & 개선점 ───────────────────────────");
            Console.WriteLine(judgment.Narrative);
        }
        else Console.WriteLine(PlanCommand.Note(s));

        Console.WriteLine();
        Console.WriteLine("▶ 다음: `pdsa act` 로 학습을 정리하고 필요 시 보강 액션을 확인하세요.");
        return 0;
    }

    private static string VerdictLabel(string v) => v switch
    {
        "met" => "met ✔ 기대 충족",
        "partial" => "partial ◐ 부분 충족",
        "unmet" => "unmet ✘ 미충족",
        _ => v,
    };
}
