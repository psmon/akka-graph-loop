# @webnori/pdsa

데밍의 **PDSA(Plan-Do-Study-Act)** 지속개선 루프를 수행하고, 각 단계를 **프로젝트별 Kùzu 그래프
메모리**에 누적하는 CLI. 반복할수록 그 프로젝트의 학습이 쌓여 "AI 에이전트를 위한 장기 메모리"가 된다.

.NET **Native AOT** 로 빌드된 단일 실행 파일이며, OS/아키텍처에 맞는 바이너리만 설치된다.

## 설치

```bash
npm install -g @webnori/pdsa
pdsa version
```

지원 플랫폼: **Windows x64**, **Linux x64**, **macOS (Apple Silicon / arm64)**.
설치 시 npm 이 현재 플랫폼에 맞는 패키지(`@webnori/pdsa-*`)만 내려받는다(네트워크 postinstall 없음).

## 한 사이클 (Plan → Do → Study → Act)

```bash
pdsa project set my-repo                 # 프로젝트 지정(프로젝트별 그래프 DB)
pdsa plan  "무엇을 왜 어떻게 할지"        # 기대 평가(성공 기준) 수립 → 새 사이클
pdsa do    "실제로 한 것"                 # Plan→Do 정리
pdsa study "결과 수치와 관찰"             # 기대 대비 판정(met/partial/unmet)
pdsa act   --note "메모"                  # 학습 + 필요 시 보강 사이클로 연결
pdsa status            # 누적/재현율      pdsa eval    # 사이클별 기대·판정·실제
pdsa view              # 그래프 뷰어(로컬)
```

LLM 설정(판정·코칭용):

```bash
pdsa config key <키>        # 또는 key-file <파일>
pdsa config model <모델>
pdsa check                  # 연결 확인
```

전체 도움말: `pdsa` (인자 없이) 또는 `pdsa <명령> --help`.

## 라이선스
MIT · https://github.com/psmon/akka-graph-loop
