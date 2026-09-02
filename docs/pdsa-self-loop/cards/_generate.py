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

VERDICT_LABEL = {"met": "MET", "partial": "PARTIAL", "unmet": "UNMET", "none": "NO VERDICT"}

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

W = 960   # 카드 높이는 내용(제목·학습 줄 수)에 맞춰 계산한다 — 아래 card() 참조


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def wrap(text, width):
    """Greedy wrap by character budget (SVG has no text flow)."""
    words, lines, cur = text.split(), [], ""
    for w in words:
        if len(cur) + len(w) + 1 <= width:
            cur = f"{cur} {w}".strip()
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def card(cid, date, era, title, verdict, learned, metric, reinforce):
    accent, pill_bg = VERDICT_COLORS[verdict]
    title_lines = wrap(title, 46)[:2]
    learn_lines = wrap(learned, 62)[:4]

    # 레이아웃을 먼저 계산해 높이를 정한다(고정 높이면 2줄 제목 + 4줄 학습에서 푸터와 겹친다).
    title_y = 178
    divider_y = title_y + 44 * len(title_lines) + 6
    learn_y = divider_y + 74
    H = learn_y + 30 * (len(learn_lines) - 1) + 66

    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" '
        f'font-family="Inter, Segoe UI, Helvetica, Arial, sans-serif" role="img" '
        f'aria-label="PDSA cycle {cid}: {esc(title)}">',
        '<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0%" stop-color="#141b2d"/><stop offset="100%" stop-color="#0b0f1a"/>'
        '</linearGradient></defs>',
        f'<rect width="{W}" height="{H}" rx="22" fill="url(#bg)"/>',
        f'<rect width="10" height="{H}" rx="5" fill="{accent}"/>',
        # header
        f'<text x="52" y="86" fill="{accent}" font-size="17" font-weight="700" letter-spacing="3.5">'
        f'CYCLE {cid:02d}</text>',
        f'<text x="52" y="116" fill="#6f7d9c" font-size="15" letter-spacing="1.2">{date} &#183; {esc(era.upper())}</text>',
        # verdict pill
        f'<rect x="{W-52-168}" y="60" width="168" height="38" rx="19" fill="{pill_bg}" stroke="{accent}" '
        f'stroke-opacity="0.55"/>',
        f'<text x="{W-52-84}" y="85" fill="{accent}" font-size="15" font-weight="700" letter-spacing="2.4" '
        f'text-anchor="middle">{VERDICT_LABEL[verdict]}</text>',
    ]

    y = title_y
    for line in title_lines:
        parts.append(f'<text x="52" y="{y}" fill="#eef2fb" font-size="34" font-weight="700">{esc(line)}</text>')
        y += 44

    parts.append(f'<line x1="52" y1="{divider_y}" x2="{W-52}" y2="{divider_y}" stroke="#26304a" stroke-width="1"/>')

    parts.append(f'<text x="52" y="{divider_y+40}" fill="{accent}" font-size="13" font-weight="700" '
                 f'letter-spacing="3">WHAT WE LEARNED</text>')

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
    for row in CYCLES:
        path = os.path.join(here, f"cycle-{row[0]:02d}.svg")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(card(*row) + "\n")
    print(f"{len(CYCLES)} cards written to {here}")


if __name__ == "__main__":
    main()
