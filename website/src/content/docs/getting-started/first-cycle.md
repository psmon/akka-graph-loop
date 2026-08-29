---
title: Your First Cycle
description: A step-by-step walkthrough of Plan → Do → Study → Act, and how each step lands in the graph.
---

A PDSA cycle is four commands. Each one **reads back coaching from the LLM** and **records structured data**
to the graph. Do the work between the steps — the point is to test the hypothesis you set in `plan`, then
capture what you learned in `study`.

## Plan — commit to an expected outcome

```bash
pdsa plan "what & why & how"
```

`plan` starts a new cycle and asks the LLM to turn your intent into a **verifiable expected evaluation** — a
one-sentence success criterion or metric. People usually plan but omit this; `pdsa` makes it explicit so
`study` has something concrete to judge against.

- Override the auto-generated criterion with `--expect "<your own>"`.
- Force a brand-new cycle (don't link it as a reinforcement of the previous one) with `--fresh`.

## Do — report what you actually did

```bash
pdsa do "what you changed / ran / observed"
```

`do` organizes **Plan → Do** from a graph-engineering view and points out the *gap* between what you planned
and what happened. Now go verify the hypothesis.

## Study — what did we learn?

```bash
pdsa study "results, numbers, observations — did the hypothesis hold?"
```

This is the heart of PDSA. `study` is **not** "Check (did it pass?)" — it's "Study (what did we learn?)".
The LLM compares your result to the Plan's expected evaluation and records a **verdict**:

<span class="verdict met">met</span> — fully met the expected outcome ·
<span class="verdict partial">partial</span> — partially ·
<span class="verdict unmet">unmet</span> — not met.

It also records the measured **actual** and a short learning narrative.

## Act — decide the next move

```bash
pdsa act --note "optional memo"
```

`act` proposes the next improvement action and decides whether **immediate reinforcement** is needed. If the
verdict wasn't `met` (or something clearly needs fixing), the next `pdsa plan` is automatically linked as a
**reinforcement cycle** (a `REINFORCES` edge) — unless you pass `--fresh`. You can also force it with
`--reinforce "<what to reinforce>"`.

:::tip
Quotes and newlines in your text are safe — everything is stored via parameter binding, not string
interpolation.
:::

## The result

After one cycle your graph holds four phase nodes (Plan/Do/Study/Act) with the expected criterion, the
verdict, and the measured actual — plus a `REINFORCES` edge if this cycle carried forward from a previous
one. Run it again next time and the memory compounds.

```bash
pdsa status    # progress + hit-rate
pdsa view      # visualize it
```

Read more about the model in [The PDSA Loop](/akka-graph-loop/concepts/pdsa-loop/) and
[Expected → Verdict → Reinforce](/akka-graph-loop/concepts/closed-loop/).
