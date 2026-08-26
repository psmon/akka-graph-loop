using AkkaGraphLoop.Core.Pdsa;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 프로젝트 경로 해석(<see cref="PdsaProjectPaths"/>) 검증: 명시 인자 우선 해석과
/// 잘못된 파일명 문자/공백에 대한 sanitize(공백/빈 문자열 → "default"). 파일시스템에 의존하지
/// 않도록 명시 인자 경로만 테스트한다.
/// </summary>
public class PdsaProjectPathsTests
{
    [Fact]
    public void ResolveProject_uses_explicit_name()
        => Assert.Equal("akka-graph-loop", PdsaProjectPaths.ResolveProject("akka-graph-loop"));

    [Fact]
    public void ResolveProject_replaces_invalid_filename_chars()
    {
        var invalid = Path.GetInvalidFileNameChars();
        Assert.NotEmpty(invalid); // 플랫폼 불변식
        var name = "proj" + invalid[0] + "x";

        var resolved = PdsaProjectPaths.ResolveProject(name);

        Assert.DoesNotContain(invalid[0], resolved);
        Assert.Contains("proj", resolved);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void ResolveProject_falls_back_to_default_for_blank(string blank)
        => Assert.Equal("default", PdsaProjectPaths.ResolveProject(blank));

    [Fact]
    public void GraphDbFor_builds_sanitized_per_project_path()
    {
        var path = PdsaProjectPaths.GraphDbFor("akka-graph-loop");
        Assert.EndsWith("graph.kuzu", path);
        Assert.Contains("akka-graph-loop", path);
        Assert.StartsWith(PdsaProjectPaths.AppRoot, path);
    }
}
