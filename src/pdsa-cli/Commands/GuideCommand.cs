using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>
/// LLM(OpenAI)을 이용해 PDSA 관련 조언을 받는다.
/// (초기 구성: LLM 인터페이스 동작 확인용 기본 패스스루. 단계별 가이드 프롬프트 로직은 이후 지침에서 확장.)
/// </summary>
public sealed class GuideCommand : ICliCommand
{
    public string Name => "guide";
    public string Summary => "LLM(OpenAI)으로 PDSA 조언 받기(기본)";
    public string Usage => "pdsa guide \"<질문/상황>\"";

    private const string SystemPrompt =
        "당신은 데밍(W. Edwards Deming)의 PDSA(Plan-Do-Study-Act) 지속개선 코치입니다. " +
        "질문에 대해 PDSA 관점에서 간결하고 실천 가능한 조언을 한국어로 제시하세요.";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        var prompt = ArgUtil.Positional(args);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Console.Error.WriteLine($"사용법: {Usage}");
            return 2;
        }

        if (!OpenAiConfig.TryLoad(out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 3;
        }

        using var llm = new OpenAiClient(options);
        Console.WriteLine($"(model: {options.Model})");
        var answer = await llm.CompleteAsync(SystemPrompt, prompt, ct);
        Console.WriteLine();
        Console.WriteLine(answer);
        return 0;
    }
}
