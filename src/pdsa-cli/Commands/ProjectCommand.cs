using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;

namespace PdsaCli.Commands;

/// <summary>
/// 프로젝트(그래프 DB) 관리. 프로젝트마다 별도 그래프 메모리가 누적되며,
/// 활성 프로젝트를 지정하면 이후 모든 명령이 그 프로젝트 DB 를 참조한다(멀티프로젝트 운영).
/// </summary>
public sealed class ProjectCommand : ICliCommand
{
    public string Name => "project";
    public string Summary => "프로젝트 지정/목록(멀티프로젝트 DB 분리)";
    public string Usage => "pdsa project set <이름> | list | show | clear";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        var sub = args.ElementAtOrDefault(0) ?? "show";
        var value = string.Join(' ', args.Skip(1)).Trim();

        switch (sub)
        {
            case "set":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa project set <이름>");
                PdsaProjectPaths.SetActiveProject(value);
                var resolved = PdsaProjectPaths.ResolveProject(null);
                using (new PdsaWorkflow(PdsaProjectPaths.GraphDbFor(resolved), resolved)) { } // DB 초기화(스키마 생성)
                Console.WriteLine($"활성 프로젝트 = {resolved}");
                Console.WriteLine($"  DB: {PdsaProjectPaths.GraphDbFor(resolved)}");
                break;

            case "clear":
                PdsaProjectPaths.ClearActiveProject();
                Console.WriteLine($"활성 프로젝트 지정 해제. 이제 현재 디렉터리 이름을 사용합니다 → {PdsaProjectPaths.CurrentProjectName()}");
                break;

            case "list":
                ListProjects();
                break;

            case "show":
                Show();
                break;

            default:
                return Fail($"알 수 없는 하위 명령: {sub}\n{Usage}");
        }
        return Task.FromResult(0);
    }

    private static void Show()
    {
        var active = PdsaProjectPaths.ActiveProject();
        var effective = PdsaProjectPaths.ResolveProject(null);
        Console.WriteLine($"활성 프로젝트 : {(active ?? "(미지정 → 현재 디렉터리)")}");
        Console.WriteLine($"실제 사용    : {effective}");
        Console.WriteLine($"DB           : {PdsaProjectPaths.GraphDbFor(effective)}");
        Console.WriteLine("\n다른 프로젝트로 전환: pdsa project set <이름>   ·   목록: pdsa project list");
    }

    private static void ListProjects()
    {
        var projects = PdsaProjectPaths.ListProjects();
        var active = PdsaProjectPaths.ResolveProject(null);
        if (projects.Count == 0)
        {
            Console.WriteLine("아직 프로젝트가 없습니다. `pdsa project set <이름>` 또는 `pdsa plan \"...\"` 로 시작하세요.");
            return;
        }
        Console.WriteLine($"프로젝트 {projects.Count}개:");
        foreach (var name in projects)
        {
            var mark = name.Equals(active, StringComparison.OrdinalIgnoreCase) ? "* " : "  ";
            var cycles = SafeCycleCount(PdsaProjectPaths.GraphDbFor(name));
            Console.WriteLine($"{mark}{name}  (사이클 {cycles}개)");
        }
        Console.WriteLine("\n* = 현재 활성 프로젝트");
    }

    private static int SafeCycleCount(string dbPath)
    {
        try { using var r = new PdsaWorkflowReader(dbPath); return r.CycleCount(); }
        catch { return 0; }
    }

    private static Task<int> Fail(string msg) { Console.Error.WriteLine(msg); return Task.FromResult(2); }
}
