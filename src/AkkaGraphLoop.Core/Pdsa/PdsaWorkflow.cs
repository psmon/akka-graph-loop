using System.Globalization;
using AkkaGraphLoop.Core.Kuzu;

namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>PDSA 한 단계(Plan/Do/Study/Act)의 기록.</summary>
public sealed record PdsaPhase(string Kind, string Input, string Llm, string Created);

/// <summary>한 사이클과 그 단계들의 요약(status 출력용).</summary>
public sealed record PdsaCycleView(long Id, string Status, string Started, IReadOnlyList<PdsaPhase> Phases);

/// <summary>
/// 프로젝트별 PDSA 학습 메모리를 Kùzu 그래프로 누적한다.
/// 스키마: <c>(:Project)-[:HAS_CYCLE]->(:Cycle)-[:HAS_PHASE]->(:Phase)</c>, 사이클 간 <c>(:Cycle)-[:NEXT]->(:Cycle)</c>.
/// 사용자 텍스트는 모두 <b>파라미터 바인딩</b>으로 저장한다(이스케이프/인젝션 걱정 없음, 개행 보존).
/// 사용할수록 그래프가 쌓여 "AI 에이전트를 위한 진보된 메모리"가 된다.
/// </summary>
public sealed class PdsaWorkflow : IDisposable
{
    public const string PlanKind = "plan";
    public const string DoKind = "do";
    public const string StudyKind = "study";
    public const string ActKind = "act";

    private readonly KuzuGraph _g;
    public string ProjectId { get; }
    public string DatabasePath { get; }

    public PdsaWorkflow(string databasePath, string projectId)
    {
        DatabasePath = databasePath;
        ProjectId = projectId;

        var parent = Directory.GetParent(databasePath);
        if (parent is not null) Directory.CreateDirectory(parent.FullName);

        _g = new KuzuGraph(databasePath);
        EnsureSchema();
        EnsureProject(projectId);
    }

