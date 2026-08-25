using AkkaGraphLoop.Samples;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 각 테스트 클래스마다 독립된 ActorSystem/Materializer 를 제공한다.
/// <see cref="Await{T}"/> 는 타임아웃을 걸어, 사이클 그래프가 데드락 없이 완료되는지(liveness)까지 검증한다.
/// </summary>
public abstract class GraphTestBase : IDisposable
{
    private readonly DemoHost _host = new("graph-test");

    protected Akka.Streams.IMaterializer Materializer => _host.Materializer;

    protected static T Await<T>(Task<T> task, int seconds = 15)
        => task.WaitAsync(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();

    protected static void Await(Task task, int seconds = 15)
        => task.WaitAsync(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();

    public void Dispose() => _host.Dispose();
}
