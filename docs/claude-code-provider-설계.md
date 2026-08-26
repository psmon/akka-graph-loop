# Claude Code 프로바이더 설계·플래닝 (pdsa CLI)

> 작성: 2026-08-27 · 대상: `src/pdsa-cli` · 범위: **설계 전용** (이번 사이클 구현 없음)
> 근거 조사: [[claude-code-self-llm-조사]] · 확장 선례: `docs/llm-auth-확장-설계.md`
> 원칙: [[plan-first-then-adopt]] — 설계 확정 후 다음 사이클에서 구현.

## 1. 목표와 채택안

pdsa CLI가 Claude Code로 구동될 때, **외부 OpenAI 대신 Claude Code(에이전트)의 자기 모델을
셀프로** 이용하도록 신규 LLM 프로바이더를 추가한다. 프롬프트 주입 편법이 아니라 **공식 인증/호출
경로**만 사용한다.

- **주 채택(P1): `AuthMode.ClaudeCode`** — Claude 로그인 OAuth 토큰 재사용 + Anthropic
  **Messages API** 직접 호출. 기존 `Codex`/`CodexClient` 구조 미러링. `ILlmClient.CompleteAsync`에
  1:1 대응.
- **폴백(P2): `AuthMode.ClaudeCli`** — `claude -p` 서브프로세스. P1의 ToS가 막히거나 Anthropic API
  직접 호출을 피하고 싶을 때. 100% 공식·무키.

두 모드는 독립 구현 가능하며, **P1을 먼저 구현하고 P2는 선택적 후속**으로 둔다.

---

## 2. 기존 아키텍처 확장 지점 (조사로 확정)

seam(단일 계약): `ILlmClient.CompleteAsync(system, user, ct) → string`.

| 파일 | 변경 | 비고 |
|---|---|---|
| `Llm/ILlmClient.cs` | `AuthMode`에 `ClaudeCode`(+`ClaudeCli`) 추가 | enum(`:4`) |
| `Llm/LlmClientFactory.cs` | `Create` switch에 신규 분기 | 현재 `Codex→CodexClient`, `_→OpenAiClient`(`:6`) |
| `Llm/OpenAiConfig.cs` | `ParseAuthMode`(`:238`)·`TryLoad`(`:24`)·`SetClaudeCode` 프리셋(cf. `SetCodex :247`)·`Describe`(`:159`) | 로드/검증/표시 |
| `Commands/ConfigCommand.cs` | `auth claude-code`(및 `claude-cli`) 케이스 | `:70`(codex 특례 `:73` 참고) |
| **신규** `Llm/ClaudeCode.cs` | 토큰 로더/갱신 | `Codex.cs` 미러 |
| **신규** `Llm/ClaudeCodeClient.cs` | Messages API 호출 `ILlmClient` | `CodexClient.cs` 미러 |
| `Commands/ModelsCommand.cs`(`:24`)·`GuideCommand.cs`(`:37`) | **팩토리 라우팅 보정** | 현재 `new OpenAiClient()` 직접 생성 → 비-OpenAI 프로바이더 미지원 |

> ⚠️ **함정(선행 로드맵과 동일 계열)**: `Models`/`Guide`가 팩토리를 우회한다. 신규 프로바이더는
> Chat-Completions 비호환이므로, 두 명령을 `LlmClientFactory.Create` 경유로 바꾸거나 프로바이더가
> `ListModelsAsync` 상당을 제공하도록 해야 `pdsa models`/`pdsa guide`가 깨지지 않는다.
> (참고: [[pdsa-init-embedded-skill]]의 `WithCulture=false` 류 우회-함정과 같은 성격.)

---

## 3. P1 상세 설계 — `AuthMode.ClaudeCode` (Messages API + OAuth 재사용)

### 3.1 토큰 조달 (공식 경로, 우선순위)
1. `claude setup-token` 로 발급한 OAuth 토큰(장수명) — config에 참조 저장 or 파일 경로.
2. `ant auth print-credentials --access-token` — 단기 토큰, **호출 직전 서브프로세스로 재취득**.
3. (지양) 로그인 자격증명 파일 직접 파싱 — 플랫폼별 비공식.

`ClaudeCode.cs`는 `Codex.cs`처럼 토큰 로드·만료판정(`IsExpiring`)·갱신을 담당. 갱신은
`EnsureFreshTokenAsync()`에서 위 CLI를 재호출하는 방식(Codex의 in-place refresh 대응).

### 3.2 HTTP 규약
```
POST https://api.anthropic.com/v1/messages
Authorization: Bearer <oauth_token>
anthropic-version: 2023-06-01
anthropic-beta: oauth-2025-04-20        # OAuth 토큰 필수
Content-Type: application/json

{ "model": "<model>", "max_tokens": 16000,
  "system": "<systemPrompt>",
  "messages": [ { "role": "user", "content": "<userPrompt>" } ] }
```
- **응답 파싱**: `content[]`에서 `type=="text"` 블록의 `text` 이어붙이기.
  (Native AOT → 기존처럼 `JsonDocument`/소스생성 직렬화 사용, 리플렉션 금지.)
