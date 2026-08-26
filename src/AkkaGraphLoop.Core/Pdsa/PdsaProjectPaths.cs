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

    /// <summary>활성 프로젝트를 저장하는 파일(개인 단위).</summary>
    public static string ActiveProjectFile => Path.Combine(AppRoot, "current-project");

    /// <summary>프로젝트별 그래프 DB 경로.</summary>
    public static string GraphDbFor(string project) =>
        Path.Combine(AppRoot, Sanitize(project), "graph.kuzu");

    /// <summary>현재 작업 디렉터리 이름을 프로젝트 식별자로 사용(없으면 "default").</summary>
    public static string CurrentProjectName()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        return string.IsNullOrWhiteSpace(dir) ? "default" : Sanitize(dir);
    }

    /// <summary>영속 지정된 활성 프로젝트(없으면 null).</summary>
    public static string? ActiveProject()
    {
        try
        {
            if (!File.Exists(ActiveProjectFile)) return null;
            var name = File.ReadAllText(ActiveProjectFile).Trim();
            return string.IsNullOrWhiteSpace(name) ? null : Sanitize(name);
        }
        catch { return null; }
    }

    /// <summary>활성 프로젝트를 지정(영속). 이후 모든 명령이 이 프로젝트 DB 를 참조한다.</summary>
    public static void SetActiveProject(string project)
    {
        Directory.CreateDirectory(AppRoot);
        File.WriteAllText(ActiveProjectFile, Sanitize(project));
    }

    /// <summary>활성 프로젝트 지정을 해제(현재 디렉터리 기반으로 복귀).</summary>
    public static void ClearActiveProject()
    {
        if (File.Exists(ActiveProjectFile)) File.Delete(ActiveProjectFile);
    }

    /// <summary>
    /// 사용할 프로젝트를 결정한다. 우선순위: 명시 인자 → 활성 프로젝트(set) → 현재 디렉터리 이름.
    /// </summary>
    public static string ResolveProject(string? explicitName)
        => Sanitize(explicitName ?? ActiveProject() ?? CurrentProjectName());

    /// <summary>그래프 DB 가 존재하는 프로젝트 이름 목록(정렬).</summary>
    public static IReadOnlyList<string> ListProjects()
    {
        if (!Directory.Exists(AppRoot)) return Array.Empty<string>();
        var names = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(AppRoot))
            if (PdsaPaths.Exists(Path.Combine(dir, "graph.kuzu")))
                names.Add(Path.GetFileName(dir));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }
}
