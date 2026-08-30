using System.Text;

namespace PdsaCli.Cli;

/// <summary>
/// 콘솔/파이프 출력 인코딩 고정. Windows 기본은 OS 활성 코드페이지(한국어=CP949)라
/// 한글 프로즈 출력이 파이프·AI 에이전트·타 OS 에서 깨진다. 모든 출력을 UTF-8(BOM 없음)로
/// 강제해 어디서든 동일 바이트가 나가게 한다. (<c>--json</c> 은 \uXXXX 이스케이프라 무관하지만
/// prose 출력에는 이 설정이 필요하다.)
/// </summary>
internal static class ConsoleEncoding
{
    /// <summary>BOM 없는 UTF-8. 프리앰블을 방출하지 않아 파이프 첫 바이트가 오염되지 않는다.</summary>
    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// stdout/stderr 를 UTF-8 로 강제한다(both writers 는 <see cref="Console.OutputEncoding"/> 로 생성됨).
    /// 표준 출력 핸들이 없는 환경 등에서 실패하면 조용히 무시한다(기능에 영향 없음).
    /// </summary>
    public static void ForceUtf8()
    {
        try { Console.OutputEncoding = Utf8; }
        catch (IOException) { /* 콘솔/핸들 없음 */ }
        catch (System.Security.SecurityException) { /* 권한 제한 */ }
    }
}
