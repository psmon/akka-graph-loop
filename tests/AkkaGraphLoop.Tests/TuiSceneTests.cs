using AkkaGraphLoop.Samples.Tui;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// TUI 장면들은 실제 Akka 그래프에 계측 tap 을 끼운 형태다. 특히 사이클 장면은 피드백 루프에
/// tap 이 들어가므로, 빠른 스텝으로 돌렸을 때 데드락 없이 완료되고 Sink 출력이 나오는지 검증한다.
/// </summary>
public class TuiSceneTests : GraphTestBase
{
    public static IEnumerable<object[]> SceneIndices()
        => Enumerable.Range(0, Scenes.All.Count).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(SceneIndices))]
    public void Scene_completes_without_deadlock_and_produces_sink_output(int index)
    {
        var scene = Scenes.All[index];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pacer = new Pacer(TimeSpan.FromMilliseconds(50), cts.Token);

        // 15초 타임아웃 내 완료되지 않으면(=데드락) 실패한다.
        Await(scene.Run(pacer, Materializer), seconds: 15);

        Assert.NotEmpty(pacer.Log); // 실제 Sink.ForEach 가 값을 받았는지
    }
}
