using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>LLM(OpenAI) 키/모델/엔드포인트를 전역 설정에 저장하거나 현재 설정을 보여준다.</summary>
public sealed class ConfigCommand : ICliCommand
{
    public string Name => "config";
    public string Summary => "LLM 키/모델/엔드포인트 설정(전역)";
    public string Usage => "pdsa config [--api-key <키>] [--model <모델>] [--base-url <URL>] | --show";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        var apiKey = ArgUtil.Option(args, "--api-key");
        var model = ArgUtil.Option(args, "--model");
        var baseUrl = ArgUtil.Option(args, "--base-url");

        if (apiKey is null && model is null && baseUrl is null)
        {
            // 표시만
            var (url, masked, mdl, ok) = OpenAiConfig.Describe();
            Console.WriteLine($"base_url : {url}");
            Console.WriteLine($"model    : {mdl}");
            Console.WriteLine($"api_key  : {masked}");
            Console.WriteLine($"상태     : {(ok ? "설정됨" : "미설정")}");
            if (!ok) Console.WriteLine($"\n설정: {Usage}");
            return Task.FromResult(0);
        }

        var path = OpenAiConfig.Save(apiKey, baseUrl, model);
        Console.WriteLine($"저장됨: {path}");
        var (u, m2, md, ok2) = OpenAiConfig.Describe();
        Console.WriteLine($"  base_url={u}  model={md}  api_key={m2}  ({(ok2 ? "설정됨" : "미설정")})");
        return Task.FromResult(0);
    }
}
