namespace PdsaCli.Llm;

/// <summary>Plan 코칭 결과: 검증 가능한 기대 평가(성공 기준) + 서술.</summary>
public sealed record PlanCoaching(string Expected, string Narrative);

/// <summary>Study 판정 결과: 판정(met/partial/unmet/unknown) + 실제 측정/근거 + 서술.</summary>
public sealed record StudyJudgment(string Verdict, string Actual, string Narrative);

/// <summary>Act 코칭 결과: 즉시 보강 필요 여부 + 무엇을 보강할지 + 서술(다음 액션).</summary>
public sealed record ActCoaching(bool Reinforce, string What, string Narrative);

/// <summary>
/// PDSA 단계별 LLM 코칭. LLM 이 설정되지 않았으면(<c>null</c>) 비어 있는 결과를 반환해
/// 기록은 계속하되 코칭·판정만 생략한다. 판정(verdict)은 LLM 자동 산출이다.
/// </summary>
public sealed class PdsaCoach(ILlmClient? llm, string lang = "ko")
{
    public bool Enabled => llm is not null;

    private readonly bool _ko = !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

    private const string SystemKo =
        "당신은 데밍(W. Edwards Deming)의 PDSA(Plan-Do-Study-Act) 지속개선 코치입니다. " +
        "간결하고 실천 가능한 한국어 코칭을 제공합니다. 3단계는 'Check(잘 됐나?)'가 아니라 " +
        "'Study(무엇을 배웠나?)' 임을 유지하세요. 요청된 태그 라인(예: '기대평가:', '판정:')은 " +
        "형식을 정확히 지켜 맨 앞줄에 출력하세요.";

    private const string SystemEn =
        "You are a Deming (W. Edwards Deming) PDSA (Plan-Do-Study-Act) continuous-improvement coach. " +
        "Provide concise, actionable coaching in English. Keep the third step as " +
        "'Study (what did we learn?)', not 'Check (did it pass?)'. Emit the requested tag lines " +
        "(e.g. 'Expected:', 'Verdict:') exactly in the required format on the leading line(s).";

    private string SystemPrompt => _ko ? SystemKo : SystemEn;

    /// <summary>
    /// Plan 을 코칭하고 검증 가능한 '기대 평가(성공 기준/측정지표)'를 세운다.
    /// <paramref name="priorLearnings"/> 가 있으면 누적 그래프 메모리(최근 사이클 학습)를 컨텍스트로
    /// 주입해 반복 실수를 피하도록 코칭한다(값이 없으면 기존과 동일하게 동작).
    /// </summary>
    public async Task<PlanCoaching> HypothesisAsync(string plan, string priorLearnings = "", CancellationToken ct = default)
    {
        var prior = string.IsNullOrWhiteSpace(priorLearnings) ? "" : (_ko
            ? "\n\n[과거 학습(최근 사이클 — 반복 실수를 피하도록 참고)]\n" + priorLearnings.Trim()
            : "\n\n[Prior learnings (recent cycles — use to avoid repeating mistakes)]\n" + priorLearnings.Trim());
        var prompt = _ko
            ? "다음은 어떤 작업의 Plan(계획) 입니다. 대개 계획만 세우고 기대 평가를 빠뜨립니다.\n" +
              "출력 형식(반드시 준수):\n" +
              "  첫 줄: `기대평가: <이 사이클이 성공인지 판정할 검증가능한 기준/측정지표를 한 문장으로>`\n" +
              "  이후: 계획 코칭 2~3줄과 '만약 ~한다면 ~가 ~만큼 개선된다' 형태의 가설 1~2개.\n\n[Plan]\n" + plan + prior
            : "The following is a task's Plan. People usually plan but omit the expected outcome.\n" +
              "Output format (must follow):\n" +
              "  First line: `Expected: <one sentence: a verifiable criterion/metric to judge whether this cycle succeeded>`\n" +
              "  Then: 2-3 lines of plan coaching and 1-2 hypotheses shaped as 'If we do X, then Y improves by Z'.\n\n[Plan]\n" + plan + prior;
        var text = await Ask(prompt, ct);
        if (text.Length == 0) return new PlanCoaching("", "");
        return new PlanCoaching(ParseTag(text, "기대평가", "expected"), StripTags(text));
    }

