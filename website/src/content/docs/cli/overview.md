---
title: CLI Overview
description: Every pdsa command at a glance, plus global options and how state is stored.
---

`pdsa` coaches an AI agent through Deming's cycle and accumulates each step into a per-project graph DB. Run
it with no arguments for full help; add `--help` to any command for its usage.

```bash
pdsa            # full help
pdsa <command> --help
```

## Commands

| Command | Purpose |
| --- | --- |
| [`plan`](/akka-graph-loop/cli/cycle/) | Enter a plan → LLM sets a verifiable **expected** evaluation (starts a cycle) |
| [`do`](/akka-graph-loop/cli/cycle/) | Report what you did → organizes Plan → Do |
| [`study`](/akka-graph-loop/cli/cycle/) | Report results → learning + **verdict** (met/partial/unmet) |
| [`act`](/akka-graph-loop/cli/cycle/) | Learnings + reinforcement decision (closes the cycle) |
| [`status`](/akka-graph-loop/cli/project/) | Recent cycles + expectation hit-rate |
| [`eval`](/akka-graph-loop/cli/project/) | Per-cycle expected / verdict / actual + hit-rate |
| [`recall`](/akka-graph-loop/cli/project/) | Read back prior-cycle learnings (planning context) |
| [`project`](/akka-graph-loop/cli/project/) | Set / list / show / clear the active project |
| [`view`](/akka-graph-loop/cli/project/) | Local graph viewer |
| [`config`](/akka-graph-loop/cli/config/) | LLM key / model / provider / auth / language |
| [`check`](/akka-graph-loop/cli/config/) | Verify the LLM with a real round-trip |
| [`models`](/akka-graph-loop/cli/config/) | List models the endpoint serves |
| [`init`](/akka-graph-loop/cli/misc/) | Install the PDSA skill into a workspace |
| [`guide`](/akka-graph-loop/cli/misc/) | One-off PDSA advice from the LLM |
| [`run`](/akka-graph-loop/cli/misc/) | Run the demo PDSA feedback cycle (Akka.Streams) |
| [`update`](/akka-graph-loop/cli/misc/) | Check the latest version and update (npm global) |
| [`version`](/akka-graph-loop/cli/misc/) | Version + runtime + stack |

## Global options

| Option | Effect |
| --- | --- |
| `--lang <en\|ko\|auto>` | Language for this invocation. See [Language](/akka-graph-loop/llm/language/). |
| `--project <name>` | Run this command against a specific project's DB (per-command). |
| `--json` | Machine-readable JSON instead of prose. Opt-in on `plan`/`do`/`study`/`act`/`status`/`eval`/`recall`. See [For AI Agents](/akka-graph-loop/guides/ai-agents/). |
| `--full` | On `status`/`eval`, print prose without the 70/90-char truncation. |
| `--help` | Usage for the command. |

## Where state lives

```
Global config : {LocalAppData}/pdsa-cli/openai.json
Graph DBs      : {LocalAppData}/pdsa-cli/{project}/graph.kuzu
Repo secret    : .secret/openai.json   (optional, lowest LLM precedence)
```

## Text is safe

Quotes and newlines inside your Plan/Do/Study text are stored via parameter binding — pass them as-is.
