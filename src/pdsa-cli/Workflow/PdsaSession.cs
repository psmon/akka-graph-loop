using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Cli;
using PdsaCli.Llm;

namespace PdsaCli.Workflow;

/// <summary>
/// 한 CLI 호출의 PDSA 세션 컨텍스트: 프로젝트 해석 → 프로젝트별 그래프 메모리 오픈 → LLM 코치 준비.
/// 프로젝트는 <c>--project</c> 로 지정하거나 현재 작업 디렉터리 이름을 사용한다.
/// </summary>
public sealed class PdsaSession : IDisposable
{
    public string Project { get; }
    public string DbPath { get; }
    public string Lang { get; }
    public PdsaWorkflow Workflow { get; }
    public PdsaCoach Coach { get; }
    public bool LlmConfigured { get; }
    public string? LlmNote { get; }

    private readonly OpenAiClient? _llm;

    private PdsaSession(string project, string dbPath, string lang, PdsaWorkflow workflow, PdsaCoach coach, OpenAiClient? llm, string? note)
    {
        Project = project;
        DbPath = dbPath;
        Lang = lang;
        Workflow = workflow;
        Coach = coach;
        _llm = llm;
        LlmConfigured = llm is not null;
        LlmNote = note;
    }

    public static PdsaSession Open(string[] args)
    {
        // 우선순위: --project 인자 → 활성 프로젝트(set) → 현재 디렉터리 이름
        var project = PdsaProjectPaths.ResolveProject(ArgUtil.Option(args, "--project"));
        var dbPath = PdsaProjectPaths.GraphDbFor(project);
        var workflow = new PdsaWorkflow(dbPath, project);
        var lang = PdsaLang.Resolve(args);   // 기록/코칭 언어

        OpenAiClient? llm = null;
        string? note = null;
        if (OpenAiConfig.TryLoad(out var options, out var error))
            llm = new OpenAiClient(options);
        else
            note = error;

        return new PdsaSession(project, dbPath, lang, workflow, new PdsaCoach(llm, lang), llm, note);
    }

    public void Dispose()
    {
        _llm?.Dispose();
        Workflow.Dispose();
    }
}
