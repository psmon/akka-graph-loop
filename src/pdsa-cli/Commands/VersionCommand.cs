using System.Reflection;
using PdsaCli.Cli;

namespace PdsaCli.Commands;

/// <summary>도구/런타임 정보를 출력한다.</summary>
public sealed class VersionCommand : ICliCommand
{
    public string Name => "version";
    public string Summary => "버전 및 런타임 정보 출력";
    public string Usage => "pdsa version";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        Console.WriteLine($"pdsa {version}");
        Console.WriteLine($"  .NET {Environment.Version}");
        Console.WriteLine($"  엔진: Akka.Streams(PDSA 피드백 사이클) · Kùzu 임베디드 그래프 DB · OpenAI");
        return Task.FromResult(0);
    }
}
