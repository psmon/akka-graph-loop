using System.Globalization;

namespace PdsaCli.Cli;

/// <summary>간단한 인자 파서: <c>--key value</c> 옵션과 <c>--flag</c> 플래그.</summary>
public static class ArgUtil
{
    /// <summary>
    /// '값을 갖는' 옵션 이름 화이트리스트. <see cref="Positional"/> 가 이 옵션들의 <b>값 토큰</b>을
    /// 본문에서 제외하기 위해 참조한다(예: <c>--project akka</c> 의 <c>akka</c> 가 plan/do/study 본문에
    /// 섞이지 않도록). 플래그(<c>--fresh</c>, <c>--force</c> 등)는 값이 없으므로 여기 넣지 않는다.
    /// </summary>
    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "--expect", "--filter", "--lang", "--limit", "--note",
        "--path", "--port", "--project", "--reinforce", "--start", "--target",
    };

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

    /// <summary>
    /// 옵션이 아닌 위치 인자들을 하나의 문자열로 합친다(예: plan/do/study/guide 본문).
    /// <c>-</c> 로 시작하는 토큰과, 화이트리스트 값-옵션(<see cref="ValueOptions"/>) 바로 뒤의
    /// '값' 토큰은 제외한다. 덕분에 <c>pdsa plan "계획" --project akka</c> 에서 <c>akka</c> 가
    /// 기록 본문에 섞이지 않는다.
    /// </summary>
    public static string Positional(string[] args)
    {
        var parts = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith('-')) continue;                          // 옵션/플래그 자체
            if (i > 0 && ValueOptions.Contains(args[i - 1])) continue; // 값-옵션의 값 토큰
            parts.Add(a);
        }
        return string.Join(' ', parts);
    }
}
