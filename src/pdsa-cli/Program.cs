using PdsaCli.Cli;
using PdsaCli.Commands;

// pdsa — 데밍 PDSA 지속개선 루프 공식 CLI.
// AI 에이전트가 이 CLI 로 PDSA 루프를 실행/기록하고, 그래프를 보고, LLM 조언을 받는다.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var router = new CommandRouter(new ICliCommand[]
{
    new PlanCommand(),
    new DoCommand(),
    new StudyCommand(),
    new ActCommand(),
    new StatusCommand(),
    new ProjectCommand(),
    new ViewCommand(),
    new ConfigCommand(),
    new CheckCommand(),
    new ModelsCommand(),
    new GuideCommand(),
    new RunCommand(),
    new VersionCommand(),
});

return await router.DispatchAsync(args, cts.Token);
