using System.Text.Json;
using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// <c>--json</c> 옵트인 출력의 계약 검증. 에이전트가 프로즈 대신 안정 필드를 읽으므로,
/// source-generated 직렬화(<see cref="PdsaJson"/>)가 camelCase 필드명을 정확히 지켜야 한다.
/// </summary>
public class PdsaJsonTests
{
    [Fact]
    public void Plan_json_serializes_expected_camelCase_fields()
    {
        var json = JsonSerializer.Serialize(
            new PlanJson("proj", 7, 3, "p95 200ms 이하", "코칭 서술", true), PdsaJson.Default.PlanJson);

        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        Assert.Equal("proj", r.GetProperty("project").GetString());
        Assert.Equal(7, r.GetProperty("cycle").GetInt64());
        Assert.Equal(3, r.GetProperty("reinforceOf").GetInt64());
        Assert.Equal("p95 200ms 이하", r.GetProperty("expected").GetString());
        Assert.True(r.GetProperty("llmEnabled").GetBoolean());
        Assert.DoesNotContain("\"Project\"", json);   // PascalCase 로 새면 안 됨
    }

    [Fact]
    public void Study_json_exposes_verdict_and_actual()
    {
        var json = JsonSerializer.Serialize(
            new StudyJson("proj", 7, "기대", "partial", "320→240ms", "학습", true), PdsaJson.Default.StudyJson);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("partial", doc.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("320→240ms", doc.RootElement.GetProperty("actual").GetString());
    }

    [Fact]
    public void Act_json_nests_hitRate()
    {
        var json = JsonSerializer.Serialize(
            new ActJson("proj", 7, true, "병목 트레이싱", "다음 액션", new HitRateJson(2, 3), 7, true),
            PdsaJson.Default.ActJson);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("reinforce").GetBoolean());
        var hr = doc.RootElement.GetProperty("hitRate");
        Assert.Equal(2, hr.GetProperty("met").GetInt32());
        Assert.Equal(3, hr.GetProperty("total").GetInt32());
    }

    [Fact]
    public void Recall_json_lists_learnings()
    {
        var json = JsonSerializer.Serialize(
            new RecallJson("proj", "캐시",
                new[] { new LearningJson(3, "unmet", "기대", "실제", "학습", "액션") }),
            PdsaJson.Default.RecallJson);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("캐시", doc.RootElement.GetProperty("topic").GetString());
        var first = doc.RootElement.GetProperty("learnings")[0];
        Assert.Equal(3, first.GetProperty("cycle").GetInt64());
        Assert.Equal("unmet", first.GetProperty("verdict").GetString());
    }

    [Fact]
    public void Status_json_carries_full_phase_text()
    {
        var longInput = new string('가', 300); // status 프로즈는 70자 절삭 — json 은 전체 보존
        var phase = new PhaseJson("plan", longInput, "llm", "2026-01-01", "기대", "", "", "");
        var cycle = new CycleJson(1, "planned", "2026-01-01", "", new[] { phase });
        var json = JsonSerializer.Serialize(
            new StatusJson("proj", "db", true, 1, new HitRateJson(0, 0), new[] { cycle }),
            PdsaJson.Default.StatusJson);
        using var doc = JsonDocument.Parse(json);
        var got = doc.RootElement.GetProperty("cycles")[0].GetProperty("phases")[0].GetProperty("input").GetString();
        Assert.Equal(longInput, got);   // 미절삭 전체
    }
}
