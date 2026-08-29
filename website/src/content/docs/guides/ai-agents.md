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

## Concurrency for multi-agent setups

If several agents (or several flows) run at once, don't rely on the global active project — pass
`--project <name>` per command so each runs against its own DB. See
[Multi-project](/akka-graph-loop/guides/multi-project/).

:::tip
Prefer an official API key for automated agent use. The `claude -p` provider is convenient but has startup
latency and can burn subscription tokens quickly — see
[Providers & Auth Modes](/akka-graph-loop/llm/providers/).
:::
