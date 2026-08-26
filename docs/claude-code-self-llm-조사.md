# Claude Code 셀프-모델 활용 조사 (pdsa CLI 신규 프로바이더)

> 조사 시점: 2026-08-27 · 대상: `src/pdsa-cli` · 범위: **조사 전용** (구현 없음)
> 목표: pdsa CLI가 외부 LLM(OpenAI)로 나가지 않고, **CLI를 구동하는 코딩 에이전트(Claude Code)가
> 공식적으로 제공하는 기능**을 통해 에이전트 자신의 모델을 셀프로 이용하도록 하는 신규
> LLM 프로바이더의 실현 가능성·인증 방식·입출력 제약·구현 난이도를 판정한다.
> 관련 설계: [[claude-code-provider-설계]] · 선행 로드맵: `docs/llm-auth-확장-설계.md`

---

## 0. 배경 — 이미 있는 "에이전트 모델 차용" 선례

pdsa CLI에는 이미 동일한 패턴이 구현되어 있다. `AuthMode.Codex` 프로바이더는 **OpenAI/ChatGPT
코딩 CLI(`codex`)의 공식 로그인 자격증명**(`~/.codex/auth.json`)을 차용해 ChatGPT 백엔드
Responses API를 호출한다.

- `src/pdsa-cli/Llm/Codex.cs` — `codex login` OAuth 토큰 로드/갱신, JWT `account_id` 추출.
- `src/pdsa-cli/Llm/CodexClient.cs` — 차용한 토큰으로 `POST {base}/responses` SSE 호출,
  `ChatGPT-Account-Id`/`originator`/`User-Agent` 헤더 부착.

즉 "**코딩 CLI가 가진 로그인으로, 별도 API 키 없이, 그 에이전트의 모델을 CLI가 쓴다**"는 요구는
Codex 경로에서 이미 검증됐다. 이번 과제는 **동일 패턴의 Claude Code 판**을 만드는 것이다.
따라서 판정 기준은 "새 기술의 가능성"이 아니라 "**어느 공식 경로가 Codex 패턴에 가장 잘 대응되는가**"이다.

### 채택 기준(후보 공통 평가축)
1. **별도 API 키 불필요** — 사용자가 이미 가진 Claude 구독/로그인을 재사용.
2. **비대화형 1-shot 완성** — `ILlmClient.CompleteAsync(system, user) → text` 계약에 맞음.
3. **공식·안정** — 문서화된 공개 기능(프롬프트 주입 편법·미공개 내부 API 아님).
4. **.NET Native AOT 적합** — 서브프로세스 or HTTP만으로 가능(리플렉션·비공개 SDK 의존 없음).
5. **결정성/저부하** — 풀 에이전트 루프(툴 사용·권한)보다 단발 완성이 예측 가능.

---

## 1. 후보별 조사

### 후보 A — MCP Sampling (`sampling/createMessage`)
서버(=CLI)가 MCP 호스트(=Claude Code)에게 "내 대신 LLM 완성 한 번 돌려달라"고 콜백하는 스펙 기능.
개념적으로는 **"에이전트가 도구에게 자기 모델을 빌려준다"**는 요구에 가장 정확히 대응된다.

- **Claude Code 지원**: ❌ 미구현. MCP 클라이언트 capability로 sampling 미제공
  (공개 이슈 `anthropics/claude-code#1785` — 기능 요청 상태).
- **Claude API MCP connector**(`mcp-client-2025-11-20`)도 "MCP 스펙 중 **tool calls만** 지원"이라
  명시 → sampling 제외.
- **프로토콜 상태**: MCP 스펙 2026-07-28에서 sampling **deprecated**(12개월 폐기 창),
  Multi Round-Trip Requests(MRTR)로 대체 예정.
- **판정**: 개념 최적이지만 **오늘 실현 불가 + 사양 자체가 퇴출 중**. 채택 불가.

### 후보 B — Headless / Print 모드 (`claude -p`)
`claude -p "<prompt>" --output-format json` 으로 Claude Code를 비대화형 실행 → 에이전트 루프를
완주하고 결과를 stdout으로 출력 후 종료. 외부 프로그램이 **서브프로세스**로 호출.

- **모델·인증 재사용**: ✅ 이미 인증된 Claude Code 세션 사용, 별도 API 키 불필요.
- **연동 형태**: 서브프로세스(입력 `-p`/stdin, 출력 stdout). 상태 없음(기본 stateless).
- **주요 플래그**: `--output-format json|text|stream-json`,
  `--permission-mode`(무인 실행 시 자동 승인), `--allowedTools`, `--cwd`.
