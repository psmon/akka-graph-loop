#!/usr/bin/env python3
"""Generate one hero card (SVG) per PDSA cycle.

The card content is written in English on purpose: the same images are shared by
both the English and Korean write-ups, so a cycle's card looks identical in either.

Source of truth is the loop's own memory, read through the CLI that cycle #22 added:

    pdsa history --to 22 --full --project akka-graph-loop
    pdsa show <n> --project akka-graph-loop

Regenerate with:  python docs/pdsa-self-loop/cards/_generate.py
"""

import os

VERDICT_COLORS = {
    "met":     ("#3ddc97", "#0d2b21"),
    "partial": ("#f5c451", "#2e2410"),
    "unmet":   ("#ef6b6b", "#2e1414"),
    "none":    ("#7b8ab8", "#161c2c"),
}

VERDICT_LABEL = {
    "en": {"met": "MET", "partial": "PARTIAL", "unmet": "UNMET", "none": "NO VERDICT"},
    "ko": {"met": "충족", "partial": "부분 충족", "unmet": "미충족", "none": "판정 없음"},
}

LABELS = {
    "en": {"cycle": "CYCLE", "learned": "WHAT WE LEARNED"},
    "ko": {"cycle": "사이클", "learned": "무엇을 배웠나"},
}

FONTS = {
    "en": "Inter, Segoe UI, Helvetica, Arial, sans-serif",
    "ko": "Pretendard, Malgun Gothic, Apple SD Gothic Neo, Noto Sans KR, sans-serif",
}

