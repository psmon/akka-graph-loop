namespace PdsaCli.Llm;

/// <summary>
/// PDSA 단계별 LLM 코칭. LLM 이 설정되지 않았으면(<c>null</c>) 빈 문자열을 반환해
/// 기록은 계속하되 코칭만 생략한다.
/// </summary>
public sealed class PdsaCoach(ILlmClient? llm)
{
    public bool Enabled => llm is not null;

    private const string System =
        "당신은 데밍(W. Edwards Deming)의 PDSA(Plan-Do-Study-Act) 지속개선 코치입니다. " +
        "간결하고 실천 가능한 한국어 코칭을 제공합니다. 3단계는 'Check(잘 됐나?)'가 아니라 " +
        "'Study(무엇을 배웠나?)' 임을 유지하세요.";

    /// <summary>Plan 을 코칭하고 검증 가능한 가설을 세운다.</summary>
    public Task<string> HypothesisAsync(string plan, CancellationToken ct) => Ask(
        "다음은 어떤 작업의 Plan(계획) 입니다. 대개 계획만 세우고 가설을 세우지 않습니다.\n" +
        "(1) 계획을 2~3줄로 간단히 코칭하고,\n" +
        "(2) 검증 가능한 '가설'을 '만약 ~한다면, ~가 ~만큼 개선될 것이다' 형태로 1~2개 세워주세요.\n" +
        "(3) 그 가설을 확인할 측정 지표도 제시하세요.\n\n[Plan]\n" + plan, ct);

    /// <summary>Plan→Do 를 그래프 엔지니어링 관점으로 정리한다.</summary>
    public Task<string> OrganizeDoAsync(string plan, string done, CancellationToken ct) => Ask(
        "아래 Plan 과 Do(실제 수행한 것)를 그래프 엔지니어링 관점으로 정리하세요.\n" +
        "- 핵심 단계/엔티티/관계를 짧게 구조화(불릿),\n" +
        "- 계획 대비 수행의 차이(gap)를 짚기.\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done, ct);

    /// <summary>결과를 Study(학습) 관점으로 분석한다.</summary>
    public Task<string> StudyAsync(string plan, string done, string study, CancellationToken ct) => Ask(
        "아래 한 사이클의 결과를 Study(학습) 관점으로 분석하세요.\n" +
        "- 무엇을 배웠나,\n- 가설은 지지/기각되었나(근거),\n- 개선점(무엇을 바꿔야 하나).\n\n" +
        "[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study 입력]\n" + study, ct);

    /// <summary>이번 사이클을 바탕으로 다음 Act(개선 액션)를 제시한다.</summary>
    public Task<string> NextActionAsync(string plan, string done, string study, CancellationToken ct) => Ask(
        "아래 사이클(P/D/S)을 바탕으로 다음에 수행할 개선 액션(Act)을 1~3개, 구체적이고 실행 가능하게 제시하세요.\n" +
        "각 액션은 다음 사이클의 Plan 으로 이어지도록 쓰세요.\n\n" +
        "[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study]\n" + study, ct);

    private async Task<string> Ask(string user, CancellationToken ct)
    {
        if (llm is null) return "";
        return await llm.CompleteAsync(System, user, ct);
    }
}
