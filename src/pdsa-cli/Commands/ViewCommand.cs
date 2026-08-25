using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Viewer;

namespace PdsaCli.Commands;

/// <summary>현재 프로젝트의 누적 그래프 메모리를 로컬 포트 뷰어로 구동한다.</summary>
public sealed class ViewCommand : ICliCommand
{
    public string Name => "view";
    public string Summary => "누적 그래프 메모리 뷰어 실행(로컬 포트)";
    public string Usage => "pdsa view [--port 5099] [--project <이름>] [--no-open]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        var port = ArgUtil.Int(args, "--port", 5099);
        var project = ArgUtil.Option(args, "--project") ?? PdsaProjectPaths.CurrentProjectName();
        var dbPath = PdsaProjectPaths.GraphDbFor(project);
        var openBrowser = !ArgUtil.Flag(args, "--no-open");

        Console.WriteLine($"프로젝트 [{project}] 그래프: {dbPath}");
        return await ViewerLauncher.LaunchAsync(port, dbPath, openBrowser, ct);
    }
}
