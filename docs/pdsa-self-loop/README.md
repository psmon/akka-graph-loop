# The Self-Improving Loop — 22 PDSA cycles on this project

**English · [한국어](README-ko.md)**

This project did not just *build* a PDSA tool. It was **built by one** — every change since
2026-08-26 went through the `pdsa` CLI's own Plan → Do → Study → Act loop, and every step is still
in the graph.

This page is that record, read back out of the loop's own memory:

```bash
pdsa history --to 22 --full --project akka-graph-loop   # the whole timeline, oldest first
pdsa show 21 --project akka-graph-loop                  # one cycle in full
```

> Those two commands did not exist when this page was started. Writing it meant hand-parsing the
> graph JSON, which is exactly the kind of gap the loop is supposed to surface — so it became
> [cycle #22](#22--history-and-show-query-the-loops-own-past). This document is the first artifact
> produced *entirely* through the CLI it describes.

---

## What 22 cycles actually changed

| | Cycle 1 | Cycle 22 |
|---|---|---|
| Tests | — | **271** |
| Could the loop judge itself? | no `expected` field existed | `met / partial / unmet` per cycle |
| LLM providers | 1 (OpenAI API key) | **5** (API key · keyless local · GPT OAuth · Codex · `claude -p`) |
| Agent-readable output | prose only | `--json` on 9 commands |
| Does a failed LLM call corrupt memory? | **yes — 100% of the time** | **no — 0/20 injected failures** |
| Can you query a past cycle? | no | `pdsa show <n>` / `pdsa history` |

**Expectation hit rate: 11 / 18 judged cycles (61%).** That number understates the trend:

```
cycles 4–14 (first 10 judged)    ████░░░░░░   4 met  →  40%
cycles 15–22 (last 8 judged)     ████████░░   7 met  →  88%
```

The loop got better at *predicting itself*, which is the actual point of writing a verifiable
expected outcome before doing the work.

---

## The shape of the record

Three cycles produced **no verdict**. That is not missing data — it is the history:

- **#1–#3** ran before the closed loop existed. The tool could record four steps but had nothing to
  compare them against.
- **#11** is the only cycle ever abandoned mid-flight: planned, never studied. It still sits in the
  graph as a plan with no study, because the loop does not quietly delete its own loose ends.

And **#12 is the only `unmet`** in the project's history — the plan asked for research documents,
the work delivered code, and Study caught the mismatch instead of rounding it up to success.

Five cycles are **reinforcement cycles** (`REINFORCES` edges), created when Act decided a gap was
not actually closed:

```
#4 ← #5      #12 ← #13      #14 ← #15 ← #16      #20 ← #21
```

`#15 (partial) → #16 (met)` is the pattern working as designed: #15 claimed a feature worked but
never demonstrated the full chain, so #16 existed only to demonstrate it.

---

## Cycle by cycle

Each card states what the cycle attempted, how it was judged, and the one thing it taught.

### Era 1 · Recording only — the loop cannot yet judge itself

#### #1 — Verify the CLI basics and grow the unit tests
![Cycle 1](cards/cycle-01.svg)

#### #2 — Cover the pure Cli/Llm logic with unit tests
![Cycle 2](cards/cycle-02.svg)

#### #3 — Graph viewer: show the active project, switch without a restart
![Cycle 3](cards/cycle-03.svg)

### Era 2 · The loop closes

#### #4 — Close the loop: expected → verdict → REINFORCES
![Cycle 4](cards/cycle-04.svg)

The first cycle that could be judged, and it judged itself **partial**. From here on, every cycle
has a verifiable success criterion written *before* the work.

### Era 3 · Auth expansion — one key becomes five ways in

#### #5 — Design first: extend LLM auth beyond a single API key
![Cycle 5](cards/cycle-05.svg)

#### #6 — Auth abstraction: AuthMode + IAuthProvider
![Cycle 6](cards/cycle-06.svg)

#### #7 — Keyless open-weight E2E and config-merge tests
![Cycle 7](cards/cycle-07.svg)

#### #8 — GPT OAuth: refresh, persist, device-code polling
![Cycle 8](cards/cycle-08.svg)

Four cycles, three `partial`s, then `met`. The partials were not failures — each one named the
specific thing that was still unproven, and the next cycle proved it.

### Era 4 · Ship and localize

#### #9 — pdsa init: embed the skill inside the binary
![Cycle 9](cards/cycle-09.svg)

#### #10 — i18n across help, coaching and config
![Cycle 10](cards/cycle-10.svg)

### Era 5 · Self-model providers — let the agent use its own model

#### #11 — Codex OAuth mode, reusing `~/.codex/auth.json`
![Cycle 11](cards/cycle-11.svg)

#### #12 — Research official self-model provider paths
![Cycle 12](cards/cycle-12.svg)

#### #13 — Write the provider survey and design docs
![Cycle 13](cards/cycle-13.svg)

#### #14 — Adopt `claude -p` as an LLM provider
![Cycle 14](cards/cycle-14.svg)

### Era 6 · Real-use defects — the ones only dogfooding finds

#### #15 — Multi-project: `--project` on every command
![Cycle 15](cards/cycle-15.svg)

#### #16 — Reinforcement: close the gap #15 never demonstrated
![Cycle 16](cards/cycle-16.svg)

#### #17 — Run the viewer in-process for the AOT single file
![Cycle 17](cards/cycle-17.svg)

#### #18 — Timeout and spinner for the Claude CLI provider
![Cycle 18](cards/cycle-18.svg)

### Era 7 · Agent-friendly — the memory starts compounding

#### #19 — Structured `--json` output, recall, `--full`
![Cycle 19](cards/cycle-19.svg)

This is where the graph stopped being an archive and became an input: `recall` feeds prior learnings
into the next `plan`, so the loop stops repeating itself.

### Era 8 · The loop audits itself

#### #20 — Survey Akka.NET Streams and adjacent stacks
![Cycle 20](cards/cycle-20.svg)

#### #21 — Atomicity, bounded retry, per-phase metrics
![Cycle 21](cards/cycle-21.svg)

#### #22 — history and show: query the loop's own past
![Cycle 22](cards/cycle-22.svg)

---

## What the record shows about running a loop

**1. Writing the expected outcome first is what makes improvement measurable.**
Cycles 1–3 produced real work and left no way to tell whether it succeeded. The verdict field is
what turned "we did things" into "we predicted 18 outcomes and hit 11."

**2. `partial` is the useful verdict.** Seven of the eighteen judged cycles were not clean successes
(six `partial`, one `unmet`). Each named a specific unproven claim, and three of them — #4, #12 and
#15 — were closed by an explicit reinforcement cycle rather than being quietly forgotten.

**3. Dogfooding finds what design reviews miss.** The orphan-cycle defect in #21 (a single transient
network error corrupting the long-term memory) and the missing query commands in #22 were both found
by *using* the tool, not by reading the code.

**4. Order matters more than the individual fixes.** #21's central finding was not "add retry" but
"do not add retry before atomicity" — retry multiplies failed attempts, and every failed attempt was
leaving an orphan cycle behind.

**5. The measuring instrument is part of the system.** #21's cold-start comparison showed a 45%
improvement that did not exist; the npm shell wrapper was adding ~190ms to the baseline. The number
was corrected in the record rather than kept.

---

## Reading the raw record yourself

Nothing here is a summary you have to trust — every cycle is queryable:

```bash
pdsa history --project akka-graph-loop                # timeline, oldest first
pdsa history --from 15 --to 16 --full                 # one reinforcement pair, untruncated
pdsa show 21 --full                                   # a cycle's four phases, metrics, links
pdsa history --json | jq '.cycles[] | {id, verdict}'  # machine-readable
pdsa view                                             # the graph itself, in a browser
```

Cards are generated from the same records by
[`cards/_generate.py`](cards/_generate.py) — English on purpose, so both language versions of this
page share exactly the same images.
