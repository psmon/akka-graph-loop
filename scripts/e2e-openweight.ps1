#!/usr/bin/env pwsh
# 키리스 오픈웨이트 E2E 스모크 (사이클 B).
#
# 실서버(OpenAI 호환, 키 불필요)에 대해 3가지 결과를 검증한다:
#   A. 원격 + auth_mode=none 은 opt-in 없이 "차단"된다(원격 무인증 가드).
#   B. allow-insecure-no-auth opt-in 후 키 없이 check 가 "실왕복 성공"한다.
#   C. 결과를 로그로 남긴다.
#
# 네트워크 의존이라 기본은 Skip. 실행하려면:  $env:PDSA_E2E_OPENWEIGHT="1"; ./scripts/e2e-openweight.ps1
# 전역설정 오염 방지: PDSA_GLOBAL_CONFIG 로 CLI 전역설정을 임시 파일로 격리한다.
#   (주의: GetFolderPath(LocalApplicationData) 는 LOCALAPPDATA env 를 따르지 않으므로 그 방식은 격리에 실패한다.)

$ErrorActionPreference = "Stop"
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8

if (-not $env:PDSA_E2E_OPENWEIGHT) {
    Write-Host "SKIP: PDSA_E2E_OPENWEIGHT 미설정 — 네트워크 오픈웨이트 E2E 를 건너뜁니다."
    Write-Host "      실행: `$env:PDSA_E2E_OPENWEIGHT='1'; ./scripts/e2e-openweight.ps1"
    exit 0
}

$server = if ($env:PDSA_E2E_BASEURL) { $env:PDSA_E2E_BASEURL } else { "https://a1.webnori.com/v1" }
$model  = if ($env:PDSA_E2E_MODEL)   { $env:PDSA_E2E_MODEL }   else { "openai/gpt-oss-20b" }
$exe = Join-Path $PSScriptRoot "../src/pdsa-cli/bin/Release/net10.0/pdsa.exe" | Resolve-Path

# ── 격리: PDSA_GLOBAL_CONFIG → 임시 파일 (사용자 실설정 미오염) ──
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("pdsa-e2e-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
$savedGlobalCfg = $env:PDSA_GLOBAL_CONFIG
$env:PDSA_GLOBAL_CONFIG = Join-Path $tmp "openai.json"

$fail = 0
try {
    Write-Host "=== E2E 오픈웨이트: server=$server model=$model ==="

    # provider 프리셋 → none + base_url
    & $exe config provider openai-compat $server | Out-Null
    & $exe config model $model | Out-Null

    # ── 결과 A: 원격 + none, opt-in 없음 → 차단 기대 ──
    Write-Host "`n[A] opt-in 없이 check (차단 기대)…"
    & $exe check
    if ($LASTEXITCODE -eq 0) { Write-Host "  ✘ FAIL(A): 원격 무인증이 차단되지 않음"; $fail++ }
    else { Write-Host "  ✔ PASS(A): 원격 무인증 차단됨 (exit=$LASTEXITCODE)" }

    # ── 결과 B: opt-in 후 → 키 없이 실왕복 성공 기대 ──
    Write-Host "`n[B] allow-insecure-no-auth opt-in 후 check (성공 기대)…"
    & $exe config allow-insecure-no-auth true | Out-Null
    & $exe check
    if ($LASTEXITCODE -eq 0) { Write-Host "  ✔ PASS(B): 키 없이 실왕복 성공" }
    else { Write-Host "  ✘ FAIL(B): opt-in 후에도 실패 (exit=$LASTEXITCODE)"; $fail++ }
}
finally {
    $env:PDSA_GLOBAL_CONFIG = $savedGlobalCfg
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

Write-Host "`n=== 결과: $(if ($fail -eq 0) {'모두 통과'} else {"$fail 건 실패"}) ==="
exit $fail
