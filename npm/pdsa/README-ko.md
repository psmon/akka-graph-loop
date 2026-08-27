# @webnori/pdsa

**[English](README.md) · 한국어**

![PDSA 루프가 그래프가 되는 순간 — @webnori/pdsa](https://raw.githubusercontent.com/psmon/akka-graph-loop/main/docs/pdsa-graph.png)

**PDSA(Plan–Do–Study–Act) 지속개선 루프 × 그래프 엔지니어링을 지원하는 CLI 서포트 툴.**
Plan 에서 세운 *기대 평가*를 Study 에서 LLM 이 판정(met/partial/unmet)하고, 각 사이클을
**프로젝트별 Kùzu 그래프 메모리**에 누적해 "AI 에이전트를 위한 장기 메모리"를 만든다.
.NET **Native AOT** 단일 실행 파일이며, OS/아키텍처에 맞는 바이너리만 설치된다. 🚧 지속 개발 중.

## 설치

```bash
npm install -g @webnori/pdsa
pdsa version
```

지원 플랫폼: **Windows x64**, **Linux x64**, **macOS (Apple Silicon / arm64)**.
설치 시 npm 이 현재 플랫폼에 맞는 패키지(`@webnori/pdsa-*`)만 내려받는다(네트워크 postinstall 없음).

## 폐루프 한 사이클 (기대 → 판정 → 보강)

```bash
pdsa project set my-repo       # 프로젝트 지정(프로젝트별 그래프 DB)
pdsa plan  "무엇을 왜 어떻게"   # 검증가능한 '기대 평가'(성공 기준) 수립 → 새 사이클
pdsa do    "실제로 한 것"       # Plan→Do 정리
pdsa study "결과 수치·관찰"     # 기대 대비 LLM 판정: met | partial | unmet
pdsa act   --note "메모"        # 학습 + 필요 시 '보강 사이클'로 자동 연결(REINFORCES)
pdsa status                     # 진행/누적 + 기대 충족률(재현율)
pdsa eval                       # 사이클별 기대/판정/실제 + 충족률
pdsa view                       # 로컬 그래프 뷰어
```

- **Plan** — LLM 이 검증 가능한 *기대 평가*(측정지표)를 세운다.
- **Study** — 기대 대비 결과를 **판정**(met/partial/unmet)하고 실제값을 기록한다.
- **Act** — 즉시 보강이 필요하면 다음 `pdsa plan` 이 자동으로 **보강 사이클**(`REINFORCES` 엣지)로 이어진다(`--fresh` 로 옵트아웃).
- **재현율** — 기대 충족률(`met / 판정된 사이클`)을 `status`·`eval`·뷰어에서 확인.

## 그래프 뷰어 (`pdsa view`)

![pdsa view 실제 화면 — 프로젝트 선택, Study 판정색, REINFORCES 엣지, 기대충족률 뱃지](https://raw.githubusercontent.com/psmon/akka-graph-loop/main/docs/pdsa-view.png)

누적된 PDSA 그래프를 로컬 웹 뷰어로 시각화한다: 헤더의 **프로젝트 드롭다운**·**기대충족률 뱃지**,
Study 노드의 **판정색**(met=초록·partial=주황·unmet=빨강), 보강 사이클을 잇는 **`REINFORCES` 엣지**.

## LLM 프로바이더 · 인증 방식 — 판정·코칭용

판정·코칭에 쓸 LLM 을 여러 방식으로 붙일 수 있다. 미설정이어도 입력은 그래프에 **기록**되며 판정·코칭만 생략된다.

```bash
# ① OpenAI(호환) API 키 — 기본
pdsa config key <키>                      # 또는 key-file <파일>(키 미노출)
pdsa config model <모델>                  # 기본 gpt-5.6-terra

# ② 키리스 오픈웨이트(로컬/호환 엔드포인트) — ollama · vLLM · LM Studio 등
pdsa config provider local                # http://localhost:11434/v1, 무인증(사설대역 자동 허용)
pdsa config provider openai-compat <URL>  # 임의 OpenAI 호환 엔드포인트
pdsa config allow-insecure-no-auth true   #   원격을 무인증으로 쓸 때만 명시적 opt-in

# ③ GPT OAuth(refresh 토큰) — device-code 로그인
pdsa config oauth device-endpoint <URL> && pdsa config oauth endpoint <token-URL> && pdsa config oauth client <id>
pdsa config login

# ④ Codex(ChatGPT 구독) — 공식 codex login 토큰 재사용  [experimental]
codex login && pdsa config auth codex

# ⑤ Claude Code(claude -p) — 이미 로그인된 Claude 를 그대로(무키)
pdsa config auth claude-cli

pdsa check                                # 어떤 방식이든 실제 왕복으로 확인
pdsa config show                          # 현재 인증/모델/언어(키 마스킹, 출처 표기)
```

로드 우선순위: 환경변수 → 전역 설정(`{LocalAppData}/pdsa-cli/openai.json`) → 레포 `.secret/openai.json`.

> ⚠️ **Claude Code(`claude -p`) 사용 시 유의**
> - **Anthropic 정책을 먼저 확인**하고, Claude Code(Claude 구독)의 이용약관·정책 범위 안에서 **Claude Code 환경 내에서만** 사용하세요.
> - `claude -p` 는 **공식 API 방식이 아니라** 에이전트 CLI 를 서브프로세스로 호출하는 방식입니다. 시작 지연이 있고 에이전트 내부 컨텍스트 때문에 **토큰을 비효율적으로 사용**해 구독 크레딧이 더 빨리 소모될 수 있습니다. 대량·자동화 용도라면 공식 API 키(①)를 권장합니다.

## 언어 (한국어 / English)

도움말과 PDSA 기록(코칭)을 취향의 언어로 쓸 수 있다. 지정이 없으면 **OS 로케일 자동 감지**(한글이면 한국어, 그 외 영어).

```bash
pdsa config lang ko          # 고정: ko | en | auto(자동)
pdsa --lang en <명령>        # 이번 호출만 영어
#   또는 환경변수: PDSA_LANG=en
```

우선순위: `--lang` > `PDSA_LANG` > `config lang` > OS 로케일 > 기본 `en`.
선택한 언어로 **도움말**과 **기록되는 코칭 문구**가 함께 표시된다.

## 관련 자료
- **PDSA — 역사적 배경, 이론, 그리고 품질의 유산** (이 프로젝트가 PDSA 위에 세워진 이유, PDCA vs PDSA, 사실검증):
  https://github.com/psmon/akka-graph-loop/blob/main/PDSA-ko.md

## 링크
- 저장소 · 전체 문서(한/영): https://github.com/psmon/akka-graph-loop
- English README: https://github.com/psmon/akka-graph-loop/blob/main/npm/pdsa/README.md

MIT
