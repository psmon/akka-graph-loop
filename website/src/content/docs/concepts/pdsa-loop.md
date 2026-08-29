---
title: The PDSA Loop
description: Why pdsa uses Plan-Do-Study-Act — and why the third step is Study, not Check.
---

**PDSA** — Plan, Do, Study, Act — is W. Edwards Deming's continuous-improvement loop. `pdsa` implements it as
a working feedback cycle and records each round to a graph so the learning accumulates.

## The four steps

| Step | Question | What `pdsa` records |
| --- | --- | --- |
| **Plan** | What will we try, and how will we know it worked? | Your intent + a verifiable **expected** evaluation |
| **Do** | What did we actually do? | The execution, organized against the plan |
| **Study** | *What did we learn?* | A **verdict** (met/partial/unmet) + the measured **actual** |
| **Act** | What do we change next? | The next improvement action; a `REINFORCES` link if needed |

## Study, not Check

The most important distinction: the third step is **Study**, not **Check**.

- *Check* asks a yes/no question — "did it pass?" — and stops there.
- *Study* asks "what did we learn?" — it compares the result to the **hypothesis** set in Plan and extracts
  knowledge, whether the outcome was met or not.

`pdsa` enforces this by making Plan produce a verifiable expected evaluation, and making Study judge against
it and narrate the learning. A `partial` or `unmet` verdict isn't a failure of the tool — it's information.

## As a real feedback cycle

This isn't just bookkeeping. The reference implementation in `AkkaGraphLoop.Core` models the loop as an
actual **Akka.Streams feedback cycle** (`PdsaLoop`), the trickiest kind of stream graph — a cycle that must
stay *live* (no deadlock) while it feeds its own output back as the next input. The learning project this
tool grew out of exists to study exactly those cyclic graphs.

## History

PDSA descends from Walter Shewhart's plan-do-see cycle, which Deming reshaped and carried to post-war Japan,
where it underpinned the quality movement (Toyota and others). Deming later insisted on *Study* over *Check*
precisely because improvement comes from learning, not inspection. See the project's essay
[PDSA — History, Theory, and the Quality Legacy](https://github.com/psmon/akka-graph-loop/blob/main/PDSA.md).

## Next

- [Graph Memory (Kùzu)](/akka-graph-loop/concepts/graph-memory/) — where each cycle is stored.
- [Expected → Verdict → Reinforce](/akka-graph-loop/concepts/closed-loop/) — closing the loop.
