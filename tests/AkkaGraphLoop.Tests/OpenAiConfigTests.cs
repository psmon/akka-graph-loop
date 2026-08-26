using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// LLM 설정 로더(<see cref="OpenAiConfig"/>) 검증: 환경변수 최우선 로드, placeholder 키의 '미설정' 판정,
/// 키 마스킹. 환경변수는 프로세스 전역이므로 각 테스트에서 저장→복원한다(클래스 내 순차 실행).
/// </summary>
public class OpenAiConfigTests
{
    private static IDisposable EnvScope(string? key, string? baseUrl, string? model)
    {
        string[] names = { "OPENAI_API_KEY", "OPENAI_BASE_URL", "OPENAI_MODEL" };
        var saved = names.Select(n => Environment.GetEnvironmentVariable(n)).ToArray();
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", key);
        Environment.SetEnvironmentVariable("OPENAI_BASE_URL", baseUrl);
        Environment.SetEnvironmentVariable("OPENAI_MODEL", model);
        return new Restore(() =>
        {
            for (var i = 0; i < names.Length; i++)
                Environment.SetEnvironmentVariable(names[i], saved[i]);
        });
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    [Fact]
    public void Env_vars_take_highest_priority_and_load_succeeds()
    {
        using var _ = EnvScope("sk-env-abcdef123456", "https://env.example/v1", "env-model-x");

        var ok = OpenAiConfig.TryLoad(out var options, out var error);

        Assert.True(ok, error);
        Assert.Equal("sk-env-abcdef123456", options.ApiKey);
        Assert.Equal("https://env.example/v1", options.BaseUrl);
        Assert.Equal("env-model-x", options.Model);
    }

    [Fact]
    public void Placeholder_key_is_treated_as_unset()
    {
        // 환경변수는 레포 .secret 보다 우선하므로 placeholder 를 강제로 얹으면 '미설정' 이어야 한다.
        using var scope = EnvScope("sk-여기에-여기에키를", null, null);

        var ok = OpenAiConfig.TryLoad(out _, out var error);

        Assert.False(ok);
        Assert.Contains("API 키가 설정되지 않았습니다", error);
    }

    [Theory]
    [InlineData("", "(미설정)")]
    [InlineData("sk-123", "****")]                       // 8자 이하
    [InlineData("sk-abcdefghij", "sk-a*****ghij")]       // 13자: 앞4 + 별5 + 뒤4
    public void Mask_hides_middle_of_key(string key, string expected)
        => Assert.Equal(expected, OpenAiConfig.Mask(key));
}
