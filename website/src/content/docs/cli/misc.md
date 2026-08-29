---
title: "init · guide · run · version"
description: Install the agent skill, get one-off advice, run the demo feedback cycle, and print version info.
---

## `pdsa init`

Install the PDSA skill into a workspace so an agent can trigger the loop.

```bash
pdsa init            # writes .claude/skills/pdsa/SKILL.md
pdsa init --lang en  # or --lang ko
```

See [For AI Agents](/akka-graph-loop/guides/ai-agents/).

## `pdsa guide`

One-off PDSA advice from the LLM (a simple pass-through prompt — it does **not** record a cycle).

```bash
pdsa guide "<question / situation>"
```

## `pdsa run`

Run the demo **PDSA feedback cycle** — the reference `PdsaLoop` implemented as a real Akka.Streams cycle —
and record the run to the graph DB. This is the learning demo, distinct from the agent workflow commands.

```bash
pdsa run
```

## `pdsa version`

Print the version, .NET runtime, and stack.

```bash
pdsa version
# pdsa <version>
#   .NET <runtime>
#   Stack: Akka.Streams (PDSA feedback cycle) · Kùzu embedded graph DB · OpenAI
```
