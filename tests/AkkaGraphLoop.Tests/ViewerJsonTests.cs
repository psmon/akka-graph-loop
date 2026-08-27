using System.Text.Json;
using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Viewer;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// <c>pdsa view</c> 인프로세스 서버의 JSON 계약 검증. 뷰어 HTML 의 vanilla JS 가
/// camelCase 필드(id/label/kind/props, from/to/type, hitRate, current/projects)를 읽으므로,
/// source-generated 직렬화(<see cref="ViewerJson"/>)가 그 계약을 정확히 지켜야 한다.
/// </summary>
public class ViewerJsonTests
{
    [Fact]
    public void Graph_dto_serializes_camelCase_contract()
    {
        var dto = new GraphDto(
            project: "demo", db: "C:/x/graph.kuzu",
            hitRate: new HitRateDto(2, 3),
            nodes: new[] { new GraphNode("c1", "Cycle #1", "Cycle", new() { ["status"] = "acted" }) },
            edges: new[] { new GraphEdge("p1", "c1", "HAS_CYCLE") },
            error: null);

        var json = JsonSerializer.Serialize(dto, ViewerJson.Default.GraphDto);

        // 노드/엣지 필드는 JS 가 기대하는 이름 그대로여야 한다.
        Assert.Contains("\"nodes\":", json);
        Assert.Contains("\"edges\":", json);
        Assert.Contains("\"id\":\"c1\"", json);
        Assert.Contains("\"label\":\"Cycle #1\"", json);
        Assert.Contains("\"kind\":\"Cycle\"", json);
        Assert.Contains("\"props\":{\"status\":\"acted\"}", json);
        Assert.Contains("\"from\":\"p1\"", json);
        Assert.Contains("\"to\":\"c1\"", json);
        Assert.Contains("\"type\":\"HAS_CYCLE\"", json);
        Assert.Contains("\"hitRate\":{\"met\":2,\"total\":3}", json);
        // null error 는 생략(WhenWritingNull).
        Assert.DoesNotContain("\"error\"", json);
        // PascalCase 로 새지 않아야 한다.
        Assert.DoesNotContain("\"Nodes\"", json);
        Assert.DoesNotContain("\"Id\"", json);
    }

    [Fact]
    public void Graph_dto_includes_error_when_present()
    {
        var dto = new GraphDto("demo", "C:/x/graph.kuzu", null, null, null, "그래프 DB 가 없습니다");
        var json = JsonSerializer.Serialize(dto, ViewerJson.Default.GraphDto);
        // 비ASCII 는 \u 로 이스케이프될 수 있으므로(브라우저가 디코드) 파싱해서 값 자체를 검증.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("그래프 DB 가 없습니다", doc.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("\"nodes\"", json);   // null 은 생략
    }

    [Fact]
    public void Projects_dto_serializes_current_and_list()
    {
        var dto = new ProjectsDto("akka-graph-loop", new[] { "akka-graph-loop", "loop-demo" });
        var json = JsonSerializer.Serialize(dto, ViewerJson.Default.ProjectsDto);
        Assert.Contains("\"current\":\"akka-graph-loop\"", json);
        Assert.Contains("\"projects\":[\"akka-graph-loop\",\"loop-demo\"]", json);
    }
}
