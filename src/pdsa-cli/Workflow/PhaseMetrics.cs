using System.Diagnostics;
using System.Globalization;
using AkkaGraphLoop.Core.Pdsa;
using PdsaCli.Llm;

namespace PdsaCli.Workflow;

/// <summary>
/// 한 단계(Plan/Do/Study/Act)의 LLM 호출 계측을 모아 <see cref="PdsaWorkflow.RecordPhase"/> 의
/// extra 딕셔너리로 넘길 형태로 만든다.
///
/// <para><b>왜</b>: Study 가 "됐다/안 됐다"를 인상으로 판정하면 사이클이 쌓여도 학습이 되지 않는다.
/// 지연·시도횟수·모델·토큰을 그래프에 남겨야 판정이 실측 근거 위에 선다.</para>
///
/// <para>지연 시간은 클라이언트 종류와 무관하게 항상 측정한다. 토큰·시도횟수는 구현체가
/// <see cref="ILlmUsageReporter"/> 를 구현할 때만 채워진다(그 외에는 생략 — 없는 값을 지어내지 않는다).</para>
///
/// 사용법: LLM 호출 <b>직전</b>에 생성하고, 호출 성공 후 <see cref="Collect"/> 를 부른다.
/// </summary>
public sealed class PhaseMetrics(ILlmClient? llm)
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    /// <summary>계측치를 <paramref name="into"/> 에 합쳐 반환한다(없는 값은 넣지 않는다).</summary>
    public Dictionary<string, string> Collect(Dictionary<string, string>? into = null)
    {
        _sw.Stop();
        var extra = into ?? new Dictionary<string, string>();
        if (llm is null) return extra;   // LLM 미설정: 기록만 하는 모드 — 계측할 호출이 없다

        extra[PdsaWorkflow.LatencyMsKey] = _sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);

        if (llm is not ILlmUsageReporter { LastCall: { } stats }) return extra;

        extra[PdsaWorkflow.AttemptsKey] = stats.Attempts.ToString(CultureInfo.InvariantCulture);
        extra[PdsaWorkflow.ModelKey] = stats.Model;
        if (stats.PromptTokens > 0)
            extra[PdsaWorkflow.PromptTokensKey] = stats.PromptTokens.ToString(CultureInfo.InvariantCulture);
        if (stats.CompletionTokens > 0)
            extra[PdsaWorkflow.CompletionTokensKey] = stats.CompletionTokens.ToString(CultureInfo.InvariantCulture);
        return extra;
    }
}
