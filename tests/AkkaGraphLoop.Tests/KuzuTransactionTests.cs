using AkkaGraphLoop.Core.Kuzu;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// Kùzu 명시적 트랜잭션(<c>BEGIN TRANSACTION</c>/<c>COMMIT</c>/<c>ROLLBACK</c>)이 C API 바인딩을
/// 통해 실제로 동작하는지 검증한다. PDSA 사이클 원자성(고아 사이클 0건)의 토대이므로,
/// 여기가 깨지면 <see cref="AkkaGraphLoop.Core.Pdsa.PdsaWorkflow"/> 는 보상 삭제로 폴백해야 한다.
/// </summary>
public class KuzuTransactionTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), "pdsa_tx_test_" + Guid.NewGuid().ToString("N"), "graph.kuzu");

    private static KuzuGraph OpenWithTable(string db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);   // PdsaWorkflow 가 하던 준비를 직접
        var g = new KuzuGraph(db);
        g.Execute("CREATE NODE TABLE IF NOT EXISTS T(id INT64, PRIMARY KEY(id))");
        return g;
    }

    private static long Count(KuzuGraph g) =>
        long.Parse(g.Query("MATCH (t:T) RETURN count(t)", 1)[0][0]);

    [Fact]
    public void Commit_persists_all_writes_in_the_scope()
    {
        using var g = OpenWithTable(TempDb());

        using (var tx = g.BeginTransaction())
        {
            g.Execute("CREATE (:T {id: 1})");
            g.Execute("CREATE (:T {id: 2})");
            tx.Commit();
        }

        Assert.Equal(2, Count(g));
    }

    [Fact]
    public void Dispose_without_commit_rolls_back_every_write()
    {
        using var g = OpenWithTable(TempDb());

        using (g.BeginTransaction())
        {
            g.Execute("CREATE (:T {id: 1})");
            g.Execute("CREATE (:T {id: 2})");
            // Commit 하지 않고 스코프를 벗어난다.
        }

        Assert.Equal(0, Count(g));
    }

    [Fact]
    public void Exception_midway_leaves_no_partial_write()
    {
        using var g = OpenWithTable(TempDb());

        Assert.ThrowsAny<Exception>(() =>
        {
            using var tx = g.BeginTransaction();
            g.Execute("CREATE (:T {id: 1})");
            g.Execute("CREATE (:T {id: 1})");   // 중복 PK → 실패
            tx.Commit();
        });

        Assert.Equal(0, Count(g));
    }

    [Fact]
    public void Rolled_back_writes_are_absent_after_reopen()
    {
        var db = TempDb();
        using (var g = OpenWithTable(db))
        {
            using (g.BeginTransaction()) g.Execute("CREATE (:T {id: 7})");
        }

        using var reopened = OpenWithTable(db);
        Assert.Equal(0, Count(reopened));
    }
}
