using AkkaGraphLoop.Samples.Cycles;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 사이클(feedback loop) 그래프의 liveness 검증.
/// 세 해법 모두 <c>Await(..., seconds: 15)</c> 타임아웃 내에 정해진 개수를 방출하고 완료해야 한다.
/// (완료되지 않으면 = 데드락 = 테스트 실패)
/// </summary>
public class CycleTests : GraphTestBase
{
    [Fact]
    public void MergePreferred_cycle_stays_live_and_counts_up()
    {
        var result = Await(CycleSamples.MergePreferredCycle(Materializer, take: 20));
        Assert.Equal(Enumerable.Range(1, 20), result);
    }

    [Fact]
    public void BufferDropHead_cycle_stays_live_and_emits_requested_count()
    {
        var result = Await(CycleSamples.BufferDropHeadCycle(Materializer, take: 20));
        Assert.Equal(20, result.Count);
        // DropHead 버퍼가 있어도 방출 개수는 요청한 만큼 채워지고, 값은 단조 증가한다.
        Assert.Equal(result.OrderBy(x => x), result);
    }

    [Fact]
    public void BalancedZipWith_cycle_stays_live_and_counts_from_zero()
    {
        var result = Await(CycleSamples.BalancedZipWithCycle(Materializer, take: 20));
        Assert.Equal(Enumerable.Range(0, 20), result);
    }
}
