using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Cli;

/// <summary>
/// pdsa 현재/최신 버전 확인. 최신은 npm 레지스트리(<c>@webnori/pdsa/latest</c>)에서 인증 없이 GET.
/// help·무인자 안내는 짧은 TTL 캐시(<c>{AppRoot}/version-check.json</c>)로 오프라인·즉시성을 보장한다.
/// 비교는 단순 x.y.z 숫자 기준(프리릴리스 태그는 무시). 모든 조회는 실패해도 조용히 폴백한다.
/// </summary>
internal static class VersionInfo
{
    public const string PackageName = "@webnori/pdsa";
    private const string LatestUrl = "https://registry.npmjs.org/@webnori/pdsa/latest";
    private static string CachePath => Path.Combine(PdsaProjectPaths.AppRoot, "version-check.json");

    /// <summary>현재 버전을 x.y.z 로 정규화(assembly 4-part → 3-part).</summary>
    public static string Current()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>레지스트리에서 최신 버전을 조회(실패 시 null). 성공하면 캐시에 기록한다.</summary>
    public static async Task<string?> FetchLatestAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = timeout };
            http.DefaultRequestHeaders.Add("User-Agent", "pdsa-cli");
            var body = await http.GetStringAsync(LatestUrl, ct);
            using var doc = JsonDocument.Parse(body);
            var latest = doc.RootElement.TryGetProperty("version", out var vp) ? vp.GetString() : null;
            if (!string.IsNullOrWhiteSpace(latest)) WriteCache(latest!);
            return latest;
        }
        catch { return null; }
    }

    /// <summary>true 면 latest 가 current 보다 높다.</summary>
    public static bool IsOutdated(string current, string? latest)
        => !string.IsNullOrWhiteSpace(latest) && Compare(latest!, current) > 0;

    /// <summary>x.y.z 숫자 비교. a&gt;b → 양수, 같으면 0, a&lt;b → 음수.</summary>
    public static int Compare(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < 3; i++)
        {
            var c = pa[i].CompareTo(pb[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int[] Parse(string v)
    {
        var parts = (v ?? "").Split('.', '-', '+');
        var r = new int[3];
        for (var i = 0; i < 3 && i < parts.Length; i++)
            int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out r[i]);
        return r;
    }

    // ── 캐시(무인자/help 안내용: 네트워크 없이 즉시 읽기) ──────────────────────

    /// <summary>캐시된 최신 버전이 <paramref name="maxAge"/> 이내로 신선하면 반환, 아니면 null.</summary>
    public static string? CachedLatestIfFresh(TimeSpan maxAge)
    {
        try
        {
            var body = File.ReadAllText(CachePath);
            using var doc = JsonDocument.Parse(body);
            var at = DateTimeOffset.Parse(doc.RootElement.GetProperty("checkedAt").GetString()!,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (DateTimeOffset.UtcNow - at > maxAge) return null;
            return doc.RootElement.GetProperty("latest").GetString();
        }
        catch { return null; }
    }

    private static void WriteCache(string latest)
    {
        try
        {
            Directory.CreateDirectory(PdsaProjectPaths.AppRoot);
            var at = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            // latest 는 버전 문자열(안전 문자)이라 이스케이프 불필요.
            File.WriteAllText(CachePath, $"{{\"checkedAt\":\"{at}\",\"latest\":\"{latest}\"}}");
        }
        catch { /* 캐시는 best-effort */ }
    }

    /// <summary>
    /// help·무인자 화면 하단에 붙일 '새 버전 있음' 안내(최신이면 null). 캐시 우선,
    /// 없으면 짧은 타임아웃으로 한 번 조회한다(오프라인이면 조용히 null).
    /// </summary>
    public static string? UpdateNoticeForHelp(bool ko)
    {
        try
        {
            var current = Current();
            var latest = CachedLatestIfFresh(TimeSpan.FromHours(24))
                         ?? FetchLatestAsync(TimeSpan.FromMilliseconds(1200), CancellationToken.None)
                            .GetAwaiter().GetResult();
            if (!IsOutdated(current, latest)) return null;
            return ko
                ? $"⬆ 새 버전 {latest} 가 있습니다(현재 {current}). 업데이트: `pdsa update`  또는  `npm i -g {PackageName}@latest`"
                : $"⬆ A new version {latest} is available (current {current}). Update: `pdsa update`  or  `npm i -g {PackageName}@latest`";
        }
        catch { return null; }
    }
}
