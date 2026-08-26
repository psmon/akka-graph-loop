using PdsaCli.Cli;
using PdsaCli.Skills;

namespace PdsaCli.Commands;

/// <summary>
/// 현재 워크스페이스에 PDSA 스킬 문서를 설치한다: <c>.claude/skills/pdsa/SKILL.md</c>.
/// 언어(en/ko)와 덮어쓰기 여부를 묻는다(비대화형은 <c>--lang</c>/<c>--force</c> 로 지정, 미지정 시 안전하게 중단).
/// 스킬 본문은 실행 파일의 임베디드 리소스에서 가져온다(AOT-safe).
/// </summary>
public sealed class InitCommand : ICliCommand
{
    public string Name => "init";
    public string Summary => "워크스페이스에 PDSA 스킬 설치(.claude/skills/pdsa/SKILL.md)";
    public string Usage => "pdsa init [--lang en|ko] [--force] [--path <워크스페이스루트>]";

    public Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (ArgUtil.Flag(args, "--help")) { Console.WriteLine(Usage); return Task.FromResult(0); }

        var root = ArgUtil.Option(args, "--path") ?? Directory.GetCurrentDirectory();
        var force = ArgUtil.Flag(args, "--force") || ArgUtil.Flag(args, "--yes");

        // ── 언어 선택 ──
        var lang = ArgUtil.Option(args, "--lang");
        if (lang is null)
        {
            if (Console.IsInputRedirected)
                return Fail("언어를 지정하세요: pdsa init --lang en|ko  (비대화형에서는 프롬프트 불가)");
            lang = PromptLang();
        }
        if (!SkillResources.IsSupported(lang))
            return Fail($"지원하지 않는 언어: {lang} (지원: {string.Join(", ", SkillResources.Langs)})");

        var target = SkillResources.TargetPath(root);
        var exists = File.Exists(target);

        // ── 덮어쓰기 확인 ──
        if (exists && !force)
        {
            if (Console.IsInputRedirected)
                return Fail($"이미 존재합니다: {target}\n  덮어쓰려면 --force 를 사용하세요.");
            if (!PromptOverwrite(target))
            {
                Console.WriteLine("취소됨 — 기존 파일을 유지합니다.");
                return Task.FromResult(0);
            }
            force = true;   // 사용자 확인 = 강제
        }

        if (!SkillResources.ShouldWrite(exists, force))
            return Fail($"이미 존재합니다: {target} (덮어쓰지 않음)");

        // ── 설치 ──
        var content = SkillResources.Load(lang!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, content);
        Console.WriteLine($"✔ 스킬 설치됨({lang}{(exists ? ", 덮어씀" : "")}): {target}");
        Console.WriteLine("  이제 이 워크스페이스에서 `/pdsa` 로 스킬을 사용할 수 있습니다.");
        return Task.FromResult(0);
    }

    private static string PromptLang()
    {
        Console.Write($"설치할 스킬 언어를 선택하세요 [{string.Join("/", SkillResources.Langs)}] (기본 {SkillResources.Langs[0]}): ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? SkillResources.Langs[0] : input.ToLowerInvariant();
    }

    private static bool PromptOverwrite(string target)
    {
        Console.Write($"이미 존재합니다: {target}\n  덮어쓸까요? [y/N]: ");
        var input = Console.ReadLine()?.Trim();
        return input is "y" or "Y" or "yes";
    }

    private static Task<int> Fail(string msg) { Console.Error.WriteLine(msg); return Task.FromResult(2); }
}
