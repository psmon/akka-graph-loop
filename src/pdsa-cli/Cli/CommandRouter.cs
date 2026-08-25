namespace PdsaCli.Cli;

/// <summary>최상위 명령 디스패처. 첫 인자를 명령 이름으로 보고 해당 명령에 위임한다.</summary>
public sealed class CommandRouter
{
    private readonly IReadOnlyList<ICliCommand> _commands;

    public CommandRouter(IEnumerable<ICliCommand> commands) => _commands = commands.ToList();

    public async Task<int> DispatchAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var name = args[0];
        var command = _commands.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            Console.Error.WriteLine($"알 수 없는 명령: {name}");
            PrintHelp();
            return 2;
        }

        try
        {
            return await command.RunAsync(args[1..], ct);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("중단됨.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"오류: {ex.Message}");
            return 1;
        }
    }

    private void PrintHelp()
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
        Console.WriteLine("사용법: pdsa <명령> [옵션]");
        Console.WriteLine();
        Console.WriteLine("명령:");
        foreach (var c in _commands)
            Console.WriteLine($"  {c.Name,-8} {c.Summary}");
        Console.WriteLine();
        Console.WriteLine("먼저 LLM 설정:  pdsa config --api-key <키> --model <모델>");
        Console.WriteLine("각 명령 상세:   pdsa <명령> --help");
    }
}
