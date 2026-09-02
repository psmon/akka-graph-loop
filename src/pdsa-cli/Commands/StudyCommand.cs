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
    public string Usage => "pdsa study \"<결과/관찰>\" [--json] [--project <이름>]";

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
        if (planPhase is null) { Console.Error.WriteLine(CycleGuard.OrphanMessage(cid)); return 3; }

        var expected = planPhase.Expected;
        var plan = planPhase.Input;
        var doPhase = s.Workflow.GetPhase(cid, PdsaWorkflow.DoKind);
        var done = doPhase?.Input ?? "";

        // 이 사이클에서 실제로 계측된 값을 판정 근거로 넘긴다 — Study 가 인상이 아니라 데이터로 판정하도록.
        var measured = MetricsEvidence(planPhase, doPhase);

        var metrics = new PhaseMetrics(s.Llm);
        var judgment = await Spinner.RunAsync("판정 중",
            c => s.Coach.JudgeAsync(expected, plan, done, study, c, measured), ct);
        var extra = metrics.Collect(new Dictionary<string, string>
        {
            ["verdict"] = judgment.Verdict,
            ["actual"] = judgment.Actual,
        });
        s.Workflow.RecordPhase(cid, PdsaWorkflow.StudyKind, study, judgment.Narrative, extra);

        if (ArgUtil.Flag(args, "--json"))
        {
            JsonOut.Write(
                new StudyJson(s.Project, cid, expected, judgment.Verdict, judgment.Actual, judgment.Narrative,
                    s.Coach.Enabled, MetricsMap.From(extra)),
                PdsaJson.Default.StudyJson);
            return 0;
        }

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

    /// <summary>
    /// 이 사이클의 앞선 단계들이 남긴 계측치를 판정 프롬프트용 근거 블록으로 만든다.
    /// 계측이 없으면(구 버전이 기록한 사이클, LLM 미설정) 빈 문자열 — 없는 근거를 지어내지 않는다.
    /// </summary>
    private static string MetricsEvidence(params PdsaPhase?[] phases)
    {
        var lines = phases
            .Where(p => p is not null)
            .Select(p => p!.MetricsLine())
            .Where(line => line.Length > 0)
            .ToArray();
        return string.Join("\n", lines);
    }

    private static string VerdictLabel(string v) => v switch
    {
        "met" => "met ✔ 기대 충족",
        "partial" => "partial ◐ 부분 충족",
        "unmet" => "unmet ✘ 미충족",
        _ => v,
    };
}
