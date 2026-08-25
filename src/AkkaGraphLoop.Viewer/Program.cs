using AkkaGraphLoop.Samples.Pdsa;
using AkkaGraphLoop.Viewer;

// 인자: --db <경로> (기본: PdsaPaths.DefaultDbPath), --port <번호> (기본: 5099)
var dbPath = GetArg(args, "--db") ?? PdsaPaths.DefaultDbPath;
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
        return Results.Json(new { error = $"Kùzu DB 가 없습니다: {dbPath}. 먼저 `dotnet run --project src/AkkaGraphLoop.Samples -- pdsa` 로 데이터를 생성하세요." });
    try
    {
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
