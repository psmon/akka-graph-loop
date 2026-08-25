using System.Text;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>현재 장면 상태를 한 프레임으로 그린다. 각 줄 끝을 지워(\x1b[K) 깜빡임 없이 갱신한다.</summary>
public static class Renderer
{
    public static void Draw(Scene scene, int index, int total, Pacer pacer)
    {
        var sb = new StringBuilder();
        void Line(string s = "") => sb.Append(s).Append("\x1b[K\n");

        var status = pacer.Paused
            ? Term.PausedBg + " 일시정지 " + Term.Reset
            : Term.ActiveBg + " 실행중 " + Term.Reset;

        Line(Term.Bold + Term.Cyan + "╔══ Akka.NET Streams Graph — TUI 튜토리얼 ══════════════════════╗" + Term.Reset);
        Line($"{Term.Bold}Scene {index}/{total}{Term.Reset}  {Term.Magenta}{scene.Category}{Term.Reset}  ·  {scene.Title}   {status}");
        Line(Term.Gray + "──────────────────────────────────────────────────────────────" + Term.Reset);

        Line(Term.Bold + "▸ 개념" + Term.Reset);
        foreach (var t in scene.Tutorial)
            Line("  " + t);
        Line();

        Line(Term.Bold + "▸ 그래프 (실제 스트림에 연결됨)" + Term.Reset);
        foreach (var d in scene.Diagram(pacer))
            Line("  " + d);
        Line();

        Line(Term.Bold + "▸ 진행 중인 스테이지" + Term.Reset);
        var anyActive = false;
        foreach (var id in ActiveNodeOrder)
        {
            var s = pacer.Get(id);
            if (s is not { Active: true }) continue;
            anyActive = true;
            Line($"  {Term.Green}{id,-14}{Term.Reset} 값={Term.Yellow}{s.Value,-8}{Term.Reset} {Term.Bar(s.Progress)}");
        }
        if (!anyActive) Line(Term.Gray + "  (대기 중…)" + Term.Reset);
        Line();

        Line(Term.Bold + "▸ Sink 출력 (실제 Sink.ForEach)" + Term.Reset);
        var log = pacer.Log;
        if (log.Count == 0) Line(Term.Gray + "  (아직 없음)" + Term.Reset);
        else
            foreach (var l in log)
                Line($"  {Term.Cyan}⟸{Term.Reset} {l}");
        Line();

        Line(Term.Gray + "──────────────────────────────────────────────────────────────" + Term.Reset);
        Line($"{Term.Dim}[ESC] 일시정지·재개   [Ctrl+C] 종료{Term.Reset}");

        Term.Home();
        Console.Out.Write(sb.ToString());
        Term.ClearToEndOfScreen();
    }

    // 진행 스테이지 목록에 표시할 노드 순서(등록된 것만 활성 시 노출)
    private static readonly string[] ActiveNodeOrder =
    {
        "SOURCE", "F1", "BROADCAST→A", "BROADCAST→B", "MERGE", "F3",
        "BALANCE→W0", "BALANCE→W1", "BALANCE→W2",
        "UNZIP→num", "UNZIP→str",
        "ZIP", "ZIPWITH", "CONCAT", "PRIORITIZED",
        "MAX(1,2)", "MAX(·,3)",
        "LOOP", "FEEDBACK",
    };
}
