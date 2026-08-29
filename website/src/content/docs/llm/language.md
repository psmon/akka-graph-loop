---
title: Language (English / 한국어)
description: Choose the language for help text and the recorded PDSA coaching — English or Korean, with OS-locale auto-detection.
---

`pdsa` speaks **English** and **한국어**. The chosen language drives both the **help text** and the
**coaching text that gets recorded** to the graph.

## Set it

```bash
pdsa config lang en          # pin: en | ko | auto
pdsa --lang ko <command>     # this invocation only
PDSA_LANG=ko pdsa status     # via environment variable
```

With nothing set, `pdsa` **auto-detects the OS locale** — Korean locale → Korean, otherwise English.

## Precedence

```
--lang  >  PDSA_LANG  >  config lang  >  OS locale  >  default (en)
```

:::note
Because the coaching narrative is stored in the graph in the chosen language, keeping a project on one
language makes the recorded memory consistent to read back later.
:::
