using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PdsaCli.Llm;

namespace PdsaCli.Cli;

/// <summary>
/// CLI 표시/기록 언어(ko|en)를 결정한다. 우선순위:
/// <c>--lang</c> 플래그 &gt; <c>PDSA_LANG</c> 환경변수 &gt; config <c>lang</c> &gt; OS 로케일 &gt; 기본 en.
/// <c>auto</c>/미설정/미상 값은 다음 순위로 넘어간다.
/// InvariantGlobalization=true 라 CultureInfo 를 못 쓰므로 env(LANG/LC_*) + Windows P/Invoke 로 감지한다.
/// </summary>
public static class PdsaLang
{
    public const string Default = "en";
    public static readonly IReadOnlyList<string> Supported = new[] { "en", "ko" };

    /// <summary>실제 환경(플래그/env/config/OS)에서 언어를 해석한다.</summary>
    public static string Resolve(string[] args)
        => Resolve(
            ArgUtil.Option(args, "--lang"),
            Environment.GetEnvironmentVariable("PDSA_LANG"),
            OpenAiConfig.ReadLang(),
            DetectOsLocale());

    /// <summary>순수 결정 로직(테스트 대상): 각 소스를 우선순위대로 정규화해 첫 유효값을 취한다.</summary>
    internal static string Resolve(string? flag, string? env, string? config, string? osLocale)
    {
        foreach (var candidate in new[] { flag, env, config, osLocale })
            if (Normalize(candidate) is { } lang)
                return lang;
        return Default;
    }

    /// <summary>로케일/언어 문자열을 ko|en 으로 정규화. auto/빈값/미상 → null(다음 순위로).</summary>
    internal static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var v = raw.Trim().ToLowerInvariant();
        if (v is "auto") return null;
        if (v.StartsWith("ko")) return "ko";        // ko, ko-KR, ko_KR.UTF-8, korean
        if (v.StartsWith("en")) return "en";        // en, en-US, english
        return null;
    }

    public static bool IsSupported(string? lang) => Normalize(lang) is not null && lang!.Trim().ToLowerInvariant() is not "auto";

    // ── OS 로케일 감지(격리) ──────────────────────────────────────────────────
    private static string? DetectOsLocale()
    {
        // Unix 계열(및 일부 셸): 표준 로케일 환경변수.
        foreach (var name in new[] { "LC_ALL", "LC_MESSAGES", "LANG", "LANGUAGE" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
                return value;
        // Windows: UI 언어(InvariantGlobalization 에서도 동작).
        if (OperatingSystem.IsWindows())
            try { return WindowsUiLanguage(); } catch { /* 감지 실패 시 다음 순위 */ }
        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string WindowsUiLanguage()
        => (GetUserDefaultUILanguage() & 0x3FF) == 0x12 ? "ko" : "en";   // primary LANGID 0x12 = Korean

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