    /// <summary>Plan→Do 를 그래프 엔지니어링 관점으로 정리한다(구조화 필드 없음).</summary>
    public Task<string> OrganizeDoAsync(string plan, string done, CancellationToken ct) => Ask(_ko
        ? "아래 Plan 과 Do(실제 수행한 것)를 그래프 엔지니어링 관점으로 정리하세요.\n" +
          "- 핵심 단계/엔티티/관계를 짧게 구조화(불릿),\n" +
          "- 계획 대비 수행의 차이(gap)를 짚기.\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done
        : "Organize the Plan and Do (what was actually done) below from a graph-engineering view.\n" +
          "- Briefly structure key steps/entities/relations (bullets),\n" +
          "- Point out the gap between plan and execution.\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done, ct);

    /// <summary>Plan 의 기대 평가와 결과를 비교해 판정(met/partial/unmet)하고 학습을 서술한다.</summary>
    /// <param name="measured">
    /// 이 사이클에서 실제로 <b>계측된</b> 값(단계별 지연·시도횟수·토큰). 비어 있지 않으면 판정 프롬프트에
    /// 주입해, Study 가 인상이 아니라 실측 근거 위에서 판정하도록 한다.
    /// </param>
    public async Task<StudyJudgment> JudgeAsync(string expected, string plan, string done, string study,
        CancellationToken ct = default, string measured = "")
    {
        var evidence = string.IsNullOrWhiteSpace(measured) ? "" : (_ko
            ? "\n\n[이 사이클의 실측 계측치 — 판정 근거로 사용]\n" + measured.Trim()
            : "\n\n[Measured telemetry for this cycle — use as evidence]\n" + measured.Trim());
        var prompt = _ko
            ? "아래 한 사이클의 결과를 Study(학습) 관점으로 분석하고, Plan 의 '기대 평가' 대비 달성 여부를 판정하세요.\n" +
              "출력 형식(반드시 준수):\n" +
              "  첫 줄: `판정: met|partial|unmet`  (기대를 완전 충족=met, 부분=partial, 미충족=unmet)\n" +
              "  둘째 줄: `실제: <측정값/근거를 한 문장으로>`\n" +
              "  이후: 무엇을 배웠나 / 가설 지지·기각(근거) / 개선점.\n\n" +
              "[기대 평가]\n" + expected + "\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study 입력]\n" + study + evidence
            : "Analyze the cycle's result from a Study (learning) view and judge attainment vs. the Plan's Expected outcome.\n" +
              "Output format (must follow):\n" +
              "  First line: `Verdict: met|partial|unmet`  (fully met=met, partial=partial, not met=unmet)\n" +
              "  Second line: `Actual: <one sentence of measurement/evidence>`\n" +
              "  Then: what we learned / hypothesis supported·refuted (evidence) / improvements.\n\n" +
              "[Expected]\n" + expected + "\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study input]\n" + study + evidence;
        var text = await Ask(prompt, ct);
        if (text.Length == 0) return new StudyJudgment("", "", "");
        return new StudyJudgment(
            NormalizeVerdict(ParseTag(text, "판정", "verdict")),
            ParseTag(text, "실제", "actual"),
            StripTags(text));
    }

    /// <summary>이번 사이클(P/D/S + 판정)을 바탕으로 다음 Act(개선 액션)와 즉시 보강 필요 여부를 제시한다.</summary>
    public async Task<ActCoaching> NextActionAsync(string plan, string done, string study, string verdict, CancellationToken ct)
    {
        var prompt = _ko
            ? "아래 사이클(P/D/S)과 판정을 바탕으로 다음 개선 액션(Act)을 제시하세요.\n" +
              "출력 형식(반드시 준수):\n" +
              "  첫 줄: `보강: yes|no`  (판정이 met 이 아니거나 당장 손봐야 할 게 있으면 yes)\n" +
              "  둘째 줄: `무엇: <yes 라면 지금 바로 보강할 핵심 한 가지, no 면 빈칸>`\n" +
              "  이후: 다음 사이클 Plan 으로 이어질 개선 액션 1~3개(구체·실행가능).\n\n" +
              "[판정]\n" + verdict + "\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study]\n" + study
            : "Based on the cycle (P/D/S) and verdict below, propose the next improvement action (Act).\n" +
              "Output format (must follow):\n" +
              "  First line: `Reinforce: yes|no`  (yes if the verdict is not met, or something needs fixing now)\n" +
              "  Second line: `What: <if yes, the one key thing to reinforce right now; blank if no>`\n" +
              "  Then: 1-3 concrete, actionable improvement actions to carry into the next cycle's Plan.\n\n" +
              "[Verdict]\n" + verdict + "\n\n[Plan]\n" + plan + "\n\n[Do]\n" + done + "\n\n[Study]\n" + study;
        var text = await Ask(prompt, ct);
        if (text.Length == 0) return new ActCoaching(false, "", "");
        var flag = ParseTag(text, "보강", "reinforce");
        var reinforce = IsAffirmative(flag)
            || (flag.Length == 0 && !string.Equals(verdict, "met", StringComparison.OrdinalIgnoreCase) && verdict.Length > 0);
        return new ActCoaching(reinforce, ParseTag(text, "무엇", "what"), StripTags(text));
    }

    private async Task<string> Ask(string user, CancellationToken ct)
    {
        if (llm is null) return "";
        return await llm.CompleteAsync(SystemPrompt, user, ct);
    }

    // ── 파싱 유틸(순수: 유닛 테스트 대상) ─────────────────────────────────────
    private static readonly string[] KnownTags =
        { "기대평가", "expected", "판정", "verdict", "실제", "actual", "보강", "reinforce", "무엇", "what" };

    /// <summary>여러 태그 후보 중 하나로 시작하는 첫 줄을 찾아 그 값을 반환한다(없으면 "").</summary>
    internal static string ParseTag(string text, params string[] tags)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            foreach (var tag in tags)
                if (line.StartsWith(tag + ":", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith(tag + " :", StringComparison.OrdinalIgnoreCase))
                    return line[(line.IndexOf(':') + 1)..].Trim();
        }
        return "";
    }

