using System.Collections.Generic;
using AkkaGraphLoop.Core.Kuzu;
using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 사이클 생성의 <b>원자성</b> 검증. 실측된 결함(LLM 실패 1회 = 고아 사이클 1개, 재현율 100%)의
/// 회귀 방어다: 고아 사이클은 <see cref="PdsaWorkflow.CurrentCycle"/> 에 잡혀 다음 Do 를 흡수하고,
/// Plan 없는 Do → Expected 공백 → Study 판정 불가 → 기대 충족률·되읽기 품질 저하로 연쇄한다.
/// </summary>
public class PdsaAtomicityTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_atomic_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    private static long CycleCount(string db)
    {
        using var g = new KuzuGraph(db);
        return long.Parse(g.Query("MATCH (c:Cycle) RETURN count(c)", 1)[0][0]);
    }

    private static long PhaseCount(string db)
    {
        using var g = new KuzuGraph(db);
        return long.Parse(g.Query("MATCH (p:Phase) RETURN count(p)", 1)[0][0]);
    }

    private static long NextEdgeCount(string db)
    {
        using var g = new KuzuGraph(db);
        return long.Parse(g.Query("MATCH (:Cycle)-[r:NEXT]->(:Cycle) RETURN count(r)", 1)[0][0]);
    }

    [Fact]
    public void Successful_plan_creates_exactly_one_cycle_with_its_plan_phase()
    {
        var db = TempDb();
        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            var cid = wf.StartCycleWithPlan(0, "캐시 도입", "가설 서술",
                new Dictionary<string, string> { ["expected"] = "p95 200ms 이하" });

            Assert.Equal(1, cid);
            Assert.Equal("planned", wf.CurrentCycle()!.Value.Status);
            Assert.Equal("p95 200ms 이하", wf.GetPhase(cid, PdsaWorkflow.PlanKind)!.Expected);
            Assert.False(wf.IsOrphanCycle(cid));
        }

        Assert.Equal(1, CycleCount(db));
        Assert.Equal(1, PhaseCount(db));
    }

    /// <summary>
    /// Plan 기록이 실패하면 사이클도 남지 않아야 한다. 화이트리스트 밖 키가 아니라 실제로 실패하는
    /// 쓰기를 유도하기 위해, 같은 사이클 id 로 두 번 기록해 중복 PK 충돌을 만든다.
    /// </summary>
    [Fact]
    public void Failed_plan_write_leaves_no_cycle_no_phase_no_edge()
    {
        var db = TempDb();
        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            // 첫 사이클을 정상 생성한 뒤 삭제해, 다음 StartCycleWithPlan 이 같은 Phase id 를 만들도록 만든다.
            var cid = wf.StartCycleWithPlan(0, "첫 계획", "서술");
            Assert.Equal(1, cid);
        }

        // Phase id 는 "{cycleId}-{kind}" 로 결정적이다. Cycle 만 지우면 id 가 1 로 되돌아가
        // 다음 Plan 기록이 기존 Phase 와 PK 충돌한다 → 트랜잭션 롤백 경로.
        using (var g = new KuzuGraph(db)) g.Execute("MATCH (c:Cycle) DETACH DELETE c");

        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            Assert.ThrowsAny<Exception>(() => wf.StartCycleWithPlan(0, "둘째 계획", "서술"));
            Assert.Null(wf.CurrentCycle());          // 고아 사이클이 남지 않았다
        }

        Assert.Equal(0, CycleCount(db));
        Assert.Equal(0, NextEdgeCount(db));
    }

    /// <summary>
    /// 실패를 20회 주입해도 고아가 0건이어야 한다(조사 기준선: 20/20 생성).
    /// </summary>
    [Fact]
    public void Twenty_injected_failures_create_zero_orphan_cycles()
    {
        var db = TempDb();
        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            wf.StartCycleWithPlan(0, "정상 계획", "서술");
        }
        using (var g = new KuzuGraph(db)) g.Execute("MATCH (c:Cycle) DETACH DELETE c");

        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            for (var i = 0; i < 20; i++)
                Assert.ThrowsAny<Exception>(() => wf.StartCycleWithPlan(0, $"실패 {i}", "서술"));
        }

        Assert.Equal(0, CycleCount(db));
    }

    /// <summary>과거 버전이 만든 고아(= Plan 없는 사이클)는 감지되어야 한다 — 커맨드 가드의 근거.</summary>
    [Fact]
    public void Legacy_orphan_cycle_is_detected()
    {
        var db = TempDb();
        using var wf = new PdsaWorkflow(db, "proj");

        var cid = wf.StartCycle();               // Plan 없이 사이클만 (구 버전이 남기던 상태)

        Assert.True(wf.IsOrphanCycle(cid));
        Assert.Null(wf.GetPhase(cid, PdsaWorkflow.PlanKind));

        wf.RecordPhase(cid, PdsaWorkflow.PlanKind, "뒤늦은 계획", "서술");
        Assert.False(wf.IsOrphanCycle(cid));
    }
}
