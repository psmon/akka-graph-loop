using PdsaCli.Skills;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// `pdsa init` 의 스킬 리소스 로더(<see cref="SkillResources"/>) 검증: 임베디드 리소스 로드(en/ko),
/// 대상 경로 해석, 덮어쓰기 보호 판정. 임베디드 매니페스트 리소스라 AOT 산출물에서도 동일하게 동작한다.
/// </summary>
public class SkillResourcesTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ko")]
    [InlineData("EN")]      // 대소문자 무관
    public void Load_returns_nonempty_skill_with_frontmatter(string lang)
    {
        var content = SkillResources.Load(lang);
        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("name: pdsa", content);      // frontmatter 존재
        Assert.Contains("Plan", content);            // 본문 존재
    }

    [Fact]
    public void Both_languages_are_supported_and_differ()
    {
        Assert.Contains("en", SkillResources.Langs);
        Assert.Contains("ko", SkillResources.Langs);
        Assert.NotEqual(SkillResources.Load("en"), SkillResources.Load("ko"));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsupported_language_is_rejected(string? lang)
    {
        Assert.False(SkillResources.IsSupported(lang));
        Assert.Throws<ArgumentException>(() => SkillResources.Load(lang!));
    }

    [Fact]
    public void TargetPath_is_dotclaude_skills_pdsa()
    {
        var p = SkillResources.TargetPath("/ws");
        Assert.EndsWith(Path.Combine(".claude", "skills", "pdsa", "SKILL.md"), p);
    }

    [Theory]
    [InlineData(false, false, true)]   // 없음 → 쓴다
    [InlineData(false, true, true)]    // 없음 + force → 쓴다
    [InlineData(true, true, true)]     // 있음 + force(확인됨) → 덮어쓴다
    [InlineData(true, false, false)]   // 있음 + 강제아님 → 보호(안 씀)
    public void ShouldWrite_protects_existing_unless_forced(bool exists, bool force, bool expected)
        => Assert.Equal(expected, SkillResources.ShouldWrite(exists, force));
}
