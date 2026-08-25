using AkkaGraphLoop.Samples.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// PDSA(Deming) 피드백 사이클 검증. Act→Plan 되먹임이 있는 실제 Akka 사이클이지만,
/// 목표 품질에 도달하면 데드락 없이 유한 회차에 수렴·종료해야 한다.
/// </summary>
public class PdsaTests : GraphTestBase
{
    [Fact]
    public void Loop_converges_to_target_in_finite_cycles()
    {
        var history = Await(PdsaLoop.Run(Materializer, start: 45, target: 90, log: _ => { }));

        Assert.NotEmpty(history);
        Assert.True(history.Count < 20, "유한 회차 내 수렴해야 한다(데드락/무한루프 아님).");

        var final = history[^1];
        Assert.True(final.Converged);              // 마지막은 목표 달성
        Assert.Equal(90, final.Quality);           // 목표에서 정확히 멈춤(캡)

        // 마지막을 제외한 모든 회차는 아직 목표 미달(수렴 원소 하나만 방출)
        Assert.All(history.Take(history.Count - 1), s => Assert.False(s.Converged));
    }

    [Fact]
    public void Quality_increases_monotonically_and_iterations_are_sequential()
    {
        var history = Await(PdsaLoop.Run(Materializer, start: 45, target: 90, log: _ => { }));

        for (var i = 1; i < history.Count; i++)
        {
            Assert.True(history[i].Quality > history[i - 1].Quality, "품질은 매 회차 개선되어야 한다.");
            Assert.Equal(history[i - 1].Iteration + 1, history[i].Iteration); // 회차는 1,2,3…
        }
        Assert.Equal(1, history[0].Iteration);
    }
}
