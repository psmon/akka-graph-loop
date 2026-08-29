---
title: Multi-project
description: Keep a separate graph memory per project — and run several projects concurrently without them clobbering each other.
---

Each project keeps its **own** graph database, so learning never bleeds between repos.

## Managing projects

```bash
pdsa project set <name>   # persist the active project
pdsa project list         # projects + cycle counts (* = active)
pdsa project show         # active project / DB path
pdsa project clear        # unset (fall back to cwd name)
```

Graph DBs accumulate at:

```
{LocalAppData}/pdsa-cli/{project}/graph.kuzu
```

## Resolution priority

```
--project <name>  (one-off, per command)
  → active project (pdsa project set)
  → current directory name
```

## Running projects concurrently

`pdsa project set` writes a **global** active-project state, so if you run several projects in parallel they
would overwrite each other. Instead, pass **`--project <name>` on each command** — that invocation runs
independently against the named project's DB:

```bash
pdsa plan "..." --project svc-a   &
pdsa plan "..." --project svc-b   &
```

The CLI is stateless per invocation (it runs and exits), and each project's DB is separate, so concurrent
runs against different projects don't collide.

:::tip[Split by role]
Within one repo you can run parallel flows by using `<project>-<role>` names as separate projects — e.g.
`myrepo-frontend`, `myrepo-infra`. A single project tracks only **one** in-progress cycle at a time, so
splitting the name gives each role its own independent cycle.
:::
