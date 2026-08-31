---
title: For AI Agents
description: pdsa is designed so an AI agent performs work as PDSA cycles and accumulates learning into graph memory — with a ready-to-use Claude Code skill.
---

`pdsa` is built for a specific workflow: an AI agent (for example, Claude Code) performs its work **as PDSA
cycles** and accumulates learning into a graph it can **recall in later sessions**.

## The idea

Instead of a flat transcript, the agent leaves behind a structured memory: hypotheses (Plan's *expected*),
outcomes (Study's *verdict* and *actual*), and follow-up chains (`REINFORCES`). Next session, it can query
that graph to remember what was tried, what worked, and what's still unfinished.

## The Claude Code skill

This repo ships a ready-to-use skill at **`.claude/skills/pdsa/SKILL.md`**. Mentioning **"pdsa"** in a new
session triggers the Plan → Do → Study → Act flow, with the CLI recording each step.

Install the skill into any workspace:

```bash
pdsa init            # writes .claude/skills/pdsa/SKILL.md
pdsa init --lang en  # or --lang ko
```

## Recommended agent loop

1. **Plan** the task and let the LLM set a verifiable *expected* evaluation.
2. **Do** the work, testing the hypothesis from Plan.
3. **Study** the result — record numbers and the verdict.
4. **Act** — capture learnings; reinforce if the verdict wasn't `met`.
5. Repeat. Use `pdsa status` / `pdsa view` to see the memory grow.

## Structured output (`--json`)

An agent shouldn't parse coaching prose. Add `--json` to `plan` / `do` / `study` / `act` / `status` / `eval`
/ `recall` and the command writes a single JSON object to stdout instead of the prose banners — exposing the
fields the CLI already parsed. Default output is unchanged, so this is a pure opt-in.

```bash
pdsa study "p95 320→240ms, cache hit 40→75%" --json
# {"project":"my-repo","cycle":7,"expected":"…","verdict":"partial","actual":"…","narrative":"…","llmEnabled":true}
```

Key fields: `verdict` is `met` / `partial` / `unmet`; `act --json` adds `reinforce` + the running `hitRate`;
`llmEnabled: false` means the LLM was unconfigured (recorded only, no coaching/verdict) — so don't rely on the
exit code alone. For prose without truncation, use `pdsa status --full` / `pdsa eval --full`.

## Recall — memory that feeds back into planning

`plan` **auto-injects** recent-cycle learnings into the coaching prompt, so the agent stops repeating past
mistakes across sessions (opt out with `--no-recall`). To pull context explicitly — for example before
planning a related task — use `pdsa recall`:

```bash
pdsa recall "cache invalidation" --json
# {"project":"my-repo","topic":"cache invalidation","learnings":[{"cycle":3,"verdict":"unmet","expected":"…","actual":"…","study":"…","act":"…"}]}
```

This turns the accumulated graph from write-only memory into a **read-back loop**: past `expected` / `verdict`
/ `actual` and learnings become planning context for the next cycle.

## Concurrency for multi-agent setups

If several agents (or several flows) run at once, don't rely on the global active project — pass
`--project <name>` per command so each runs against its own DB. See
[Multi-project](/akka-graph-loop/guides/multi-project/).

:::tip
Prefer an official API key for automated agent use. The `claude -p` provider is convenient but has startup
latency and can burn subscription tokens quickly — see
[Providers & Auth Modes](/akka-graph-loop/llm/providers/).
:::