- **공식·안정성**: ✅ CI/CD·스크립트 연동 목적의 공식 문서화 기능.
- **비용/제약(조사값, 재확인 필요)**: 헤드리스 사용량이 대화형과 분리된 **주간 크레딧 풀**에서
  차감된다는 정보 있음(플랜별 상이). 표준 API 요율. → 값·정책은 구현 착수 전 실측/재확인 대상.
- **주의**: 응답이 **풀 에이전트 루프** 산출물(툴 사용·서두·요약 포함 가능)이라 "단발 채팅 완성"
  형태로 정제하려면 프롬프트/파싱 정돈 필요. 부하·지연이 채팅 API보다 큼.
- **판정**: ✅ **공식·무키·서브프로세스만으로 가능** → .NET AOT에 마찰 최저. 단발 완성 정제가 과제.
- 출처: `https://code.claude.com/docs/en/automation/headless-mode.md`

### 후보 C — Claude Agent SDK (`@anthropic-ai/claude-agent-sdk` / `claude-agent-sdk`)
Claude Code의 하네스·툴·컨텍스트 관리를 라이브러리로 패키징. `query()` 1-shot 지원.

- **모델·인증 재사용**: ✅ Claude Pro/Max **구독 인증**으로 동작(번들 CLI 바이너리 경유),
  별도 API 키 불필요(또는 `ANTHROPIC_API_KEY`/Bedrock·Vertex·Foundry 프록시 선택 가능).
- **연동 형태**: **라이브러리 임베드** — **Python 3.10+ / TypeScript 전용**.
- **성숙도**: 프로덕션 준비됨.
- **판정**: ⚠️ **C#/.NET 바인딩 없음** → pdsa(.NET AOT)에서 직접 임베드 불가.
  굳이 쓰려면 Node/Python 사이드카를 서브프로세스로 띄워야 하는데, 그럴 바엔 후보 B(`claude -p`)가
  더 단순. **간접 채택 후보(비권장)**.
- 출처: `https://code.claude.com/docs/en/agent-sdk/typescript`,
  `https://support.claude.com/en/articles/15036540`

### 후보 D — Claude 로그인 자격증명 재사용 + Anthropic Messages API 직접 호출  ★Codex 패턴의 직접 대응
`CodexClient`가 `codex login` 토큰으로 ChatGPT 백엔드를 치듯, **Claude Code 로그인의 OAuth
토큰을 얻어 `POST https://api.anthropic.com/v1/messages`를 직접 호출**한다.

- **토큰 획득(공식 경로)**:
  - `ant auth print-credentials --access-token` — 활성 프로파일의 단기 액세스 토큰을 출력
    (공식 CLI, 문서화됨). RAW HTTP에 넘기는 정식 방법.
  - `claude setup-token` — Claude 구독에서 Anthropic API 용 OAuth 토큰을 발급하는 공식 명령.
  - (Codex 유사) 로그인 자격증명 파일 직접 파싱 — 위치·포맷이 플랫폼별로 다르고 비공식적 → 지양,
    위 두 CLI 경유를 권장.
- **호출 규약**(claude-api 스킬 문서 기준): OAuth 토큰은 `x-api-key`가 아니라
  `Authorization: Bearer <token>` **+ `anthropic-beta: oauth-2025-04-20`** 헤더 필요,
  `anthropic-version: 2023-06-01`.
- **입출력**: `{system, messages:[{role:user}]}` → `content[].text`. `ILlmClient.CompleteAsync`
  계약에 **1:1 대응**(단발·시스템/유저 분리·순수 텍스트).
- **모델·인증 재사용**: ✅ 구독/로그인 재사용, 별도 API 키 불필요.
- **연동 형태**: 기존 `HttpClient`(+선택 SSE) 재사용. Native AOT 적합(이미 `JsonDocument` 사용).
- **성숙도**: Anthropic SDK/`ant` CLI가 정확히 이 헤더 조합을 공식 문서로 안내. 안정.
- **열린 이슈(구현 전 검증)**:
  - **ToS/약관**: 구독(Pro/Max) OAuth 토큰으로 RAW Messages API를 프로그램에서 호출하는 것이
    사용자 플랜 약관상 허용 범위인지 확인 필요(개인 로컬 도구 사용 맥락). → 검증 항목.
  - **토큰 수명/갱신**: `print-credentials` 토큰은 단기 → 호출 직전 재취득(`CodexClient`의
    `EnsureFreshTokenAsync` 패턴과 동일하게 서브프로세스 재호출로 갱신) 필요.
  - **모델 ID 결정**: 기본값(예: `claude-opus-4-8`)을 config에 두거나 Claude Code가 쓰는 모델을
    반영. `pdsa config model` 로 오버라이드 가능하게.
