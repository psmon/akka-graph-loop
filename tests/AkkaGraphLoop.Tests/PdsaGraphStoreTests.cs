using AkkaGraphLoop.Samples.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// PDSA 사이클이 Akka 스트림 진행 '중' Kùzu 임베디드 그래프 DB에 실시간 기록되고,
/// 그 기록을 Cypher 로 되읽을 수 있는지 검증한다(네이티브 libkuzu 필요 — 빌드 시 자동 다운로드).
/// </summary>
public class PdsaGraphStoreTests : GraphTestBase
{
    [Fact]
    public void Pdsa_progress_is_recorded_as_graph_and_readable_via_cypher()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "pdsa_kuzu_test_" + Guid.NewGuid().ToString("N"));
        using var store = new KuzuPdsaStore(dbPath, start: 45, target: 90);

        var history = Await(PdsaLoop.Run(Materializer, start: 45, target: 90, log: _ => { }, store: store));

        var progress = store.ReadProgress();
        Assert.Equal(history.Count, progress.Count);              // 모든 회차가 Cycle 노드로 기록됨
        Assert.Equal(history.Count - 1, store.CountNextEdges());  // 연속 회차가 NEXT 로 연결됨(경로)
        Assert.Contains("수렴=True", progress[^1]);               // 마지막 노드는 수렴 상태
        Assert.All(progress.Take(progress.Count - 1), line => Assert.Contains("수렴=False", line));
    }
}
