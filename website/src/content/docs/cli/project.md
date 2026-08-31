---
title: "project · status · eval · view"
description: Manage projects, inspect accumulated cycles and the hit-rate, and open the graph viewer.
---

## `pdsa project`

Manage the active project (each project = a separate graph DB). See
[Multi-project](/akka-graph-loop/guides/multi-project/).

```bash
pdsa project set <name>   # persist the active project
pdsa project list         # projects + cycle counts (* = active)
pdsa project show         # active project / DB path
pdsa project clear        # unset (fall back to cwd name)
```

## `pdsa status`

Recent cycles and the expectation hit-rate ([recall](/akka-graph-loop/concepts/recall/)).

```bash
pdsa status [--project <name>] [--limit 5] [--full] [--json]
```

Shows accumulated cycle count, the hit-rate (`met / cycles-with-a-verdict`), and a summary of recent cycles
with their per-step text and verdicts. Per-step prose is truncated to one line by default — add `--full` for
the untruncated text, or `--json` for the complete structured record.

## `pdsa eval`

Per-cycle breakdown: expected / verdict / actual, plus the hit-rate.

```bash
pdsa eval [--project <name>] [--limit 10] [--full] [--json]
```

Use it to audit *why* the hit-rate is what it is — which expectations were met, partially met, or missed.
`--full` disables truncation; `--json` returns the per-cycle records as data.

## `pdsa recall`

Read accumulated **learnings** back out of graph memory — the same context `plan` injects automatically. Give
a topic keyword to filter to related cycles, or omit it for the most recent.

```bash
pdsa recall ["<topic>"] [--limit 5] [--json] [--project <name>]
```

Returns each matching cycle's `expected` / `verdict` / `actual` and the Study/Act learning narratives (full,
untruncated). An agent can call this before planning to pull relevant prior context on demand. See
[For AI Agents](/akka-graph-loop/guides/ai-agents/).

:::note
Don't confuse this with **recall** the metric ([expectation hit-rate](/akka-graph-loop/concepts/recall/)).
The `recall` *command* reads learnings back; the hit-rate is `met / cycles-with-a-verdict`.
:::

## `pdsa view`

Open the local, self-contained graph viewer. See [Graph Viewer](/akka-graph-loop/guides/viewer/).

```bash
pdsa view
```

Serves the recorded graph in-process on a local port (works from the AOT single-file install). Study nodes
are colored by verdict; `REINFORCES` edges and a hit-rate badge are shown.
