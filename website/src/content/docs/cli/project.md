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
pdsa status [--project <name>] [--limit 5]
```

Shows accumulated cycle count, the hit-rate (`met / cycles-with-a-verdict`), and a summary of recent cycles
with their per-step text and verdicts.

## `pdsa eval`

Per-cycle breakdown: expected / verdict / actual, plus the hit-rate.

```bash
pdsa eval [--project <name>]
```

Use it to audit *why* the hit-rate is what it is — which expectations were met, partially met, or missed.

## `pdsa view`

Open the local, self-contained graph viewer. See [Graph Viewer](/akka-graph-loop/guides/viewer/).

```bash
pdsa view
```

Serves the recorded graph in-process on a local port (works from the AOT single-file install). Study nodes
are colored by verdict; `REINFORCES` edges and a hit-rate badge are shown.
