namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>PDSA 그래프 DB의 기본 경로. 샘플(기록)과 뷰어(조회)가 같은 위치를 공유한다.</summary>
public static class PdsaPaths
{
    /// <summary>
    /// 기본 Kùzu DB 경로. 작업 디렉터리에 무관하게 안정적이도록 OS 임시 폴더 하위에 둔다.
    /// (Kùzu 0.11 은 DB 를 단일 파일로 만든다. 구버전 호환을 위해 파일/디렉터리 모두 처리.)
    /// </summary>
    public static string DefaultDbPath { get; } =
        Path.Combine(Path.GetTempPath(), "akka-graph-loop", "pdsa_kuzu_db");

    /// <summary>DB(파일 또는 디렉터리)가 존재하는지 검사한다.</summary>
    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>DB 를 초기화(삭제)한다: 파일/디렉터리 및 <c>.wal</c> 사이드카를 제거하고 부모 폴더를 만든다.</summary>
    public static void Reset(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);

        var wal = path + ".wal";
        if (File.Exists(wal)) File.Delete(wal);

        var parent = Directory.GetParent(path);
        if (parent is not null) Directory.CreateDirectory(parent.FullName);
    }
}
