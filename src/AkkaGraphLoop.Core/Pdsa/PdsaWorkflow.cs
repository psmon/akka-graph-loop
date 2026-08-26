using System.Globalization;
using AkkaGraphLoop.Core.Kuzu;

namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>
/// PDSA 한 단계(Plan/Do/Study/Act)의 기록. 폐루프 필드:
/// Expected(Plan 기대 평가), Verdict/Actual(Study 판정·실제), Reinforce(Act 보강 필요 여부).
/// </summary>
public sealed record PdsaPhase(
    string Kind, string Input, string Llm, string Created,
    string Expected = "", string Verdict = "", string Actual = "", string Reinforce = "");

/// <summary>한 사이클과 그 단계들의 요약(status 출력용). Verdict = 그 사이클 Study 판정.</summary>
public sealed record PdsaCycleView(long Id, string Status, string Started, IReadOnlyList<PdsaPhase> Phases, string Verdict = "");

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

    /// <summary>Phase 노드에 SET 가능한 폐루프 메타 컬럼 화이트리스트.</summary>
    private static readonly string[] MetaColumns = { "expected", "verdict", "actual", "reinforce" };

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
        // 신규 DB 는 폐루프 컬럼을 포함해 생성. 기존 DB 는 아래 마이그레이션이 ALTER 로 채운다.
        _g.Execute("CREATE NODE TABLE IF NOT EXISTS Phase(id STRING, cycle INT64, kind STRING, input STRING, llm STRING, created STRING, " +
                   "expected STRING DEFAULT '', verdict STRING DEFAULT '', actual STRING DEFAULT '', reinforce STRING DEFAULT '', PRIMARY KEY(id))");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS HAS_CYCLE(FROM Project TO Cycle)");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS HAS_PHASE(FROM Cycle TO Phase)");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS NEXT(FROM Cycle TO Cycle)");
        _g.Execute("CREATE REL TABLE IF NOT EXISTS REINFORCES(FROM Cycle TO Cycle)");
        MigratePhaseColumns();
    }

    /// <summary>기존 스키마 DB 에 폐루프 컬럼이 없으면 멱등하게 ALTER 로 추가한다.</summary>
    private void MigratePhaseColumns()
    {
        foreach (var col in MetaColumns)
            if (!HasPhaseColumn(col))
                _g.Execute($"ALTER TABLE Phase ADD {col} STRING DEFAULT ''");
    }

    /// <summary>Phase 테이블에 해당 컬럼이 있는지 프로브 쿼리로 확인(없으면 예외 → false).</summary>
    private bool HasPhaseColumn(string col)
    {
        try { _g.Query($"MATCH (p:Phase) RETURN p.{col} LIMIT 1", 1); return true; }
        catch { return false; }
    }

    private void EnsureProject(string projectId)
    {
        var rows = _g.Query("MATCH (p:Project {id: $id}) RETURN count(p)", 1, P(("id", projectId)));
        if (rows.Count > 0 && rows[0][0] != "0") return;
        _g.Execute("CREATE (:Project {id: $id, name: $name, created: $created})",
            P(("id", projectId), ("name", projectId), ("created", Now())));
    }

    /// <summary>
    /// 새 사이클을 시작한다(Plan 단계에서 호출). 직전 사이클과 NEXT 로 연결하고,
    /// <paramref name="reinforceOf"/> 가 지정되면 그 사이클을 보강하는 <c>REINFORCES</c> 엣지도 만든다.
    /// </summary>
    public long StartCycle(long reinforceOf = 0)
    {
        var id = NextCycleId();
        _g.Execute("CREATE (:Cycle {id: $id, started: $started, status: 'planning'})",
            P(("id", id), ("started", Now())));
        _g.Execute("MATCH (p:Project {id: $pid}), (c:Cycle {id: $id}) CREATE (p)-[:HAS_CYCLE]->(c)",
            P(("pid", ProjectId), ("id", id)));
        if (id > 1)
            _g.Execute("MATCH (a:Cycle {id: $a}), (b:Cycle {id: $b}) CREATE (a)-[:NEXT]->(b)",
                P(("a", id - 1), ("b", id)));
        if (reinforceOf > 0 && reinforceOf != id)
            _g.Execute("MATCH (a:Cycle {id: $a}), (b:Cycle {id: $b}) CREATE (a)-[:REINFORCES]->(b)",
                P(("a", id), ("b", reinforceOf)));
        return id;
    }

    /// <summary>
    /// 직전(가장 최근) 사이클이 Act 에서 '즉시 보강'을 요구했으면 그 사이클 id 를, 아니면 0 을 반환한다.
    /// Plan 시작 전에 호출해 새 사이클을 보강 사이클로 이을지 결정한다.
    /// </summary>
    public long PendingReinforceTarget()
    {
        var cur = CurrentCycle();
        if (cur is null) return 0;
        var act = GetPhase(cur.Value.Id, ActKind);
        return act is not null && act.Reinforce.StartsWith("yes", StringComparison.OrdinalIgnoreCase)
            ? cur.Value.Id : 0;
    }

    /// <summary>가장 최근(진행 중) 사이클. 없으면 null.</summary>
    public (long Id, string Status)? CurrentCycle()
    {
        var rows = _g.Query("MATCH (c:Cycle) RETURN c.id, c.status ORDER BY c.id DESC LIMIT 1", 2);
        if (rows.Count == 0) return null;
        return (long.Parse(rows[0][0], CultureInfo.InvariantCulture), rows[0][1]);
    }

    /// <summary>
    /// 한 단계를 기록하고 사이클 상태를 갱신한다. <paramref name="extra"/> 로 폐루프 메타
    /// (expected/verdict/actual/reinforce)를 함께 저장할 수 있다(화이트리스트 키만 반영).
    /// </summary>
    public void RecordPhase(long cycleId, string kind, string input, string llm,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var phaseId = $"{cycleId}-{kind}";
        _g.Execute(
            "CREATE (:Phase {id: $id, cycle: $cycle, kind: $kind, input: $input, llm: $llm, created: $created})",
            P(("id", phaseId), ("cycle", cycleId), ("kind", kind), ("input", input), ("llm", llm), ("created", Now())));
        _g.Execute("MATCH (c:Cycle {id: $cid}), (p:Phase {id: $pid}) CREATE (c)-[:HAS_PHASE]->(p)",
            P(("cid", cycleId), ("pid", phaseId)));
        _g.Execute("MATCH (c:Cycle {id: $cid}) SET c.status = $st",
            P(("cid", cycleId), ("st", StatusAfter(kind))));

        if (extra is not null)
            foreach (var (k, v) in extra)
                if (Array.IndexOf(MetaColumns, k) >= 0)   // 화이트리스트 컬럼만(주입 안전)
                    _g.Execute($"MATCH (p:Phase {{id: $pid}}) SET p.{k} = $val",
                        P(("pid", phaseId), ("val", v)));
    }

    /// <summary>한 사이클의 특정 단계(텍스트 + 폐루프 메타)를 읽는다.</summary>
    public PdsaPhase? GetPhase(long cycleId, string kind)
    {
        var rows = _g.Query(
            "MATCH (p:Phase {id: $id}) RETURN p.kind, p.input, p.llm, p.created, p.expected, p.verdict, p.actual, p.reinforce", 8,
            P(("id", $"{cycleId}-{kind}")));
        if (rows.Count == 0) return null;
        var r = rows[0];
        return new PdsaPhase(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7]);
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
                    "MATCH (:Cycle {id: $id})-[:HAS_PHASE]->(p:Phase) " +
                    "RETURN p.kind, p.input, p.llm, p.created, p.expected, p.verdict, p.actual, p.reinforce", 8,
                    P(("id", id)))
                .Select(r => new PdsaPhase(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7]))
                .OrderBy(p => KindOrder(p.Kind))
                .ToList();
            var verdict = phases.FirstOrDefault(p => p.Kind == StudyKind)?.Verdict ?? "";
            views.Add(new PdsaCycleView(id, c[1], c[2], phases, verdict));
        }
        return views;
    }

    /// <summary>기대 충족률(재현율): Study 판정이 있는 사이클 중 met 비율. (met, 판정있는 사이클 수).</summary>
    public (int Met, int Total) HitRate()
    {
        var rows = _g.Query(
            "MATCH (p:Phase {kind: 'study'}) WHERE p.verdict IS NOT NULL AND p.verdict <> '' RETURN p.verdict", 1);
        var total = rows.Count;
        var met = rows.Count(r => string.Equals(r[0], "met", StringComparison.OrdinalIgnoreCase));
        return (met, total);
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
