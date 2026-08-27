using AkkaGraphLoop.Core.Pdsa;

// 인자: --db <경로> | --project <이름> (기본: 데모 DB), --port <번호> (기본: 5099)
var startupProject = GetArg(args, "--project");
var startupDbPath = GetArg(args, "--db")
             ?? (startupProject is not null ? PdsaProjectPaths.GraphDbFor(startupProject) : PdsaPaths.DefaultDbPath);
var port = int.TryParse(GetArg(args, "--port"), out var p) ? p : 5099;

// 시작 프로젝트명이 없으면(=--db 로만 구동) DB 경로의 상위 폴더명에서 유추한다.
startupProject ??= DeriveProjectName(startupDbPath);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

// 그래프 페이지
app.MapGet("/", () => Results.Content(PdsaViewerHtml.Page, "text/html; charset=utf-8"));

// 프로젝트 목록 + 현재 선택(헤더 드롭다운용).
app.MapGet("/api/projects", () => Results.Json(new
{
    current = startupProject,
    projects = PdsaProjectPaths.ListProjects(),
}));

// 그래프 데이터(JSON): 매 요청마다 DB를 열어 최신 상태를 읽고 닫는다(락 보유 안 함).
// ?project=<이름> 이 있으면 그 프로젝트 DB로 전환해 읽는다(재시작 불필요). 없으면 시작 시 DB.
app.MapGet("/api/graph", (HttpContext ctx) =>
{
    var requested = ctx.Request.Query["project"].ToString();
    var project = string.IsNullOrWhiteSpace(requested) ? startupProject : requested;
    var dbPath = string.IsNullOrWhiteSpace(requested) ? startupDbPath : PdsaProjectPaths.GraphDbFor(requested);

    if (!PdsaPaths.Exists(dbPath))
        return Results.Json(new { project, db = dbPath, error = $"그래프 DB 가 없습니다: {dbPath}. 먼저 `pdsa plan ...`(워크플로) 또는 `-- pdsa`(데모) 로 데이터를 생성하세요." });
    try
    {
        // 워크플로 스키마(Project/Cycle/Phase)면 그것을, 아니면 데모 스키마(Run/Cycle)를 읽는다.
        using (var wf = new PdsaWorkflowReader(dbPath))
            if (wf.HasWorkflowSchema())
            {
                var m = wf.Read();
                // 기대 충족률(재현율): verdict 있는 Study 노드 중 met 비율.
                var verdicts = m.Nodes
                    .Where(n => n.Kind == "Phase" && n.Props.TryGetValue("kind", out var k) && k == "study"
                                && n.Props.ContainsKey("verdict"))
                    .Select(n => n.Props["verdict"]).ToList();
                var hit = new { met = verdicts.Count(v => v == "met"), total = verdicts.Count };
                return Results.Json(new { project, db = dbPath, hitRate = hit, nodes = m.Nodes, edges = m.Edges });
            }

        using var reader = new KuzuPdsaReader(dbPath);
        var demo = reader.Read();
        return Results.Json(new { project, db = dbPath, nodes = demo.Nodes, edges = demo.Edges });
    }
    catch (Exception ex)
    {
        return Results.Json(new { project, db = dbPath, error = ex.Message });
    }
});

Console.WriteLine($"■ PDSA 그래프 뷰어 실행 중 → http://localhost:{port}");
Console.WriteLine($"   프로젝트: {startupProject ?? "(데모)"}");
Console.WriteLine($"   DB: {startupDbPath}");
Console.WriteLine($"   (데이터가 없으면 먼저: dotnet run --project src/AkkaGraphLoop.Samples -- pdsa)");
app.Run();

static string? GetArg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// {AppRoot}/{project}/graph.kuzu 규약에서 상위 폴더명이 프로젝트명이다.
static string? DeriveProjectName(string dbPath)
{
    try { return Path.GetFileName(Path.GetDirectoryName(dbPath)); }
    catch { return null; }
}
