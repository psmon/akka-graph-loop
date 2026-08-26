# LLM 인증/모델 설정 확장 설계 (Plan-first)

> 목표: `pdsa` CLI의 LLM 인증을 **GPT API 키 단일 방식**에서
> **① OpenAI 호환 오픈웨이트(키리스 로컬 포함) ② GPT OAuth** 로 확장한다.
> 사용자 요구 = "도입 전 플래닝 먼저, 그다음 반영". 본 문서는 **도입 전 설계 확정**이 목적.

## 1. 현재 구조 (as-is)

```
LlmOptions(BaseUrl, ApiKey, Model, ReasoningEffort)      // ILlmClient.cs:4  — 단일 인증(정적 키)
OpenAiConfig.Resolve()  env > global(json) > repo(.secret/openai.json)
OpenAiConfig.TryLoad()  ApiKey 비었거나 placeholder면 실패 → 키리스 로컬 차단
OpenAiClient(options)   ctor에서 Authorization: Bearer <ApiKey> 1회 고정   // OpenAiClient.cs:35
```

인증 방식은 한 가지(Bearer 정적 키). `base_url`은 이미 설정 가능하므로
오픈웨이트 엔드포인트는 *부분적으로* 가능하지만 **빈 키를 TryLoad가 거부**해 실제로는 막혀 있다.

## 2. 변경 영향 지점 (커버리지 100% — 가설1 검증)

| # | 파일:위치 | 현재 | 변경 |
|---|---|---|---|
| 1 | `Llm/ILlmClient.cs:4` `LlmOptions` | 4-필드 record | `AuthMode` + OAuth 필드 추가 |
| 2 | `Llm/OpenAiClient.cs:31-36` ctor | Bearer 키 1회 고정 | 인증 전략 주입(요청 시 토큰) / 무인증 허용 |
| 3 | `Llm/OpenAiConfig.cs` | Resolve/TryLoad/Set*/Describe | auth_mode·provider·oauth 읽기/쓰기, 키 요구 완화 |
| 4 | `Commands/ConfigCommand.cs` | key/model/base-url… | `provider`, `auth`, `login` 서브명령 추가 |
| 5a | `Commands/CheckCommand.cs:27` | `new OpenAiClient(options)` | **무변경** (시그니처 유지) |
| 5b | `Commands/ModelsCommand.cs:24` | 〃 | **무변경** |
| 5c | `Commands/GuideCommand.cs:37` | 〃 | **무변경** |
| 5d | `Workflow/PdsaSession.cs:43` | 〃 | **무변경** |

핵심 설계 결정: **`new OpenAiClient(LlmOptions)` 시그니처를 보존**하고 인증 분기를
`OpenAiClient` 내부 + `IAuthProvider`로 밀어넣는다 → 4개 호출부(5a~5d)는 손대지 않는다.
= 최소 침습, 하위호환.

## 3. to-be 데이터 모델

```csharp
public enum AuthMode { ApiKey, OAuth, None }   // None = 키리스 로컬(ollama/vLLM/LM Studio)

public sealed record LlmOptions(
    string BaseUrl, string Model, string? ReasoningEffort = null,
    AuthMode Auth = AuthMode.ApiKey,
    string? ApiKey = null,                       // Auth=ApiKey
    OAuthOptions? OAuth = null);                 // Auth=OAuth

public sealed record OAuthOptions(
    string TokenEndpoint, string? ClientId, string? RefreshToken,
    string? AccessToken, long ExpiresAtUnix);    // 캐시된 토큰 + 만료
```

### 인증 전략(내부)
```csharp
internal interface IAuthProvider {
    Task<AuthenticationHeaderValue?> GetHeaderAsync(CancellationToken ct);  // null = 무인증
}
// ApiKeyAuth  : Bearer <ApiKey>           (기존 동작 그대로)
// OAuthAuth   : 만료 검사→refresh→Bearer <access_token> (요청 시 주입, 캐시)
// NoAuth      : null 반환
```
`OpenAiClient`는 요청마다 `IAuthProvider.GetHeaderAsync`로 헤더를 세팅
(ctor 1회 고정 → 요청 시 주입으로 전환. OAuth 토큰 갱신을 위해 필수).

## 4. config UX (설정 표면)

