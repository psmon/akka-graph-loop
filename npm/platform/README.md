# 플랫폼 패키지 (`@webnori/pdsa-<plat>`)

이 디렉터리의 플랫폼 패키지들은 **소스로 커밋하지 않는다**. CI(`.github/workflows/release.yml`)가
각 OS 러너에서 `dotnet publish -r <rid>` 로 만든 Native AOT 바이너리 + Kùzu 네이티브 라이브러리를
`npm/scripts/assemble.mjs` 로 조립해 생성한다.

- `@webnori/pdsa-win32-x64`   — `pdsa.exe` + `kuzu_shared.dll` (`os: win32`, `cpu: x64`)
- `@webnori/pdsa-linux-x64`   — `pdsa` + `libkuzu.so` (`os: linux`, `cpu: x64`)
- `@webnori/pdsa-darwin-arm64`— `pdsa` + `libkuzu.dylib` (`os: darwin`, `cpu: arm64`)

메인 패키지 `@webnori/pdsa` 는 이 셋을 `optionalDependencies` 로 참조하며, npm 이 설치 시
현재 플랫폼에 맞는 하나만 내려받는다. 런처 `npm/pdsa/bin/pdsa.js` 가 해당 바이너리를 실행한다.

로컬에서 하나를 조립해 보려면:

```bash
dotnet publish src/pdsa-cli -c Release -r win-x64 -p:Version=0.0.1
node npm/scripts/assemble.mjs platform 0.0.1 win32-x64 \
  src/pdsa-cli/bin/Release/net10.0/win-x64/publish
```
