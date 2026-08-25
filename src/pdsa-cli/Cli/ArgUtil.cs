using System.Globalization;

namespace PdsaCli.Cli;

/// <summary>간단한 인자 파서: <c>--key value</c> 옵션과 <c>--flag</c> 플래그.</summary>
public static class ArgUtil
{
    public static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static bool Flag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

    public static double Double(string[] args, string name, double fallback)
        => double.TryParse(Option(args, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static int Int(string[] args, string name, int fallback)
        => int.TryParse(Option(args, name), out var v) ? v : fallback;

    /// <summary>옵션이 아닌 첫 위치 인자들을 하나의 문자열로 합친다(예: guide 프롬프트).</summary>
    public static string Positional(string[] args)
        => string.Join(' ', args.Where(a => !a.StartsWith('-')));
}
