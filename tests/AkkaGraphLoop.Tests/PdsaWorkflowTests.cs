using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 프로젝트별 PDSA 워크플로 그래프 메모리 검증: 사이클/단계 기록, 특수문자 안전 저장,
/// 별도 오픈 간 누적(persistent), 되읽기.
/// </summary>
public class PdsaWorkflowTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_wf_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    [Fact]
    public void Records_full_cycle_and_reads_back_phases_with_special_chars()
    {
        var db = TempDb();
        var plan = "캐시 도입: it's a \"test\" & 특수문자\n둘째 줄";

        using (var wf = new PdsaWorkflow(db, "proj-a"))
        {
            var cid = wf.StartCycle();
            wf.RecordPhase(cid, PdsaWorkflow.PlanKind, plan, "가설: 만약 캐시를 도입하면 p95 가 개선된다");
            wf.RecordPhase(cid, PdsaWorkflow.DoKind, "레디스 붙임", "");
            wf.RecordPhase(cid, PdsaWorkflow.StudyKind, "p95 320→110", "학습: 가설 지지");
            wf.RecordPhase(cid, PdsaWorkflow.ActKind, "다음 반영", "다음 액션: 워밍업 추가");

            Assert.Equal(1, cid);
            Assert.Equal("acted", wf.CurrentCycle()!.Value.Status);
            Assert.Equal(plan, wf.GetPhase(cid, PdsaWorkflow.PlanKind)!.Input); // 특수문자 원문 보존
        }
    }

    [Fact]
    public void Cycles_accumulate_across_separate_opens()
    {
        var db = TempDb();

        using (var wf = new PdsaWorkflow(db, "proj-b"))
            wf.RecordPhase(wf.StartCycle(), PdsaWorkflow.PlanKind, "1차 계획", "");

        // 완전히 새로 열어도(=별도 CLI 호출) 누적되어야 한다.
        using (var wf = new PdsaWorkflow(db, "proj-b"))
        {
            var cid = wf.StartCycle();
            Assert.Equal(2, cid);                 // 이전 사이클 이어서 증가
            Assert.Equal(2, wf.CycleCount());
            wf.RecordPhase(cid, PdsaWorkflow.PlanKind, "2차 계획", "");

            var recent = wf.Recent(10);
            Assert.Equal(2, recent.Count);
            Assert.Equal(2, recent[0].Id);        // 최신이 먼저
        }
    }
}
