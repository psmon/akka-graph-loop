# Codex(GPT 구독) OAuth 모드 (experimental)

ChatGPT 구독(Plus/Pro/Team/Enterprise)으로 로그인한 **공식 Codex CLI** 의 인증을 재사용해
`pdsa` 를 API 키 없이 쓴다. (Hermes 에이전트의 방식을 참조. **Claude 는 이 방식을 지원하지 않고, Codex(GPT)는 공식 지원**한다.)

## 왜 이렇게 하나
표준 OpenAI API(`api.openai.com/v1`)는 OAuth 로그인이 없고 API 키만 쓴다. 반면 Codex/ChatGPT 구독은
OAuth(device/PKCE)로 로그인하는 **별도 백엔드**(`chatgpt.com/backend-api/codex`, Responses API)를 쓴다.
그래서 pdsa 는 브라우저 로그인 흐름을 직접 구현하지 않고, **공식 `codex login` 이 만든 토큰을 재사용**한다.

## 메커니즘 (구현 요약)
- **토큰 소스**: `~/.codex/auth.json`(또는 `$CODEX_HOME/auth.json`) — `{ tokens: { access_token(JWT), refresh_token, id_token, account_id } }`
- **account_id**: `tokens.account_id`, 없으면 access_token(JWT)의 `["https://api.openai.com/auth"].chatgpt_account_id`
- **갱신**: 만료 임박(skew 120s) 시 `POST https://auth.openai.com/oauth/token`
  `grant_type=refresh_token, client_id=app_EMoamEEZ73f0CkXaXp7hrann`. refresh_token 은 **단회성** → 갱신분을 `auth.json` 에 **원자적으로 재기록**(codex CLI 계속 동작).
- **추론**: `POST https://chatgpt.com/backend-api/codex/responses` (표준 Responses API, SSE 스트리밍)
  헤더 `Authorization: Bearer <access>`, `ChatGPT-Account-Id`, `originator: codex_cli_rs`, `User-Agent: codex_cli_rs/...`(Cloudflare 우회).
  응답은 `response.output_text.delta` 누적 → `response.completed`.

## 사용법
```bash
codex login                 # 공식 Codex CLI 로 ChatGPT 구독 로그인(~/.codex/auth.json 생성)
pdsa config auth codex      # auth_mode=codex, base=chatgpt.com/backend-api/codex, model=gpt-5-codex
pdsa check                  # 토큰 갱신(필요 시) 후 /responses 왕복 확인
```
API 키로 되돌리려면: `pdsa config auth apikey`.

## 상태: experimental — 사용자 E2E 필요
로컬 계약(JWT/토큰 저장소/refresh/SSE 파싱)은 **단위 테스트로 검증**됐다(`CodexTests`).
그러나 실제 Codex 백엔드 호출은 **미검증**이다(단회성 토큰·구독 과금 때문에 리포 작업 중 실행 안 함).

**E2E 체크리스트**(Codex 로그인 사용자):
1. `codex login` → `~/.codex/auth.json` 생성 확인
2. `pdsa config auth codex` → `config show` 에 `auth_mode: codex(GPT 구독)`, `상태: 설정됨`
3. `pdsa check` → `✔ 성공 … 응답: OK` 이면 통과
4. 실패 시 HTTP 상태(401 재로그인 / 403 Cloudflare / 429 한도)와 메시지 기록 → 필요 시 Responses 요청 스키마/헤더 보정
