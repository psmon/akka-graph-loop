using System.Text;
using AkkaGraphLoop.Core.Pdsa;

namespace PdsaCli.Cli;

/// <summary>
/// recall 학습을 plan 코칭 프롬프트에 주입할 압축 블록으로 포맷한다.
/// 사이클당 각 필드를 상한(<paramref name="perFieldCap"/>)으로 잘라 프롬프트 팽창·LLM 타임아웃을 막는다.
/// 라벨은 언어 중립(영문 짧은 키)이라 KO/EN 코치가 모두 파싱할 수 있다.
/// </summary>
internal static class LearningFormat
{
    public static string ToPromptBlock(IReadOnlyList<PdsaLearning> learnings, int perFieldCap = 240)
    {
        if (learnings.Count == 0) return "";
        var sb = new StringBuilder();
        foreach (var l in learnings)
        {
            sb.Append('#').Append(l.Cycle);
            if (l.Verdict.Length > 0) sb.Append(" [").Append(l.Verdict).Append(']');
            sb.Append('\n');
            if (l.Expected.Length > 0) sb.Append("  expected: ").Append(Cap(l.Expected, perFieldCap)).Append('\n');
            if (l.Actual.Length > 0) sb.Append("  actual: ").Append(Cap(l.Actual, perFieldCap)).Append('\n');
            var learned = l.Study.Length > 0 ? l.Study : l.Act;
            if (learned.Length > 0) sb.Append("  learned: ").Append(Cap(learned, perFieldCap)).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    private static string Cap(string s, int cap)
    {
        var t = s.ReplaceLineEndings(" ").Trim();
        return t.Length <= cap ? t : t[..cap] + "…";
    }
}
