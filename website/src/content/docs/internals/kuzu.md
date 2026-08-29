---
title: Kùzu Interop
description: How pdsa embeds the Kùzu graph database via P/Invoke, and how the native library is provisioned at build time.
---

Recording uses **[Kùzu](https://kuzudb.com/)** — an in-process embedded graph database with Cypher, *"the
SQLite/DuckDB of graph DBs."* It runs inside the `pdsa` process; there is no server to manage.

## P/Invoke, not NuGet

There is no official Kùzu NuGet package, so `pdsa` calls the **C API directly**:

- `Kuzu/KuzuNative.cs` — the raw C API bindings via P/Invoke.
- `KuzuGraph` — a thin, ergonomic wrapper used by the rest of `AkkaGraphLoop.Core`.

This keeps the interop AOT-friendly (see [Native AOT Build](/akka-graph-loop/internals/aot/)) — no reflection
or dynamic loading of managed assemblies.

## Native library at build time

The native `libkuzu` (~12 MB) is **not committed to git**. Instead it's downloaded automatically during
build/publish:

- `native/Kuzu.targets` fetches `libkuzu`, **pinned to v0.11.3**.
- It's copied to the output folder for the **host OS/arch**, next to the executable.

That means a clean checkout builds without any manual native-library steps, and the published binary carries
the correct `libkuzu` for its platform.

## The recorded model

The graph holds PDSA **cycles**, their four **phase** nodes (Plan/Do/Study/Act), the Plan's **expected**
evaluation, the Study's **verdict** and **actual**, and `REINFORCES` edges between cycles. The readers in
`AkkaGraphLoop.Core` query this with Cypher — the same data surfaced by `pdsa status`, `pdsa eval`, and the
[viewer](/akka-graph-loop/guides/viewer/).
