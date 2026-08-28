namespace PdsaCli.Llm;

/// <summary>
/// 공식 Claude Code CLI(<c>claude -p</c>) 실행파일 해석. 별도 토큰 설정 없이 이미 로그인된 Claude 를 그대로 쓴다.
/// 우선순위: <c>PDSA_CLAUDE_CLI</c> env &gt; config <c>claude_cli_path</c> &gt; PATH 의 <c>claude</c>(.exe/.cmd/.bat).
/// </summary>
public static class ClaudeCli
{
    /// <summary>claude 실행파일 경로(찾으면 절대경로/명령명, 없으면 null).</summary>
    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("PDSA_CLAUDE_CLI");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        var cfg = OpenAiConfig.ReadClaudeCliPath();
        if (!string.IsNullOrWhiteSpace(cfg) && File.Exists(cfg)) return cfg;

        return FindOnPath();
    }

    public static bool IsAvailable() => Resolve() is not null;

    /// <summary>기본 타임아웃(초). CLI 시작지연이 있어도 넉넉히, 그러나 무한 hang 은 막는다.</summary>
    public const int DefaultTimeoutSec = 180;

    /// <summary>claude -p 호출 타임아웃. 우선순위: <c>PDSA_CLAUDE_TIMEOUT_SEC</c> env &gt; config <c>claude_cli_timeout_sec</c> &gt; 기본 180s.</summary>
    public static TimeSpan ResolveTimeout()
        => ParseTimeout(
            Environment.GetEnvironmentVariable("PDSA_CLAUDE_TIMEOUT_SEC"),
            OpenAiConfig.ReadClaudeCliTimeoutSec(),
            DefaultTimeoutSec);

    /// <summary>타임아웃 해석(순수): env(문자열) &gt; config(정수) &gt; 기본. 0/음수/파싱실패는 무시하고 다음 후보로.</summary>
    internal static TimeSpan ParseTimeout(string? env, int? cfg, int defSec)
    {
        if (int.TryParse(env?.Trim(), out var e) && e > 0) return TimeSpan.FromSeconds(e);
        if (cfg is int c && c > 0) return TimeSpan.FromSeconds(c);
        return TimeSpan.FromSeconds(defSec);
    }

    private static string? FindOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        var names = OperatingSystem.IsWindows()
            ? new[] { "claude.exe", "claude.cmd", "claude.bat", "claude" }
            : new[] { "claude" };
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }
}
