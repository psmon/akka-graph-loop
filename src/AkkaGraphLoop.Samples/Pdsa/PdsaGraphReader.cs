using AkkaGraphLoop.Samples.Kuzu;

namespace AkkaGraphLoop.Samples.Pdsa;

/// <summary>그래프 노드(뷰어용 직렬화 모델).</summary>
public sealed record GraphNode(string Id, string Label, string Kind, Dictionary<string, string> Props);

/// <summary>그래프 엣지(뷰어용 직렬화 모델).</summary>
public sealed record GraphEdge(string From, string To, string Type);

/// <summary>PDSA 그래프 전체 모델.</summary>
public sealed record PdsaGraphModel(List<GraphNode> Nodes, List<GraphEdge> Edges);

/// <summary>
/// 기존 Kùzu DB(스키마 생성 없이)를 열어 PDSA 그래프를 조회하는 읽기 전용 리더.
/// <c>(:Run)-[:HAS_CYCLE]->(:Cycle)</c> 와 <c>(:Cycle)-[:NEXT]->(:Cycle)</c> 를 노드/엣지 모델로 만든다.
/// </summary>
public sealed class KuzuPdsaReader : IDisposable
{
    private readonly KuzuGraph _graph;

    public KuzuPdsaReader(string databasePath)
    {
        if (!PdsaPaths.Exists(databasePath))
            throw new FileNotFoundException($"Kùzu DB 경로가 없습니다: {databasePath}. 먼저 `-- pdsa` 로 데이터를 생성하세요.");
        _graph = new KuzuGraph(databasePath, readOnly: true); // 뷰어는 DB 를 수정하지 않는다
    }

    public PdsaGraphModel Read()
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        foreach (var r in _graph.Query("MATCH (r:Run) RETURN r.id, r.start, r.target", 3))
            nodes.Add(new GraphNode(
                Id: $"Run:{r[0]}",
                Label: "Run",
                Kind: "Run",
                Props: new() { ["start"] = r[1], ["target"] = r[2] }));

        foreach (var c in _graph.Query("MATCH (c:Cycle) RETURN c.id, c.quality, c.converged ORDER BY c.id", 3))
            nodes.Add(new GraphNode(
                Id: $"Cycle:{c[0]}",
                Label: $"Cycle #{c[0]}",
                Kind: "Cycle",
                Props: new() { ["quality"] = c[1], ["converged"] = c[2] }));

        foreach (var e in _graph.Query("MATCH (r:Run)-[:HAS_CYCLE]->(c:Cycle) RETURN r.id, c.id", 2))
            edges.Add(new GraphEdge($"Run:{e[0]}", $"Cycle:{e[1]}", "HAS_CYCLE"));

        foreach (var e in _graph.Query("MATCH (a:Cycle)-[:NEXT]->(b:Cycle) RETURN a.id, b.id", 2))
            edges.Add(new GraphEdge($"Cycle:{e[0]}", $"Cycle:{e[1]}", "NEXT"));

        return new PdsaGraphModel(nodes, edges);
    }

    public void Dispose() => _graph.Dispose();
}
