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
    new RunCommand(),
    new GuideCommand(),
    new ViewCommand(),
    new VersionCommand(),
});

return await router.DispatchAsync(args, cts.Token);
