using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>PDSA 의 Plan: 계획을 입력받아 LLM 으로 가설을 세우고 새 사이클을 시작한다.</summary>
public sealed class PlanCommand : ICliCommand
{
    public string Name => "plan";
    public string Summary => "계획 입력 → 가설 수립(새 사이클 시작)";
    public string Usage => "pdsa plan \"<계획>\" [--project <이름>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }
        var plan = ArgUtil.Positional(args);
        if (string.IsNullOrWhiteSpace(plan)) { Console.Error.WriteLine($"사용법: {Usage}"); return 2; }

        using var s = PdsaSession.Open(args);
        var cid = s.Workflow.StartCycle();

        Console.WriteLine($"■ [{s.Project}] 사이클 #{cid} 시작 — Plan 기록됨");
        var hypothesis = await s.Coach.HypothesisAsync(plan, ct);
        s.Workflow.RecordPhase(cid, PdsaWorkflow.PlanKind, plan, hypothesis);

        if (s.Coach.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("── 코칭 & 가설 ─────────────────────────────");
            Console.WriteLine(hypothesis);
        }
        else
        {
            Console.WriteLine(Note(s));
        }

        Console.WriteLine();
        Console.WriteLine("▶ 다음: 이 가설을 바탕으로 작업을 수행(Do)한 뒤 `pdsa do \"<수행한 것>\"` 로 알려주세요.");
        return 0;
    }

    internal static string Note(PdsaSession s) =>
        $"(LLM 미설정: 코칭을 생략하고 기록만 했습니다.)\n{s.LlmNote}";
}
