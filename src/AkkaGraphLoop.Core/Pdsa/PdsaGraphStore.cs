using System.Globalization;
using AkkaGraphLoop.Core.Kuzu;

namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>PDSA 진행 상황을 그래프로 기록하는 저장소(스트림 진행 중 실시간 호출됨).</summary>
public interface IPdsaGraphStore
{
    /// <summary>한 사이클(회차)의 결과를 노드로 기록하고, 직전 회차와 NEXT 엣지로 연결한다.</summary>
    void RecordCycle(PdsaState state);

    /// <summary>기록된 그래프를 되읽어 사람이 읽을 수 있는 진행 로그로 반환한다.</summary>
    IReadOnlyList<string> ReadProgress();
}

/// <summary>
/// Kùzu 임베디드 그래프 DB 기반 구현. PDSA 사이클을 <b>(:Run)-[:HAS_CYCLE]->(:Cycle)</b> 및
/// <b>(:Cycle)-[:NEXT]->(:Cycle)</b> 그래프로 실시간 기록하고, Cypher 로 되읽는다.
/// </summary>
public sealed class KuzuPdsaStore : IPdsaGraphStore, IDisposable
{
    private readonly KuzuGraph _graph;

    public string DatabasePath { get; }

    public KuzuPdsaStore(string databasePath, double start, double target)
    {
        DatabasePath = databasePath;
        _graph = new KuzuGraph(databasePath);

        // 스키마: 실행(Run) / 사이클(Cycle) 노드, HAS_CYCLE / NEXT 관계
        _graph.Execute("CREATE NODE TABLE Run(id INT64, start DOUBLE, target DOUBLE, PRIMARY KEY(id))");
        _graph.Execute("CREATE NODE TABLE Cycle(id INT64, quality DOUBLE, converged BOOLEAN, PRIMARY KEY(id))");
        _graph.Execute("CREATE REL TABLE HAS_CYCLE(FROM Run TO Cycle)");
        _graph.Execute("CREATE REL TABLE NEXT(FROM Cycle TO Cycle)");
        _graph.Execute($"CREATE (:Run {{id: 1, start: {F(start)}, target: {F(target)}}})");
    }

    public void RecordCycle(PdsaState s)
    {
        _graph.Execute(
            $"CREATE (:Cycle {{id: {s.Iteration}, quality: {F(s.Quality)}, converged: {(s.Converged ? "true" : "false")}}})");
        _graph.Execute(
            $"MATCH (r:Run {{id: 1}}), (c:Cycle {{id: {s.Iteration}}}) CREATE (r)-[:HAS_CYCLE]->(c)");
        if (s.Iteration > 1)
            _graph.Execute(
                $"MATCH (a:Cycle {{id: {s.Iteration - 1}}}), (b:Cycle {{id: {s.Iteration}}}) CREATE (a)-[:NEXT]->(b)");
    }

    public IReadOnlyList<string> ReadProgress()
    {
        var rows = _graph.Query(
            "MATCH (c:Cycle) RETURN c.id, c.quality, c.converged ORDER BY c.id", columns: 3);
        return rows
            .Select(r => $"(:Cycle #{r[0]})  품질={r[1]}  수렴={r[2]}")
            .ToList();
    }

    /// <summary>NEXT 엣지로 이어진 회차 수(= 사이클 진행 경로의 길이)를 센다.</summary>
    public int CountNextEdges()
    {
        var rows = _graph.Query("MATCH (:Cycle)-[e:NEXT]->(:Cycle) RETURN count(e)", columns: 1);
        return rows.Count > 0 && int.TryParse(rows[0][0], out var n) ? n : 0;
    }

    private static string F(double d) => d.ToString("0.0###", CultureInfo.InvariantCulture);

    public void Dispose() => _graph.Dispose();
}