# id, date, era, title, verdict, learned, metric, reinforce note
CYCLES = [
    (1, "2026-08-26", "Recording only",
     "Verify the CLI basics and grow the unit tests", "none",
     "The loop recorded four steps but had no expected outcome to compare against, so nothing could be judged.",
     "no verdict field yet", ""),
    (2, "2026-08-26", "Recording only",
     "Cover the pure Cli/Llm logic with unit tests", "none",
     "Testing the pure logic first made every later refactor cheap - the auth and i18n rewrites landed on top of it.",
     "no verdict field yet", ""),
    (3, "2026-08-26", "Recording only",
     "Graph viewer: show the active project, switch without a restart", "none",
     "Seeing the accumulated graph made the missing pieces obvious - you cannot improve what you cannot look at.",
     "no verdict field yet", ""),
    (4, "2026-08-26", "The loop closes",
     "Close the loop: expected -> verdict -> REINFORCES", "partial",
     "One passing example does not prove the judging criteria are stable. The loop could finally judge itself - "
     "and its first verdict on itself was 'partial'.",
     "73 tests", "reinforced by #5"),
    (5, "2026-08-26", "Auth expansion",
     "Design first: extend LLM auth beyond a single API key", "partial",
     "Keeping the OpenAiClient(LlmOptions) constructor intact confines the whole auth expansion to four files, "
     "with zero churn at the call sites.",
     "5/5 change points identified", "reinforces #4"),
    (6, "2026-08-26", "Auth expansion",
     "Auth abstraction: AuthMode + IAuthProvider", "partial",
     "Keyless mode is only safe when it is limited to private ranges and a remote endpoint demands an explicit opt-in.",
     "91 tests", ""),
    (7, "2026-08-26", "Auth expansion",
     "Keyless open-weight E2E and config-merge tests", "partial",
     "Static OS paths leak into tests: without a path seam, an E2E run can overwrite the user's real global config.",
     "100 tests", ""),
    (8, "2026-08-26", "Auth expansion",
     "GPT OAuth: refresh, persist, device-code polling", "met",
     "Inject the refresher, the HTTP transport and the clock, and token expiry and polling become deterministic "
     "unit tests instead of flaky network ones.",
     "129 tests", ""),
    (9, "2026-08-26", "Ship and localize",
     "pdsa init: embed the skill inside the binary", "partial",
     "Culture-coded resource names (SKILL.en.md) are split into satellite assemblies by MSBuild. "
     "WithCulture=false plus an explicit LogicalName keeps them in the main assembly.",
     "141 tests", ""),
    (10, "2026-08-26", "Ship and localize",
     "i18n across help, coaching and config", "met",
     "Even with InvariantGlobalization=true the locale is still detectable - environment variables plus a "
     "GetUserDefaultUILanguage P/Invoke.",
     "163 tests", ""),
    (11, "2026-08-26", "Self-model providers",
     "Codex OAuth mode, reusing ~/.codex/auth.json", "none",
     "The only cycle ever left open: planned, then abandoned mid-flight. It still sits in the graph as a "
     "plan with no study - the loop does not hide its own loose ends.",
     "left unfinished", ""),
    (12, "2026-08-26", "Self-model providers",
     "Research official self-model provider paths", "unmet",
     "The plan asked for research documents; the work delivered code instead. Study caught the mismatch and "
     "recorded the only 'unmet' in the project's history.",
     "175 tests", "reinforced by #13"),
    (13, "2026-08-26", "Self-model providers",
     "Write the provider survey and design docs", "met",
     "When a cycle's deliverable is a document, the document has to be the expected outcome - otherwise the "
     "work drifts into code and the plan silently fails.",
     "4 candidates judged", "reinforces #12"),
    (14, "2026-08-26", "Self-model providers",
     "Adopt `claude -p` as an LLM provider", "met",
     "The official headless CLI gives a keyless provider, so no prompt-injection workaround was needed to let "
     "the agent use its own model.",
     "189 tests / 7.4s round-trip", "reinforced by #15"),
    (15, "2026-08-27", "Real-use defects",
     "Multi-project: --project on every command", "partial",
     "Positional() was folding option values into the recorded text, so the feature was silently broken. "
     "A value-option whitelist fixed plan, do, study and guide at once.",
     "191 tests", "reinforces #14"),
    (16, "2026-08-27", "Real-use defects",
     "Reinforcement: close the gap #15 never demonstrated", "met",
     "A gap you did not demonstrate is not closed. The reinforcement cycle exists precisely to demonstrate it - "
     "partial became met with evidence.",
     "0 leaks across 8 phases", "reinforces #15"),
    (17, "2026-08-27", "Real-use defects",
     "Run the viewer in-process for the AOT single file", "met",
     "Spawning a second executable breaks a single-file install. HttpListener plus source-generated JSON keeps "
     "the binary self-contained.",
     "194 tests", ""),
    (18, "2026-08-28", "Real-use defects",
     "Timeout and spinner for the Claude CLI provider", "met",
     "A provider with no timeout can hang forever, and a silent wait looks like a freeze. Both are UX defects, "
     "not cosmetics.",
     "1,175ms timeout kill verified", ""),
    (19, "2026-08-30", "Agent-friendly",
     "Structured --json output, recall, --full", "met",
     "recall is not merely a query. Feeding past learnings into the next plan is what makes the memory compound "
     "instead of just accumulate.",
     "213 tests", ""),
    (20, "2026-09-02", "The loop audits itself",
     "Survey Akka.NET Streams and adjacent stacks", "met",
     "The headline Akka.Streams loop runs only the demo - the real command path was plain synchronous code. "
     "And RetryFlow does not exist in Akka.NET, unlike JVM Akka.",
     "20 candidates judged", "reinforced by #21"),
    (21, "2026-09-02", "The loop audits itself",
     "Atomicity, bounded retry, per-phase metrics", "met",
     "Fix atomicity before retry. Retry multiplies failed attempts, and every failed attempt used to leave an "
     "orphan cycle that the next step silently adopted.",
     "orphans 100% -> 0% / 259 tests", "reinforces #20"),
    (22, "2026-09-02", "The loop audits itself",
     "history and show: query the loop's own past", "met",
     "Dogfooding found the gap. Writing this very document was impossible without hand-parsing the graph, "
     "so the missing query became the next cycle.",
     "271 tests", ""),
]