    /// <summary>알려진 태그 라인을 제거한 나머지(서술부)를 반환한다.</summary>
    internal static string StripTags(string text)
    {
        var kept = text.Split('\n').Where(raw =>
        {
            var line = raw.Trim();
            return !KnownTags.Any(tag =>
                line.StartsWith(tag + ":", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith(tag + " :", StringComparison.OrdinalIgnoreCase));
        });
        return string.Join('\n', kept).Trim();
    }

    /// <summary>판정 문자열을 met/partial/unmet/unknown 으로 정규화(영/한 허용, 미충족 우선).</summary>
    internal static string NormalizeVerdict(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        if (v.Length == 0) return "";
        if (v.Contains("unmet") || v.Contains("미충족") || v.Contains("미달") || v.Contains("실패")) return "unmet";
        if (v.Contains("partial") || v.Contains("부분")) return "partial";
        if (v.Contains("met") || v.Contains("충족") || v.Contains("달성") || v.Contains("성공")) return "met";
        return "unknown";
    }

    private static bool IsAffirmative(string s)
    {
        var v = s.Trim().ToLowerInvariant();
        return v.StartsWith("yes") || v.StartsWith("y") || v.StartsWith("true")
            || v.StartsWith("필요") || v.StartsWith("예") || v.StartsWith("응");
    }
}
