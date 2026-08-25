namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>
/// PDSA 그래프 메모리의 <b>개인 / 앱 / 프로젝트별</b> 경로를 해석한다.
/// 그래프 DB 는 프로젝트마다 별도로 누적되어, 사용할수록 그 프로젝트의 학습이 쌓인다.
///   <c>{LocalAppData}/pdsa-cli/{project}/graph.kuzu</c>
/// </summary>
public static class PdsaProjectPaths
{
    public const string AppName = "pdsa-cli";

    /// <summary>개인/앱 루트: <c>{LocalAppData}/pdsa-cli</c>.</summary>
    public static string AppRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create),
        AppName);

    /// <summary>전역 LLM 설정 파일 경로(개인 단위, 모든 프로젝트 공용).</summary>
    public static string GlobalConfigFile => Path.Combine(AppRoot, "openai.json");

    /// <summary>프로젝트별 그래프 DB 경로.</summary>
    public static string GraphDbFor(string project) =>
        Path.Combine(AppRoot, Sanitize(project), "graph.kuzu");

    /// <summary>현재 작업 디렉터리 이름을 프로젝트 식별자로 사용(없으면 "default").</summary>
    public static string CurrentProjectName()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        return string.IsNullOrWhiteSpace(dir) ? "default" : Sanitize(dir);
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }
}