# 한글판 카드 데이터: (id, 제목, 배운 것, 지표, 보강 표기). 날짜·시기·판정은 CYCLES 와 공유한다.
CYCLES_KO = {
    1: ("CLI 기본기능 검증과 유닛테스트 보강",
        "네 단계를 기록하긴 했지만 비교할 기대값이 없어 무엇도 판정할 수 없었다.",
        "판정 필드 자체가 없음", ""),
    2: ("Cli/Llm 순수 로직을 유닛테스트로 커버",
        "순수 로직을 먼저 테스트해 둔 덕에 이후 인증·i18n 재작성이 값싸게 얹혔다.",
        "판정 필드 자체가 없음", ""),
    3: ("그래프 뷰어: 활성 프로젝트 표시, 재시작 없이 전환",
        "누적된 그래프를 눈으로 보자 빠진 것이 드러났다. 볼 수 없는 것은 개선할 수 없다.",
        "판정 필드 자체가 없음", ""),
    4: ("폐루프: 기대평가 → 판정 → REINFORCES",
        "한 번의 성공 사례가 판정 기준의 안정성을 보장하지는 않는다. 루프가 드디어 자신을 판정했고, 자기 자신에 대한 첫 판정은 '부분 충족'이었다.",
        "테스트 73개", "#5 가 보강"),
    5: ("설계 먼저: API 키 단일 방식을 넘어선 LLM 인증",
        "OpenAiClient(LlmOptions) 생성자를 보존하면 인증 확장 전체가 4개 파일 안에 갇히고 호출부는 하나도 건드리지 않는다.",
        "변경 지점 5/5 식별", "#4 를 보강"),
    6: ("인증 추상화: AuthMode + IAuthProvider",
        "키 없는 모드는 사설 대역으로 제한하고 원격 엔드포인트에는 명시적 opt-in 을 요구할 때만 안전하다.",
        "테스트 91개", ""),
    7: ("키리스 오픈웨이트 E2E와 설정 병합 테스트",
        "정적 OS 경로는 테스트로 새어 나온다. 경로 주입 seam 이 없으면 E2E 실행이 사용자의 실제 전역 설정을 덮어쓴다.",
        "테스트 100개", ""),
    8: ("GPT OAuth: 갱신·저장·device-code 폴링",
        "리프레셔·HTTP 전송·시계를 주입하면 토큰 만료와 폴링이 불안정한 네트워크 테스트가 아니라 결정적 단위테스트가 된다.",
        "테스트 129개", ""),
    9: ("pdsa init: 스킬을 바이너리에 임베드",
        "SKILL.en.md 처럼 문화권 코드가 든 리소스명은 MSBuild 가 위성 어셈블리로 분리한다. WithCulture=false 와 명시적 LogicalName 이 있어야 메인 어셈블리에 남는다.",
        "테스트 141개", ""),
    10: ("도움말·코칭·설정 전반의 i18n",
         "InvariantGlobalization=true 에서도 로케일 감지는 가능하다. 환경변수와 GetUserDefaultUILanguage P/Invoke 조합이면 된다.",
         "테스트 163개", ""),
    11: ("Codex OAuth 모드 (~/.codex/auth.json 재사용)",
         "유일하게 중도에 버려진 회차. 계획만 있고 Study 가 없다. 루프는 자기 미결을 조용히 지우지 않으므로 그 상태 그대로 그래프에 남아 있다.",
         "미완료로 남음", ""),
    12: ("공식 셀프모델 연동 경로 조사",
         "계획은 조사 문서를 요구했는데 수행은 코드를 냈다. Study 가 그 어긋남을 잡아내 프로젝트 역사상 유일한 '미충족'을 기록했다.",
         "테스트 175개", "#13 이 보강"),
    13: ("프로바이더 조사·설계 문서 작성",
         "산출물이 문서인 회차는 문서 자체가 기대 평가여야 한다. 그러지 않으면 작업이 코드로 흘러가고 계획은 조용히 실패한다.",
         "후보 4개 판정", "#12 를 보강"),
    14: ("claude -p 를 LLM 프로바이더로 채택",
         "공식 헤드리스 CLI 가 키 없는 프로바이더를 준다. 에이전트가 자기 모델을 쓰게 하는 데 프롬프트 주입 편법이 필요 없었다.",
         "테스트 189개 · 왕복 7.4초", "#15 가 보강"),
    15: ("멀티프로젝트: 모든 명령에 --project",
         "Positional() 이 옵션 값을 기록 본문에 접어 넣고 있어서 기능이 조용히 깨져 있었다. 값-옵션 화이트리스트 하나가 plan·do·study·guide 를 한꺼번에 고쳤다.",
         "테스트 191개", "#14 를 보강"),
    16: ("보강: #15 가 실증하지 못한 갭을 닫는다",
         "실증하지 않은 갭은 닫힌 게 아니다. 보강 사이클은 바로 그것을 실증하려고 존재한다 — 부분 충족이 근거와 함께 충족이 됐다.",
         "8개 단계 전부 누출 0", "#15 를 보강"),
    17: ("AOT 단일 실행파일을 위한 인프로세스 뷰어",
         "두 번째 실행파일을 띄우는 구조는 단일 파일 설치본에서 깨진다. HttpListener 와 소스젠 JSON 이면 바이너리가 자족적으로 남는다.",
         "테스트 194개", ""),
    18: ("Claude CLI 프로바이더의 타임아웃과 스피너",
         "타임아웃 없는 프로바이더는 영원히 멈출 수 있고, 조용한 대기는 정지처럼 보인다. 둘 다 겉치레가 아니라 UX 결함이다.",
         "타임아웃 kill 1,175ms 검증", ""),
    19: ("구조화 --json 출력, recall, --full",
         "recall 은 단순한 조회가 아니다. 과거 학습을 다음 계획에 먹이는 것이 메모리를 그냥 쌓이는 게 아니라 복리로 만든다.",
         "테스트 213개", ""),
    20: ("Akka.NET Streams와 주변 스택 조사",
         "간판인 Akka.Streams 루프는 데모만 돌리고 있었고 실제 명령 경로는 순수 동기 코드였다. 그리고 RetryFlow 는 JVM Akka 와 달리 Akka.NET 에 존재하지 않는다.",
         "후보 20개 판정", "#21 이 보강"),
    21: ("원자성, 제한적 재시도, 단계별 계측",
         "재시도보다 원자성이 먼저다. 재시도는 실패 시도를 늘리는데, 실패 하나마다 고아 사이클이 남고 다음 단계가 그걸 조용히 주워 갔다.",
         "고아 100% → 0% · 테스트 259개", "#20 을 보강"),
    22: ("history 와 show: 루프 자신의 과거를 조회한다",
         "도그푸딩이 공백을 찾았다. 바로 이 문서를 쓰는 일이 그래프를 손으로 파싱하지 않고는 불가능했고, 그 빠진 조회가 다음 사이클이 됐다.",
         "테스트 271개", ""),
}

