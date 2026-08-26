using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// LLM 코치(<see cref="PdsaCoach"/>) 검증: null LLM 이면 기록만 하고 코칭은 빈 문자열,
/// 설정 시 각 단계 프롬프트에 원문(Plan/Do/Study)이 포함되고 데밍 Study 시스템 프롬프트가 전달된다.
/// </summary>
public class PdsaCoachTests
{
    /// <summary>마지막 (system,user) 프롬프트를 캡처하고 고정 응답을 돌려주는 페이크.</summary>
    private sealed class FakeLlm : ILlmClient
    {
        public string? LastSystem { get; private set; }
        public string? LastUser { get; private set; }
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            LastSystem = systemPrompt;
            LastUser = userPrompt;
            return Task.FromResult("COACH-OK");
        }
    }

    [Fact]
    public void Enabled_reflects_whether_llm_is_present()
    {
        Assert.False(new PdsaCoach(null).Enabled);
        Assert.True(new PdsaCoach(new FakeLlm()).Enabled);
    }

    [Fact]
    public async Task Null_llm_returns_empty_for_every_phase()
    {
        var coach = new PdsaCoach(null);
        Assert.Equal("", await coach.HypothesisAsync("p", default));
        Assert.Equal("", await coach.OrganizeDoAsync("p", "d", default));
        Assert.Equal("", await coach.StudyAsync("p", "d", "s", default));
        Assert.Equal("", await coach.NextActionAsync("p", "d", "s", default));
    }

    [Fact]
    public async Task Hypothesis_prompt_includes_plan_and_deming_system()
    {
        var fake = new FakeLlm();
        var result = await new PdsaCoach(fake).HypothesisAsync("PLAN_TEXT_XYZ", default);

        Assert.Equal("COACH-OK", result);
        Assert.Contains("PLAN_TEXT_XYZ", fake.LastUser);
        Assert.Contains("가설", fake.LastUser);       // Plan 단계는 '가설'을 요구
        Assert.Contains("Study", fake.LastSystem);    // Check 아님 — 데밍 Study
    }

    [Fact]
    public async Task OrganizeDo_prompt_includes_plan_and_do()
    {
        var fake = new FakeLlm();
        await new PdsaCoach(fake).OrganizeDoAsync("PLAN_A", "DO_B", default);
        Assert.Contains("PLAN_A", fake.LastUser);
        Assert.Contains("DO_B", fake.LastUser);
    }

    [Fact]
    public async Task Study_and_NextAction_prompts_include_all_three_inputs()
    {
        var fake = new FakeLlm();

        await new PdsaCoach(fake).StudyAsync("PLAN_A", "DO_B", "STUDY_C", default);
        Assert.Contains("PLAN_A", fake.LastUser);
        Assert.Contains("DO_B", fake.LastUser);
        Assert.Contains("STUDY_C", fake.LastUser);

        await new PdsaCoach(fake).NextActionAsync("PLAN_A", "DO_B", "STUDY_C", default);
        Assert.Contains("PLAN_A", fake.LastUser);
        Assert.Contains("DO_B", fake.LastUser);
        Assert.Contains("STUDY_C", fake.LastUser);
    }
}