    private void EnsureSchema()
    {
        _g.Execute("CREATE NODE TABLE IF NOT EXISTS Project(id STRING, name STRING, created STRING, PRIMARY KEY(id))");
        _g.Execute("CREATE NODE TABLE IF NOT EXISTS Cycle(id INT64, started STRING, status STRING, PRIMARY KEY(id))");
        _g.Execute("CREATE NODE TABLE IF NOT EXISTS Phase(id STRING, cycle INT64, kind STRING, input STRING, llm STRING, created STRING, PRIMARY KEY(id))");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS HAS_CYCLE(FROM Project TO Cycle)");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS HAS_PHASE(FROM Cycle TO Phase)");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS NEXT(FROM Cycle TO Cycle)");
    }

    private void EnsureProject(string projectId)
    {
        var rows = _g.Query("MATCH (p:Project {id: $id}) RETURN count(p)", 1, P(("id", projectId)));
        if (rows.Count > 0 && rows[0][0] != "0") return;
        _g.Execute("CREATE (:Project {id: $id, name: $name, created: $created})",
            P(("id", projectId), ("name", projectId), ("created", Now())));
    }

    /// <summary>새 사이클을 시작한다(Plan 단계에서 호출). 직전 사이클과 NEXT 로 연결.</summary>
    public long StartCycle()
    {
        var id = NextCycleId();
        _g.Execute("CREATE (:Cycle {id: $id, started: $started, status: 'planning'})",
            P(("id", id), ("started", Now())));
        _g.Execute("MATCH (p:Project {id: $pid}), (c:Cycle {id: $id}) CREATE (p)-[:HAS_CYCLE]->(c)",
            P(("pid", ProjectId), ("id", id)));
        if (id > 1)
            _g.Execute("MATCH (a:Cycle {id: $a}), (b:Cycle {id: $b}) CREATE (a)-[:NEXT]->(b)",
                P(("a", id - 1), ("b", id)));
        return id;
    }

    /// <summary>가장 최근(진행 중) 사이클. 없으면 null.</summary>
    public (long Id, string Status)? CurrentCycle()
    {
        var rows = _g.Query("MATCH (c:Cycle) RETURN c.id, c.status ORDER BY c.id DESC LIMIT 1", 2);
        if (rows.Count == 0) return null;
        return (long.Parse(rows[0][0], CultureInfo.InvariantCulture), rows[0][1]);
    }

    /// <summary>한 단계를 기록하고 사이클 상태를 갱신한다.</summary>
    public void RecordPhase(long cycleId, string kind, string input, string llm)
    {
        var phaseId = $"{cycleId}-{kind}";
        _g.Execute(
            "CREATE (:Phase {id: $id, cycle: $cycle, kind: $kind, input: $input, llm: $llm, created: $created})",
            P(("id", phaseId), ("cycle", cycleId), ("kind", kind), ("input", input), ("llm", llm), ("created", Now())));
        _g.Execute("MATCH (c:Cycle {id: $cid}), (p:Phase {id: $pid}) CREATE (c)-[:HAS_PHASE]->(p)",
            P(("cid", cycleId), ("pid", phaseId)));
        _g.Execute("MATCH (c:Cycle {id: $cid}) SET c.status = $st",
            P(("cid", cycleId), ("st", StatusAfter(kind))));
    }

    /// <summary>한 사이클의 특정 단계 텍스트(input/llm)를 읽는다.</summary>
    public PdsaPhase? GetPhase(long cycleId, string kind)
    {
        var rows = _g.Query("MATCH (p:Phase {id: $id}) RETURN p.kind, p.input, p.llm, p.created", 4,
            P(("id", $"{cycleId}-{kind}")));
        if (rows.Count == 0) return null;
        return new PdsaPhase(rows[0][0], rows[0][1], rows[0][2], rows[0][3]);
    }

    /// <summary>최근 사이클들과 각 단계를 요약해 반환(status 출력용).</summary>
    public IReadOnlyList<PdsaCycleView> Recent(int limit = 5)
    {
        var cycleRows = _g.Query(
            "MATCH (c:Cycle) RETURN c.id, c.status, c.started ORDER BY c.id DESC LIMIT $lim", 3,
            P(("lim", (long)limit)));
        var views = new List<PdsaCycleView>();
        foreach (var c in cycleRows)
        {
            var id = long.Parse(c[0], CultureInfo.InvariantCulture);
            var phases = _g.Query(
                    "MATCH (:Cycle {id: $id})-[:HAS_PHASE]->(p:Phase) RETURN p.kind, p.input, p.llm, p.created", 4,
                    P(("id", id)))
                .Select(r => new PdsaPhase(r[0], r[1], r[2], r[3]))
                .OrderBy(p => KindOrder(p.Kind))
                .ToList();
            views.Add(new PdsaCycleView(id, c[1], c[2], phases));
        }
        return views;
    }

    public int CycleCount()
    {
        var rows = _g.Query("MATCH (c:Cycle) RETURN count(c)", 1);
        return rows.Count > 0 && int.TryParse(rows[0][0], out var n) ? n : 0;
    }

    private long NextCycleId()
    {
        var rows = _g.Query("MATCH (c:Cycle) RETURN max(c.id)", 1);
        if (rows.Count == 0 || string.IsNullOrEmpty(rows[0][0]))
            return 1;
        return long.TryParse(rows[0][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) ? max + 1 : 1;
    }

    private static string StatusAfter(string kind) => kind switch
    {
        PlanKind => "planned",
        DoKind => "did",
        StudyKind => "studied",
        ActKind => "acted",
        _ => "unknown",
    };

    private static int KindOrder(string kind) => kind switch
    {
        PlanKind => 0, DoKind => 1, StudyKind => 2, ActKind => 3, _ => 9,
    };

    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static Dictionary<string, object> P(params (string Name, object Value)[] items)
    {
        var d = new Dictionary<string, object>(items.Length);
        foreach (var (name, value) in items) d[name] = value;
        return d;
    }

    public void Dispose() => _g.Dispose();
}
