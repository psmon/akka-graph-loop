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
