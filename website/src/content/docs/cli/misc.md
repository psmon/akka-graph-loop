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

## `pdsa update`

Check the latest version on the npm registry and update the global install.

```bash
pdsa update            # check latest, then update (npm global)
pdsa update --check    # only report current vs. latest (no install)
```

- The `pdsa` help / no-argument screen also shows a **"new version available"** note when you're behind
  (24 h cached, so it stays instant and works offline).
- **Pre-check, no force-kill:** if another `pdsa` process is running, `update` does **not** kill it. On
  Windows a running instance locks the native binary, so `update` asks you to close it first, then retries.
- **Self-lock avoidance:** on Windows the actual `npm i -g` runs in a fresh console while `pdsa` exits, so the
  running executable/DLL is unlocked before replacement; on Linux/macOS it runs inline.

:::note[EPERM: unlink kuzu_shared.dll (Windows)]
If `npm i -g @webnori/pdsa` prints `npm warn cleanup … EPERM … unlink kuzu_shared.dll`, a running `pdsa`
instance — usually **`pdsa view`** — is holding the native library, so npm can't clean its staging folder.
The install itself still succeeds; close the running instance (or use `pdsa update`, which pre-checks and
cleans up leftover `.pdsa-*` staging folders) and the warning goes away.
:::

## `pdsa version`

Print the version, .NET runtime, and stack.

```bash
pdsa version
# pdsa <version>
#   .NET <runtime>
#   Stack: Akka.Streams (PDSA feedback cycle) · Kùzu embedded graph DB · OpenAI
```
