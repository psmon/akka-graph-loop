using PdsaCli.Llm;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// env·정적 경로 오버라이드는 프로세스 전역이라 병렬 실행되면 서로 오염된다.
/// 이 컬렉션에 속한 클래스들은 다른 컬렉션과 병렬로 돌지 않는다.
/// </summary>
[CollectionDefinition("LlmConfig", DisableParallelization = true)]
public class LlmConfigCollection;

/// <summary>
/// 설정 필드병합 격리테스트: 임시 repo(.secret)/global json + env 를 조합해
/// <c>repo &lt; global &lt; env</c> 우선순위가 필드 단위(base_url/api_key/model/auth_mode)로
/// 적용되는지 검증한다. 경로 seam(<c>GlobalPathOverride</c>/<c>RepoPathOverride</c>)로 실사용자 파일과 격리한다.
/// </summary>
[Collection("LlmConfig")]
public sealed class OpenAiConfigMergeTests : IDisposable
{
    private static readonly string[] EnvNames =
        { "OPENAI_API_KEY", "OPENAI_BASE_URL", "OPENAI_MODEL", "OPENAI_AUTH_MODE" };

    private readonly string?[] _savedEnv;
    private readonly string _repoPath;
    private readonly string _globalPath;

    public OpenAiConfigMergeTests()
    {
        _savedEnv = EnvNames.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach (var n in EnvNames) Environment.SetEnvironmentVariable(n, null);

        _repoPath = Path.Combine(Path.GetTempPath(), $"pdsa-repo-{Guid.NewGuid():N}.json");
        _globalPath = Path.Combine(Path.GetTempPath(), $"pdsa-global-{Guid.NewGuid():N}.json");
        OpenAiConfig.RepoPathOverride = _repoPath;
        OpenAiConfig.GlobalPathOverride = _globalPath;
    }

    public void Dispose()
    {
        // 코치 지침: 오버라이드/파일/env 를 반드시 복원해 테스트 간 오염 0.
        OpenAiConfig.RepoPathOverride = null;
        OpenAiConfig.GlobalPathOverride = null;
        for (var i = 0; i < EnvNames.Length; i++)
            Environment.SetEnvironmentVariable(EnvNames[i], _savedEnv[i]);
        TryDelete(_repoPath);
        TryDelete(_globalPath);
    }

    // ── base_url ──────────────────────────────────────────────────────────
    [Fact]
    public void BaseUrl_repo_only_is_used_when_higher_layers_absent()
    {
        WriteRepo("""{ "base_url": "http://repo/v1", "api_key": "k" }""");
        Assert.Equal("http://repo/v1", Resolve().BaseUrl);
    }

    [Fact]
    public void BaseUrl_global_overrides_repo()
    {
        WriteRepo("""{ "base_url": "http://repo/v1", "api_key": "k" }""");
        WriteGlobal("""{ "base_url": "http://global/v1" }""");
        Assert.Equal("http://global/v1", Resolve().BaseUrl);
    }

    [Fact]
    public void BaseUrl_env_overrides_global_and_repo()
    {
        WriteRepo("""{ "base_url": "http://repo/v1", "api_key": "k" }""");
        WriteGlobal("""{ "base_url": "http://global/v1" }""");
        Environment.SetEnvironmentVariable("OPENAI_BASE_URL", "http://env/v1");
        Assert.Equal("http://env/v1", Resolve().BaseUrl);
    }

    // ── api_key ───────────────────────────────────────────────────────────
    [Fact]
    public void ApiKey_global_overrides_repo()
    {
        WriteRepo("""{ "api_key": "sk-repo" }""");
        WriteGlobal("""{ "api_key": "sk-global" }""");
        Assert.Equal("sk-global", Resolve().ApiKey);
    }

    [Fact]
    public void ApiKey_env_wins_over_all()
    {
        WriteRepo("""{ "api_key": "sk-repo" }""");
        WriteGlobal("""{ "api_key": "sk-global" }""");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-env");
        Assert.Equal("sk-env", Resolve().ApiKey);
    }

    // ── model ─────────────────────────────────────────────────────────────
    [Fact]
    public void Model_repo_fallback_then_global_override()
    {
        WriteRepo("""{ "model": "repo-model", "api_key": "k" }""");
        Assert.Equal("repo-model", Resolve().Model);          // 폴백
        WriteGlobal("""{ "model": "global-model" }""");
        Assert.Equal("global-model", Resolve().Model);        // 상위 덮어씀
    }

    // ── auth_mode ─────────────────────────────────────────────────────────
    [Fact]
    public void AuthMode_repo_none_used_as_fallback()
    {
        WriteRepo("""{ "auth_mode": "none", "base_url": "http://localhost/v1" }""");
        Assert.Equal(AuthMode.None, Resolve().Auth);
    }

