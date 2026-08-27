using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Viewer;

/// <summary>
/// <c>pdsa view</c> 의 인프로세스 그래프 뷰어. 별도 실행 파일(AkkaGraphLoop.Viewer)에
/// 의존하지 않고 CLI 자체가 <see cref="HttpListener"/>(BCL, AOT-안전)로 localhost 에
/// 웹 서버를 띄운다. 라우트는 독립 뷰어와 동일: <c>/</c>, <c>/api/projects</c>, <c>/api/graph</c>.
/// JSON 은 source-generated 직렬화(<see cref="ViewerJson"/>)로 AOT 에서도 리플렉션 없이 동작한다.
/// </summary>
public static class ViewerServer
{
    public static async Task<int> RunAsync(int port, string startupDbPath, string? startupProject, bool openBrowser, CancellationToken ct)
    {
        var url = $"http://localhost:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"포트 {port} 에서 뷰어를 시작하지 못했습니다: {ex.Message}");
            Console.Error.WriteLine($"다른 포트로 시도하세요: pdsa view --port {port + 1}");
            return 1;
        }

        Console.WriteLine($"■ PDSA 그래프 뷰어 실행 중 → {url.TrimEnd('/')}   (종료: Ctrl+C)");
        Console.WriteLine($"   프로젝트: {startupProject ?? "(데모)"}");
        Console.WriteLine($"   DB: {startupDbPath}");

        // Ctrl+C(취소) 시 GetContextAsync 를 깨우기 위해 listener 를 정지.
        using var reg = ct.Register(() => { try { listener.Stop(); } catch { } });

        if (openBrowser)
            _ = Task.Run(async () => { try { await Task.Delay(800, ct); TryOpenBrowser(url.TrimEnd('/')); } catch { } }, ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch (Exception) when (ct.IsCancellationRequested) { break; } // Stop() 로 인한 정상 종료
                catch (HttpListenerException) { break; }

                _ = Task.Run(() => HandleRequest(context, startupDbPath, startupProject));
            }
        }
        finally
        {
            try { listener.Close(); } catch { }
        }
        return 0;
    }

    private static void HandleRequest(HttpListenerContext context, string startupDbPath, string? startupProject)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            switch (path)
            {
                case "/":
                    WriteHtml(context, PdsaViewerHtml.Page);
                    break;
                case "/api/projects":
                    WriteJson(context, JsonSerializer.Serialize(
                        new ProjectsDto(startupProject, PdsaProjectPaths.ListProjects().ToList()),
                        ViewerJson.Default.ProjectsDto));
                    break;
                case "/api/graph":
                    WriteJson(context, BuildGraphJson(context, startupDbPath, startupProject));
                    break;
                default:
                    context.Response.StatusCode = 404;
                    break;
            }
        }
        catch
        {
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.OutputStream.Close(); } catch { }
        }
    }

    /// <summary>
    /// 그래프 데이터(JSON): 매 요청마다 DB 를 열어 최신 상태를 읽고 닫는다(락 보유 안 함).
    /// <c>?project=&lt;이름&gt;</c> 가 있으면 그 프로젝트 DB 로 전환해 읽는다(재시작 불필요).
    /// </summary>
    private static string BuildGraphJson(HttpListenerContext context, string startupDbPath, string? startupProject)
    {
        var requested = context.Request.QueryString["project"];
        var hasReq = !string.IsNullOrWhiteSpace(requested);
        var project = hasReq ? requested : startupProject;
        var dbPath = hasReq ? PdsaProjectPaths.GraphDbFor(requested!) : startupDbPath;

        GraphDto dto;
        if (!PdsaPaths.Exists(dbPath))
        {
            dto = new GraphDto(project, dbPath, null, null, null,
                $"그래프 DB 가 없습니다: {dbPath}. 먼저 `pdsa plan ...` 로 데이터를 생성하세요.");
        }
        else
        {
            try
            {
                using var wf = new PdsaWorkflowReader(dbPath);
                if (wf.HasWorkflowSchema())
                {
                    var m = wf.Read();
                    var verdicts = m.Nodes
                        .Where(n => n.Kind == "Phase" && n.Props.TryGetValue("kind", out var k) && k == "study"
                                    && n.Props.ContainsKey("verdict"))
                        .Select(n => n.Props["verdict"]).ToList();
                    var hit = new HitRateDto(verdicts.Count(v => v == "met"), verdicts.Count);
                    dto = new GraphDto(project, dbPath, hit, m.Nodes, m.Edges, null);
                }
                else
                {
                    using var reader = new KuzuPdsaReader(dbPath);
                    var demo = reader.Read();
                    dto = new GraphDto(project, dbPath, null, demo.Nodes, demo.Edges, null);
                }
            }
            catch (Exception ex)
            {
                dto = new GraphDto(project, dbPath, null, null, null, ex.Message);
            }
        }
        return JsonSerializer.Serialize(dto, ViewerJson.Default.GraphDto);
    }

    private static void WriteHtml(HttpListenerContext context, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteJson(HttpListenerContext context, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void TryOpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* 헤드리스 환경 등에서는 무시 */ }
    }
}

// 뷰어 응답 DTO. JS 는 camelCase 필드(id/label/kind/props, from/to/type, hitRate)를 기대하므로
// 소스젠 컨텍스트에 CamelCase 정책을 지정한다. GraphNode/GraphEdge(Core)는 그대로 재사용.
internal sealed record ProjectsDto(string? current, IReadOnlyList<string> projects);
internal sealed record HitRateDto(int met, int total);
internal sealed record GraphDto(
    string? project, string db, HitRateDto? hitRate,
    IReadOnlyList<GraphNode>? nodes, IReadOnlyList<GraphEdge>? edges, string? error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectsDto))]
[JsonSerializable(typeof(GraphDto))]
internal partial class ViewerJson : JsonSerializerContext;
