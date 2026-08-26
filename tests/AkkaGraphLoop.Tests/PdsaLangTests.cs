using PdsaCli.Cli;
using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 언어 결정기(<see cref="PdsaLang"/>)와 코치 프롬프트 언어 주입 검증.
/// 우선순위: --lang &gt; PDSA_LANG &gt; config &gt; OS 로케일 &gt; 기본 en. auto/미상은 다음 순위로.
/// </summary>
public class PdsaLangTests
{
    // ── 순수 결정 로직(flag, env, config, osLocale) ──
    [Theory]
    [InlineData("en", "ko", "ko", "ko", "en")]   // 플래그가 최우선
    [InlineData(null, "ko", "en", "en", "ko")]   // env 가 config/os 보다 우선
    [InlineData(null, null, "ko", "en", "ko")]   // config 가 os 보다 우선
    [InlineData(null, null, null, "ko-KR", "ko")]// os 로케일 폴백
    [InlineData(null, null, null, "en-US", "en")]
    [InlineData(null, null, null, null, "en")]   // 아무것도 없으면 기본 en
    public void Resolve_follows_priority(string? flag, string? env, string? config, string? os, string expected)
        => Assert.Equal(expected, PdsaLang.Resolve(flag, env, config, os));

    [Theory]
    [InlineData("auto", null, "ko", "ko")]        // flag=auto → 건너뜀 → config ko
    [InlineData("auto", "auto", "auto", "en")]    // 전부 auto → 기본 en
    [InlineData("fr", null, null, "en")]          // 미지원 flag → 건너뜀 → 기본 en
    public void Resolve_skips_auto_and_invalid(string? flag, string? env, string? config, string expected)
        => Assert.Equal(expected, PdsaLang.Resolve(flag, env, config, null));

    [Theory]
    [InlineData("ko", "ko")]
    [InlineData("ko-KR", "ko")]
    [InlineData("ko_KR.UTF-8", "ko")]
    [InlineData("korean", "ko")]
    [InlineData("en-US", "en")]
    [InlineData("english", "en")]
    [InlineData("auto", null)]
    [InlineData("", null)]
    [InlineData("C", null)]
    [InlineData("fr-FR", null)]
    public void Normalize_maps_locale_to_supported(string? raw, string? expected)
        => Assert.Equal(expected, PdsaLang.Normalize(raw));

    // ── 코치 프롬프트 언어 주입 ──
    private sealed class CapturingLlm : ILlmClient
    {
        public string? System, User;
        public Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
        {
            System = system; User = user;
            return Task.FromResult("Expected: x\n기대평가: x\nnarrative");
        }
    }

    [Fact]
    public async Task Coach_en_uses_english_system_and_prompt()
    {
        var llm = new CapturingLlm();
        await new PdsaCoach(llm, "en").HypothesisAsync("do the thing", default);
        Assert.Contains("English", llm.System);
        Assert.Contains("Expected:", llm.User);
    }

    [Fact]
    public async Task Coach_ko_uses_korean_system_and_prompt()
    {
        var llm = new CapturingLlm();
        await new PdsaCoach(llm, "ko").HypothesisAsync("작업 수행", default);
        Assert.Contains("한국어", llm.System);
        Assert.Contains("기대평가", llm.User);
    }

    [Fact]
    public async Task Coach_default_is_korean()
    {
        var llm = new CapturingLlm();
        await new PdsaCoach(llm).HypothesisAsync("x", default);   // 기본 ko(하위호환)
        Assert.Contains("한국어", llm.System);
    }
}