    [Fact]
    public void AuthMode_global_overrides_repo()
    {
        WriteRepo("""{ "auth_mode": "none" }""");
        WriteGlobal("""{ "auth_mode": "apikey", "api_key": "k" }""");
        Assert.Equal(AuthMode.ApiKey, Resolve().Auth);
    }

    [Fact]
    public void AuthMode_env_overrides_global_and_repo()
    {
        WriteRepo("""{ "auth_mode": "none" }""");
        WriteGlobal("""{ "auth_mode": "apikey", "api_key": "k" }""");
        Environment.SetEnvironmentVariable("OPENAI_AUTH_MODE", "none");
        Environment.SetEnvironmentVariable("OPENAI_BASE_URL", "http://localhost/v1");
        Assert.Equal(AuthMode.None, Resolve().Auth);
    }

    // ── 정책 필드(병합 대상 아님) ─────────────────────────────────────────────
    [Fact]
    public void Provider_preset_is_write_only_marker_that_sets_baseurl_and_authmode()
    {
        // provider 는 LlmOptions 로 읽히는 병합필드가 아니라, base_url/auth_mode 를 세팅하는 프리셋 마커다.
        OpenAiConfig.SetProvider("local", null);          // → global 임시파일에 기록
        var o = Resolve();
        Assert.Equal("http://localhost:11434/v1", o.BaseUrl);
        Assert.Equal(AuthMode.None, o.Auth);
    }

    [Fact]
    public void AllowInsecureNoAuth_is_global_only_not_repo()
    {
        // repo 에 넣어도 opt-in 이 활성화되면 안 된다(보안: 명시적 global 설정으로만).
        WriteRepo("""{ "auth_mode": "none", "base_url": "https://remote.example/v1", "allow_insecure_no_auth": true }""");
        Assert.False(OpenAiConfig.TryLoad(out _, out var err1));   // repo 플래그 무시 → 원격 none 차단
        Assert.Contains("사설대역", err1);

        // global 에 넣으면 opt-in 활성화된다.
        WriteGlobal("""{ "allow_insecure_no_auth": true }""");
        Assert.True(OpenAiConfig.TryLoad(out _, out _));           // global 플래그 인정 → 통과
    }

    // ── OAuth 설정 읽기/영속/미노출 ───────────────────────────────────────────
    [Fact]
    public void OAuth_refresh_token_read_from_file_not_config()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"pdsa-rt-{Guid.NewGuid():N}.txt");
        File.WriteAllText(tokenFile, "SECRET-REFRESH-123");
        try
        {
            WriteGlobal($$"""{ "auth_mode": "oauth", "oauth_access_token": "a", "oauth_refresh_token_file": {{System.Text.Json.JsonSerializer.Serialize(tokenFile)}} }""");
            var o = Resolve();
            Assert.Equal(AuthMode.OAuth, o.Auth);
            Assert.Equal("SECRET-REFRESH-123", o.OAuth!.RefreshToken);   // 파일에서 읽음
        }
        finally { TryDelete(tokenFile); }
    }

    [Fact]
    public void OAuth_refresh_token_file_secret_is_not_leaked_by_describe()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"pdsa-rt-{Guid.NewGuid():N}.txt");
        File.WriteAllText(tokenFile, "TOPSECRET-REFRESH");
        try
        {
            WriteGlobal($$"""{ "auth_mode": "oauth", "oauth_access_token": "acc", "oauth_refresh_token_file": {{System.Text.Json.JsonSerializer.Serialize(tokenFile)}} }""");
            var d = OpenAiConfig.Describe();
            // Describe 의 어떤 필드에도 refresh 비밀이 노출되면 안 된다.
            foreach (var s in new[] { d.BaseUrl, d.MaskedKey, d.Model, d.Reasoning, d.Auth, d.KeySource })
                Assert.DoesNotContain("TOPSECRET-REFRESH", s);
            Assert.True(d.Configured);   // access token 있으므로 설정됨
        }
        finally { TryDelete(tokenFile); }
    }

    [Fact]
    public void PersistOAuthToken_roundtrips_access_and_expiry()
    {
        WriteGlobal("""{ "auth_mode": "oauth" }""");
        OpenAiConfig.PersistOAuthToken(new OAuthToken("persisted-access", "persisted-refresh", 987654));
        var o = Resolve();
        Assert.Equal("persisted-access", o.OAuth!.AccessToken);
        Assert.Equal(987654, o.OAuth.ExpiresAtUnix);
        Assert.Equal("persisted-refresh", o.OAuth.RefreshToken);
    }

    // ── helpers ───────────────────────────────────────────────────────────
    private static LlmOptions Resolve()
    {
        OpenAiConfig.TryLoad(out var options, out _);   // TryLoad 는 유효성과 무관하게 options=Resolve() 를 채운다
        return options;
    }

    private void WriteRepo(string json) => File.WriteAllText(_repoPath, json);
    private void WriteGlobal(string json) => File.WriteAllText(_globalPath, json);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
