using System.Collections.Immutable;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace AkkaGraphLoop.Samples.Pdsa;

/// <summary>PDSA 사이클을 한 바퀴 도는 개선 상태.</summary>
/// <param name="Iteration">사이클 회차(1부터).</param>
/// <param name="Quality">현재 품질 점수(0~100).</param>
/// <param name="Target">목표 품질 점수.</param>
public sealed record PdsaState(int Iteration, double Quality, double Target)
{
    public bool Converged => Quality >= Target;
}

/// <summary>
/// 데밍(W. Edwards Deming)의 <b>PDSA</b>(Plan–Do–Study–Act) 지속개선 루프를
/// 실제 Akka.Streams <b>피드백 사이클 그래프</b>로 구현한 독립 샘플.
///
/// <code>
///  seed ─▶ (MergePreferred) ─▶ Plan ─▶ Do ─▶ Study ─▶ Act ─▶ TakeWhile(미달) ─▶ Broadcast ─▶ Sink
///               ▲                                                                      │
///               └────────────────── (다음 회차 준비) ◀── Feedback ◀────────────────────┘
/// </code>
///
/// - Act 의 결과가 다음 Plan 으로 <b>되먹여지는</b> 것이 PDSA 의 핵심(= 그래프 사이클).
/// - MergePreferred 의 우선 포트로 피드백을 넣어 데드락 없이 루프가 흐른다(liveness).
/// - 품질이 목표에 도달하면 <c>TakeWhile(inclusive)</c> 이 수렴 원소까지 방출한 뒤 루프를 종료한다.
///
/// 참고: 데밍은 PDCA 의 'Check'(잘 됐나? 라는 이분법적 통제)보다,
/// 'Study'(무엇을 배웠나? 라는 분석적 성찰)를 강조해 PDSA 를 선호했다.
/// </summary>
public static class PdsaLoop
{
    public static Task<IImmutableList<PdsaState>> Run(
        IMaterializer mat,
        double start = 45,
        double target = 90,
        Action<string>? log = null)
    {
        log ??= Console.WriteLine;
        var seed = Source.Single(new PdsaState(1, start, target));

        // 개선량: 목표에 가까울수록 줄어드는 '수확 체감', 단 최소 이득을 보장해 유한 회차에 수렴.
        double Gain(double q) => Math.Max(6.0, (target - q) * 0.45);

        var plan = Flow.Create<PdsaState>().Select(s =>
        {
            log($"");
            log($"━━━ PDSA 사이클 #{s.Iteration} ━━━  (현재 품질 {s.Quality:0.0} / 목표 {s.Target:0.0})");
            log($"  [Plan ] 목표까지 {s.Target - s.Quality:0.0} 만큼 격차. 개선 가설을 세우고 변화안·측정법을 계획.");
            return s;
        });

        var @do = Flow.Create<PdsaState>().Select(s =>
        {
            log($"  [Do   ] 소규모로 변화안을 실행하고 데이터를 수집 (예상 개선 +{Gain(s.Quality):0.0}).");
            return s;
        });

        var study = Flow.Create<PdsaState>().Select(s =>
        {
            var improved = Math.Min(s.Target, Math.Round(s.Quality + Gain(s.Quality), 1));
            log($"  [Study] 결과 분석: 품질 {s.Quality:0.0} → {improved:0.0}. '무엇을 배웠나'(변동 원인)를 이해.");
            return s with { Quality = improved };
        });

        var act = Flow.Create<PdsaState>().Select(s =>
        {
            if (s.Converged)
                log($"  [Act  ] 목표 달성 ({s.Quality:0.0} ≥ {s.Target:0.0}) ✔ 표준화(채택) 후 사이클 종료(수렴).");
            else
                log($"  [Act  ] 개선을 표준화하고, 남은 격차를 다음 Plan 으로 피드백 ▷ 루프 계속.");
            return s;
        });

        // 다음 회차 준비: 회차 번호만 증가시켜 되먹인다(품질은 이미 Study 에서 갱신됨).
        var feedback = Flow.Create<PdsaState>().Select(s => s with { Iteration = s.Iteration + 1 });

        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var merge = b.Add(new MergePreferred<PdsaState>(1)); // secondary 1 + preferred 1
            var broadcast = b.Add(new Broadcast<PdsaState>(2));
            var outFlow = b.Add(Flow.Create<PdsaState>());

            b.From(seed).To(merge.In(0)); // 초기 상태 -> secondary
            b.From(merge.Out)
                .Via(plan).Via(@do).Via(study).Via(act)
                .Via(Flow.Create<PdsaState>().TakeWhile(s => !s.Converged, inclusive: true)) // 수렴 원소까지 방출 후 종료
                .To(broadcast.In);

            b.From(broadcast.Out(0)).To(outFlow.Inlet);                 // 수집(Sink)
            b.From(broadcast.Out(1)).Via(feedback).To(merge.Preferred); // 피드백 -> preferred
            return new SourceShape<PdsaState>(outFlow.Outlet);
        }));

        return graph.RunWith(Sink.Seq<PdsaState>(), mat);
    }

    /// <summary>콘솔 실행 진입점: 소개 → 사이클 로그 → 요약.</summary>
    public static async Task RunConsole(IMaterializer mat)
    {
        Console.WriteLine("┌───────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  PDSA 루프 (Deming) — Akka.Streams 피드백 사이클로 구현      │");
        Console.WriteLine("│  Plan → Do → Study → Act → (Act 결과를 다음 Plan 으로 피드백) │");
        Console.WriteLine("└───────────────────────────────────────────────────────────┘");

        var history = await Run(mat);

        var final = history[^1];
        Console.WriteLine("");
        Console.WriteLine($"■ 수렴 완료: 총 {history.Count}회 사이클, 최종 품질 {final.Quality:0.0} (목표 {final.Target:0.0}).");
        Console.WriteLine($"■ 데밍 포인트: 3단계는 'Check(잘 됐나?)'가 아니라 'Study(무엇을 배웠나?)' — 지속적 학습이 루프를 굴린다.");
    }
}
