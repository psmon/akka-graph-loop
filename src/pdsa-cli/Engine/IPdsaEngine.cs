using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Engine;

/// <summary>PDSA 실행 결과(사이클 이력 + 그래프 DB 되읽기).</summary>
public sealed record PdsaRunResult(
    IReadOnlyList<PdsaState> History,
    IReadOnlyList<string> GraphProgress,
    int NextEdges,
    string DbPath);

/// <summary>PDSA 루프 실행 엔진 추상화(구현: Akka.Streams + Kùzu).</summary>
public interface IPdsaEngine
{
    Task<PdsaRunResult> RunAsync(double start, double target, Action<string>? onLog, CancellationToken ct);
}
