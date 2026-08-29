---
title: Providers & Auth Modes
description: Attach an LLM for judging and coaching — OpenAI keys, keyless open-weight endpoints, GPT OAuth, Codex, or Claude Code.
---

The LLM is what turns Plan into a verifiable expectation and judges Study. You can attach one in several
ways. **Without an LLM, inputs are still recorded** to the graph — only coaching and the verdict are skipped.

Verify any mode with a real round-trip:

```bash
pdsa check
pdsa config show     # current auth / model / language (key masked)
```

## ① OpenAI(-compatible) API key — default

```bash
pdsa config key <key>              # or key-file <path> to keep the key out of config
pdsa config model <model>          # default: gpt-5.6-terra
pdsa config reasoning <level>      # none | low | medium | high | xhigh | max
pdsa models [--filter gpt-5.6]     # list models the endpoint serves
```

## ② Keyless open-weight (local / compatible endpoints)

For ollama, vLLM, LM Studio, and other OpenAI-compatible servers:

```bash
pdsa config provider local                # http://localhost:11434/v1, no auth
pdsa config provider openai-compat <URL>  # any OpenAI-compatible endpoint
pdsa config allow-insecure-no-auth true   # explicit opt-in to use a REMOTE endpoint with no auth
```

Private address ranges are auto-allowed without auth. Using a **remote** endpoint with no auth requires the
explicit `allow-insecure-no-auth true` opt-in.

## ③ GPT OAuth (refresh token) — device-code login

```bash
pdsa config oauth device-endpoint <URL>
pdsa config oauth endpoint <token-URL>
pdsa config oauth client <client-id>
pdsa config login                         # device-code flow, persists the token
```

The access token is refreshed automatically when it expires; the refresh token can be kept out of config via
`pdsa config oauth refresh-token-file <path>`.

## ④ Codex (ChatGPT subscription) — experimental

Reuses the official `codex login` token and calls the Responses API:

```bash
codex login
pdsa config auth codex
```

## ⑤ Claude Code (`claude -p`) — no key

Uses your already-logged-in Claude Code; no token setup:

```bash
pdsa config auth claude-cli
pdsa config claude-cli-path <path>       # optional: pin the executable
pdsa config claude-cli-timeout <seconds> # optional: cap the round-trip (default 180s)
```

:::caution[Read Anthropic's policy first]
- Use `claude -p` only within the terms of your Claude Code (Claude subscription) plan — i.e. **inside the
  Claude Code environment**.
- `claude -p` is **not** the official API path; it invokes the agent CLI as a subprocess. It has startup
  latency and, because of the agent's internal context, can **use tokens inefficiently** (burning
  subscription credits faster). For bulk or automated use, prefer an official API key (①).
- Executable resolution: `PDSA_CLAUDE_CLI` env → config `claude_cli_path` → `claude` on `PATH`.
- Timeout resolution: `PDSA_CLAUDE_TIMEOUT_SEC` env → config `claude_cli_timeout_sec` → 180s default. A hung
  call is stopped with a clear message instead of blocking forever.
:::

## Configuration precedence

Load priority for the core settings:

```
env vars (OPENAI_API_KEY / OPENAI_MODEL / OPENAI_BASE_URL / OPENAI_REASONING_EFFORT)
  → global config  {LocalAppData}/pdsa-cli/openai.json
  → repo .secret/openai.json
```

See the full command surface in the [CLI reference: config · check · models](/akka-graph-loop/cli/config/).
