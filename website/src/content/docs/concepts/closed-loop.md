---
title: Expected → Verdict → Reinforce
description: How pdsa closes the improvement loop — a verifiable expectation, an LLM verdict, and an automatic reinforcement link.
---

`pdsa` closes the improvement loop with three linked ideas: a **verifiable expectation** in Plan, an **LLM
verdict** in Study, and an **automatic reinforcement** link carried into the next Plan.

## 1. Expected (Plan)

When you run `pdsa plan`, the LLM turns your intent into a one-sentence **expected evaluation** — a success
criterion or metric you can actually check later. This is the hypothesis the cycle will be judged against.

```bash
pdsa plan "Cache the models list so repeat calls avoid a network round-trip"
#   → expected: "A second `models` call within the TTL returns with zero network requests."
```

Override it explicitly when you already know your criterion:

```bash
pdsa plan "..." --expect "p95 latency of the second call < 5ms"
```

## 2. Verdict (Study)

`pdsa study` compares your reported result to that expectation and records one of:

<span class="verdict met">met</span> — the expected outcome was fully achieved ·
<span class="verdict partial">partial</span> — partially achieved ·
<span class="verdict unmet">unmet</span> — not achieved.

Alongside the verdict it stores the measured **actual** and a short learning narrative. The verdict is an
LLM judgment, normalized to those categories.

## 3. Reinforce (Act → next Plan)

`pdsa act` decides whether **immediate reinforcement** is needed. If the verdict wasn't `met` — or something
clearly still needs work — the **next** `pdsa plan` is automatically linked to this cycle with a
**`REINFORCES`** edge, forming a chain of follow-up cycles.

```bash
pdsa act                      # auto-decides based on the verdict
pdsa act --reinforce "..."    # force reinforcement of a specific thing
pdsa plan "..." --fresh       # opt OUT of linking; start an independent cycle
```

This is what makes the history a *loop* rather than a list: unfinished work threads forward until it's
resolved, and the graph shows the chain.

## Putting it together

```
Plan(expected) ──> Do ──> Study(verdict, actual) ──> Act
     ▲                                                 │
     └──────────── REINFORCES (if verdict ≠ met) ──────┘
```

The health of the whole loop is summarized by **[Recall](/akka-graph-loop/concepts/recall/)** — the share of
cycles that met their expectation.