```
# 오픈웨이트 프리셋 — 키리스 로컬을 한 번에
pdsa config provider local          # base_url=http://localhost:11434/v1, auth=none
pdsa config provider openai-compat <url>   # 임의 호환 엔드포인트, auth=none|key
pdsa config provider openai         # 기본값 복귀(auth=apikey)

# OAuth
pdsa config auth oauth
pdsa config login                   # device-code/refresh 흐름으로 토큰 획득·캐시
```
저장 스키마(global json) 확장: `auth_mode`, `provider`, `oauth: { token_endpoint, client_id, refresh_token, ... }`.
**키파일 미노출 원칙 유지** — OAuth refresh_token도 `oauth_file` 참조 방식 허용.

## 5. 하위호환 회귀 시나리오 (가설2 검증 — 기존 사용자 조작 0)

| 시나리오 | 기대 |
|---|---|
| 기존 `.secret/openai.json`(api_key만) | `auth_mode` 없으면 **기본 ApiKey** → 기존과 동일 동작, 조작 0 |
| 기존 `pdsa config key <키>` | 그대로 동작, `auth_mode=apikey` 암묵 |
| 환경변수 `OPENAI_API_KEY` | 최우선 유지 |
| `pdsa check`/`models`/`guide` | 호출부 무변경 → 회귀 없음 |
| 신규 `provider local` | 키 없이 `TryLoad` 통과(Auth=None 경로) |

## 5b. 미해결 쟁점 (Do 단계 코치 지적 — 도입 전 확정 필요)

1. **`AuthMode=None` 보안 가드**
   - 실수로 원격 `base_url`에 무인증 요청이 나가는 걸 막는다.
   - 규칙: `None`은 **loopback/사설 대역 호스트**(`localhost`/`127.*`/`::1`/`10.*`/`192.168.*`)에만 자동 허용.
   - 원격 호스트에 `None`을 쓰려면 명시적 opt-in(`--allow-insecure-remote`) + stderr 경고.
2. **필드별 병합 규칙** (`env > global > repo`를 필드 단위로 확정)
   - `auth_mode`/`oauth`는 **레코드 단위가 아니라 필드 단위**로 상위 계층이 덮어씀.
   - `OPENAI_AUTH_MODE`, `OPENAI_ACCESS_TOKEN` 환경변수를 최우선으로 인정.
   - `provider` 프리셋은 **베이스값만 제공** — 사용자의 명시적 `base_url`/`model` 설정이 항상 재정의(프리셋 < 개별설정).
3. **검증 배치 매핑** (회귀 시나리오 5종 → 책임 모듈)
   | 시나리오 | 테스트 종류 | 모듈 |
   |---|---|---|
   | api_key만/config key/env 우선 | 단위 | `OpenAiConfigTests` |
   | check/models/guide 무변경 | 단위(호출부 시그니처) | 기존 커맨드 테스트 |
   | provider local 키리스 통과 | 단위 | `OpenAiConfigTests` |
   | OAuth 만료→refresh | 단위(**fake `IAuthProvider`**) | `AuthProviderTests` |
   | ollama 실왕복 | 수동/통합(opt-in) | E2E |

## 5c. 설정 예시 (global json — 실행 가능 형태)

```jsonc
// (기존) API 키 — auth_mode 없으면 ApiKey 기본, 무변경
{ "api_key": "sk-...", "model": "gpt-5.6-terra" }

// 오픈웨이트 키리스 로컬 (ollama)
{ "provider": "local", "auth_mode": "none",
  "base_url": "http://localhost:11434/v1", "model": "llama3.1" }

// 원격 OpenAI-호환 무키 (명시적 opt-in 필수)
{ "provider": "openai-compat", "auth_mode": "none",
  "base_url": "https://my-host/v1", "allow_insecure_no_auth": true }

// GPT OAuth (토큰은 oauth_file 참조로 미노출 가능)
{ "auth_mode": "oauth",
  "oauth": { "token_endpoint": "https://.../token", "client_id": "...",
             "refresh_token_file": "C:/…/.secret/oauth.json" } }
```

## 5d. 승인 기준 (측정 가능 — 도입 완료 게이트)

| # | 기준 | 측정 |
|---|---|---|
| 1 | 기존 회귀 5종 통과 | `OpenAiConfigTests` 그린, 기존 73개 테스트 무붕괴 |
| 2 | 키리스 로컬 요청 성공 | `provider local`에서 `pdsa check` 왕복 OK(키 없이) |
| 3 | 원격 무키는 차단 | `allow_insecure_no_auth` 없으면 `TryLoad` 실패 + 경고 |
| 4 | OAuth 만료 토큰 자동 갱신 | fake `IAuthProvider` 단위테스트: 만료→refresh→Bearer |
| 5 | 필드 병합 규칙 준수 | env > global > repo, 프리셋 < 개별설정 검증 테스트 |

