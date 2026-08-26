#!/usr/bin/env node
'use strict';

// @webnori/pdsa 런처: 현재 OS/아키텍처에 맞는 플랫폼 패키지의 네이티브 AOT 바이너리를
// 찾아 그대로 실행하고, 인자/표준입출력/종료코드를 투명하게 전달한다.
// 플랫폼 패키지는 optionalDependencies 로 설치되며, npm 이 os/cpu 로 호환되는 하나만 설치한다.

const { spawnSync } = require('child_process');

const PACKAGES = {
  'win32-x64': '@webnori/pdsa-win32-x64',
  'linux-x64': '@webnori/pdsa-linux-x64',
  'darwin-arm64': '@webnori/pdsa-darwin-arm64',
};

const key = `${process.platform}-${process.arch}`;
const pkg = PACKAGES[key];

if (!pkg) {
  console.error(
    `[pdsa] 지원하지 않는 플랫폼입니다: ${key}\n` +
    `       지원 플랫폼: ${Object.keys(PACKAGES).join(', ')}`
  );
  process.exit(1);
}

const binName = process.platform === 'win32' ? 'pdsa.exe' : 'pdsa';

let binPath;
try {
  binPath = require.resolve(`${pkg}/bin/${binName}`);
} catch (e) {
  console.error(
    `[pdsa] 플랫폼 패키지 '${pkg}' 를 찾을 수 없습니다.\n` +
    `       'npm install -g @webnori/pdsa' 로 재설치하거나, optionalDependencies 설치가\n` +
    `       차단되지 않았는지(--no-optional 등) 확인하세요.`
  );
  process.exit(1);
}

const result = spawnSync(binPath, process.argv.slice(2), { stdio: 'inherit' });

if (result.error) {
  console.error(`[pdsa] 실행 실패: ${result.error.message}`);
  process.exit(1);
}

// 시그널로 종료된 경우 비정상 종료코드로 전달.
process.exit(result.status === null ? 1 : result.status);
