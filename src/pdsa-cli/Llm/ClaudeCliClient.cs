using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PdsaCli.Llm;

/// <summary>
/// 공식 Claude Code CLI(<c>claude -p --output-format json</c>)를 서브프로세스로 호출하는 <see cref="ILlmClient"/>.
/// 이미 로그인된 Claude 를 그대로 쓰므로 API 키·토큰 설정이 없다(100% 공식 경로). system 은 <c>--append-system-prompt</c>,
/// user 는 stdin 으로 전달하고 결과 JSON 의 <c>result</c> 를 반환한다.
/// </summary>
public sealed class ClaudeCliClient : ILlmClient
{
    private readonly string _model;
    private readonly TimeSpan _timeout;
    private readonly Func<string, IReadOnlyList<string>, string, CancellationToken, Task<(int Exit, string Stdout, string Stderr)>> _run;
    private readonly Func<string?> _resolveExe;

    public ClaudeCliClient(string? model = null,
        Func<string, IReadOnlyList<string>, string, CancellationToken, Task<(int, string, string)>>? runner = null,
        Func<string?>? exeResolver = null,
        TimeSpan? timeout = null)
    {
        _model = model ?? "";
        _run = runner ?? RunProcessAsync;
        _resolveExe = exeResolver ?? ClaudeCli.Resolve;
        _timeout = timeout ?? ClaudeCli.ResolveTimeout();
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var exe = _resolveExe()
            ?? throw new InvalidOperationException(
                "Claude Code CLI(`claude`)를 찾을 수 없습니다. 설치/로그인 후 다시 시도하세요.\n" +
                "  경로 지정: pdsa config claude-cli-path <경로>  또는 env PDSA_CLAUDE_CLI");

        var args = new List<string> { "-p", "--output-format", "json", "--max-turns", "1", "--append-system-prompt", systemPrompt };
        if (UsesClaudeModel(_model)) { args.Add("--model"); args.Add(_model); }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_timeout > TimeSpan.Zero) timeoutCts.CancelAfter(_timeout);
        int exit; string stdout, stderr;
        try
        {
            (exit, stdout, stderr) = await _run(exe, args, userPrompt, timeoutCts.Token);
        }
        // 타임아웃(내부 CTS 발화)과 사용자 취소(외부 ct)를 구분: 후자는 그대로 전파.
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"claude -p 응답이 {_timeout.TotalSeconds:0}s 내에 오지 않아 중단했습니다. " +
                "조정: pdsa config claude-cli-timeout <초>  또는 env PDSA_CLAUDE_TIMEOUT_SEC");
        }
        if (exit != 0 && stdout.Trim().Length == 0)
            throw new InvalidOperationException($"claude -p 실행 실패(exit {exit}): {Truncate(stderr, 300)}");

        return ParseResult(stdout);
    }

    /// <summary>claude -p JSON 출력에서 최종 텍스트(<c>result</c>)를 뽑는다. 오류/거부는 예외로.</summary>
    internal static string ParseResult(string stdout)
    {
        var json = stdout.Trim();
        if (json.Length == 0) throw new InvalidOperationException("claude -p 응답이 비어 있습니다.");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True)
                throw new InvalidOperationException($"claude 오류 응답: {Str(root, "result") ?? Str(root, "subtype") ?? "unknown"}");
            var subtype = Str(root, "subtype");
            if (subtype is not null && subtype != "success")
                throw new InvalidOperationException($"claude 미완료 응답(subtype={subtype}).");
            return Str(root, "result")?.Trim()
                ?? throw new InvalidOperationException("claude 응답에 result 가 없습니다.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"claude -p JSON 파싱 실패: {ex.Message}");
        }
    }

    /// <summary>모델을 --model 로 넘길지: 값이 있고 claude 계열일 때만(기본 OpenAI 모델명 오전달 방지).</summary>
    internal static bool UsesClaudeModel(string? model)
        => !string.IsNullOrWhiteSpace(model) && model!.Trim().StartsWith("claude", StringComparison.OrdinalIgnoreCase);

    private static async Task<(int, string, string)> RunProcessAsync(
        string exe, IReadOnlyList<string> args, string stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            // 레포 CLAUDE.md/도구 컨텍스트 미로드로 저비용·정제(코칭은 전달된 텍스트만 대상).
            WorkingDirectory = Path.GetTempPath(),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        try
        {
            await proc.StandardInput.WriteAsync(stdin.AsMemory(), ct);
            proc.StandardInput.Close();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return (proc.ExitCode, await stdoutTask, await stderrTask);
        }
        // 타임아웃/취소 시 매달린 claude 프로세스(및 자식)를 정리해 좀비·토큰 소모를 막는다.
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* 이미 종료/권한 등 무시 */ }
            throw;
        }
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
