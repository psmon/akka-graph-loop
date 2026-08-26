namespace PdsaCli.Cli;

/// <summary>최상위 명령 디스패처. 첫 인자를 명령 이름으로 보고 해당 명령에 위임한다. 도움말은 ko/en 지원.</summary>
public sealed class CommandRouter
{
    private readonly IReadOnlyList<ICliCommand> _commands;

    public CommandRouter(IEnumerable<ICliCommand> commands) => _commands = commands.ToList();

    public async Task<int> DispatchAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp(PdsaLang.Resolve(args));
            return 0;
        }

        var name = args[0];
        var command = _commands.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            var lang = PdsaLang.Resolve(args);
            Console.Error.WriteLine(lang == "ko" ? $"알 수 없는 명령: {name}" : $"Unknown command: {name}");
            PrintHelp(lang);
            return 2;
        }

        try
        {
            return await command.RunAsync(args[1..], ct);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("중단됨. / Aborted.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"오류/Error: {ex.Message}");
            return 1;
        }
    }

    private void PrintHelp(string lang)
    {
        if (lang == "ko") PrintHelpKo(); else PrintHelpEn();
    }

    private void PrintHelpKo()
    {
        Console.WriteLine("pdsa — PDSA 루프 엔지니어를 지원하는 CLI");
        Console.WriteLine();
        Console.WriteLine("이 CLI 는 AI 에이전트가 데밍의 PDSA(Plan-Do-Study-Act) 지속개선 루프를");
        Console.WriteLine("수행하도록 코칭합니다. 플래닝(Plan) 단계에서 가장 먼저 사용하세요.");
        Console.WriteLine();
        Console.WriteLine("한 사이클:");
        Console.WriteLine("  1) plan   계획을 입력하면 LLM 이 '가설'까지 세워 코칭합니다(대개 계획만 하고");
        Console.WriteLine("            가설을 빠뜨리므로). → 이 가설을 받아 작업을 수행하세요.");
        Console.WriteLine("  2) do     수행한 것을 알려주면 Plan→Do 과정을 그래프로 정리합니다.");
        Console.WriteLine("  3) study  결과를 알려주면 무엇을 배웠는지 학습하고 개선점을 도출합니다.");
        Console.WriteLine("  4) act    다음에 수행할 개선 액션을 코칭합니다 → 반영해 다음 plan 으로.");
        Console.WriteLine();
        Console.WriteLine("반복할수록 그래프 DB(프로젝트별)에 학습이 누적되어, 공정 자체를 개선하는");
        Console.WriteLine("PDSA 철학을 지원하고 AI 에이전트를 위한 '진보된 메모리'가 됩니다.");
        Console.WriteLine();
        Console.WriteLine("언어: 기본은 OS 로케일 자동. 고정하려면 `pdsa config lang <en|ko|auto>`");
        Console.WriteLine("      또는 `--lang en|ko` / 환경변수 PDSA_LANG. 이 언어로 도움말·기록이 표시됩니다.");
        Console.WriteLine();
        Console.WriteLine("사용법: pdsa <명령> [옵션]");
        Console.WriteLine();
        Console.WriteLine("명령:");
        foreach (var c in _commands)
            Console.WriteLine($"  {c.Name,-8} {c.Summary}");
        Console.WriteLine();
        Console.WriteLine("먼저 LLM 설정:  pdsa config key <키>  (또는 key-file <파일>) ,  pdsa config model <모델>");
        Console.WriteLine("호출 확인:      pdsa check");
        Console.WriteLine("각 명령 상세:   pdsa <명령> --help");
    }

    private void PrintHelpEn()
    {
        Console.WriteLine("pdsa — a CLI that coaches engineers through the PDSA loop");
        Console.WriteLine();
        Console.WriteLine("This CLI coaches an AI agent to run Deming's PDSA (Plan-Do-Study-Act)");
        Console.WriteLine("continuous-improvement loop. Use it first, at the Plan step.");
        Console.WriteLine();
        Console.WriteLine("One cycle:");
        Console.WriteLine("  1) plan   Enter a plan and the LLM coaches you — including a 'hypothesis'");
        Console.WriteLine("            (usually omitted). → Take that hypothesis and do the work.");
        Console.WriteLine("  2) do     Report what you did; it organizes Plan→Do into the graph.");
        Console.WriteLine("  3) study  Report results; it learns what you learned and derives improvements.");
        Console.WriteLine("  4) act    It coaches the next improvement action → carry it into the next plan.");
        Console.WriteLine();
        Console.WriteLine("The more you iterate, the more learning accumulates in the per-project graph DB —");
        Console.WriteLine("supporting the PDSA philosophy of improving the process itself, an 'advanced");
        Console.WriteLine("memory' for AI agents.");
        Console.WriteLine();
        Console.WriteLine("Language: defaults to your OS locale. Pin it with `pdsa config lang <en|ko|auto>`,");
        Console.WriteLine("          or `--lang en|ko` / the PDSA_LANG env var. Help and records use this language.");
        Console.WriteLine();
        Console.WriteLine("Usage: pdsa <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (var c in _commands)
            Console.WriteLine($"  {c.Name,-8} {EnSummary(c)}");
        Console.WriteLine();
        Console.WriteLine("Configure the LLM first:  pdsa config key <key>  (or key-file <file>) ,  pdsa config model <model>");
        Console.WriteLine("Verify the call:          pdsa check");
        Console.WriteLine("Per-command detail:       pdsa <command> --help");
    }

    /// <summary>영문 도움말용 명령 요약(없으면 명령 자체 요약으로 폴백).</summary>
    private static string EnSummary(ICliCommand c) => c.Name switch
    {
        "init" => "Install the PDSA skill into this workspace (.claude/skills/pdsa/SKILL.md)",
        "plan" => "Enter a plan → set the expected outcome (start a new cycle)",
        "do" => "Report what you did → organize Plan→Do into the graph",
        "study" => "Report results → judge vs. expected & learn (not 'Check')",
        "act" => "Summarize learnings + decide immediate reinforce (end the cycle)",
        "status" => "PDSA progress/accumulated state for the current project",
        "eval" => "Expectation hit-rate (recall) + per-cycle expected/verdict/actual",
        "project" => "Set/list the active project (separate multi-project DBs)",
        "view" => "Open the accumulated graph-memory viewer (local port)",
        "config" => "LLM key/model/auth/lang settings — key inline or by file path",
        "check" => "Verify LLM connection (real call test)",
        "models" => "List supported models",
        "guide" => "Get one-off PDSA guidance from the LLM (OpenAI)",
        "run" => "Run the PDSA loop (Akka streams) + record to the graph DB",
        "version" => "Print version and runtime info",
        _ => c.Summary,
    };
}
