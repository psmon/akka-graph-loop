using Akka.Streams;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>
/// TUI 튜토리얼의 한 장면. 실제 Akka 그래프(<see cref="Run"/>)와 그 위상을 그린
/// ASCII 다이어그램(<see cref="Diagram"/>)을 함께 담는다.
/// </summary>
public sealed record Scene(
    string Title,
    string Category,
    string[] Tutorial,
    Func<Pacer, string[]> Diagram,
    Func<Pacer, IMaterializer, Task> Run);

public static class SceneDraw
{
    /// <summary>노드 라벨을 상태에 따라 색칠한다(활성=초록/일시정지=노랑, 통과수 표시).</summary>
    public static string Node(Pacer p, string id, string text)
    {
        var s = p.Get(id);
        var body = s is { Active: true }
            ? (p.Paused ? Term.PausedBg : Term.ActiveBg) + $"[{text}]" + Term.Reset
            : $"[{text}]";
        var count = s is { Count: > 0 } ? Term.Gray + $"×{s.Count}" + Term.Reset : "";
        return body + count;
    }

    /// <summary>노드에 흐르는 현재 값을 (있으면) 회색으로 표시.</summary>
    public static string Flowing(Pacer p, string id)
    {
        var s = p.Get(id);
        return s is { Active: true } ? Term.Yellow + s.Value + Term.Reset : "  ";
    }
}
