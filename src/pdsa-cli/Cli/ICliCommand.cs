namespace PdsaCli.Cli;

/// <summary>pdsa-cli 의 하위 명령 계약.</summary>
public interface ICliCommand
{
    /// <summary>명령 이름(예: "run", "guide", "view").</summary>
    string Name { get; }

    /// <summary>한 줄 요약(도움말 목록에 표시).</summary>
    string Summary { get; }

    /// <summary>사용법 문자열.</summary>
    string Usage { get; }

    /// <summary>명령 실행. 반환값은 프로세스 종료 코드.</summary>
    Task<int> RunAsync(string[] args, CancellationToken ct);
}
