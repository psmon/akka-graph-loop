#!/usr/bin/env node
// npm 배포 산출물 조립 스크립트(로컬 재현 + CI 공용).
//
//   node npm/scripts/assemble.mjs platform <version> <plat> <publishDir>
//       → npm/platform/@webnori/pdsa-<plat>/ 에 package.json + bin(바이너리 + 네이티브 kuzu lib) 조립
//   node npm/scripts/assemble.mjs main <version>
//       → npm/pdsa/package.json 의 version 과 optionalDependencies 버전을 <version> 으로 고정
//
// plat ∈ win32-x64 | linux-x64 | darwin-arm64

import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync, copyFileSync, rmSync, chmodSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const NPM_ROOT = resolve(__dirname, '..');        // repo/npm
const MAIN_PKG = join(NPM_ROOT, 'pdsa', 'package.json');

const PLATFORMS = {
  'win32-x64':   { os: 'win32',  cpu: 'x64',   exe: 'pdsa.exe', lib: 'kuzu_shared.dll' },
  'linux-x64':   { os: 'linux',  cpu: 'x64',   exe: 'pdsa',     lib: 'libkuzu.so' },
  'darwin-arm64':{ os: 'darwin', cpu: 'arm64', exe: 'pdsa',     lib: 'libkuzu.dylib' },
};

const PLATFORM_PKG_NAMES = Object.keys(PLATFORMS).map(p => `@webnori/pdsa-${p}`);

function readJson(p) { return JSON.parse(readFileSync(p, 'utf8')); }
function writeJson(p, obj) { writeFileSync(p, JSON.stringify(obj, null, 2) + '\n'); }

function assembleMain(version) {
  const pkg = readJson(MAIN_PKG);
  pkg.version = version;
  pkg.optionalDependencies = Object.fromEntries(PLATFORM_PKG_NAMES.map(n => [n, version]));
  writeJson(MAIN_PKG, pkg);
  console.log(`[assemble] main @webnori/pdsa → ${version} (optionalDeps 고정)`);
}

function assemblePlatform(version, plat, publishDir) {
  const spec = PLATFORMS[plat];
  if (!spec) throw new Error(`알 수 없는 플랫폼: ${plat} (지원: ${Object.keys(PLATFORMS).join(', ')})`);
  if (!existsSync(publishDir)) throw new Error(`publishDir 없음: ${publishDir}`);

  const pkgDir = join(NPM_ROOT, 'platform', '@webnori', `pdsa-${plat}`);
  const binDir = join(pkgDir, 'bin');
  rmSync(pkgDir, { recursive: true, force: true });
  mkdirSync(binDir, { recursive: true });

  // 실행 파일 복사(필수)
  const exeSrc = join(publishDir, spec.exe);
  if (!existsSync(exeSrc)) throw new Error(`실행 파일 없음: ${exeSrc}`);
  const exeDst = join(binDir, spec.exe);
  copyFileSync(exeSrc, exeDst);
  if (spec.os !== 'win32') chmodSync(exeDst, 0o755);

  // 네이티브 kuzu 라이브러리 복사(publishDir 에서 kuzu 관련 공유 lib 전부)
  const libs = readdirSync(publishDir).filter(f =>
    /^(kuzu_shared|libkuzu)/i.test(f) && /\.(dll|so|dylib)$/i.test(f));
  if (!libs.some(f => f.toLowerCase() === spec.lib.toLowerCase()))
    throw new Error(`네이티브 lib(${spec.lib})를 publishDir 에서 찾지 못함. 찾은 것: [${libs.join(', ')}]`);
  for (const f of libs) copyFileSync(join(publishDir, f), join(binDir, f));

  const pkgJson = {
    name: `@webnori/pdsa-${plat}`,
    version,
    description: `pdsa CLI native binary for ${spec.os}/${spec.cpu}.`,
    license: 'MIT',
    repository: { type: 'git', url: 'git+https://github.com/psmon/akka-graph-loop.git' },
    os: [spec.os],
    cpu: [spec.cpu],
    files: ['bin/'],
  };
  writeJson(join(pkgDir, 'package.json'), pkgJson);
  console.log(`[assemble] @webnori/pdsa-${plat}@${version} → ${pkgDir}`);
  console.log(`           bin: ${spec.exe} + [${libs.join(', ')}]`);
}

const [mode, version, plat, publishDir] = process.argv.slice(2);
if (!mode || !version) {
  console.error('사용법:\n  assemble.mjs platform <version> <plat> <publishDir>\n  assemble.mjs main <version>');
  process.exit(2);
}
if (mode === 'main') assembleMain(version);
else if (mode === 'platform') assemblePlatform(version, plat, publishDir);
else { console.error(`알 수 없는 mode: ${mode}`); process.exit(2); }
