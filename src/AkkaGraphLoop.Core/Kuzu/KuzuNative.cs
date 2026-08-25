using System.Runtime.InteropServices;

namespace AkkaGraphLoop.Core.Kuzu;

/// <summary>
/// Kùzu 임베디드 그래프 DB의 C API(<c>kuzu.h</c>) P/Invoke 선언.
/// 플랫폼별 공유 라이브러리(Windows: kuzu_shared.dll / Linux: libkuzu.so / macOS: libkuzu.dylib)를
/// <see cref="NativeLibrary.SetDllImportResolver"/> 로 찾아 로드한다.
/// </summary>
internal static class KuzuNative
{
    private const string Lib = "kuzu";

    static KuzuNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(KuzuNative).Assembly, (name, assembly, searchPath) =>
        {
            if (name != Lib) return IntPtr.Zero;
            foreach (var candidate in new[] { "kuzu_shared", "libkuzu", "kuzu" })
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                    return handle;
            return IntPtr.Zero;
        });
    }

    // kuzu_state: 0 = 성공, 1 = 오류
    public const int Success = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemConfig
    {
        public ulong BufferPoolSize;
        public ulong MaxNumThreads;
        [MarshalAs(UnmanagedType.U1)] public bool EnableCompression;
        [MarshalAs(UnmanagedType.U1)] public bool ReadOnly;
        public ulong MaxDbSize;
        [MarshalAs(UnmanagedType.U1)] public bool AutoCheckpoint;
        public ulong CheckpointThreshold;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Database { public IntPtr Handle; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Connection { public IntPtr Handle; }

    [StructLayout(LayoutKind.Sequential)]
    public struct QueryResult { public IntPtr Handle; [MarshalAs(UnmanagedType.U1)] public bool OwnedByCpp; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FlatTuple { public IntPtr Handle; [MarshalAs(UnmanagedType.U1)] public bool OwnedByCpp; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Value { public IntPtr Handle; [MarshalAs(UnmanagedType.U1)] public bool OwnedByCpp; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PreparedStatement { public IntPtr Statement; public IntPtr BoundValues; }

    [DllImport(Lib)] public static extern SystemConfig kuzu_default_system_config();

    [DllImport(Lib)] public static extern int kuzu_database_init(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string databasePath, SystemConfig config, out Database outDatabase);
    [DllImport(Lib)] public static extern void kuzu_database_destroy(ref Database database);

    [DllImport(Lib)] public static extern int kuzu_connection_init(ref Database database, out Connection outConnection);
    [DllImport(Lib)] public static extern void kuzu_connection_destroy(ref Connection connection);

    [DllImport(Lib)] public static extern int kuzu_connection_query(
        ref Connection connection, [MarshalAs(UnmanagedType.LPUTF8Str)] string query, out QueryResult outResult);

    [DllImport(Lib)] public static extern byte kuzu_query_result_is_success(ref QueryResult result);
    [DllImport(Lib)] public static extern IntPtr kuzu_query_result_get_error_message(ref QueryResult result);
    [DllImport(Lib)] public static extern byte kuzu_query_result_has_next(ref QueryResult result);
    [DllImport(Lib)] public static extern int kuzu_query_result_get_next(ref QueryResult result, out FlatTuple outTuple);
    [DllImport(Lib)] public static extern void kuzu_query_result_destroy(ref QueryResult result);

    [DllImport(Lib)] public static extern int kuzu_flat_tuple_get_value(ref FlatTuple tuple, ulong index, out Value outValue);

    [DllImport(Lib)] public static extern IntPtr kuzu_value_to_string(ref Value value);
    [DllImport(Lib)] public static extern void kuzu_value_destroy(ref Value value);

    [DllImport(Lib)] public static extern void kuzu_destroy_string(IntPtr str);

    // 파라미터 바인딩(임의 텍스트를 이스케이프 없이 안전하게 저장)
    [DllImport(Lib)] public static extern int kuzu_connection_prepare(
        ref Connection connection, [MarshalAs(UnmanagedType.LPUTF8Str)] string query, out PreparedStatement outStatement);
    [DllImport(Lib)] public static extern byte kuzu_prepared_statement_is_success(ref PreparedStatement statement);
    [DllImport(Lib)] public static extern IntPtr kuzu_prepared_statement_get_error_message(ref PreparedStatement statement);
    [DllImport(Lib)] public static extern int kuzu_prepared_statement_bind_string(
        ref PreparedStatement statement, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Lib)] public static extern int kuzu_prepared_statement_bind_int64(
        ref PreparedStatement statement, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, long value);
    [DllImport(Lib)] public static extern int kuzu_connection_execute(
        ref Connection connection, ref PreparedStatement statement, out QueryResult outResult);
    [DllImport(Lib)] public static extern void kuzu_prepared_statement_destroy(ref PreparedStatement statement);
}
