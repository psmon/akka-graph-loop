using System.Collections.Generic;
using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 회차별 조회(`show`/`history`)를 뒷받침하는 Core 조회 API 검증:
/// <see cref="PdsaWorkflow.Cycle"/> · <see cref="PdsaWorkflow.Range"/> · <see cref="PdsaWorkflow.ReinforceLinks"/>.
///
/// <para>핵심 불변식: 세 조회(<c>Recent</c>/<c>Cycle</c>/<c>Range</c>)가 <b>같은 내부 경로</b>를 지나므로
/// 조회 방식에 따라 값이 달라지면 안 된다 — 명령별로 다른 쿼리를 두면 생기는 불일치를 막는다.</para>
/// </summary>
public class PdsaHistoryTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_hist_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    /// <summary>사이클 n 개를 채운다. 짝수 회차는 met, 홀수 회차는 partial 판정.</summary>
    private static PdsaWorkflow Seed(int count, string db)
    {
        var wf = new PdsaWorkflow(db, "proj");
        for (var i = 1; i <= count; i++)
        {
            var cid = wf.StartCycleWithPlan(0, $"계획 {i}", $"코칭 {i}",
                new Dictionary<string, string> { ["expected"] = $"기대 {i}" });
            wf.RecordPhase(cid, PdsaWorkflow.DoKind, $"수행 {i}", "");
            wf.RecordPhase(cid, PdsaWorkflow.StudyKind, $"결과 {i}", $"학습 {i}",
                new Dictionary<string, string> { ["verdict"] = i % 2 == 0 ? "met" : "partial", ["actual"] = $"실제 {i}" });
            wf.RecordPhase(cid, PdsaWorkflow.ActKind, "", $"액션 {i}",
                new Dictionary<string, string> { ["reinforce"] = "no" });
        }
        return wf;
    }

    [Fact]
    public void Cycle_returns_one_cycle_with_all_four_phases()
    {
        using var wf = Seed(3, TempDb());

        var c = wf.Cycle(2);

        Assert.NotNull(c);
        Assert.Equal(2, c!.Id);
        Assert.Equal("met", c.Verdict);
        Assert.Equal(new[] { "plan", "do", "study", "act" }, c.Phases.Select(p => p.Kind).ToArray());
        Assert.Equal("기대 2", c.Phases[0].Expected);
        Assert.Equal("실제 2", c.Phases[2].Actual);
    }

    [Fact]
    public void Cycle_returns_null_for_a_missing_id()
    {
        using var wf = Seed(2, TempDb());
        Assert.Null(wf.Cycle(99));
    }

    [Fact]
    public void Range_is_ascending_by_default()
    {
        using var wf = Seed(5, TempDb());

        // history 는 "어떻게 여기까지 왔나"를 읽으므로 오름차순이 기본이다(status 는 최신순).
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, wf.Range().Select(c => c.Id).ToArray());
        Assert.Equal(new long[] { 5, 4, 3, 2, 1 }, wf.Range(ascending: false).Select(c => c.Id).ToArray());
    }

    [Theory]
    [InlineData(2, 4, new long[] { 2, 3, 4 })]
    [InlineData(4, 0, new long[] { 4, 5 })]          // to 생략 = 끝까지
    [InlineData(0, 2, new long[] { 1, 2 })]          // from 생략 = 처음부터
    [InlineData(0, 0, new long[] { 1, 2, 3, 4, 5 })] // 둘 다 생략 = 전체
    public void Range_bounds_are_inclusive_and_optional(long from, long to, long[] expected)
    {
        using var wf = Seed(5, TempDb());
        Assert.Equal(expected, wf.Range(from, to).Select(c => c.Id).ToArray());
    }

    [Fact]
    public void Range_returns_empty_for_a_range_with_no_cycles()
    {
        using var wf = Seed(3, TempDb());
        Assert.Empty(wf.Range(90, 99));
    }

    [Fact]
    public void Range_limit_keeps_the_requested_ordering()
    {
        using var wf = Seed(5, TempDb());
        Assert.Equal(new long[] { 1, 2 }, wf.Range(limit: 2).Select(c => c.Id).ToArray());
        Assert.Equal(new long[] { 5, 4 }, wf.Range(ascending: false, limit: 2).Select(c => c.Id).ToArray());
    }

    /// <summary>조회 경로 통합의 회귀 방어: 같은 사이클을 어느 API 로 읽든 값이 같아야 한다.</summary>
    [Fact]
    public void Recent_Cycle_and_Range_agree_on_the_same_cycle()
    {
        using var wf = Seed(4, TempDb());

        var viaRecent = wf.Recent(1).Single();          // 최신 1개 = #4
        var viaCycle = wf.Cycle(4)!;
        var viaRange = wf.Range(4, 4).Single();

        foreach (var other in new[] { viaCycle, viaRange })
        {
            Assert.Equal(viaRecent.Id, other.Id);
            Assert.Equal(viaRecent.Status, other.Status);
            Assert.Equal(viaRecent.Verdict, other.Verdict);
            Assert.Equal(viaRecent.Phases.Select(p => p.Kind), other.Phases.Select(p => p.Kind));
            Assert.Equal(viaRecent.Phases.Select(p => p.Input), other.Phases.Select(p => p.Input));
        }
    }

    [Fact]
    public void ReinforceLinks_report_both_directions()
    {
        var db = TempDb();
        using var wf = Seed(1, db);

        var reinforcing = wf.StartCycleWithPlan(1, "보강 계획", "코칭");   // #2 -[:REINFORCES]-> #1

        var origin = wf.ReinforceLinks(1);
        Assert.Equal(0, origin.Reinforces);                    // #1 은 아무것도 보강하지 않는다
        Assert.Equal(new[] { reinforcing }, origin.ReinforcedBy.ToArray());

        var later = wf.ReinforceLinks(reinforcing);
        Assert.Equal(1, later.Reinforces);                     // #2 는 #1 을 보강한다
        Assert.Empty(later.ReinforcedBy);
    }

    [Fact]
    public void ReinforceLinks_are_empty_for_a_standalone_cycle()
    {
        using var wf = Seed(2, TempDb());

        var (reinforces, reinforcedBy) = wf.ReinforceLinks(1);

        Assert.Equal(0, reinforces);
        Assert.Empty(reinforcedBy);
    }
}
