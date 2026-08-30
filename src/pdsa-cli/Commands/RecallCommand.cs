using System.Linq;
using PdsaCli.Cli;
using PdsaCli.Workflow;

namespace PdsaCli.Commands;

/// <summary>
/// 누적 그래프 메모리에서 과거 사이클의 학습을 되읽는다(recall). 주제 키워드가 있으면 관련 학습만
/// 추린다. plan 은 이 학습을 자동 주입하지만, 에이전트가 계획 전 컨텍스트를 직접 당겨오는 명시적 표면.
/// </summary>
public sealed class RecallCommand : ICliCommand
{
    public string Name => "recall";
    public string Summary => "과거 사이클 학습 되읽기(계획 컨텍스트)";
    public string Usage => "pdsa recall [\"<주제 키워드>\"] [--limit 5] [--json] [--project <이름>]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        var topic = ArgUtil.Positional(args);
        var limit = ArgUtil.Int(args, "--limit", 5);
        var keyword = string.IsNullOrWhiteSpace(topic) ? null : topic;

        using var s = PdsaSession.Open(args);
        var learnings = s.Workflow.RecentLearnings(limit, keyword);

        if (ArgUtil.Flag(args, "--json"))
        {
            var items = learnings
                .Select(l => new LearningJson(l.Cycle, l.Verdict, l.Expected, l.Actual, l.Study, l.Act))
                .ToList();
            JsonOut.Write(new RecallJson(s.Project, keyword, items), PdsaJson.Default.RecallJson);
            return Task.FromResult(0);
        }

        Console.WriteLine($"프로젝트 : {s.Project}");
        Console.WriteLine(keyword is null ? "되읽기 : 최근 학습" : $"되읽기 : \"{keyword}\" 관련 학습");
        if (learnings.Count == 0)
        {
            Console.WriteLine("\n해당하는 과거 학습이 없습니다. `pdsa plan \"<계획>\"` 으로 사이클을 쌓아보세요.");
            return Task.FromResult(0);
        }

        Console.WriteLine();
        foreach (var l in learnings)
        {
            var badge = l.Verdict.Length > 0 ? $"  [{l.Verdict}]" : "";
            Console.WriteLine($"■ 사이클 #{l.Cycle}{badge}");
            if (l.Expected.Length > 0) Console.WriteLine($"  기대: {l.Expected}");
            if (l.Actual.Length > 0) Console.WriteLine($"  실제: {l.Actual}");
            var learned = l.Study.Length > 0 ? l.Study : l.Act;
            if (learned.Length > 0) Console.WriteLine($"  학습: {learned}");
            Console.WriteLine();
        }
        Console.WriteLine("▶ 다음: 이 학습을 반영해 `pdsa plan \"<계획>\"` 로 새 사이클을 시작하세요(plan 이 자동으로도 주입합니다).");
        return Task.FromResult(0);
    }
}
