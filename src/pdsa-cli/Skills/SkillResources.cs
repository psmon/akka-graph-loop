using System.Reflection;

namespace PdsaCli.Skills;

/// <summary>
/// `pdsa init` 이 설치하는 스킬 문서를 실행 파일의 <b>임베디드 매니페스트 리소스</b>에서 로드한다.
/// 매니페스트 리소스는 타입 리플렉션이 아니므로 Native AOT publish 산출물에서도 그대로 동작한다.
/// </summary>
public static class SkillResources
{
    /// <summary>지원 언어(리소스로 임베드된 것). 첫 번째가 기본값.</summary>
    public static readonly IReadOnlyList<string> Langs = new[] { "en", "ko" };

    public static bool IsSupported(string? lang)
        => lang is not null && Langs.Contains(lang, StringComparer.OrdinalIgnoreCase);

    /// <summary>언어별 스킬 문서 본문(임베디드 리소스 <c>SKILL.&lt;lang&gt;.md</c>).</summary>
    public static string Load(string lang)
    {
        if (!IsSupported(lang))
            throw new ArgumentException($"지원하지 않는 언어: {lang} (지원: {string.Join(", ", Langs)})");
        var name = $"SKILL.{lang.ToLowerInvariant()}.md";
        var asm = typeof(SkillResources).Assembly;
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"임베디드 리소스를 찾을 수 없습니다: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>워크스페이스 루트 기준 스킬 설치 대상 경로: <c>.claude/skills/pdsa/SKILL.md</c>.</summary>
    public static string TargetPath(string workspaceRoot)
        => Path.Combine(workspaceRoot, ".claude", "skills", "pdsa", "SKILL.md");

    /// <summary>기존 파일이 없거나 강제(확인됨)일 때만 쓴다. 존재하는데 강제 아니면 보호(false).</summary>
    public static bool ShouldWrite(bool exists, bool force) => !exists || force;
}
