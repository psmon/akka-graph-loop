---
title: "config · check · models"
description: Configure the LLM (key, model, provider, auth, language), verify it, and list available models.
---

See [Providers & Auth Modes](/akka-graph-loop/llm/providers/) for the conceptual guide; this page is the
command surface.

## `pdsa config`

```bash
pdsa config <subcommand> <value>
pdsa config show          # current settings (key masked)
```

| Subcommand | Purpose |
| --- | --- |
| `key <key>` | Set an OpenAI-compatible API key. |
| `key-file <path>` | Reference a key file (keeps the key out of config). |
| `model <model>` | Model id (default `gpt-5.6-terra`). |
| `reasoning <level>` | `none \| low \| medium \| high \| xhigh \| max`. |
| `base-url <URL>` | Endpoint base URL. |
| `provider <local\|openai-compat [URL]\|openai>` | Preset an endpoint (may set `auth=none`). |
| `auth <apikey\|oauth\|none\|codex\|claude-cli>` | Authentication mode. |
| `claude-cli-path <path>` | Pin the `claude` executable. |
| `claude-cli-timeout <sec>` | Cap the `claude -p` round-trip (default 180s). |
| `allow-insecure-no-auth <true\|false>` | Opt in to a **remote** no-auth endpoint. |
| `oauth <endpoint\|device-endpoint\|client\|refresh-token\|refresh-token-file> <value>` | OAuth settings. |
| `login` | Run the OAuth device-code login and persist the token. |
| `lang <en\|ko\|auto>` | Language for help + recorded coaching. |
| `show` | Print current settings (key masked). |

## `pdsa check`

Verify the configured LLM with a real, minimal round-trip.

```bash
pdsa check
# ✔ 성공 (…ms). / ✘ failure with the reason.
```

Works for every auth mode. On failure it prints the error and a hint to confirm the model exists on the
endpoint.

## `pdsa models`

List model ids the endpoint serves (OpenAI-compatible `GET /models`).

```bash
pdsa models [--filter <substring>]
pdsa models --filter gpt-5.6
```
