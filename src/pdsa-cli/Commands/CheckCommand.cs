using System.Diagnostics;
using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>LLM 이 실제로 호출되는지 확인한다(짧은 프롬프트로 왕복 테스트).</summary>
public sealed class CheckCommand : ICliCommand
{
    public string Name => "check";
    public string Summary => "LLM 연결 확인(실제 호출 테스트)";
    public string Usage => "pdsa check";

    public async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return 0; }

        if (!OpenAiConfig.TryLoad(out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 3;
        }

        var (url, masked, model, reasoning, auth, source, _) = OpenAiConfig.Describe();
        Console.WriteLine($"LLM 호출 확인 중… base_url={url}  model={model}  auth={auth}  reasoning={reasoning}  key={masked} ({source})");

        var llm = LlmClientFactory.Create(options);
        var sw = Stopwatch.StartNew();
        try
        {
            var reply = await llm.CompleteAsync(
                "You are a health check. Reply with exactly: OK",
                "정상 동작하면 'OK' 한 단어만 답하세요.", ct);
            sw.Stop();
            Console.WriteLine($"✔ 성공 ({sw.ElapsedMilliseconds}ms). 응답: {Trim(reply)}");
            return 0;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.Error.WriteLine($"✘ 실패 ({sw.ElapsedMilliseconds}ms): {ex.Message}");
            Console.Error.WriteLine("  모델명이 base_url 엔드포인트에 존재하는지 확인하세요: pdsa config model <모델>");
            return 1;
        }
        finally
        {
            (llm as IDisposable)?.Dispose();
        }
    }

    private static string Trim(string s) => s.Length <= 200 ? s : s[..200] + "…";
}
