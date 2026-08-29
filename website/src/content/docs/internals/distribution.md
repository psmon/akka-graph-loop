---
title: Distribution (npm)
description: How @webnori/pdsa ships platform binaries through npm's optionalDependencies, built and published by CI on version tags.
---

`pdsa` is published to npm as **`@webnori/pdsa`** using an esbuild-style **`optionalDependencies`** layout.

## The layout

- The **main package** holds a tiny Node launcher (`bin/pdsa.js`) and depends on three platform packages.
- The **platform packages** each carry one native binary, gated by `os` / `cpu`:
  - `@webnori/pdsa-win32-x64`
  - `@webnori/pdsa-linux-x64`
  - `@webnori/pdsa-darwin-arm64`

On install, npm resolves `optionalDependencies` against your platform and fetches **only** the matching
binary — there's **no postinstall** step and no network access beyond the package download. The launcher
then execs the platform binary.

## Built and published by CI

A GitHub Actions workflow (`.github/workflows/release.yml`) builds all three binaries — each on its own
runner where host = target — and publishes the packages on a **`v*`** tag.

```bash
git tag v0.0.6
git push origin v0.0.6      # → CI builds win/linux/macOS, publishes to npm
```

See the packaging sources under `npm/` in the repository.

:::note[Two tag namespaces]
`v*` tags publish the **CLI to npm**. Documentation for this site is published separately on **`doc-v*`**
tags — see the repository's `.github/workflows/docs.yml`. The two are independent so you can ship docs
without cutting a CLI release.
:::
