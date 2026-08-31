---
name: pdsa
description: >-
  Run a Deming PDSA (Plan-Do-Study-Act) continuous-improvement cycle for a coding task using this
  repo's local `pdsa` CLI, recording every step into a per-project Kùzu graph memory that accumulates
  across runs. Use when the user mentions "pdsa", 지속개선/개선 사이클/회고, plan-do-study-act, wants a
  task planned with a verifiable hypothesis, wants a finished task closed out with learnings and next
  actions, or wants the task's learnings kept as long-term graph memory. Triggers: "pdsa", "지속개선",
  "회고", "개선 사이클", "plan do study act", "가설 세워", "다음 개선점".
---

# PDSA 지속개선 사이클 (pdsa CLI)

데밍의 PDSA(Plan → Do → Study → Act) 루프로 작업을 수행하고, 각 단계를 **프로젝트별 그래프 DB(Kùzu)** 에
누적한다. 반복할수록 그 프로젝트의 학습이 쌓여 "AI 에이전트를 위한 장기 메모리"가 된다.

## 0. CLI 호출 방법 정하기 (한 번)

이 저장소 루트에서 아래로 사용 가능한 형태를 정한다. 이 문서의 `pdsa` 를 정한 형태로 바꿔 읽는다.

- `pdsa version` 이 동작하면 → 그대로 `pdsa` 사용.
- 아니면 개발 트리 형태: `dotnet run --project src/pdsa-cli -- <명령>`.
  - 반복 호출이 잦으면 한 번 빌드해 두는 게 빠르다:
    `dotnet build src/pdsa-cli -c Release` → 이후 `src/pdsa-cli/bin/Release/net10.0/pdsa <명령>`.

## 1. 시작 전 점검

1. **LLM 연결 확인**: `pdsa check`
   - 성공(✔)이면 진행.
   - 실패면 사용자에게 설정을 요청:
     `pdsa config key <키>` 또는 `pdsa config key-file <파일경로>`(키 미노출), 그리고 `pdsa config model <모델>`.
     지원 모델은 `pdsa models --filter gpt-5.6` 로 확인(기본 `gpt-5.6-terra`).
2. **프로젝트 지정**: `pdsa project set <프로젝트명>` (보통 현재 저장소 이름).
   - 이후 모든 기록이 이 프로젝트 DB 에 쌓인다. 확인/목록: `pdsa project show`, `pdsa project list`.
   - **동시 실행(멀티프로젝트)**: `project set` 은 개인 단위 전역 상태라, 여러 프로젝트를 병렬로 돌리면 서로 덮어쓴다. 대신 각 명령에 `--project <이름>` 을 붙이면 그 호출만 해당 프로젝트 DB 로 독립 실행된다(인자 없으면 전역/현재 디렉터리로 폴백). CLI 는 상태 없이 실행 후 종료되므로, 서로 다른 프로젝트는 DB 가 분리되어 동시에 돌려도 충돌하지 않는다.
     - 예: `pdsa plan "…" --project svc-a` 와 `pdsa plan "…" --project svc-b` 를 동시에.

## 2. 한 사이클 (P → D → S → A)

작업 하나당 최소 한 사이클을 돈다. 각 단계의 **CLI 출력을 읽고 실제 작업에 반영**한다.

1. **Plan** — 이번에 할 일을 입력한다.
   `pdsa plan "<무엇을 왜 어떻게 할지>"`
   → 출력의 **`기대 평가:`** 한 줄(검증가능한 성공 기준)과 **코칭·가설** 서술을 읽는다. 그 기대 평가를 검증하는 방향으로 작업을 진행한다.
   → 최근 사이클의 학습이 코칭에 **자동 주입**된다(누적 메모리 되먹임). 끄려면 `--no-recall`.
2. **Do** — 실제 수행한 내용을 보고한다.
   `pdsa do "<실제로 한 것: 변경/명령/관찰>"`
   → 출력의 **[Plan→Do 정리]** 를 확인한다(계획 대비 차이 파악).
3. **Study** — 결과/관찰(측정값 포함)을 보고한다.
   `pdsa study "<결과 수치와 관찰. 가설이 맞았는지>"`
   → 출력의 **[학습·개선점]** 을 읽는다. ('Check(됐나?)' 가 아니라 '무엇을 배웠나?')
