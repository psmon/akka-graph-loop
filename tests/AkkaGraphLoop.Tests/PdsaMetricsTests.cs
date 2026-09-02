using System.Collections.Generic;
using AkkaGraphLoop.Core.Kuzu;
using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 단계별 계측(latencyMs/attempts/model/토큰) 저장·되읽기와, <b>구 스키마 DB 하위호환</b> 검증.
/// 계측 컬럼은 기존 <c>MigratePhaseColumns</c> ALTER 패턴으로 추가되므로, 이미 쌓인 사용자 DB 가
/// 새 바이너리에서 그대로 열리고 데이터가 보존되는지가 가장 중요한 회귀 지점이다.
/// </summary>
public class PdsaMetricsTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_metrics_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    [Fact]
    public void Metrics_are_stored_and_read_back()
    {
        using var wf = new PdsaWorkflow(TempDb(), "proj");

        var cid = wf.StartCycleWithPlan(0, "계획", "서술", new Dictionary<string, string>
        {
            ["expected"] = "p95 200ms 이하",
            [PdsaWorkflow.LatencyMsKey] = "2346",
            [PdsaWorkflow.AttemptsKey] = "2",
            [PdsaWorkflow.ModelKey] = "gpt-5.6-terra",
            [PdsaWorkflow.PromptTokensKey] = "1200",
            [PdsaWorkflow.CompletionTokensKey] = "450",
        });

        var phase = wf.GetPhase(cid, PdsaWorkflow.PlanKind)!;
        Assert.Equal("2346", phase.LatencyMs);
        Assert.Equal("2", phase.Attempts);
        Assert.Equal("gpt-5.6-terra", phase.Model);
        Assert.Equal("1200", phase.PromptTokens);
        Assert.Equal("450", phase.CompletionTokens);
        Assert.Equal("p95 200ms 이하", phase.Expected);      // 기존 폐루프 필드도 그대로
        Assert.True(phase.HasMetrics);
    }

    [Fact]
    public void Phase_without_metrics_reports_none()
    {
        using var wf = new PdsaWorkflow(TempDb(), "proj");
        var cid = wf.StartCycleWithPlan(0, "계획", "서술");

        var phase = wf.GetPhase(cid, PdsaWorkflow.PlanKind)!;
        Assert.False(phase.HasMetrics);
        Assert.Equal("", phase.MetricsLine());               // 없는 근거를 지어내지 않는다
    }

    [Fact]
    public void MetricsLine_summarizes_only_the_values_present()
    {
        using var wf = new PdsaWorkflow(TempDb(), "proj");
        var cid = wf.StartCycleWithPlan(0, "계획", "서술", new Dictionary<string, string>
        {
            [PdsaWorkflow.LatencyMsKey] = "1500",
            [PdsaWorkflow.AttemptsKey] = "1",
            [PdsaWorkflow.ModelKey] = "m1",
        });

        var line = wf.GetPhase(cid, PdsaWorkflow.PlanKind)!.MetricsLine();
        Assert.Contains("1500ms", line);
        Assert.Contains("m1", line);
        Assert.DoesNotContain("시도", line);                  // 1회는 잡음이므로 생략
        Assert.DoesNotContain("토큰", line);
    }

    /// <summary>
    /// 계측 컬럼이 없던 시절의 DB 를 그대로 만들어, 새 코드가 열었을 때
    /// (1) 예외 없이 열리고 (2) 컬럼이 추가되고 (3) 기존 데이터가 보존되는지 확인한다.
    /// </summary>
    [Fact]
    public void Legacy_schema_database_migrates_and_preserves_data()
    {
        var db = TempDb();
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);

        // ── 구 스키마(폐루프 컬럼까지만, 계측 컬럼 없음)로 DB 를 만든다 ──
        using (var g = new KuzuGraph(db))
        {
            g.Execute("CREATE NODE TABLE Project(id STRING, name STRING, created STRING, PRIMARY KEY(id))");
            g.Execute("CREATE NODE TABLE Cycle(id INT64, started STRING, status STRING, PRIMARY KEY(id))");
            g.Execute("CREATE NODE TABLE Phase(id STRING, cycle INT64, kind STRING, input STRING, llm STRING, " +
                      "created STRING, expected STRING DEFAULT '', verdict STRING DEFAULT '', " +
                      "actual STRING DEFAULT '', reinforce STRING DEFAULT '', PRIMARY KEY(id))");
            g.Execute("CREATE REL TABLE HAS_CYCLE(FROM Project TO Cycle)");
            g.Execute("CREATE REL TABLE HAS_PHASE(FROM Cycle TO Phase)");
            g.Execute("CREATE REL TABLE NEXT(FROM Cycle TO Cycle)");
            g.Execute("CREATE REL TABLE REINFORCES(FROM Cycle TO Cycle)");

            g.Execute("CREATE (:Project {id: 'proj', name: 'proj', created: '2026-01-01 00:00:00'})");
            g.Execute("CREATE (:Cycle {id: 1, started: '2026-01-01 00:00:00', status: 'planned'})");
            g.Execute("CREATE (:Phase {id: '1-plan', cycle: 1, kind: 'plan', input: '옛 계획', llm: '옛 코칭', " +
                      "created: '2026-01-01 00:00:00', expected: '옛 기대', verdict: '', actual: '', reinforce: ''})");
            g.Execute("MATCH (c:Cycle {id: 1}), (p:Phase {id: '1-plan'}) CREATE (c)-[:HAS_PHASE]->(p)");
        }

        // ── 새 코드로 연다: 마이그레이션이 계측 컬럼을 채워야 한다 ──
        using (var wf = new PdsaWorkflow(db, "proj"))
        {
            var phase = wf.GetPhase(1, PdsaWorkflow.PlanKind);

            Assert.NotNull(phase);
            Assert.Equal("옛 계획", phase!.Input);            // 기존 데이터 보존
            Assert.Equal("옛 기대", phase.Expected);
            Assert.Equal("", phase.LatencyMs);               // 새 컬럼은 기본값
            Assert.False(phase.HasMetrics);

            // 회차 조회(show/history 의 근간)도 구 스키마 DB 에서 그대로 동작해야 한다.
            Assert.Equal("옛 계획", wf.Cycle(1)!.Phases.Single().Input);
            Assert.Equal(new long[] { 1 }, wf.Range().Select(c => c.Id).ToArray());
            Assert.Equal((0, Array.Empty<long>()),
                (wf.ReinforceLinks(1).Reinforces, wf.ReinforceLinks(1).ReinforcedBy.ToArray()));

            // 마이그레이션 후 새 사이클에는 계측이 정상 기록된다.
            var cid = wf.StartCycleWithPlan(0, "새 계획", "서술",
                new Dictionary<string, string> { [PdsaWorkflow.LatencyMsKey] = "111" });
            Assert.Equal("111", wf.GetPhase(cid, PdsaWorkflow.PlanKind)!.LatencyMs);
        }
    }
}