## 6. 도입 순서(다음 사이클들)

1. **사이클 A** ✅ **완료(PDSA #6)**: `LlmOptions`/`AuthMode`/`IAuthProvider` 도입 + `OpenAiClient` 요청시 주입.
   - `ILlmClient.cs`(AuthMode/OAuthOptions/LlmOptions 확장), 신규 `IAuthProvider.cs`(ApiKeyAuth/NoAuth/OAuthAuth 스텁 + IsPrivateEndpoint), `OpenAiClient.cs`(요청별 헤더 주입, 시그니처 보존), `OpenAiConfig.cs`(auth_mode/provider/None 완화), `ConfigCommand.cs`(provider/auth/allow-insecure-no-auth).
   - 게이트 통과: 91 테스트 그린(기존 73 무붕괴), 빌드 경고0, 실키 check 회귀없음.
   - **이월**: repo<global<env 필드병합 격리테스트(경로주입 리팩터 선행 필요), OAuth는 토큰 직접제공만 지원(획득/갱신 미구현), `.local` DNS 실해석 재검토.
2. **사이클 B** ✅ **완료(PDSA #7)**: 경로주입 seam + 필드병합 테스트 + 실서버 키리스 E2E.
   - 경로 seam: `PDSA_GLOBAL_CONFIG` env + internal `GlobalPathOverride`/`RepoPathOverride`(테스트>env>기본). 전역설정을 임시 파일로 격리 가능 → E2E·테스트가 실사용자 설정을 오염시키지 않음.
   - `OpenAiConfigMergeTests`(신규): repo<global<env 필드병합 검증(**병합필드 4개** = `base_url`/`api_key`/`model`/`auth_mode`). 정책 필드는 별도 명시: `provider`=write-전용 프리셋 마커(병합 대상 아님), `allow_insecure_no_auth`=**global-only** opt-in(보안: env/repo 주입 불가).
   - 실서버 E2E `scripts/e2e-openweight.ps1`(`PDSA_E2E_OPENWEIGHT` gate): `a1.webnori.com` 대상 결과A(원격+none opt-in없음→차단 exit3)·결과B(opt-in후 키리스 실왕복 성공 ~1.3s).
   - 게이트: 102 테스트 그린, 경고0. seam 밖 직접 경로접근 0.
   - ⚠ **사고·교훈**: 초기 E2E가 `LOCALAPPDATA` 오버라이드로 격리 시도했으나 .NET `GetFolderPath`가 그 env 를 무시해 **실 전역설정을 오염**(테스트 3개 회귀). → 원복 + `PDSA_GLOBAL_CONFIG` 도입 + 테스트 seam 격리로 재발방지. **정적 OS 경로는 통합·E2E 전에 주입 seam 을 먼저 두라.**
3. **사이클 C** ✅ **완료(PDSA #8, 판정 met)**: OAuth 실구현.
   - `Llm/OAuth.cs`(신규): `OAuthToken`/`ITokenRefresher`/`HttpTokenRefresher`(grant_type=refresh_token) + device-code(`IDeviceCodeClient`/`HttpDeviceCodeClient`/`DeviceCodeLogin` 폴링 상태머신). transport·`now`·delay 주입으로 결정적 테스트.
   - `OAuthAuth`: 유효토큰 그대로 / 만료(skew 30s)·부재→refresh→Bearer / 성공 시 `onRefreshed`로 영속. refresh 없으면 명확 실패.
   - `OpenAiConfig`: oauth refresh_token(+**refresh_token_file 미노출**)/expires_at 읽기, `PersistOAuthToken`, oauth 세터, `login` 배선.
   - `ConfigCommand`: async 전환 + `oauth`/`login` 서브명령(device-code UX, 불완전설정 안내).
   - 게이트: 129 테스트 그린(102→129), 경고0. `OAuthAuthTests`(refresh core)·`DeviceCodeLoginTests`(폴링)·`OAuthHttpTests`(실 transport)·비노출 테스트.
   - **미검증(수동/후속)**: 실 OAuth provider E2E(refresh/device UX/provider별 오류응답), `refresh_token_file` 원자적 쓰기·소유자 권한.

## 결과: 3방식 인증 확장 완료
`ApiKey`(기존) · `None`(키리스 오픈웨이트: 로컬 자동 + 원격 opt-in) · `OAuth`(device-code 로그인 + 자동 refresh). 최소 침습(호출부 4곳 무변경) + 하위호환(auth_mode 미지정=ApiKey) 유지.

각 사이클은 `pdsa plan→do→study→act`로 가설·측정을 남긴다.
```
```
