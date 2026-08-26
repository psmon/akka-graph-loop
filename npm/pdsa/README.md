# @webnori/pdsa

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

## LLM(OpenAI 호환) 설정 — 판정·코칭용

```bash
pdsa config key <키>        # 또는 key-file <파일>(키 미노출)
pdsa config model <모델>    # 기본 gpt-5.6-terra
pdsa check                  # 연결 확인
```

로드 우선순위: 환경변수 → 전역 설정(`{LocalAppData}/pdsa-cli/openai.json`) → 레포 `.secret/openai.json`.
LLM 미설정 시에도 입력은 그래프에 **기록**되며 판정·코칭만 생략된다.

## 링크
- 저장소 · 전체 문서(한/영): https://github.com/psmon/akka-graph-loop
- English README: https://github.com/psmon/akka-graph-loop/blob/main/README-en.md

MIT
