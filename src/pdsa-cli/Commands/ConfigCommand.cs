using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Commands;

/// <summary>
/// LLM 설정. 키와 모델을 <b>분리</b>해서 설정한다(키 설정 후 모델만 갈아끼우기 가능).
/// 키는 직접 입력하거나 <b>파일 위치</b>로 지정할 수 있다(키 미노출).
/// </summary>
public sealed class ConfigCommand : ICliCommand
{
    public string Name => "config";
    public string Summary => "LLM 키/모델 설정(분리) — 키 직접 또는 파일 위치";
    public string Usage =>
        "pdsa config key <키> | key-file <파일경로> | model <모델> | base-url <URL> | show";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help") || args.Length == 0 || args[0] is "show")
        {
            if (args.Length == 0 || args.ElementAtOrDefault(0) is "show" or "--help" or null)
                Show();
            return Task.FromResult(0);
        }

        var sub = args[0];
        var value = string.Join(' ', args.Skip(1)).Trim();

        switch (sub)
        {
            case "key":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config key <키>");
                Console.WriteLine($"저장됨(키): {OpenAiConfig.SetKey(value)}");
                break;
            case "key-file":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config key-file <파일경로>");
                if (!File.Exists(value)) Console.WriteLine($"주의: 파일이 아직 없습니다: {value}");
                Console.WriteLine($"저장됨(키파일 참조): {OpenAiConfig.SetKeyFile(value)}");
                break;
            case "model":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config model <모델>");
                Console.WriteLine($"저장됨(모델={value}): {OpenAiConfig.SetModel(value)}");
                break;
            case "base-url":
                if (string.IsNullOrWhiteSpace(value)) return Fail("사용법: pdsa config base-url <URL>");
                Console.WriteLine($"저장됨(base-url={value}): {OpenAiConfig.SetBaseUrl(value)}");
                break;
            default:
                return Fail($"알 수 없는 하위 명령: {sub}\n{Usage}");
        }

        Console.WriteLine();
        Show();
        return Task.FromResult(0);
    }

    private static void Show()
    {
        var (url, masked, model, source, ok) = OpenAiConfig.Describe();
        Console.WriteLine($"base_url : {url}");
        Console.WriteLine($"model    : {model}");
        Console.WriteLine($"api_key  : {masked}  ({source})");
        Console.WriteLine($"상태     : {(ok ? "설정됨 — `pdsa check` 로 호출 확인" : "미설정")}");
    }

    private static Task<int> Fail(string msg) { Console.Error.WriteLine(msg); return Task.FromResult(2); }
}
