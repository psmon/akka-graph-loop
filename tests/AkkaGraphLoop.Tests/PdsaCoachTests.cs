using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// LLM 코치(<see cref="PdsaCoach"/>) 폐루프 검증: null LLM 이면 빈 결과, 설정 시 센티넬 라인 파싱
/// (기대평가/판정/실제/보강), verdict 정규화, 보강 폴백.
/// </summary>
public class PdsaCoachTests
{
    /// <summary>고정 응답을 돌려주고 (system,user)를 캡처하는 페이크.</summary>
    private sealed class FakeLlm(string response) : ILlmClient
    {
        public string? LastUser { get; private set; }
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            LastUser = userPrompt;
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void Enabled_reflects_llm_presence()
    {
        Assert.False(new PdsaCoach(null).Enabled);
        Assert.True(new PdsaCoach(new FakeLlm("x")).Enabled);
    }

    [Fact]
    public async Task Null_llm_returns_empty_results()
    {
        var coach = new PdsaCoach(null);
        Assert.Equal(new PlanCoaching("", ""), await coach.HypothesisAsync("p"));
        Assert.Equal("", await coach.OrganizeDoAsync("p", "d", default));
        Assert.Equal(new StudyJudgment("", "", ""), await coach.JudgeAsync("e", "p", "d", "s", default));
        Assert.Equal(new ActCoaching(false, "", ""), await coach.NextActionAsync("p", "d", "s", "unmet", default));
    }

    [Fact]
    public async Task Hypothesis_parses_expected_and_strips_tag_from_narrative()
    {
        var fake = new FakeLlm("기대평가: p95 200ms 이하\n- 코칭 한 줄\n- 가설 한 줄");
        var r = await new PdsaCoach(fake).HypothesisAsync("계획X");

        Assert.Equal("p95 200ms 이하", r.Expected);
        Assert.DoesNotContain("기대평가:", r.Narrative);
        Assert.Contains("코칭 한 줄", r.Narrative);
        Assert.Contains("계획X", fake.LastUser); // 프롬프트에 원문 포함
    }

    [Fact]
    public async Task Hypothesis_injects_prior_learnings_into_prompt()
    {
        var fake = new FakeLlm("기대평가: X\n코칭");
        await new PdsaCoach(fake).HypothesisAsync("계획X", "#3 [unmet]\n  learned: 캐시 무효화 누락", default);

        Assert.Contains("과거 학습", fake.LastUser!);            // 주입 헤더(ko)
        Assert.Contains("캐시 무효화 누락", fake.LastUser!);      // 주입된 학습 본문
        Assert.Contains("계획X", fake.LastUser!);                // 원 계획도 유지
    }

    [Fact]
    public async Task Hypothesis_without_prior_learnings_omits_injection_block()
    {
        var fake = new FakeLlm("기대평가: X\n코칭");
        await new PdsaCoach(fake).HypothesisAsync("계획X", "   ", default); // 공백만 → 주입 없음

        Assert.DoesNotContain("과거 학습", fake.LastUser!);
    }

    [Fact]
    public async Task Judge_parses_verdict_and_actual()
    {
        var fake = new FakeLlm("판정: partial\n실제: 320ms→240ms\n학습 서술");
        var r = await new PdsaCoach(fake).JudgeAsync("기대", "p", "d", "s", default);

        Assert.Equal("partial", r.Verdict);
        Assert.Equal("320ms→240ms", r.Actual);
        Assert.DoesNotContain("판정:", r.Narrative);
        Assert.Contains("학습 서술", r.Narrative);
    }

    [Fact]
    public async Task NextAction_parses_reinforce_yes()
    {
        var fake = new FakeLlm("보강: yes\n무엇: 병목 트레이싱\n- 다음 액션");
        var r = await new PdsaCoach(fake).NextActionAsync("p", "d", "s", "partial", default);

        Assert.True(r.Reinforce);
        Assert.Equal("병목 트레이싱", r.What);
    }

    [Fact]
    public async Task NextAction_falls_back_to_reinforce_when_verdict_not_met_and_tag_missing()
    {
        var fake = new FakeLlm("- 태그 없는 다음 액션만");
        Assert.True((await new PdsaCoach(fake).NextActionAsync("p", "d", "s", "unmet", default)).Reinforce);
        Assert.False((await new PdsaCoach(fake).NextActionAsync("p", "d", "s", "met", default)).Reinforce);
    }

    // ── 순수 파서 ────────────────────────────────────────────────
    [Theory]
    [InlineData("met", "met")]
    [InlineData("MET", "met")]
    [InlineData("unmet", "unmet")]
    [InlineData("미충족", "unmet")]
    [InlineData("partial", "partial")]
    [InlineData("부분 충족", "partial")]
    [InlineData("충족됨", "met")]
    [InlineData("", "")]
    [InlineData("모호함", "unknown")]
    public void NormalizeVerdict_maps_english_and_korean(string raw, string expected)
        => Assert.Equal(expected, PdsaCoach.NormalizeVerdict(raw));

    [Fact]
    public void ParseTag_reads_first_matching_line_case_insensitive()
    {
        Assert.Equal("hello", PdsaCoach.ParseTag("판정: hello\n실제: x", "판정", "verdict"));
        Assert.Equal("v", PdsaCoach.ParseTag("VERDICT: v", "판정", "verdict"));
        Assert.Equal("", PdsaCoach.ParseTag("아무 태그 없음", "판정"));
    }

    [Fact]
    public void StripTags_removes_only_known_sentinel_lines()
    {
        var text = "기대평가: X\n판정: met\n실제 서술 내용\n일반 줄";
        var stripped = PdsaCoach.StripTags(text);
        Assert.DoesNotContain("기대평가:", stripped);
        Assert.DoesNotContain("판정:", stripped);
        Assert.Contains("실제 서술 내용", stripped);
        Assert.Contains("일반 줄", stripped);
    }
}
