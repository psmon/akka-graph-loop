---
title: "Cycle: plan · do · study · act"
description: The four commands that make up a PDSA cycle, with their flags.
---

The four cycle commands. Each records to the active project's graph and (if an LLM is configured) prints
coaching. See [Your First Cycle](/akka-graph-loop/getting-started/first-cycle/) for a narrative walkthrough.

## `pdsa plan`

Enter a plan; the LLM sets a verifiable **expected** evaluation and a new cycle starts.

```bash
pdsa plan "<what & why & how>" [--expect "<expected>"] [--fresh] [--no-recall] [--json] [--project <name>]
```

| Flag | Effect |
| --- | --- |
| `--expect "<text>"` | Use your own expected evaluation instead of the LLM-generated one. |
| `--fresh` | Start an independent cycle; do **not** link it as a reinforcement of the previous one. |
| `--no-recall` | Skip auto-injecting recent-cycle learnings into the coaching prompt. |
| `--json` | Emit a JSON object instead of prose. See [For AI Agents](/akka-graph-loop/guides/ai-agents/). |
| `--project <name>` | Record to a specific project's DB. |

If the previous cycle's `act` asked for reinforcement, `plan` links this cycle to it with a `REINFORCES`
edge (unless `--fresh`).

Recent-cycle learnings are **auto-injected** into the coaching prompt so the LLM avoids repeating past
mistakes — pull them explicitly with [`pdsa recall`](/akka-graph-loop/cli/project/), or disable injection with
`--no-recall`.

## `pdsa do`

Report what you actually did; organizes **Plan → Do** and flags the gap.

```bash
pdsa do "<what you changed / ran / observed>" [--json] [--project <name>]
```

Requires an in-progress cycle (run `pdsa plan` first).

## `pdsa study`

Report results; the LLM judges them against the expected evaluation and records a **verdict**.

```bash
pdsa study "<results / metrics / observations>" [--json] [--project <name>]
```

Records: `verdict` (<span class="verdict met">met</span> / <span class="verdict partial">partial</span> /
<span class="verdict unmet">unmet</span>), the measured **actual**, and a learning narrative.

## `pdsa act`

Learnings + reinforcement decision; **closes** the cycle.

```bash
pdsa act [--note "<memo>"] [--reinforce "<what to reinforce>"] [--json] [--project <name>]
```

| Flag | Effect |
| --- | --- |
| `--note "<memo>"` | Attach a free-form memo. |
| `--reinforce "<text>"` | Force reinforcement of a specific thing (next `plan` links here). |
| `--json` | Emit a JSON object (includes `reinforce`, `what`, and the running `hitRate`). |

If the verdict wasn't `met`, the next `pdsa plan` auto-links as a reinforcement cycle unless it's run with
`--fresh`.

:::tip[Machine-readable output]
Every cycle command takes `--json` — the prose banners are skipped and a single JSON object is written to
stdout, exposing the fields the CLI already parsed (`expected` / `verdict` / `actual` / …). Default output is
unchanged. See [For AI Agents](/akka-graph-loop/guides/ai-agents/).
:::
