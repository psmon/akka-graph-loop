using System.Collections.Generic;
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

    /// <summary>학습이 있는 사이클 한 개를 기록하는 헬퍼(전체 폐루프).</summary>
    private static void RecordLearningCycle(PdsaWorkflow wf, string plan, string expected, string actual, string studyLlm)
    {
        var cid = wf.StartCycle();
        wf.RecordPhase(cid, PdsaWorkflow.PlanKind, plan, "가설 서술",
            new Dictionary<string, string> { ["expected"] = expected });
        wf.RecordPhase(cid, PdsaWorkflow.StudyKind, "결과 관찰", studyLlm,
            new Dictionary<string, string> { ["verdict"] = "partial", ["actual"] = actual });
    }

    [Fact]
    public void RecentLearnings_returns_full_untruncated_text_and_skips_learningless_cycles()
    {
        var db = TempDb();
        var longActual = new string('가', 300); // 절삭되면 안 됨(status 는 70/90 자 절삭)

        using var wf = new PdsaWorkflow(db, "proj-rl");
        RecordLearningCycle(wf, "캐시 계획", "p95 200ms 이하", longActual, "학습: 캐시 히트율이 관건");
        wf.RecordPhase(wf.StartCycle(), PdsaWorkflow.PlanKind, "계획만 있는 사이클", ""); // 학습 없음 → 제외

        var learnings = wf.RecentLearnings(5);

        Assert.Single(learnings);                          // 학습 없는 사이클은 건너뜀
        Assert.Equal("p95 200ms 이하", learnings[0].Expected);
        Assert.Equal(longActual, learnings[0].Actual);     // 미절삭 전체 보존
        Assert.Equal("partial", learnings[0].Verdict);
        Assert.Contains("캐시 히트율", learnings[0].Study);
    }

    [Fact]
    public void RecentLearnings_keyword_filters_and_limit_caps()
    {
        var db = TempDb();
        using var wf = new PdsaWorkflow(db, "proj-rl2");
        RecordLearningCycle(wf, "레디스 캐시 도입", "캐시 적중률 향상", "적중률 40→80", "학습: 캐시 워밍업 필요");
        RecordLearningCycle(wf, "인덱스 추가", "쿼리 지연 감소", "320ms→90ms", "학습: 복합 인덱스 효과");

        var cacheOnly = wf.RecentLearnings(5, "캐시");
        Assert.Single(cacheOnly);
        Assert.Contains("캐시", cacheOnly[0].Expected + cacheOnly[0].Study);

        Assert.Equal(2, wf.RecentLearnings(5).Count);      // 키워드 없으면 둘 다
        Assert.Single(wf.RecentLearnings(1));              // limit 상한
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
