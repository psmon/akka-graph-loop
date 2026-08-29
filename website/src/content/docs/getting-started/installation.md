---
title: Installation
description: Install the pdsa CLI from npm as a self-contained native binary, or run it from a dev tree.
---

`pdsa` ships as a **Native AOT single executable** (Akka.NET and Kùzu bundled) and is distributed through
npm. There is no .NET runtime to install for the released binary.

## Install from npm

```bash
npm i -g @webnori/pdsa
pdsa version
```

The main package is a tiny Node launcher that depends on three platform packages
(`@webnori/pdsa-{win32-x64,linux-x64,darwin-arm64}`) gated by `os`/`cpu`. On install, npm fetches **only**
the binary matching your platform — no postinstall step and no network access beyond the package download.

| Platform | Package | Notes |
| --- | --- | --- |
| Windows x64 | `@webnori/pdsa-win32-x64` | |
| Linux x64 | `@webnori/pdsa-linux-x64` | |
| macOS (Apple Silicon) | `@webnori/pdsa-darwin-arm64` | arm64 only |

## Run from a dev tree

If you cloned [`akka-graph-loop`](https://github.com/psmon/akka-graph-loop), substitute
`dotnet run --project src/pdsa-cli -- <command>` for `pdsa`:

```bash
dotnet run --project src/pdsa-cli -- version
```

For repeated calls, build once and run the produced binary:

```bash
dotnet build src/pdsa-cli -c Release
./src/pdsa-cli/bin/Release/net10.0/pdsa version
```

Building from source requires the **.NET 10 SDK**. The native `libkuzu` (~12 MB) is downloaded automatically
at build time (`native/Kuzu.targets`, pinned to v0.11.3) and copied next to the output — it is not committed
to git.

## Verify

```bash
pdsa version     # prints version + runtime + stack
pdsa             # full help (no arguments)
```

To confirm an LLM is wired up, run a real round-trip once you've configured a provider:

```bash
pdsa check
```

:::note
Without an LLM configured, `pdsa` still **records** every Plan/Do/Study/Act input to the graph — only the
coaching and verdict are skipped. See [Providers & Auth Modes](/akka-graph-loop/llm/providers/).
:::

## Next

- [Quickstart](/akka-graph-loop/getting-started/quickstart/) — the shortest path to a recorded cycle.
- [Your First Cycle](/akka-graph-loop/getting-started/first-cycle/) — Plan → Do → Study → Act, explained.
