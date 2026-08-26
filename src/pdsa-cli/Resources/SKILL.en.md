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

## 2. One cycle (P → D → S → A)

Run at least one cycle per task. **Read each step's CLI output and apply it to the actual work.**

1. **Plan** — enter what you're about to do.
   `pdsa plan "<what, why, and how>"`
   → Read the **[Hypothesis]** and **[Metrics]** in the output. Proceed in a direction that tests that hypothesis.
2. **Do** — report what you actually did.
   `pdsa do "<what you actually did: changes/commands/observations>"`
   → Check the **[Plan→Do summary]** (spot gaps vs. the plan).
3. **Study** — report results/observations (including measurements).
   `pdsa study "<result numbers and observations; was the hypothesis right?>"`
   → Read the **[Learnings & improvements]**. (Not "Check (did it pass?)" but "What did we learn?")
4. **Act** — get the next improvement action.
   `pdsa act`  (optional: `--note "<note>"`)
   → Take the **[Next improvement action]** and carry it into the next cycle's `pdsa plan`.

## 3. Operating rules

- **Summarize each step's output** (hypothesis/summary/learnings/improvements) briefly for the user, and **apply it to the next work**.
- Don't stop at planning — actually test the hypothesis `plan` set, and leave the learning via `study`.
- When switching between repos/projects, start with `pdsa project set <name>` (each project's memory is separate).
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
| `pdsa status` / `pdsa view` | Accumulated state / graph viewer |
| `pdsa config …` / `pdsa check` / `pdsa models` | LLM key·model config / connection check / model list |

Full help: `pdsa` (no args) or `pdsa <command> --help`.
