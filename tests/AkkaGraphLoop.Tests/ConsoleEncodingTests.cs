using PdsaCli.Cli;

namespace AkkaGraphLoop.Tests;

/// <summary>
/// 출력 인코딩 고정 검증. Windows 기본 CP949 대신 UTF-8(BOM 없음)로 내보내야
/// 한글 프로즈가 파이프·에이전트·타 OS 에서 깨지지 않는다.
/// </summary>
public class ConsoleEncodingTests
{
    [Fact]
    public void Utf8_is_bom_less_utf8()
    {
        Assert.Equal("utf-8", ConsoleEncoding.Utf8.WebName);
        Assert.Empty(ConsoleEncoding.Utf8.GetPreamble());   // BOM 프리앰블 없음
    }

    [Fact]
    public void Utf8_encodes_korean_as_utf8_not_cp949()
    {
        // '가' U+AC00 → UTF-8 EA B0 80 (CP949 였다면 B0 A1 로 2바이트)
        Assert.Equal(new byte[] { 0xEA, 0xB0, 0x80 }, ConsoleEncoding.Utf8.GetBytes("가"));
    }
}
