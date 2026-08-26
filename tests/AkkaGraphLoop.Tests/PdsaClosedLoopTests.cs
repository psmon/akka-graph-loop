using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// PDSA 폐루프(기대→판정→보강) 그래프 메모리 검증: Phase 메타 필드 영속, 기대 충족률(재현율),
/// 보강 사이클 자동 연결(PendingReinforceTarget) 및 REINFORCES 엣지.
/// </summary>
public class PdsaClosedLoopTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_loop_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    [Fact]
    public void Records_and_reads_back_closed_loop_meta()
    {
        var db = TempDb();
        using var wf = new PdsaWorkflow(db, "proj");
        var cid = wf.StartCycle();
        wf.RecordPhase(cid, PdsaWorkflow.PlanKind, "계획", "가설",
            new Dictionary<string, string> { ["expected"] = "p95 200ms 이하" });
        wf.RecordPhase(cid, PdsaWorkflow.StudyKind, "결과", "학습",
            new Dictionary<string, string> { ["verdict"] = "partial", ["actual"] = "240ms" });

        Assert.Equal("p95 200ms 이하", wf.GetPhase(cid, PdsaWorkflow.PlanKind)!.Expected);
        var study = wf.GetPhase(cid, PdsaWorkflow.StudyKind)!;
        Assert.Equal("partial", study.Verdict);
        Assert.Equal("240ms", study.Actual);
    }

    [Fact]
    public void HitRate_counts_only_met_over_cycles_with_verdict()
    {
        var db = TempDb();
        using var wf = new PdsaWorkflow(db, "proj");

        var c1 = wf.StartCycle();
        wf.RecordPhase(c1, PdsaWorkflow.StudyKind, "r1", "", new Dictionary<string, string> { ["verdict"] = "met" });
        var c2 = wf.StartCycle();
        wf.RecordPhase(c2, PdsaWorkflow.StudyKind, "r2", "", new Dictionary<string, string> { ["verdict"] = "partial" });
        var c3 = wf.StartCycle();
        wf.RecordPhase(c3, PdsaWorkflow.StudyKind, "r3", ""); // 판정 없음 → 분모 제외

        var (met, total) = wf.HitRate();
        Assert.Equal(1, met);
        Assert.Equal(2, total);
    }

    [Fact]
    public void PendingReinforceTarget_detects_act_reinforce_and_StartCycle_links_REINFORCES()
    {
        var db = TempDb();
        long c1;
        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            c1 = wf.StartCycle();
            wf.RecordPhase(c1, PdsaWorkflow.ActKind, "메모", "다음액션",
                new Dictionary<string, string> { ["reinforce"] = "yes:병목 제거" });

            Assert.Equal(c1, wf.PendingReinforceTarget()); // 직전 Act 가 보강 요구
            var c2 = wf.StartCycle(wf.PendingReinforceTarget());
            Assert.Equal(c1 + 1, c2);
        }

        // 읽기 전용 리더로 REINFORCES 엣지가 만들어졌는지 확인.
        using var reader = new PdsaWorkflowReader(db);
        var model = reader.Read();
        Assert.Contains(model.Edges, e => e.Type == "REINFORCES"
            && e.From == $"Cycle:{c1 + 1}" && e.To == $"Cycle:{c1}");
    }

    [Fact]
    public void No_pending_reinforce_when_act_says_no()
    {
        var db = TempDb();
        using var wf = new PdsaWorkflow(db, "proj");
        var c1 = wf.StartCycle();
        wf.RecordPhase(c1, PdsaWorkflow.ActKind, "메모", "다음액션",
            new Dictionary<string, string> { ["reinforce"] = "no" });
        Assert.Equal(0, wf.PendingReinforceTarget());
    }
}
