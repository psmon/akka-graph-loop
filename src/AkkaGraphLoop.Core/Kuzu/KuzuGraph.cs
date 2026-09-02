using System.Runtime.InteropServices;
using static AkkaGraphLoop.Core.Kuzu.KuzuNative;

namespace AkkaGraphLoop.Core.Kuzu;

/// <summary>
/// Kùzu 임베디드 그래프 DB에 대한 얇은 관리형 래퍼.
/// 하나의 데이터베이스 + 하나의 커넥션을 열고 Cypher 를 실행/조회한다.
/// (커넥션은 스레드 세이프하지 않으므로 단일 스레드에서 순차적으로 사용할 것.)
/// </summary>
public sealed class KuzuGraph : IDisposable
{
    private Database _db;
    private Connection _conn;
    private bool _disposed;

    public KuzuGraph(string databasePath, bool readOnly = false)
    {
        var config = kuzu_default_system_config();
        config.ReadOnly = readOnly;
        if (kuzu_database_init(databasePath, config, out _db) != Success)
            throw new InvalidOperationException($"Kùzu 데이터베이스를 열지 못했습니다: {databasePath}");
        if (kuzu_connection_init(ref _db, out _conn) != Success)
        {
            kuzu_database_destroy(ref _db);
            throw new InvalidOperationException("Kùzu 커넥션 생성에 실패했습니다.");
        }
    }

    /// <summary>결과를 읽지 않는 Cypher(DDL/CREATE 등)를 실행한다.</summary>
    public void Execute(string cypher)
    {
        RunChecked(cypher, out var result);
        kuzu_query_result_destroy(ref result);
    }

    /// <summary>
    /// 명시적 트랜잭션을 연다. <see cref="KuzuTransaction.Commit"/> 없이 Dispose 되면
    /// <c>ROLLBACK</c> 한다(= 예외로 빠져나가면 부분 쓰기가 남지 않는다).
    /// 여러 문(statement)에 걸친 쓰기를 한 단위로 묶을 때 사용한다.
    /// </summary>
    public KuzuTransaction BeginTransaction() => new(this);

    /// <summary>
    /// <see cref="KuzuGraph.BeginTransaction"/> 이 반환하는 트랜잭션 스코프.
    /// 성공 경로에서만 <see cref="Commit"/> 을 부르고, 그 외에는 Dispose 가 롤백한다.
    /// </summary>
    public sealed class KuzuTransaction : IDisposable
    {
        private readonly KuzuGraph _graph;
        private bool _settled;

        internal KuzuTransaction(KuzuGraph graph)
        {
            _graph = graph;
            _graph.Execute("BEGIN TRANSACTION");
        }

        /// <summary>변경을 확정한다. 한 번만 유효하다.</summary>
        public void Commit()
        {
            if (_settled) return;
            _settled = true;
            _graph.Execute("COMMIT");
        }

        /// <summary>Commit 되지 않았으면 롤백한다(예외 전파 중일 수 있으므로 실패는 삼킨다).</summary>
        public void Dispose()
        {
            if (_settled) return;
            _settled = true;
            try { _graph.Execute("ROLLBACK"); }
            catch { /* 이미 중단된 트랜잭션 — 원 예외를 가리지 않는다 */ }
        }
    }

    /// <summary>
    /// 파라미터 바인딩으로 Cypher 를 실행한다(임의 텍스트를 이스케이프 없이 안전하게 처리).
    /// 값 타입: <see cref="string"/> 또는 <see cref="long"/>/<see cref="int"/>. Cypher 에서는 <c>$name</c> 로 참조.
    /// </summary>
    public void Execute(string cypher, IReadOnlyDictionary<string, object> parameters)
    {
        var result = RunPrepared(cypher, parameters);
        kuzu_query_result_destroy(ref result);
    }

