using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// LLM 설정 로더(<see cref="OpenAiConfig"/>) 검증: 환경변수 최우선 로드, placeholder 키의 '미설정' 판정,
/// 키 마스킹. 환경변수는 프로세스 전역이므로 각 테스트에서 저장→복원한다(클래스 내 순차 실행).
/// </summary>
[Collection("LlmConfig")]
public class OpenAiConfigTests : IDisposable
{
    // 앰비언트 전역설정/레포 .secret 에 의존하지 않도록 seam 을 존재하지 않는 임시 경로로 격리한다
    // (실사용자 설정 오염·의존 방지). 각 테스트 종료 시 복원.
    public OpenAiConfigTests()
    {
        OpenAiConfig.GlobalPathOverride = Path.Combine(Path.GetTempPath(), $"pdsa-none-{Guid.NewGuid():N}.json");
        OpenAiConfig.RepoPathOverride = Path.Combine(Path.GetTempPath(), $"pdsa-none-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        OpenAiConfig.GlobalPathOverride = null;
        OpenAiConfig.RepoPathOverride = null;
    }

    private static IDisposable EnvScope(string? key, string? baseUrl, string? model, string? authMode = null)
    {
        string[] names = { "OPENAI_API_KEY", "OPENAI_BASE_URL", "OPENAI_MODEL", "OPENAI_AUTH_MODE" };
        var saved = names.Select(n => Environment.GetEnvironmentVariable(n)).ToArray();
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", key);
        Environment.SetEnvironmentVariable("OPENAI_BASE_URL", baseUrl);
        Environment.SetEnvironmentVariable("OPENAI_MODEL", model);
        Environment.SetEnvironmentVariable("OPENAI_AUTH_MODE", authMode);
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

    [Fact]
    public void None_auth_on_private_endpoint_loads_without_key()
    {
        // 키리스 로컬: auth_mode=none + localhost → 키 없이 통과, Auth=None.
        using var _ = EnvScope(key: "", baseUrl: "http://localhost:11434/v1", model: "llama3.1", authMode: "none");

        var ok = OpenAiConfig.TryLoad(out var options, out var error);

        Assert.True(ok, error);
        Assert.Equal(AuthMode.None, options.Auth);
    }

    [Fact]
    public void None_auth_on_remote_endpoint_is_blocked_without_optin()
    {
        // 원격 무키는 명시적 opt-in 없이는 차단.
        using var scope = EnvScope(key: "", baseUrl: "https://remote.example/v1", model: "m", authMode: "none");

        var ok = OpenAiConfig.TryLoad(out _, out var error);

        Assert.False(ok);
        Assert.Contains("사설대역", error);
    }

    [Fact]
    public void Default_authmode_is_apikey_for_backward_compat()
    {
        // auth_mode 미지정 + 유효 키 → 기존 동작(ApiKey) 유지.
        using var _ = EnvScope("sk-env-abcdef123456", "https://env.example/v1", "m");

        var ok = OpenAiConfig.TryLoad(out var options, out var error);

        Assert.True(ok, error);
        Assert.Equal(AuthMode.ApiKey, options.Auth);
    }

    [Theory]
    [InlineData("", "(미설정)")]
    [InlineData("sk-123", "****")]                       // 8자 이하
    [InlineData("sk-abcdefghij", "sk-a*****ghij")]       // 13자: 앞4 + 별5 + 뒤4
    public void Mask_hides_middle_of_key(string key, string expected)
        => Assert.Equal(expected, OpenAiConfig.Mask(key));
}
