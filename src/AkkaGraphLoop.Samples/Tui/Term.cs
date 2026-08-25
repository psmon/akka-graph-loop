using System.Runtime.InteropServices;
using System.Text;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>ANSI 터미널 제어 헬퍼(색/커서/화면 지우기)와 Windows 가상 터미널 활성화.</summary>
public static class Term
{
    public const string Reset = "\x1b[0m";
    public const string Dim = "\x1b[2m";
    public const string Bold = "\x1b[1m";
    public const string Green = "\x1b[32m";
    public const string Cyan = "\x1b[36m";
    public const string Yellow = "\x1b[33m";
    public const string Magenta = "\x1b[35m";
    public const string Gray = "\x1b[90m";
    public const string ActiveBg = "\x1b[30;42m"; // 검정 글자 + 초록 배경(활성 노드)
    public const string PausedBg = "\x1b[30;43m"; // 검정 글자 + 노랑 배경

    public static void Home() => Console.Out.Write("\x1b[H");
    public static void ClearScreen() => Console.Out.Write("\x1b[2J\x1b[H");
    public static void ClearToEndOfScreen() => Console.Out.Write("\x1b[J");
    public static void HideCursor() => Console.Out.Write("\x1b[?25l");
    public static void ShowCursor() => Console.Out.Write("\x1b[?25h");

    /// <summary>프로그레스 바 문자열. 예: [■■■■□□□□] 50%</summary>
    public static string Bar(double progress, int width = 10)
    {
        var filled = (int)Math.Round(Math.Clamp(progress, 0, 1) * width);
        return "[" + new string('■', filled) + new string('□', width - filled) + $"] {progress * 100,3:0}%";
    }

    /// <summary>Windows 콘솔에서 ANSI 이스케이프가 동작하도록 가상 터미널 처리를 켠다.</summary>
    public static void EnableVirtualTerminal()
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (GetConsoleMode(handle, out var mode))
                SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch
        {
            // 콘솔이 없거나(리다이렉트) 지원되지 않으면 조용히 무시 — 최악의 경우 이스케이프가 보일 뿐
        }
    }

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
