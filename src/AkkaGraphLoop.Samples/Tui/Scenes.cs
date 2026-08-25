using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;
using static AkkaGraphLoop.Samples.Tui.SceneDraw;

namespace AkkaGraphLoop.Samples.Tui;

/// <summary>
/// 튜토리얼 장면 목록. 각 <see cref="Scene.Run"/> 은 실제 Akka 그래프를 구성하되,
/// junction 사이에 <see cref="Pacer.Tap{T}"/> 를 끼워 흐름을 5초 단위로 늦추고 TUI 에 상태를 알린다.
/// Sink 는 실제 <c>Sink.ForEach</c> 로, 받은 값이 그대로 TUI 출력 로그가 된다.
/// </summary>
public static class Scenes
{
    private static Sink<T, Task<Done>> Log<T>(Pacer p, Func<T, string> fmt)
        => Sink.ForEach<T>(v => p.SinkReceived(fmt(v)));

    public static IReadOnlyList<Scene> All => new[]
    {
        BroadcastMerge(),
        Balance(),
        Unzip(),
        Zip(),
        ZipWithMax(),
        Concat(),
        PickMaxOfThree(),
        MergePreferredCycle(),
        BufferDropHeadCycle(),
        BalancedZipWithCycle(),
    };

    // ── 1. Broadcast + Merge ─────────────────────────────────────────────
    private static Scene BroadcastMerge() => new(
        Title: "Broadcast + Merge",
        Category: "기본 · fan-out+fan-in",
        Tutorial: new[]
        {
            "Broadcast: 입력 하나를 여러 출력으로 복제한다.",
            "Merge: 여러 입력을 하나의 출력으로 공정하게 합친다.",
            "여기선 원소를 두 갈래로 복제해 각각 +100/+1000 한 뒤 다시 합친다.",
        },
        Diagram: p => new[]
        {
            $"{Node(p, "SOURCE", "SOURCE")} ─▶ [BROADCAST] ─┬─▶ {Node(p, "BROADCAST→A", "+100")} ─┐",
            $"                              └─▶ {Node(p, "BROADCAST→B", "+1000")} ┴─▶ {Node(p, "MERGE", "MERGE")} ─▶ [SINK]",
        },
        Run: (p, mat) =>
        {
            var src = Source.From(new[] { 1, 2 });
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var bcast = b.Add(new Broadcast<int>(2));
                var merge = b.Add(new Merge<int>(2));
                var outFlow = b.Add(p.Tap<int>("MERGE"));

                b.From(src).Via(p.Tap<int>("SOURCE")).To(bcast.In);
                b.From(bcast.Out(0)).Via(Flow.Create<int>().Select(x => x + 100)).Via(p.Tap<int>("BROADCAST→A")).To(merge.In(0));
                b.From(bcast.Out(1)).Via(Flow.Create<int>().Select(x => x + 1000)).Via(p.Tap<int>("BROADCAST→B")).To(merge.In(1));
                b.From(merge.Out).To(outFlow.Inlet);
                return new SourceShape<int>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<int>(p, v => v.ToString()), mat);
        });

    // ── 2. Balance ───────────────────────────────────────────────────────
    private static Scene Balance() => new(
        Title: "Balance",
        Category: "fan-out",
        Tutorial: new[]
        {
            "Balance: 입력을 '가용한 출력 하나'로만 흘려 여러 워커에 부하를 분산한다.",
            "Broadcast(복제)와 달리 각 원소는 한 워커만 처리한다.",
        },
        Diagram: p => new[]
        {
            $"{Node(p, "SOURCE", "SOURCE")} ─▶ [BALANCE] ─┬─▶ {Node(p, "BALANCE→W0", "W0")} ─┐",
            $"                            ├─▶ {Node(p, "BALANCE→W1", "W1")} ┤",
            $"                            └─▶ {Node(p, "BALANCE→W2", "W2")} ┴─▶ {Node(p, "MERGE", "MERGE")} ─▶ [SINK]",
        },
        Run: (p, mat) =>
        {
            var src = Source.From(Enumerable.Range(1, 3));
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var balance = b.Add(new Balance<int>(3));
                var merge = b.Add(new Merge<string>(3));
                var outFlow = b.Add(p.Tap<string>("MERGE"));

                b.From(src).Via(p.Tap<int>("SOURCE")).To(balance.In);
                for (var i = 0; i < 3; i++)
                {
                    var w = i;
                    b.From(balance.Out(i))
                        .Via(Flow.Create<int>().Select(x => $"W{w}:{x}"))
                        .Via(p.Tap<string>($"BALANCE→W{w}"))
                        .To(merge.In(i));
                }
                b.From(merge.Out).To(outFlow.Inlet);
                return new SourceShape<string>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<string>(p, v => v), mat);
        });

    // ── 3. UnZip ─────────────────────────────────────────────────────────
    private static Scene Unzip() => new(
        Title: "UnZip",
        Category: "fan-out",
        Tutorial: new[]
        {
            "UnZip: KeyValuePair<K,V> 스트림을 K 스트림과 V 스트림으로 분리한다.",
            "분리한 두 갈래를 각각 가공한 뒤 다시 Merge 로 합쳐 출력한다.",
        },
        Diagram: p => new[]
        {
            $"[SOURCE (n,s)] ─▶ [UNZIP] ─┬─▶ {Node(p, "UNZIP→num", "num:*")} ─┐",
            $"                           └─▶ {Node(p, "UNZIP→str", "str:*")} ┴─▶ {Node(p, "MERGE", "MERGE")} ─▶ [SINK]",
        },
        Run: (p, mat) =>
        {
            var pairs = Source.From(new[]
            {
                new KeyValuePair<int, string>(1, "a"),
                new KeyValuePair<int, string>(2, "b"),
            });
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var unzip = b.Add(new UnZip<int, string>());
                var merge = b.Add(new Merge<string>(2));
                var outFlow = b.Add(p.Tap<string>("MERGE"));

                b.From(pairs).To(unzip.In);
                b.From(unzip.Out0).Via(Flow.Create<int>().Select(x => $"num:{x}")).Via(p.Tap<string>("UNZIP→num")).To(merge.In(0));
                b.From(unzip.Out1).Via(Flow.Create<string>().Select(s => $"str:{s}")).Via(p.Tap<string>("UNZIP→str")).To(merge.In(1));
                b.From(merge.Out).To(outFlow.Inlet);
                return new SourceShape<string>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<string>(p, v => v), mat);
        });

    // ── 4. Zip ───────────────────────────────────────────────────────────
    private static Scene Zip() => new(
        Title: "Zip",
        Category: "fan-in",
        Tutorial: new[]
        {
            "Zip: 두 스트림을 위치별로 (a, b) 쌍으로 결합한다.",
            "두 입력이 '모두' 도착해야 한 쌍을 방출한다(동기화 지점).",
        },
        Diagram: p => new[]
        {
            "[SOURCE 1,2,3] ─┐",
            $"                ├─▶ {Node(p, "ZIP", "ZIP")} ─▶ [SINK]",
            "[SOURCE a,b,c] ─┘",
        },
        Run: (p, mat) =>
        {
            var nums = Source.From(new[] { 1, 2, 3 });
            var lets = Source.From(new[] { "a", "b", "c" });
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var zip = b.Add(new Zip<int, string>());
                var outFlow = b.Add(p.Tap<(int, string)>("ZIP", t => $"{t.Item1}{t.Item2}"));
                b.From(nums).To(zip.In0);
                b.From(lets).To(zip.In1);
                b.From(zip.Out).To(outFlow.Inlet);
                return new SourceShape<(int, string)>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<(int, string)>(p, t => $"{t.Item1}{t.Item2}"), mat);
        });

    // ── 5. ZipWith (Max) ─────────────────────────────────────────────────
    private static Scene ZipWithMax() => new(
        Title: "ZipWith (Max)",
        Category: "fan-in",
        Tutorial: new[]
        {
            "ZipWith: 여러 입력을 '함수'로 결합한다(여기선 원소별 최댓값).",
            "[1,9,3] 과 [5,2,8] → [5,9,8].",
        },
        Diagram: p => new[]
        {
            "[SOURCE 1,9,3] ─┐",
            $"                ├─▶ {Node(p, "ZIPWITH", "max(a,b)")} ─▶ [SINK]",
            "[SOURCE 5,2,8] ─┘",
        },
        Run: (p, mat) =>
        {
            var left = Source.From(new[] { 1, 9, 3 });
            var right = Source.From(new[] { 5, 2, 8 });
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var zipWith = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
                var outFlow = b.Add(p.Tap<int>("ZIPWITH"));
                b.From(left).To(zipWith.In0);
                b.From(right).To(zipWith.In1);
                b.From(zipWith.Out).To(outFlow.Inlet);
                return new SourceShape<int>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<int>(p, v => v.ToString()), mat);
        });

    // ── 6. Concat ────────────────────────────────────────────────────────
    private static Scene Concat() => new(
        Title: "Concat",
        Category: "fan-in",
        Tutorial: new[]
        {
            "Concat: 첫 스트림을 '모두' 흘린 뒤 다음 스트림을 이어붙인다(순차 결합).",
            "[1,2,3] 다음에 [10,20] → [1,2,3,10,20].",
        },
        Diagram: p => new[]
        {
            "[SOURCE 1,2,3] ─┐(먼저)",
            $"                ├─▶ {Node(p, "CONCAT", "CONCAT")} ─▶ [SINK]",
            "[SOURCE 10,20] ─┘(다음)",
        },
        Run: (p, mat) =>
        {
            var first = Source.From(new[] { 1, 2, 3 });
            var second = Source.From(new[] { 10, 20 });
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var concat = b.Add(new Concat<int, int>());
                var outFlow = b.Add(p.Tap<int>("CONCAT"));
                b.From(first).To(concat.In(0));
                b.From(second).To(concat.In(1));
                b.From(concat.Out).To(outFlow.Inlet);
                return new SourceShape<int>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<int>(p, v => v.ToString()), mat);
        });

    // ── 7. PickMaxOfThree (partial graph) ────────────────────────────────
    private static Scene PickMaxOfThree() => new(
        Title: "PickMaxOfThree",
        Category: "partial graph",
        Tutorial: new[]
        {
            "부분 그래프: ZipWith(Max) 두 개를 연결해 3입력 최댓값 컴포넌트를 만든다.",
            "UniformFanInShape 로 캡슐화하면 재사용 가능한 junction 이 된다. max(1,2,3)=3.",
        },
        Diagram: p => new[]
        {
            "[1] ─┐",
            $"      ├─▶ {Node(p, "MAX(1,2)", "max")} ─┐",
            "[2] ─┘              │",
            $"                    ├─▶ {Node(p, "MAX(·,3)", "max")} ─▶ [SINK]",
            "[3] ────────────────┘",
        },
        Run: (p, mat) =>
        {
            var graph = Source.FromGraph(GraphDsl.Create(b =>
            {
                var z1 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
                var z2 = b.Add(ZipWith.Apply<int, int, int>((a, c) => Math.Max(a, c)));
                var outFlow = b.Add(p.Tap<int>("MAX(·,3)"));

                b.From(Source.Single(1)).To(z1.In0);
                b.From(Source.Single(2)).To(z1.In1);
                b.From(z1.Out).Via(p.Tap<int>("MAX(1,2)")).To(z2.In0);
                b.From(Source.Single(3)).To(z2.In1);
                b.From(z2.Out).To(outFlow.Inlet);
                return new SourceShape<int>(outFlow.Outlet);
            }));
            return graph.RunWith(Log<int>(p, v => v.ToString()), mat);
        });

    // ── 8. Cycle: MergePreferred ─────────────────────────────────────────
    private static Scene MergePreferredCycle() => new(
        Title: "Cycle · MergePreferred",
        Category: "cycle / loop",
        Tutorial: new[]
        {
            "사이클(피드백 loop): 출력의 일부를 다시 입력으로 되먹인다.",
            "MergePreferred 의 '우선 포트'로 피드백(+1)을 넣어 루프가 멈추지 않게 한다.",
            "seed=1 에서 시작해 1,2,3… 으로 세며 올라간다(데드락 없이 liveness 확보).",
        },
        Diagram: p => new[]
        {
            $"[seed 1] ─▶ (MergePreferred) ─▶ {Node(p, "LOOP", "loop")} ─▶ [BROADCAST] ─▶ [SINK]",
            $"                 ▲                                    │",
            $"                 └──── {Node(p, "FEEDBACK", "+1 피드백")} ◀─────────────────┘",
        },
        Run: (p, mat) => RunCycle(p, mat, useMergePreferred: true, useBuffer: false, balanced: false));

    // ── 9. Cycle: Buffer(DropHead) ───────────────────────────────────────
    private static Scene BufferDropHeadCycle() => new(
        Title: "Cycle · Buffer(DropHead)",
        Category: "cycle / loop",
        Tutorial: new[]
        {
            "Merge 사이클 + 피드백 경로에 Buffer(n, DropHead).",
            "버퍼가 넘치면 '오래된' 원소를 버려(fair) 데드락을 피한다.",
            "boundedness ↔ 완전성 트레이드오프: liveness 를 위해 일부 원소를 희생.",
        },
        Diagram: p => new[]
        {
            $"[seed 1] ─▶ (Merge) ─▶ {Node(p, "LOOP", "loop")} ─▶ [BROADCAST] ─▶ [SINK]",
            $"              ▲                                 │",
            $"              └── {Node(p, "FEEDBACK", "+1 · Buffer(DropHead)")} ◀────────┘",
        },
        Run: (p, mat) => RunCycle(p, mat, useMergePreferred: false, useBuffer: true, balanced: false));

    // ── 10. Cycle: Balanced ZipWith ──────────────────────────────────────
    private static Scene BalancedZipWithCycle() => new(
        Title: "Cycle · Balanced ZipWith",
        Category: "cycle / loop",
        Tutorial: new[]
        {
            "ZipWith 는 두 입력이 모두 있어야 방출 → 입출력이 1:1 로 '균형' 잡힌다.",
            "Source.Single(0) 을 Concat 앞에 두어 초기 원소를 주입(닭-달걀 문제 해결).",
            "Concat 에 .Async() 로 버퍼를 둬 부트스트랩 데드락을 방지. 0,1,2… 로 센다.",
        },
        Diagram: p => new[]
        {
            $"[driver] ─▶ (ZipWith Keep.Right) ─▶ {Node(p, "LOOP", "loop")} ─▶ [BROADCAST] ─▶ [SINK]",
            $"                 ▲                                        │",
            $"      [start 0] ─▶ (Concat.Async) ◀─ {Node(p, "FEEDBACK", "+1")} ◀───────────┘",
        },
        Run: (p, mat) => RunCycle(p, mat, useMergePreferred: false, useBuffer: false, balanced: true));

    private const int CycleTake = 4;

    private static Task RunCycle(Pacer p, IMaterializer mat, bool useMergePreferred, bool useBuffer, bool balanced)
    {
        var graph = Source.FromGraph(GraphDsl.Create(b =>
        {
            var broadcast = b.Add(new Broadcast<int>(2));
            var limit = Flow.Create<int>().Take(CycleTake);
            var loopTap = b.Add(p.Tap<int>("LOOP"));

            if (balanced)
            {
                var driver = Source.From(Enumerable.Range(1, CycleTake));
                var start = Source.Single(0);
                var zip = b.Add(ZipWith.Apply<int, int, int>((_, fed) => fed));
                var concat = b.Add(new Concat<int, int>().Async());
                var feedback = Flow.Create<int>().Select(x => x + 1);

                b.From(driver).To(zip.In0);
                b.From(zip.Out).Via(limit).To(broadcast.In);
                b.From(broadcast.Out(0)).To(loopTap.Inlet);
                b.From(start).To(concat.In(0));
                b.From(broadcast.Out(1)).Via(feedback).Via(p.Tap<int>("FEEDBACK")).To(concat.In(1));
                b.From(concat.Out).To(zip.In1);
            }
            else if (useMergePreferred)
            {
                var merge = b.Add(new MergePreferred<int>(1));
                var feedback = Flow.Create<int>().Select(x => x + 1);
                b.From(Source.Single(1)).To(merge.In(0));
                b.From(merge.Out).Via(limit).To(broadcast.In);
                b.From(broadcast.Out(0)).To(loopTap.Inlet);
                b.From(broadcast.Out(1)).Via(feedback).Via(p.Tap<int>("FEEDBACK")).To(merge.Preferred);
            }
            else // useBuffer
            {
                var merge = b.Add(new Merge<int>(2));
                var feedback = Flow.Create<int>().Select(x => x + 1).Buffer(10, OverflowStrategy.DropHead);
                b.From(Source.Single(1)).To(merge.In(0));
                b.From(merge.Out).Via(limit).To(broadcast.In);
                b.From(broadcast.Out(0)).To(loopTap.Inlet);
                b.From(broadcast.Out(1)).Via(feedback).Via(p.Tap<int>("FEEDBACK")).To(merge.In(1));
            }

            return new SourceShape<int>(loopTap.Outlet);
        }));
        return graph.RunWith(Log<int>(p, v => v.ToString()), mat);
    }
}
