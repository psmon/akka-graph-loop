---
title: Quickstart
description: Configure an LLM, pick a project, and run a full PDSA cycle in a few commands.
---

This is the shortest path from a fresh install to a recorded, LLM-judged cycle.

## 1. Configure an LLM (once)

Pick whichever provider you have. The default is an OpenAI-compatible API key:

```bash
pdsa config key <your-api-key>     # or: key-file <path> to keep it out of config
pdsa config model gpt-5.6-terra    # any model your endpoint serves
pdsa check                         # verify with a real round-trip
```

Other options — keyless local models, OAuth, Codex, or your logged-in Claude Code — are covered in
[Providers & Auth Modes](/akka-graph-loop/llm/providers/).

## 2. Pick a project

Each project gets its own graph DB, so learning stays separated:

```bash
pdsa project set my-repo
pdsa project show
```

## 3. Run one cycle

```bash
pdsa plan  "Add a request timeout to the API client and prove it fires without breaking the happy path"
pdsa do    "Wrapped the call in a linked CancellationTokenSource; added a unit test for the timeout path"
pdsa study "204 tests green; timeout fired at 1.2s with a friendly message; happy path unaffected"
pdsa act   --note "Follow-up: add an OS-level test that no child process survives the kill"
```

Read the output at each step — `plan` gives you an **expected evaluation** to aim at, and `study` returns a
**verdict** (met / partial / unmet).

## 4. See what accumulated

```bash
pdsa status     # recent cycles + expectation hit-rate (recall)
pdsa eval       # per-cycle expected / verdict / actual
pdsa view       # open the local graph viewer
```

## What just happened

- `plan` asked the LLM to commit to a **verifiable success criterion** and started a cycle.
- `do` organized what you actually did against the plan.
- `study` compared the result to the expected criterion and recorded a **verdict**.
- `act` captured learnings and, if reinforcement is needed, links the next `plan` as a follow-up cycle.
- Every step became nodes and edges in a **per-project Kùzu graph** you can query and visualize.

Next: walk through each step in depth in [Your First Cycle](/akka-graph-loop/getting-started/first-cycle/).
