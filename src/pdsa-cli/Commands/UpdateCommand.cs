using System.Diagnostics;
using PdsaCli.Cli;

namespace PdsaCli.Commands;

/// <summary>
/// 최신 버전을 확인하고 npm 전역 패키지를 업데이트한다.
/// 프리체크: 다른 pdsa 프로세스가 실행 중이면(자기 자신 제외) <b>강제 종료하지 않고</b> 종료를 안내한 뒤 중단한다
/// (Windows 는 실행 중 바이너리를 교체할 수 없어 EPERM 발생). 충돌이 없으면 진행한다.
/// 자기잠금 회피: Windows 는 새 콘솔 창에서 npm 을 실행하도록 예약하고 pdsa 는 즉시 종료(파일 잠금 해제),
/// Unix 는 실행 중 교체가 가능하므로 인라인 실행한다.
/// </summary>
public sealed class UpdateCommand : ICliCommand
{
    public string Name => "update";
    public string Summary => "최신 버전 확인 및 업데이트(npm 전역)";
    public string Usage => "pdsa update [--check]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        var current = VersionInfo.Current();
        Console.WriteLine($"현재 버전 : {current}");
        Console.Write("최신 확인 : ");
        var latest = await VersionInfo.FetchLatestAsync(TimeSpan.FromSeconds(5), ct);
        if (latest is null)
        {
            Console.WriteLine("실패");
            Console.Error.WriteLine("최신 버전을 확인하지 못했습니다(네트워크/레지스트리). 잠시 후 다시 시도하세요.");
            return 1;
        }
        Console.WriteLine(latest);

        if (!VersionInfo.IsOutdated(current, latest))
        {
            Console.WriteLine("✔ 이미 최신입니다.");
            return 0;
        }
        Console.WriteLine($"⬆ 업데이트 가능: {current} → {latest}");

        var cmd = $"npm i -g {VersionInfo.PackageName}@latest";
        if (ArgUtil.Flag(args, "--check"))
        {
            Console.WriteLine($"▶ 업데이트하려면: {cmd}");
            return 0;
        }

        // 프리체크: 다른 pdsa 프로세스가 살아 있으면 강제 종료하지 않고 안내 후 중단.
        var others = RunningPdsaPids();
        if (others.Count > 0)
        {
            var pids = string.Join(", ", others);
            if (OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine($"다른 pdsa 프로세스가 실행 중입니다 (PID: {pids}).");
                Console.Error.WriteLine("Windows 는 실행 중인 바이너리를 교체할 수 없습니다. 해당 프로세스(예: `pdsa view`)를 먼저 종료한 뒤");
                Console.Error.WriteLine($"다시 `pdsa update` 를 실행하세요. (강제 종료하지 않습니다. 수동: {cmd})");
                return 3;
            }
            Console.WriteLine($"참고: 다른 pdsa 프로세스가 실행 중입니다 (PID: {pids}). 계속 진행합니다.");
        }

        CleanupStagingDirs();

        if (OperatingSystem.IsWindows())
        {
            // 자기잠금 회피: 새 콘솔에서 npm 을 실행하고(1초 대기 후) pdsa 는 즉시 종료해 exe/DLL 잠금을 푼다.
            Console.WriteLine("새 콘솔 창에서 업데이트를 진행합니다(이 창은 종료됩니다)…");
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c start \"pdsa update\" cmd /k \"timeout /t 1 /nobreak >nul & {cmd}\"")
                { UseShellExecute = true });
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"업데이트 창을 열지 못했습니다: {ex.Message}");
                Console.Error.WriteLine($"수동으로 실행하세요: {cmd}");
                return 1;
            }
        }

        // Unix: 실행 중에도 파일 교체가 가능하므로 인라인으로 실행한다.
        Console.WriteLine($"▶ {cmd}");
        try
        {
            var psi = new ProcessStartInfo("npm", $"i -g {VersionInfo.PackageName}@latest") { UseShellExecute = false };
            var p = Process.Start(psi);
            if (p is null) { Console.Error.WriteLine("npm 실행 실패. `npm` 이 PATH 에 있는지 확인하세요."); return 1; }
            await p.WaitForExitAsync(ct);
            if (p.ExitCode == 0) { Console.WriteLine($"✔ 업데이트 완료: {latest}"); return 0; }
            Console.Error.WriteLine($"npm 이 종료코드 {p.ExitCode} 로 끝났습니다. 위 출력을 확인하세요.");
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"업데이트 실행 실패: {ex.Message}. 수동으로: {cmd}");
            return 1;
        }
    }

    /// <summary>현재 프로세스를 제외한, 실행 중인 pdsa 프로세스 PID 목록.</summary>
    private static List<int> RunningPdsaPids()
    {
        var self = Environment.ProcessId;
        var pids = new List<int>();
        try
        {
            foreach (var p in Process.GetProcessesByName("pdsa"))
            {
                try { if (p.Id != self) pids.Add(p.Id); }
                catch { /* 접근 불가 프로세스는 무시 */ }
                finally { p.Dispose(); }
            }
        }
        catch { /* 프로세스 열거 실패 시 프리체크 생략 */ }
        return pids;
    }

    /// <summary>npm 전역 <c>@webnori</c> 스코프의 고아 <c>.pdsa-*</c> 스테이징 폴더를 best-effort 정리한다.</summary>
    private static void CleanupStagingDirs()
    {
        try
        {
            var root = RunNpmCapture("root -g");
            if (string.IsNullOrWhiteSpace(root)) return;
            var scope = Path.Combine(root, "@webnori");
            if (!Directory.Exists(scope)) return;
            foreach (var d in Directory.GetDirectories(scope, ".pdsa-*"))
                try { Directory.Delete(d, true); }
                catch { /* 잠긴 파일이 남아 있으면 건너뜀 */ }
        }
        catch { /* 정리는 best-effort */ }
    }

    private static string? RunNpmCapture(string args)
    {
        try
        {
            var psi = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe", $"/c npm {args}")
                : new ProcessStartInfo("npm", args);
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            var p = Process.Start(psi);
            if (p is null) return null;
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            var first = outp.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            return string.IsNullOrWhiteSpace(first) ? null : first;
        }
        catch { return null; }
    }
}
