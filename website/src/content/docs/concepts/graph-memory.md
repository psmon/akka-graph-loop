---
title: Graph Memory (Kùzu)
description: How pdsa turns a stream of PDSA cycles into a queryable, per-project graph — long-term memory for AI agents.
---

Every step you record becomes **nodes and edges** in a graph database. Because the structure is a graph —
not a flat log — an agent can traverse relationships: which cycles reinforced which, how a hypothesis fared,
what was learned where.

## Why a graph

A PDSA history is inherently relational:

- A **cycle** contains four **phase** nodes (Plan, Do, Study, Act).
- A Plan holds an **expected** evaluation; the matching Study holds a **verdict** and the measured **actual**.
- A cycle can **`REINFORCES`** a previous cycle, forming chains of follow-up work.

Modeling this as a graph lets you ask questions a log can't answer cheaply — *"show every cycle that
reinforced an `unmet` result"* — and gives an AI agent a durable, structured memory to recall.

## Kùzu, embedded

Recording uses **[Kùzu](https://kuzudb.com/)** — an in-process embedded graph database with Cypher, often
described as *"the SQLite/DuckDB of graph DBs."* It runs inside the `pdsa` process; there is no server.

- There is no official NuGet package, so the C API is called via **P/Invoke** (`Kuzu/KuzuNative.cs`) with a
  thin wrapper (`KuzuGraph`).
- The native `libkuzu` (~12 MB) is **downloaded at build time** (`native/Kuzu.targets`, pinned to v0.11.3)
  and copied next to the output for the host OS/arch. Binaries are not committed to git.

See [Kùzu Interop](/akka-graph-loop/internals/kuzu/) for the interop details.

## One database per project

Each project keeps a separate graph, so memory never bleeds across repos:

```
{LocalAppData}/pdsa-cli/{project}/graph.kuzu
```

Resolution priority: `--project <name>` (one-off) → active project (`pdsa project set`) → current directory
name. See [Multi-project](/akka-graph-loop/guides/multi-project/).

## Reading it back

- **Cypher** — the graph is a normal Kùzu database; the readers in `AkkaGraphLoop.Core` query it with Cypher.
- **`pdsa status` / `pdsa eval`** — recent cycles, per-cycle expected/verdict/actual, and the hit-rate.
- **`pdsa view`** — a local visual explorer. See [Graph Viewer](/akka-graph-loop/guides/viewer/).

:::tip[Long-term memory for AI agents]
The whole point: an agent performs work *as PDSA cycles*, and the graph becomes memory it can recall in later
sessions — not just a transcript, but a structured record of hypotheses, verdicts, and what was learned.
:::
