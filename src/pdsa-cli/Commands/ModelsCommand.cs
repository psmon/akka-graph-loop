using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>엔드포인트가 지원하는 모델 목록을 조회한다(GET /models).</summary>
public sealed class ModelsCommand : ICliCommand
{
    public string Name => "models";
    public string Summary => "지원 모델 목록 조회";
    public string Usage => "pdsa models [--filter <문자열>]";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        if (!OpenAiConfig.TryLoad(out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 3;
        }

        var filter = ArgUtil.Option(args, "--filter");
        using var llm = new OpenAiClient(options);
        try
        {
            var ids = await llm.ListModelsAsync(ct);
            var shown = filter is null ? ids : ids.Where(i => i.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            Console.WriteLine($"모델 {shown.Count}개{(filter is null ? "" : $" (필터: {filter})")}:");
            foreach (var id in shown)
                Console.WriteLine($"  {id}{(id == options.Model ? "  ← 현재" : "")}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"실패: {ex.Message}");
            return 1;
        }
    }
}
