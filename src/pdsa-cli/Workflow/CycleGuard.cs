namespace PdsaCli.Workflow;

/// <summary>
/// 사이클 상태에 대한 공통 가드 메시지.
///
/// <para>Plan 이 없는 사이클(= 고아)에 Do/Study/Act 가 붙으면, LLM 은 "Plan: 입력 없음" 상태로
/// 그대로 진행하고 기대 평가가 비어 Study 판정이 불가능해진다. 그 사이클은 기대 충족률의 분모와
/// 되읽기(recall) 품질까지 오염시키므로, 조용히 진행하지 말고 멈춰서 알린다.</para>
///
/// <para>현재 버전은 고아를 <b>만들지 않지만</b>(<c>StartCycleWithPlan</c>), 이전 버전이 만들어 둔
/// 고아가 남아 있는 DB 가 있으므로 이 가드는 계속 필요하다.</para>
/// </summary>
public static class CycleGuard
{
    public static string OrphanMessage(long cycleId) =>
        $"사이클 #{cycleId} 에 Plan 이 없습니다(이전 버전에서 LLM 호출 실패로 남은 빈 사이클).\n" +
        $"  `pdsa plan \"<계획>\"` 으로 계획을 먼저 입력하세요. 이 빈 사이클을 건너뛰려면 `--fresh` 를 함께 쓰세요.";
}