    /// <summary>Cypher 조회 결과를 행 단위 문자열 배열로 반환한다.</summary>
    public List<string[]> Query(string cypher, int columns)
    {
        RunChecked(cypher, out var result);
        var rows = new List<string[]>();
        try
        {
            while (kuzu_query_result_has_next(ref result) != 0)
            {
                kuzu_query_result_get_next(ref result, out var tuple);
                var row = new string[columns];
                for (var i = 0; i < columns; i++)
                {
                    kuzu_flat_tuple_get_value(ref tuple, (ulong)i, out var value);
                    var ptr = kuzu_value_to_string(ref value);
                    row[i] = Marshal.PtrToStringUTF8(ptr) ?? "";
                    kuzu_destroy_string(ptr);
                    kuzu_value_destroy(ref value);
                }
                rows.Add(row);
            }
        }
        finally
        {
            kuzu_query_result_destroy(ref result);
        }
        return rows;
    }

    private QueryResult RunPrepared(string cypher, IReadOnlyDictionary<string, object> parameters)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (kuzu_connection_prepare(ref _conn, cypher, out var prepared) != Success ||
            kuzu_prepared_statement_is_success(ref prepared) == 0)
        {
            var msg = Marshal.PtrToStringUTF8(kuzu_prepared_statement_get_error_message(ref prepared)) ?? "(알 수 없는 오류)";
            kuzu_prepared_statement_destroy(ref prepared);
            throw new InvalidOperationException($"Kùzu prepare 실패: {msg}\n  Cypher: {cypher}");
        }

        try
        {
            foreach (var (name, value) in parameters)
            {
                var state = value switch
                {
                    string s => kuzu_prepared_statement_bind_string(ref prepared, name, s),
                    long l => kuzu_prepared_statement_bind_int64(ref prepared, name, l),
                    int i => kuzu_prepared_statement_bind_int64(ref prepared, name, i),
                    _ => throw new NotSupportedException($"지원하지 않는 파라미터 타입: {value?.GetType().Name}"),
                };
                if (state != Success)
                    throw new InvalidOperationException($"Kùzu 파라미터 바인딩 실패: ${name}");
            }

            if (kuzu_connection_execute(ref _conn, ref prepared, out var result) != Success ||
                kuzu_query_result_is_success(ref result) == 0)
            {
                var msg = Marshal.PtrToStringUTF8(kuzu_query_result_get_error_message(ref result)) ?? "(알 수 없는 오류)";
                kuzu_query_result_destroy(ref result);
                throw new InvalidOperationException($"Kùzu 실행 실패: {msg}\n  Cypher: {cypher}");
            }
            return result;
        }
        finally
        {
            kuzu_prepared_statement_destroy(ref prepared);
        }
    }

    /// <summary>파라미터 바인딩 조회.</summary>
    public List<string[]> Query(string cypher, int columns, IReadOnlyDictionary<string, object> parameters)
    {
        var result = RunPrepared(cypher, parameters);
        return ReadRows(ref result, columns);
    }

    private static List<string[]> ReadRows(ref QueryResult result, int columns)
    {
        var rows = new List<string[]>();
        try
        {
            while (kuzu_query_result_has_next(ref result) != 0)
            {
                kuzu_query_result_get_next(ref result, out var tuple);
                var row = new string[columns];
                for (var i = 0; i < columns; i++)
                {
                    kuzu_flat_tuple_get_value(ref tuple, (ulong)i, out var value);
                    var ptr = kuzu_value_to_string(ref value);
                    row[i] = Marshal.PtrToStringUTF8(ptr) ?? "";
                    kuzu_destroy_string(ptr);
                    kuzu_value_destroy(ref value);
                }
                rows.Add(row);
            }
        }
        finally
        {
            kuzu_query_result_destroy(ref result);
        }
        return rows;
    }

    private void RunChecked(string cypher, out QueryResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (kuzu_connection_query(ref _conn, cypher, out result) != Success ||
            kuzu_query_result_is_success(ref result) == 0)
        {
            var messagePtr = kuzu_query_result_get_error_message(ref result);
            var message = Marshal.PtrToStringUTF8(messagePtr) ?? "(알 수 없는 오류)";
            kuzu_query_result_destroy(ref result);
            throw new InvalidOperationException($"Kùzu 쿼리 실패: {message}\n  Cypher: {cypher}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        kuzu_connection_destroy(ref _conn);
        kuzu_database_destroy(ref _db);
    }
}
