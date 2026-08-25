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
        Console.WriteLine("pdsa — 데밍 PDSA 지속개선 루프 공식 CLI (AI 에이전트 지원용)");
        Console.WriteLine();
        Console.WriteLine("사용법: pdsa <명령> [옵션]");
        Console.WriteLine();
        Console.WriteLine("명령:");
        foreach (var c in _commands.OrderBy(c => c.Name))
            Console.WriteLine($"  {c.Name,-8} {c.Summary}");
        Console.WriteLine();
        Console.WriteLine("각 명령의 상세 사용법: pdsa <명령> --help");
    }
}
