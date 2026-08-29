---
title: Recall (expectation hit-rate)
description: pdsa's headline metric — the share of cycles that met the expectation they set.
---

**Recall** is `pdsa`'s one-number summary of how well work is meeting the expectations it sets for itself.

## Definition

```
recall = met / (cycles that have a verdict)
```

- The numerator counts cycles whose Study verdict was <span class="verdict met">met</span>.
- The denominator counts only cycles that were actually judged — a cycle with no `study` yet doesn't drag
  the rate down.

So a recall of `7/14 (50%)` means: of 14 cycles that reached a verdict, 7 fully met their expected outcome.

## Where it shows up

```bash
pdsa status    # recent cycles + the hit-rate
pdsa eval      # per-cycle expected / verdict / actual, then the hit-rate
pdsa view      # a live hit-rate badge in the viewer
```

## How to read it

Recall is a **learning signal, not a grade**. A lower number isn't inherently bad — it often means you're
setting genuinely uncertain, falsifiable expectations (which is the point). Watch the *trend* across cycles
and whether `partial`/`unmet` results are being **reinforced** into follow-up cycles rather than dropped.

:::note
Because the denominator is "cycles with a verdict," recall only moves when you actually run `pdsa study`.
Recording a Plan and Do without a Study leaves the cycle unjudged and out of the ratio.
:::

Related: [Expected → Verdict → Reinforce](/akka-graph-loop/concepts/closed-loop/).
