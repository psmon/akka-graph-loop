using PdsaCli.Cli;
using PdsaCli.Commands;

// pdsa — 데밍 PDSA 지속개선 루프 공식 CLI.
// AI 에이전트가 이 CLI 로 PDSA 루프를 실행/기록하고, 그래프를 보고, LLM 조언을 받는다.

// 출력을 UTF-8 로 고정(Windows 기본 CP949 에서 한글 프로즈가 파이프/에이전트/타 OS 에 깨지는 문제 방지).
ConsoleEncoding.ForceUtf8();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var router = new CommandRouter(new ICliCommand[]
{
    new InitCommand(),
    new PlanCommand(),
    new DoCommand(),
    new StudyCommand(),
    new ActCommand(),
    new StatusCommand(),
    new EvalCommand(),
    new RecallCommand(),
    new ProjectCommand(),
    new ViewCommand(),
    new ConfigCommand(),
    new CheckCommand(),
    new ModelsCommand(),
    new GuideCommand(),
    new RunCommand(),
    new UpdateCommand(),
    new VersionCommand(),
});

return await router.DispatchAsync(args, cts.Token);
