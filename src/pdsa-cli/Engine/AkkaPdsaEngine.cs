using Akka.Actor;
using Akka.Configuration;
using Akka.Streams;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Engine;

/// <summary>
/// 조사한 PDSA 루프를 <b>Akka.Streams 피드백 사이클</b>로 처리하고, 각 회차를
/// <b>Kùzu 내장 그래프 DB</b>에 실시간 기록하는 엔진. (AkkaGraphLoop.Core 재사용)
/// </summary>
public sealed class AkkaPdsaEngine : IPdsaEngine
{
    public async Task<PdsaRunResult> RunAsync(double start, double target, Action<string>? onLog, CancellationToken ct)
    {
        // 명시적 Config 를 전달해 app.config 를 읽는 System.Configuration.ConfigurationManager 경로를 우회한다.
        // (single-file publish / AOT 에서 "Configuration system failed to initialize" 크래시 방지 — akka.net #4876)
        using var system = ActorSystem.Create("pdsa-cli", ConfigurationFactory.Default());
        var materializer = system.Materializer();

        var dbPath = PdsaPaths.DefaultDbPath;
        PdsaPaths.Reset(dbPath);
        using var store = new KuzuPdsaStore(dbPath, start, target);

        var history = await PdsaLoop.Run(materializer, start, target, onLog, store).WaitAsync(ct);

        return new PdsaRunResult(history, store.ReadProgress(), store.CountNextEdges(), dbPath);
    }
}
