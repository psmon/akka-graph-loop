using System.Collections.Concurrent;
using Akka;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>노드(스테이지) 하나의 실시간 상태.</summary>
public sealed class NodeState
{
    public string Value = "";
    public double Progress;   // 0.0 ~ 1.0 (현재 스텝 진행률)
    public bool Active;
    public int Count;         // 이 노드를 통과한 원소 수
}

/// <summary>
/// 그래프의 흐름 속도를 제어하고(스텝당 ~5초), TUI 가 읽을 실시간 상태를 보관한다.
/// - 각 스테이지는 <see cref="Tap{T}"/> 를 통해 흐르며, tap 이 원소를 붙잡고 대기하므로
///   Akka 의 backpressure 로 그래프 전체가 스텝 단위로 진행된다.
/// - ESC 로 <see cref="TogglePause"/> 하면 카운트다운이 멈춰 그래프가 그 자리에서 정지한다.
/// - Ctrl+C 는 <see cref="CancellationToken"/> 으로 tap 의 대기를 즉시 취소해 스트림을 종료시킨다.
/// </summary>
public sealed class Pacer
{
    private readonly TimeSpan _step;
    private readonly CancellationToken _ct;
    private readonly ConcurrentDictionary<string, NodeState> _nodes = new();
    private readonly object _logLock = new();
    private readonly List<string> _log = new();

    public Pacer(TimeSpan step, CancellationToken ct)
    {
        _step = step;
        _ct = ct;
    }

    public volatile bool Paused;
    public CancellationToken CancellationToken => _ct;

    public void TogglePause() => Paused = !Paused;

    public NodeState? Get(string nodeId) => _nodes.TryGetValue(nodeId, out var s) ? s : null;

    public IReadOnlyList<string> Log
    {
        get { lock (_logLock) return _log.ToArray(); }
    }

    /// <summary>실제 Sink 가 원소를 받을 때마다 호출되어 출력 로그를 남긴다.</summary>
    public void SinkReceived(string value)
    {
        lock (_logLock)
        {
            _log.Add(value);
            if (_log.Count > 8) _log.RemoveAt(0);
        }
    }

    /// <summary>
    /// 그래프에 삽입하는 계측 스테이지. 원소가 이 지점을 지날 때 노드를 활성화하고 ~5초 대기한다.
    /// 대기 중 일시정지면 진행률을 멈추고, 취소되면 예외로 스트림을 종료시킨다.
    /// </summary>
    public Flow<T, T, NotUsed> Tap<T>(string nodeId, Func<T, string>? format = null)
    {
        return Flow.Create<T>().SelectAsync(1, async value =>
        {
            await StepAsync(nodeId, format is null ? value?.ToString() ?? "∅" : format(value));
            return value;
        });
    }

    private async Task StepAsync(string nodeId, string value)
    {
        var ns = _nodes.GetOrAdd(nodeId, _ => new NodeState());
        ns.Value = value;
        ns.Progress = 0;
        ns.Active = true;

        var elapsed = TimeSpan.Zero;
        var tick = TimeSpan.FromMilliseconds(100);
        try
        {
            while (elapsed < _step)
            {
                await Task.Delay(tick, _ct);
                if (Paused) continue;                 // 일시정지: 진행률 동결
                elapsed += tick;
                ns.Progress = Math.Min(1.0, elapsed.TotalMilliseconds / _step.TotalMilliseconds);
            }
        }
        finally
        {
            ns.Active = false;
            ns.Count++;
        }
    }
}
