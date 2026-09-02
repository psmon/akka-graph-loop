using System.Collections.Generic;
using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// PDSA 의 Plan: 계획을 입력받아 LLM 으로 '기대 평가(성공 기준)'를 세우고 새 사이클을 시작한다.
/// 직전 사이클이 Act 에서 보강을 요구했다면 이번 사이클을 그 사이클의 '보강 사이클'로 잇는다.
/// </summary>
public sealed class PlanCommand : ICliCommand
{
    public string Name => "plan";
    public string Summary => "계획 입력 → 기대 평가 수립(새 사이클 시작)";
    public string Usage => "pdsa plan \"<계획>\" [--expect \"<기대 평가>\"] [--fresh] [--no-recall] [--json] [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }
        var plan = ArgUtil.Positional(args);
        if (string.IsNullOrWhiteSpace(plan)) { Console.Error.WriteLine($"사용법: {Usage}"); return 2; }

        using var s = PdsaSession.Open(args);

        // 직전 사이클이 보강을 요구했으면(그리고 --fresh 아니면) 이번을 보강 사이클로 잇는다(읽기 전용).
        var reinforceOf = ArgUtil.Flag(args, "--fresh") ? 0 : s.Workflow.PendingReinforceTarget();

        // 누적 그래프 메모리 되먹임: 최근 사이클 학습을 코칭 프롬프트에 주입(--no-recall 로 생략).
        var priorLearnings = s.Coach.Enabled && !ArgUtil.Flag(args, "--no-recall")
            ? LearningFormat.ToPromptBlock(s.Workflow.RecentLearnings(3))
            : "";

        // 코칭이 성공한 뒤에야 그래프에 쓴다. 여기서 실패/취소되면 사이클은 생성되지 않는다
        // (이전에는 StartCycle 이 먼저라 실패마다 Phase 0개짜리 고아 사이클이 남았다).
        var metrics = new PhaseMetrics(s.Llm);
        var coaching = await Spinner.RunAsync("코칭 중", c => s.Coach.HypothesisAsync(plan, priorLearnings, c), ct);
        var expected = ArgUtil.Option(args, "--expect") ?? coaching.Expected;

        var extra = metrics.Collect(new Dictionary<string, string> { ["expected"] = expected });
        var cid = s.Workflow.StartCycleWithPlan(reinforceOf, plan, coaching.Narrative, extra);

        if (ArgUtil.Flag(args, "--json"))
        {
            JsonOut.Write(new PlanJson(s.Project, cid, reinforceOf, expected, coaching.Narrative, s.Coach.Enabled,
                    MetricsMap.From(extra)),
                PdsaJson.Default.PlanJson);
            return 0;
        }

        if (reinforceOf > 0)
            Console.WriteLine($"■ [{s.Project}] 보강 사이클 #{cid} 시작 (원 사이클 #{reinforceOf} 이월) — Plan 기록됨");
        else
            Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} 시작 — Plan 기록됨");

        if (expected.Length > 0)
            Console.WriteLine($"  기대 평가: {expected}");

        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 코칭 & 가설 ─────────────────────────────");
            Console.WriteLine(coaching.Narrative);
        }
        else
        {
            Console.WriteLine(Note(s));
        }

        Console.WriteLine();
        Console.WriteLine("▶ 다음: 이 기대 평가를 검증하도록 작업을 수행(Do)한 뒤 `pdsa do \"<수행한 것>\"` 로 알려주세요.");
        return 0;
    }

    internal static string Note(PdsaSession s) =>
        $"(LLM 미설정: 코칭·판정을 생략하고 기록만 했습니다.)\n{s.LlmNote}";
}
