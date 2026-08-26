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

    /// <summary>누적 사이클 수(목록 표시용).</summary>
    public int CycleCount()
    {
        try
        {
            var rows = _graph.Query("MATCH (c:Cycle) RETURN count(c)", 1);
            return rows.Count > 0 && int.TryParse(rows[0][0], out var n) ? n : 0;
        }
        catch { return 0; }
    }

    public PdsaGraphModel Read()
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        foreach (var r in _graph.Query("MATCH (p:Project) RETURN p.id, p.name", 2))
            nodes.Add(new GraphNode($"Project:{r[0]}", r[1], "Project", new() { ["id"] = r[0] }));

        foreach (var r in _graph.Query("MATCH (c:Cycle) RETURN c.id, c.status ORDER BY c.id", 2))
            nodes.Add(new GraphNode($"Cycle:{r[0]}", $"Cycle #{r[0]}", "Cycle", new() { ["status"] = r[1] }));

        // 폐루프 메타 컬럼(expected/verdict/actual/reinforce)이 있으면 8컬럼, 없으면(구 스키마) 4컬럼 폴백.
        var phaseRows = SafeQuery(
            "MATCH (ph:Phase) RETURN ph.id, ph.kind, ph.input, ph.llm, ph.expected, ph.verdict, ph.actual, ph.reinforce", 8);
        var withMeta = phaseRows is not null;
        phaseRows ??= _graph.Query("MATCH (ph:Phase) RETURN ph.id, ph.kind, ph.input, ph.llm", 4);
        foreach (var r in phaseRows)
        {
            var props = new Dictionary<string, string>
            {
                ["kind"] = r[1],
                ["input"] = Trunc(r[2]),
                ["llm"] = Trunc(r[3]),
            };
            if (withMeta)
            {
                // 값이 있을 때만 노출(패널/색상용).
                if (r[4].Length > 0) props["expected"] = Trunc(r[4]);
                if (r[5].Length > 0) props["verdict"] = r[5];
                if (r[6].Length > 0) props["actual"] = Trunc(r[6]);
                if (r[7].Length > 0) props["reinforce"] = Trunc(r[7]);
            }
            nodes.Add(new GraphNode($"Phase:{r[0]}", r[1], "Phase", props));
        }

        foreach (var r in _graph.Query("MATCH (p:Project)-[:HAS_CYCLE]->(c:Cycle) RETURN p.id, c.id", 2))
            edges.Add(new GraphEdge($"Project:{r[0]}", $"Cycle:{r[1]}", "HAS_CYCLE"));

        foreach (var r in _graph.Query("MATCH (c:Cycle)-[:HAS_PHASE]->(ph:Phase) RETURN c.id, ph.id", 2))
            edges.Add(new GraphEdge($"Cycle:{r[0]}", $"Phase:{r[1]}", "HAS_PHASE"));

        foreach (var r in _graph.Query("MATCH (a:Cycle)-[:NEXT]->(b:Cycle) RETURN a.id, b.id", 2))
            edges.Add(new GraphEdge($"Cycle:{r[0]}", $"Cycle:{r[1]}", "NEXT"));

        // 보강 사이클: (보강 사이클)-[:REINFORCES]->(원 사이클). 구 스키마엔 테이블이 없을 수 있어 SafeQuery.
        foreach (var r in SafeQuery("MATCH (a:Cycle)-[:REINFORCES]->(b:Cycle) RETURN a.id, b.id", 2) ?? [])
            edges.Add(new GraphEdge($"Cycle:{r[0]}", $"Cycle:{r[1]}", "REINFORCES"));

        return new PdsaGraphModel(nodes, edges);
    }

    /// <summary>컬럼/테이블이 없어 실패할 수 있는 조회. 실패 시 null(호출부가 폴백).</summary>
    private List<string[]>? SafeQuery(string cypher, int columns)
    {
        try { return _graph.Query(cypher, columns); }
        catch { return null; }
    }

    private static string Trunc(string s, int n = 140)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");

    public void Dispose() => _graph.Dispose();
}
