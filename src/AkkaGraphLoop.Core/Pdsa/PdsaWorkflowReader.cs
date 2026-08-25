using AkkaGraphLoop.Core.Kuzu;

namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>
/// 워크플로 그래프(Project/Cycle/Phase)를 읽어 뷰어용 노드/엣지 모델로 만든다(읽기 전용).
/// 데모 스키마(Run/Cycle)와 구분되며, 프로젝트별 누적 메모리를 시각화한다.
/// </summary>
public sealed class PdsaWorkflowReader : IDisposable
{
    private readonly KuzuGraph _graph;

    public PdsaWorkflowReader(string databasePath)
    {
        if (!PdsaPaths.Exists(databasePath))
            throw new FileNotFoundException($"그래프 DB 가 없습니다: {databasePath}");
        _graph = new KuzuGraph(databasePath, readOnly: true);
    }

    /// <summary>이 DB 가 워크플로 스키마(Project 테이블)를 가졌는지.</summary>
    public bool HasWorkflowSchema()
    {
        try { _graph.Query("MATCH (p:Project) RETURN count(p)", 1); return true; }
        catch { return false; }
    }

    public PdsaGraphModel Read()
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        foreach (var r in _graph.Query("MATCH (p:Project) RETURN p.id, p.name", 2))
            nodes.Add(new GraphNode($"Project:{r[0]}", r[1], "Project", new() { ["id"] = r[0] }));

        foreach (var r in _graph.Query("MATCH (c:Cycle) RETURN c.id, c.status ORDER BY c.id", 2))
            nodes.Add(new GraphNode($"Cycle:{r[0]}", $"Cycle #{r[0]}", "Cycle", new() { ["status"] = r[1] }));

        foreach (var r in _graph.Query("MATCH (ph:Phase) RETURN ph.id, ph.kind, ph.input, ph.llm", 4))
            nodes.Add(new GraphNode($"Phase:{r[0]}", r[1], "Phase", new()
            {
                ["kind"] = r[1],
                ["input"] = Trunc(r[2]),
                ["llm"] = Trunc(r[3]),
            }));

        foreach (var r in _graph.Query("MATCH (p:Project)-[:HAS_CYCLE]->(c:Cycle) RETURN p.id, c.id", 2))
            edges.Add(new GraphEdge($"Project:{r[0]}", $"Cycle:{r[1]}", "HAS_CYCLE"));

        foreach (var r in _graph.Query("MATCH (c:Cycle)-[:HAS_PHASE]->(ph:Phase) RETURN c.id, ph.id", 2))
            edges.Add(new GraphEdge($"Cycle:{r[0]}", $"Phase:{r[1]}", "HAS_PHASE"));

        foreach (var r in _graph.Query("MATCH (a:Cycle)-[:NEXT]->(b:Cycle) RETURN a.id, b.id", 2))
            edges.Add(new GraphEdge($"Cycle:{r[0]}", $"Cycle:{r[1]}", "NEXT"));

        return new PdsaGraphModel(nodes, edges);
    }

    private static string Trunc(string s, int n = 140)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");

    public void Dispose() => _graph.Dispose();
}
