using PdsaCli.Cli;
using PdsaCli.Viewer;

namespace PdsaCli.Commands;

/// <summary>그래프(DB) 뷰어를 로컬 포트로 구동한다.</summary>
public sealed class ViewCommand : ICliCommand
{
    public string Name => "view";
    public string Summary => "그래프 DB 뷰어 실행(로컬 포트)";
    public string Usage => "pdsa view [--port 5099] [--no-open]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        var port = ArgUtil.Int(args, "--port", 5099);
        var openBrowser = !ArgUtil.Flag(args, "--no-open");
        return await ViewerLauncher.LaunchAsync(port, openBrowser, ct);
    }
}
