---
title: Native AOT Build
description: How pdsa ships as a Native AOT single executable with Akka.NET and Kùzu — and the gotchas that makes possible.
---

`pdsa` is published as a **Native AOT single executable** per platform, with Akka.NET and Kùzu bundled. Each
platform is published on its own CI runner (host = target).

```bash
dotnet publish src/pdsa-cli -c Release -r win-x64   -p:Version=0.0.1   # Windows
dotnet publish src/pdsa-cli -c Release -r linux-x64 -p:Version=0.0.1   # Linux
dotnet publish src/pdsa-cli -c Release -r osx-arm64 -p:Version=0.0.1   # macOS (Apple Silicon)
```

## Making Akka.NET AOT-safe

Akka's default configuration path goes through `ConfigurationManager` (app.config), which **crashes under
AOT / single-file**. The fix is to pass an explicit `ConfigurationFactory.Default()` so that path is never
taken — see `AkkaPdsaEngine`.

## The rest of the stack

- **Kùzu** is a prebuilt C++ library called via **P/Invoke** — inherently AOT-friendly, no managed
  reflection. See [Kùzu Interop](/akka-graph-loop/internals/kuzu/).
- **OpenAI** calls go through `HttpClient` with **source-generated JSON** (`JsonSerializerContext`), so no
  runtime reflection-based serialization is needed — safe under AOT.
- New features follow the same rule: no reflection or dynamic code (e.g. the `claude -p` provider and the
  in-process viewer add only `Console`/`Task`/`Process` and static JSON).

## Why single-file matters

Because everything is one native binary, distribution is trivial: npm fetches the right platform package and
you get a runnable `pdsa` with no .NET runtime install. See
[Distribution (npm)](/akka-graph-loop/internals/distribution/).