ERA_KO = {
    "Recording only": "기록만",
    "The loop closes": "루프가 닫힌다",
    "Auth expansion": "인증 확장",
    "Ship and localize": "배포와 현지화",
    "Self-model providers": "셀프모델 프로바이더",
    "Real-use defects": "실사용 결함",
    "Agent-friendly": "에이전트 친화",
    "The loop audits itself": "루프가 자신을 감사한다",
}


W = 960   # 카드 높이는 내용(제목·학습 줄 수)에 맞춰 계산한다 — 아래 card() 참조


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def char_em(c):
    """대략적인 글자 폭(em). 한글/CJK 는 정사각형에 가깝고 라틴은 그 절반쯤이다."""
    return 1.0 if ord(c) > 0x1100 else 0.52


def wrap(text, em_budget):
    """SVG 에는 텍스트 흐름이 없으므로 em 폭 예산으로 직접 줄바꿈한다."""
    words, lines, cur = text.split(), [], ""
    for w in words:
        trial = f"{cur} {w}".strip()
        if sum(char_em(c) for c in trial) <= em_budget:
            cur = trial
        else:
            if cur:
                lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def card(cid, date, era, title, verdict, learned, metric, reinforce, lang="en"):
    accent, pill_bg = VERDICT_COLORS[verdict]
    # 제목 34px, 학습 19px, 본문 폭 856px → em 예산 = 856/글자크기
    title_lines = wrap(title, 856 / 34)[:2]
    learn_lines = wrap(learned, 856 / 19)[:4]

    # 레이아웃을 먼저 계산해 높이를 정한다(고정 높이면 2줄 제목 + 4줄 학습에서 푸터와 겹친다).
    title_y = 178
    divider_y = title_y + 44 * len(title_lines) + 6
    learn_y = divider_y + 74
    H = learn_y + 30 * (len(learn_lines) - 1) + 66

    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" '
        f'font-family="{FONTS[lang]}" role="img" '
        f'aria-label="PDSA cycle {cid}: {esc(title)}">',
        '<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0%" stop-color="#141b2d"/><stop offset="100%" stop-color="#0b0f1a"/>'
        '</linearGradient></defs>',
        f'<rect width="{W}" height="{H}" rx="22" fill="url(#bg)"/>',
        f'<rect width="10" height="{H}" rx="5" fill="{accent}"/>',
        # header
        f'<text x="52" y="86" fill="{accent}" font-size="17" font-weight="700" letter-spacing="3.5">'
        f'{LABELS[lang]["cycle"]} {cid:02d}</text>',
        f'<text x="52" y="116" fill="#6f7d9c" font-size="15" letter-spacing="1.2">{date} &#183; {esc(era.upper())}</text>',
        # verdict pill
        f'<rect x="{W-52-168}" y="60" width="168" height="38" rx="19" fill="{pill_bg}" stroke="{accent}" '
        f'stroke-opacity="0.55"/>',
        f'<text x="{W-52-84}" y="85" fill="{accent}" font-size="15" font-weight="700" letter-spacing="2.4" '
        f'text-anchor="middle">{VERDICT_LABEL[lang][verdict]}</text>',
    ]

    y = title_y
    for line in title_lines:
        parts.append(f'<text x="52" y="{y}" fill="#eef2fb" font-size="34" font-weight="700">{esc(line)}</text>')
        y += 44

    parts.append(f'<line x1="52" y1="{divider_y}" x2="{W-52}" y2="{divider_y}" stroke="#26304a" stroke-width="1"/>')

    parts.append(f'<text x="52" y="{divider_y+40}" fill="{accent}" font-size="13" font-weight="700" '
                 f'letter-spacing="3">{LABELS[lang]["learned"]}</text>')

    y = learn_y
    for line in learn_lines:
        parts.append(f'<text x="52" y="{y}" fill="#c2ccE4" font-size="19">{esc(line)}</text>')
        y += 30

    # footer
    parts.append(f'<text x="52" y="{H-34}" fill="#7b8ab8" font-size="15" font-weight="600">{esc(metric)}</text>')
    if reinforce:
        parts.append(f'<text x="{W-52}" y="{H-34}" fill="{accent}" font-size="15" text-anchor="end">'
                     f'&#8618; {esc(reinforce)}</text>')

    parts.append("</svg>")
    return "\n".join(parts)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    en_dir, ko_dir = here, os.path.join(os.path.dirname(here), "cards-ko")
    os.makedirs(ko_dir, exist_ok=True)

    def write(path, svg):
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(svg + "\n")

    for cid, date, era, title, verdict, learned, metric, reinforce in CYCLES:
        write(os.path.join(en_dir, f"cycle-{cid:02d}.svg"),
              card(cid, date, era, title, verdict, learned, metric, reinforce, "en"))

        ko_title, ko_learned, ko_metric, ko_reinforce = CYCLES_KO[cid]
        write(os.path.join(ko_dir, f"cycle-{cid:02d}.svg"),
              card(cid, date, ERA_KO[era], ko_title, verdict, ko_learned, ko_metric, ko_reinforce, "ko"))

    print(f"{len(CYCLES)} EN cards -> {en_dir}")
    print(f"{len(CYCLES)} KO cards -> {ko_dir}")


if __name__ == "__main__":
    main()