4. **Act** — 다음 개선 액션을 받는다.
   `pdsa act`  (선택: `--note "<메모>"`)
   → 출력의 **[다음 개선 액션]** 을 받아, 그것을 반영해 다음 사이클의 `pdsa plan` 으로 잇는다.

## 3. 운영 규칙

- 각 단계 출력(가설/정리/학습/개선점)을 **사용자에게 짧게 요약**해 전달하고, **다음 작업에 반영**한다.
- 계획만 하고 끝내지 말 것 — `plan` 이 세운 가설을 실제로 검증하고 `study` 로 학습을 남긴다.
- 여러 저장소/프로젝트를 오갈 땐 시작 시 `pdsa project set <이름>` 으로 전환한다(각 프로젝트 메모리 분리). 병렬로 동시에 돌릴 땐 `project set` 대신 각 명령에 `--project <이름>` 을 붙인다(위 §1.2).
- **팁(공식 아님) — 역할별 분리**: 한 프로젝트 안에서 여러 흐름을 병렬로 돌릴 땐 `<프로젝트>-<역할>` 식 이름을 별도 프로젝트로 써서 `--project` 로 분리하면 각 역할이 독립 사이클을 가진다(예: `myrepo-frontend`, `myrepo-infra`). 한 프로젝트의 '진행 중 사이클' 은 하나만 추적되므로, 동시 진행이 필요하면 이렇게 이름을 나눈다.
- 누적 상태 확인: `pdsa status` (최근 사이클/단계). 그래프 시각화: `pdsa view` (로컬 포트 뷰어).
- 텍스트에 따옴표/개행이 있어도 그대로 전달 가능(파라미터 바인딩으로 안전 저장됨).

## 4. 명령 요약

| 명령 | 용도 |
|---|---|
| `pdsa project set/list/show/clear` | 활성 프로젝트 지정·목록(멀티프로젝트 DB 분리) |
| `pdsa plan "…"` | 계획 입력 → 가설·측정지표 코칭(새 사이클) |
| `pdsa do "…"` | 수행 보고 → Plan→Do 그래프 정리 |
| `pdsa study "…"` | 결과 보고 → 학습·개선점 |
| `pdsa act [--note "…"]` | 다음 개선 액션(사이클 종료) |
| `pdsa recall ["<주제>"]` | 과거 사이클 학습 되읽기(계획 컨텍스트). plan 이 자동 주입 |
| `pdsa status` / `pdsa view` | 누적 상태 / 그래프 뷰어 |
| `pdsa update [--check]` | 최신 버전 확인·업데이트(npm 전역). `--check` 는 확인만 |
| `pdsa config …` / `pdsa check` / `pdsa models` | LLM 키·모델 설정 / 연결 확인 / 모델 목록 |

전체 도움말: `pdsa`(인자 없이) 또는 `pdsa <명령> --help`.

## 5. 에이전트용 구조화 출력(`--json`) & 메모리 되읽기(`recall`)

프로즈(한국어 코칭)를 정규식으로 긁지 말 것. `plan`/`do`/`study`/`act`/`status`/`eval`/`recall` 에
**`--json`** 을 붙이면 stdout 에 한 줄 JSON 객체만 방출된다(프로즈 배너 생략, 기본 출력은 불변).
CLI 가 이미 파싱해 둔 필드를 그대로 노출하므로 파싱이 안정적이다(camelCase).

- `plan --json` → `{project, cycle, reinforceOf, expected, narrative, llmEnabled}`
- `study --json` → `{project, cycle, expected, verdict, actual, narrative, llmEnabled}` (`verdict` = `met|partial|unmet`)
- `act --json` → `{project, cycle, reinforce, what, narrative, hitRate:{met,total}, cycleCount, llmEnabled}`
- `status --json` → 전체(미절삭) 사이클/단계. `eval --json` → 사이클별 기대/판정/실제.
- `recall ["<주제>"] --json` → `{project, topic, learnings:[{cycle, verdict, expected, actual, study, act}]}`

되읽기: `pdsa recall "<주제>"` 로 관련 과거 학습을 당겨와 계획 전 컨텍스트로 삼는다(주제 생략 시 최근 학습).
프로즈 상태를 전체로 보려면 `pdsa status --full` / `pdsa eval --full` (절삭 해제).
`llmEnabled:false` 면 LLM 미설정으로 코칭·판정이 생략된 것(기록만 됨) — 종료코드만 보지 말 것.