- **판정**: ✅ **`ILlmClient` 계약 최적합 + 기존 Codex 인프라 재사용 + 공식 토큰 경로**.
  단, ToS 확인이 선결.

---

## 2. 비교표

| 후보 | 무키 재사용 | 1-shot 완성 적합 | 연동 형태 | .NET AOT | 공식·안정 | 오늘 실현 | 종합 |
|---|---|---|---|---|---|---|---|
| A. MCP sampling | ✅(개념) | ✅ | MCP 콜백 | – | ❌ 미구현·폐기중 | ❌ | 불가 |
| B. `claude -p` 헤드리스 | ✅ | △(에이전트 산출물 정제 필요) | 서브프로세스 | ✅ | ✅ | ✅ | **채택(대안)** |
| C. Agent SDK | ✅ | ✅ | 라이브러리(Py/TS) | ❌(바인딩無) | ✅ | △(사이드카) | 비권장 |
| D. 로그인 토큰+Messages API | ✅ | ✅(1:1) | HTTP(+SSE) | ✅ | ✅(공식 토큰 경로) | ✅(ToS 확인後) | **채택(주)** |

범례: ✅ 적합 · △ 조건부 · ❌ 부적합 · – 해당없음

---

## 3. 판정 (채택 후보)

- **주 채택: 후보 D** — Claude 로그인 OAuth 토큰(`ant auth print-credentials` / `claude setup-token`)
  재사용 + Anthropic Messages API 직접 호출. **기존 `CodexClient`/`Codex.cs` 구조를 그대로 미러링**
  하므로 구현 난이도·리스크가 가장 낮고 `ILlmClient` 계약에 1:1 대응. 선결: **ToS 확인**.
- **대안 채택: 후보 B** — `claude -p` 서브프로세스. Anthropic API 직접 호출을 피하고 싶거나 D의
  ToS가 걸릴 때의 **폴백**. 100% 공식·무키지만 에이전트 루프 산출물 정제/부하가 트레이드오프.
- **기각: 후보 A(미구현·폐기), 후보 C(.NET 바인딩 없음 → 사이드카는 B보다 열등).**

구체 설계·삽입점·데이터 계약·PDSA 후속 사이클은 [[claude-code-provider-설계]] 참조.

---

## 4. 미확인 가정 / 후속 실험 항목

1. **[검증]** 구독 OAuth 토큰으로 RAW Messages API 프로그램 호출의 약관 허용 여부(후보 D 선결).
2. **[실측]** `claude -p` 및 Agent SDK/헤드리스의 주간 크레딧 풀 정책·요율(후보 B 비용).
3. **[실측]** `ant auth print-credentials --access-token` 출력 포맷·토큰 수명·갱신 주기.
4. **[결정]** 기본 모델 ID 소스(config 고정 vs Claude Code 사용 모델 반영).
5. **[결정]** `claude -p`(D 폴백) 채택 시 출력 정제 프롬프트·파싱 규약.
6. **[확인]** `ModelsCommand`/`GuideCommand`가 `LlmClientFactory` 우회 → 신규 프로바이더는
   해당 명령에서 라우팅 보정 필요(설계 문서에서 다룸).

## 5. 인용 출처
- Claude Code Headless Mode: `https://code.claude.com/docs/en/automation/headless-mode.md`
- Agent SDK(TS): `https://code.claude.com/docs/en/agent-sdk/typescript`
- Agent SDK × Claude 플랜: `https://support.claude.com/en/articles/15036540`
- Claude API MCP connector(tool calls만 지원): `https://platform.claude.com/docs/en/agents-and-tools/mcp-connector`
- MCP sampling 사양(+deprecation): `https://modelcontextprotocol.io/specification/draft/client/sampling`
- Claude Code MCP sampling 이슈: `https://github.com/anthropics/claude-code/issues/1785`
- `ant auth print-credentials` + `anthropic-beta: oauth-2025-04-20` 규약: Anthropic SDK/CLI 공식 문서(claude-api 스킬 내 `shared/anthropic-cli.md`)