- **스트리밍**: 불요(단발). 필요 시 기존 SSE 유틸 재사용 가능하나 1차 구현은 non-stream.
- **오류**: `stop_reason=="refusal"`·4xx/5xx는 명시 처리(빈/거부 응답을 성공으로 오인 금지).

### 3.3 `ILlmClient` 매핑
`PdsaCoach`는 `CompleteAsync(SystemPrompt, user)`만 호출하므로, `ClaudeCodeClient.CompleteAsync`가
위 요청을 만들어 텍스트를 반환하면 Plan/Do/Study/Act 전 단계가 변경 없이 동작.

### 3.4 config / 표면
- `pdsa config auth claude-code` → `SetClaudeCode()`: `auth_mode=claudecode`,
  `base_url=https://api.anthropic.com/v1`, 기본 `model`(예: `claude-opus-4-8`) 설정.
- `pdsa config model <id>` 로 모델 오버라이드.
- 토큰 소스: `pdsa config claude-token-file <path>` 또는 "print-credentials 자동" 플래그.
- `pdsa check` → `LlmClientFactory` 경유라 신규 모드 자동 지원(1-shot 왕복 검증).
- `Describe()`에 `auth=claude-code`, 토큰 소스, 모델 표기.

### 3.5 검증 시나리오
- `pdsa config auth claude-code && pdsa check` → ✔.
- `pdsa plan/do/study/act` 1사이클이 Claude 모델로 코칭 산출.
- 토큰 만료 상황에서 자동 갱신 후 성공.

---

## ✅ 채택·구현 결과 (2026-08-27, PDSA #14)

사용자 지시로 **P2(`claude -p`)를 정식 채택**(Claude CLI 주 사용 + 공식 지원 + 무키 + P1 ToS 선결조건 불요).
P1(Messages API + OAuth)은 ToS 미해소로 보류.

- 구현: 신규 `Llm/ClaudeCli.cs`(실행파일 해석: `PDSA_CLAUDE_CLI` env > config `claude_cli_path` > PATH), `Llm/ClaudeCliClient.cs`(`claude -p --output-format json --max-turns 1 --append-system-prompt <system>` + user=stdin + `--model`은 claude* 모델만 + WorkingDirectory=임시). `AuthMode.ClaudeCli`, `pdsa config auth claude-cli` / `config claude-cli-path`, `LlmClientFactory` 라우팅, `GuideCommand` 팩토리 보정(설계 §2 우회함정 해소).
- 검증: 189 테스트 그린(`ClaudeCliTests` 14), 경고0. **실 E2E**: `config auth claude-cli` → `pdsa check` → 실제 `claude -p` 왕복 성공(7.4s, 응답 OK). 토큰·과금 설정이 없어 완전 검증됨.
- 사용법: `pdsa config auth claude-cli` → `pdsa check`/`pdsa plan …`. 모델 바꾸려면 `pdsa config model claude-sonnet-5`.

## 4. P2 폴백 설계 — `AuthMode.ClaudeCli` (`claude -p`)  *(채택됨 — 위 결과 참고)*

`ClaudeCliClient.CompleteAsync`:
```
claude -p "<합성 프롬프트>" --output-format json [--permission-mode ...] [--cwd ...]
```
- system+user를 하나의 프롬프트로 합성(또는 system 지시를 상단 고정).
- `--output-format json` 결과에서 최종 텍스트 필드 추출.
- **정제 과제**: 에이전트 서두/요약/툴 흔적 제거 → "코칭 텍스트만" 파싱. `PdsaCoach`의
  태그 규약(`기대평가:`/`판정:` 등)을 프롬프트로 강제해 파서 재사용.
- **장점**: Anthropic API 직접 호출·ToS 이슈 회피, 순수 공식·무키.
- **단점**: 지연·부하↑, 출력 변동성↑, 주간 크레딧 풀 차감.

---

## 5. 리스크 / 미결 (구현 착수 전 해소)
1. **[검증·차단성]** 구독 OAuth로 RAW Messages API 프로그램 호출의 약관 허용 범위 → 불가 시 P2 승격.
2. **[실측]** 토큰 수명·갱신 주기, 주간 크레딧 풀 정책/요율.
3. **[결정]** 기본 모델 ID 소스 및 `pdsa config` 표면.
4. **[보정]** `ModelsCommand`/`GuideCommand` 팩토리 라우팅.
5. **[결정]** P1/P2 동시 제공 여부(우선 P1, P2는 후속 사이클).

## 6. 다음 PDSA 사이클 계획 (구현 착수 시)
1. **Plan**: "미결 1(약관) 해소 후 P1 최소구현 — `ClaudeCode.cs`+`ClaudeCodeClient.cs`,
   factory/config/enum 배선, `pdsa check` 통과"를 가설로.
2. **Do**: `CodexClient`를 템플릿으로 미러 구현, `Models`/`Guide` 라우팅 보정.
3. **Study**: `pdsa check` + 1사이클 코칭 산출 확인, 토큰 갱신 경로 검증.
4. **Act**: 통과 시 P2(`claude -p`) 폴백 착수 or 문서화. 실패 시 원인별 개선.
