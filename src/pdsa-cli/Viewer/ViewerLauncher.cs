using System.Diagnostics;

namespace PdsaCli.Viewer;

/// <summary>
/// 그래프(DB) 뷰어를 구동하는 장치. 배포된 뷰어 실행 파일이 옆에 있으면 그것을,
/// 없으면(개발 트리) <c>dotnet run</c> 으로 뷰어 프로젝트를 실행한다.
/// </summary>
public static class ViewerLauncher
{
    public static async Task<int> LaunchAsync(int port, string? dbPath, string? project, bool openBrowser, CancellationToken ct)
    {
        var url = $"http://localhost:{port}";
        var psi = BuildStartInfo(port, dbPath, project);
        if (psi is null)
        {
            Console.Error.WriteLine("뷰어를 찾지 못했습니다(배포된 AkkaGraphLoop.Viewer 실행 파일 또는 개발 트리의 뷰어 프로젝트).");
            return 1;
        }

        psi.UseShellExecute = false;
        Console.WriteLine($"■ 그래프 뷰어 실행 → {url}   (종료: Ctrl+C)");
        using var proc = Process.Start(psi);
        if (proc is null) { Console.Error.WriteLine("뷰어 프로세스를 시작하지 못했습니다."); return 1; }

        if (openBrowser)
        {
            await Task.Delay(1500, ct);
            TryOpenBrowser(url);
        }

        try { await proc.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { TryKill(proc); throw; }
        return proc.ExitCode;
    }

    private static ProcessStartInfo? BuildStartInfo(int port, string? dbPath, string? project)
    {
        // 1) 배포 시나리오: 실행 파일 옆의 뷰어 실행 파일
        var exeName = OperatingSystem.IsWindows() ? "AkkaGraphLoop.Viewer.exe" : "AkkaGraphLoop.Viewer";
        var sibling = Path.Combine(AppContext.BaseDirectory, exeName);
        if (File.Exists(sibling))
        {
            var psi = new ProcessStartInfo(sibling) { ArgumentList = { "--port", port.ToString() } };
            AddDbAndProject(psi, dbPath, project);
            return psi;
        }

        // 2) 개발 트리: 상위로 올라가며 뷰어 프로젝트를 찾아 dotnet run
        var csproj = FindUp("src/AkkaGraphLoop.Viewer/AkkaGraphLoop.Viewer.csproj");
        if (csproj is not null)
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "run", "--project", csproj, "--", "--port", port.ToString() },
            };
            AddDbAndProject(psi, dbPath, project);
            return psi;
        }

        return null;
    }

    private static void AddDbAndProject(ProcessStartInfo psi, string? dbPath, string? project)
    {
        if (dbPath is not null) { psi.ArgumentList.Add("--db"); psi.ArgumentList.Add(dbPath); }
        // 프로젝트명을 함께 넘겨 뷰어 헤더/선택기가 현재 프로젝트를 정확히 표시하도록 한다.
        if (project is not null) { psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(project); }
    }

    private static string? FindUp(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static void TryOpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* 헤드리스 환경 등에서는 무시 */ }
    }

    private static void TryKill(Process proc)
    {
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
    }
}
