using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PdsaCli.Cli;

// `--json` 옵트인 출력용 DTO. 각 커맨드가 이미 내부에서 계산해 둔 구조화 필드
// (expected/verdict/actual/reinforce 등)를 프로즈로 재렌더링하지 않고 그대로 노출한다.
// AOT 안전: 리플렉션 직렬화 금지 → source-generated camelCase(<see cref="PdsaJson"/>).
// 필드명은 이미 camelCase 이므로 정책과 함께 그대로 유지된다(pdsa view 의 ViewerJson 과 동일 규약).

internal sealed record HitRateJson(int met, int total);

internal sealed record PlanJson(
    string project, long cycle, long reinforceOf, string expected, string narrative, bool llmEnabled);

internal sealed record DoJson(
    string project, long cycle, string narrative, bool llmEnabled);

internal sealed record StudyJson(
    string project, long cycle, string expected, string verdict, string actual, string narrative, bool llmEnabled);

internal sealed record ActJson(
    string project, long cycle, bool reinforce, string what, string narrative,
    HitRateJson hitRate, int cycleCount, bool llmEnabled);

internal sealed record PhaseJson(
    string kind, string input, string llm, string created,
    string expected, string verdict, string actual, string reinforce);

internal sealed record CycleJson(
    long id, string status, string started, string verdict, IReadOnlyList<PhaseJson> phases);

internal sealed record StatusJson(
    string project, string db, bool llmConfigured, int cycleCount, HitRateJson hitRate,
    IReadOnlyList<CycleJson> cycles);

internal sealed record EvalCycleJson(
    long id, string status, string verdict, string expected, string actual, string reinforce);

internal sealed record EvalJson(
    string project, HitRateJson hitRate, IReadOnlyList<EvalCycleJson> cycles);

internal sealed record LearningJson(
    long cycle, string verdict, string expected, string actual, string study, string act);

internal sealed record RecallJson(
    string project, string? topic, IReadOnlyList<LearningJson> learnings);

/// <summary>구조화 출력을 stdout 에 한 줄 JSON 으로 방출(AOT 안전 소스젠 경유).</summary>
internal static class JsonOut
{
    public static void Write<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Console.WriteLine(JsonSerializer.Serialize(value, typeInfo));
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlanJson))]
[JsonSerializable(typeof(DoJson))]
[JsonSerializable(typeof(StudyJson))]
[JsonSerializable(typeof(ActJson))]
[JsonSerializable(typeof(StatusJson))]
[JsonSerializable(typeof(EvalJson))]
[JsonSerializable(typeof(RecallJson))]
internal partial class PdsaJson : JsonSerializerContext;
