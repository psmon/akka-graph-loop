using Akka.Actor;
using Akka.Streams;

namespace AkkaGraphLoop.Samples;

/// <summary>
/// ActorSystem 과 Materializer 의 수명을 관리하는 헬퍼.
/// Akka.Streams 그래프는 <see cref="IMaterializer"/> 위에서 실행(materialize)된다.
/// </summary>
public sealed class DemoHost : IDisposable
{
    public ActorSystem System { get; }
    public IMaterializer Materializer { get; }

    public DemoHost(string name = "graph-demo")
    {
        System = ActorSystem.Create(name);
        Materializer = System.Materializer();
    }

    public void Dispose()
    {
        System.Dispose();
    }
}
