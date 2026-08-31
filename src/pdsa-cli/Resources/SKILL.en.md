---
name: pdsa
description: >-
  Run a Deming PDSA (Plan-Do-Study-Act) continuous-improvement cycle for a coding task using this
  repo's local `pdsa` CLI, recording every step into a per-project Kùzu graph memory that accumulates
  across runs. Use when the user mentions "pdsa", continuous improvement / retrospective, plan-do-study-act,
  wants a task planned with a verifiable hypothesis, wants a finished task closed out with learnings and
  next actions, or wants the task's learnings kept as long-term graph memory. Triggers: "pdsa",
  "continuous improvement", "retrospective", "improvement cycle", "plan do study act", "form a hypothesis",
  "next improvement".
---

# PDSA Continuous-Improvement Cycle (pdsa CLI)

Run work through Deming's PDSA (Plan → Do → Study → Act) loop, accumulating each step into a
**per-project graph DB (Kùzu)**. The more you iterate, the more that project's learning builds up —
a "long-term memory for AI agents".

## 0. Decide how to invoke the CLI (once)

From this repo root, decide which form works and read `pdsa` in this doc as that form.

- If `pdsa version` works → use `pdsa` directly.
- Otherwise, dev-tree form: `dotnet run --project src/pdsa-cli -- <command>`.
  - If you call it often, building once is faster:
    `dotnet build src/pdsa-cli -c Release` → then `src/pdsa-cli/bin/Release/net10.0/pdsa <command>`.

## 1. Pre-flight checks

1. **Verify LLM connection**: `pdsa check`
   - On success (✔), proceed.
   - On failure, ask the user to configure:
     `pdsa config key <key>` or `pdsa config key-file <path>` (key not exposed), and `pdsa config model <model>`.
     List supported models with `pdsa models --filter gpt-5.6` (default `gpt-5.6-terra`).
2. **Set the project**: `pdsa project set <name>` (usually the current repo name).
   - From then on every record accumulates into this project's DB. Check/list: `pdsa project show`, `pdsa project list`.
   - **Concurrent runs (multi-project)**: `project set` is per-user global state, so running several projects in parallel would overwrite each other. Instead pass `--project <name>` on each command — that call alone runs independently against that project's DB (falling back to the global/current-dir project when omitted). The CLI is stateless (runs then exits), so different projects have separate DBs and never conflict when run at the same time.
     - e.g. `pdsa plan "…" --project svc-a` and `pdsa plan "…" --project svc-b` concurrently.

## 2. One cycle (P → D → S → A)

Run at least one cycle per task. **Read each step's CLI output and apply it to the actual work.**

1. **Plan** — enter what you're about to do.
   `pdsa plan "<what, why, and how>"`
   → Read the **`Expected:`** line (a verifiable success criterion) and the **coaching & hypotheses** narrative. Proceed in a direction that tests it.
   → Recent-cycle learnings are **auto-injected** into the coaching (accumulated-memory feedback). Disable with `--no-recall`.
2. **Do** — report what you actually did.
   `pdsa do "<what you actually did: changes/commands/observations>"`
   → Check the **[Plan→Do summary]** (spot gaps vs. the plan).
3. **Study** — report results/observations (including measurements).
   `pdsa study "<result numbers and observations; was the hypothesis right?>"`
   → Read the **learnings & improvements** narrative and the **`Verdict:`** (`met|partial|unmet`). (Not "Check (did it pass?)" but "What did we learn?")
4. **Act** — get the next improvement action.
   `pdsa act`  (optional: `--note "<note>"`)
   → Take the **next improvement action** narrative and carry it into the next cycle's `pdsa plan`.

## 3. Operating rules

- **Summarize each step's output** (hypothesis/summary/learnings/improvements) briefly for the user, and **apply it to the next work**.
- Don't stop at planning — actually test the hypothesis `plan` set, and leave the learning via `study`.
- When switching between repos/projects, start with `pdsa project set <name>` (each project's memory is separate). To run them in parallel, pass `--project <name>` on each command instead of `project set` (see §1.2).
- **Tip (not official) — separating by role**: to run multiple flows in parallel within one project, use `<project>-<role>` as distinct project names and split them with `--project` so each role keeps its own cycle (e.g. `myrepo-frontend`, `myrepo-infra`). Only one "in-progress cycle" is tracked per project, so split the name when you need concurrent progress.
- Check accumulated state: `pdsa status` (recent cycles/steps). Visualize the graph: `pdsa view` (local port viewer).
- Text with quotes/newlines can be passed as-is (safely stored via parameter binding).

## 4. Command summary

| Command | Purpose |
|---|---|
| `pdsa project set/list/show/clear` | Set/list the active project (separate multi-project DBs) |
| `pdsa plan "…"` | Enter a plan → hypothesis & metrics coaching (new cycle) |
| `pdsa do "…"` | Report execution → Plan→Do graph summary |
| `pdsa study "…"` | Report results → learnings & improvements |
| `pdsa act [--note "…"]` | Next improvement action (ends the cycle) |
| `pdsa recall ["<topic>"]` | Recall prior-cycle learnings (planning context); auto-injected into plan |
| `pdsa status` / `pdsa view` | Accumulated state / graph viewer |
| `pdsa update [--check]` | Check the latest version & update (npm global); `--check` only checks |
| `pdsa config …` / `pdsa check` / `pdsa models` | LLM key·model config / connection check / model list |

Full help: `pdsa` (no args) or `pdsa <command> --help`.

## 5. Structured output for agents (`--json`) & memory recall (`recall`)

Don't regex the (Korean) coaching prose. Add **`--json`** to `plan`/`do`/`study`/`act`/`status`/`eval`/`recall`
and stdout emits a single-line JSON object only (prose banners skipped; default output unchanged).
It exposes the fields the CLI already parsed, so parsing is stable (camelCase).

- `plan --json` → `{project, cycle, reinforceOf, expected, narrative, llmEnabled}`
- `study --json` → `{project, cycle, expected, verdict, actual, narrative, llmEnabled}` (`verdict` = `met|partial|unmet`)
- `act --json` → `{project, cycle, reinforce, what, narrative, hitRate:{met,total}, cycleCount, llmEnabled}`
- `status --json` → full (untruncated) cycles/phases. `eval --json` → per-cycle expected/verdict/actual.
- `recall ["<topic>"] --json` → `{project, topic, learnings:[{cycle, verdict, expected, actual, study, act}]}`

Recall: `pdsa recall "<topic>"` pulls relevant prior learnings as pre-planning context (omit the topic for
the most recent). For full prose without truncation use `pdsa status --full` / `pdsa eval --full`.
If `llmEnabled` is `false`, the LLM is unconfigured so coaching/verdict were skipped (record-only) — don't rely on exit code alone.
