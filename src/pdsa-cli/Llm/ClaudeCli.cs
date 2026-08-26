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
