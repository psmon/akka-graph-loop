using AkkaGraphLoop.Core.Pdsa;
using AkkaGraphLoop.Viewer;

// 인자: --db <경로> | --project <이름> (기본: 데모 DB), --port <번호> (기본: 5099)
var project = GetArg(args, "--project");
var dbPath = GetArg(args, "--db")
             ?? (project is not null ? PdsaProjectPaths.GraphDbFor(project) : PdsaPaths.DefaultDbPath);
var port = int.TryParse(GetArg(args, "--port"), out var p) ? p : 5099;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

// 그래프 페이지
app.MapGet("/", () => Results.Content(ViewerHtml.Page, "text/html; charset=utf-8"));

// 그래프 데이터(JSON): 매 요청마다 DB를 열어 최신 상태를 읽고 닫는다(락 보유 안 함).
app.MapGet("/api/graph", () =>
{
    if (!PdsaPaths.Exists(dbPath))
        return Results.Json(new { error = $"그래프 DB 가 없습니다: {dbPath}. 먼저 `pdsa plan ...`(워크플로) 또는 `-- pdsa`(데모) 로 데이터를 생성하세요." });
    try
    {
        // 워크플로 스키마(Project/Cycle/Phase)면 그것을, 아니면 데모 스키마(Run/Cycle)를 읽는다.
        using (var wf = new PdsaWorkflowReader(dbPath))
            if (wf.HasWorkflowSchema())
                return Results.Json(wf.Read());

        using var reader = new KuzuPdsaReader(dbPath);
        return Results.Json(reader.Read());
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message });
    }
});

Console.WriteLine($"■ PDSA 그래프 뷰어 실행 중 → http://localhost:{port}");
Console.WriteLine($"   DB: {dbPath}");
Console.WriteLine($"   (데이터가 없으면 먼저: dotnet run --project src/AkkaGraphLoop.Samples -- pdsa)");
app.Run();

static string? GetArg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
