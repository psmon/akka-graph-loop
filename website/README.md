# pdsa — product site & docs

The landing page + documentation for the `pdsa` CLI, built with [Astro Starlight](https://starlight.astro.build/).
Deployed to GitHub Pages at **https://psmon.github.io/akka-graph-loop/**.

## Develop

```bash
cd website
npm install
npm run dev       # local dev server
npm run build     # production build → dist/
npm run preview   # preview the production build
```

## Conventions

- **White mode only.** The theme switcher is removed and the light palette is forced
  (`src/components/LightThemeProvider.astro`, `src/styles/theme.css`).
- **English only.**
- `base` is `/akka-graph-loop/` (a GitHub Pages *project* site). Hand-written cross-links include that
  prefix; Starlight's own navigation handles it automatically.
- Docs live in `src/content/docs/`. The landing page is `src/content/docs/index.mdx` (splash template).
- Diagrams are hand-authored SVGs in `src/assets/` so they stay crisp in light mode.

## Publish

Publishing is triggered by a **`doc-v*`** git tag (independent of the CLI's `v*` npm-release tags):

```bash
git tag doc-v1.0.0
git push origin doc-v1.0.0     # → .github/workflows/docs.yml builds & deploys to Pages
```

The repo's **Settings → Pages → Build and deployment → Source** must be set to **GitHub Actions** (one-time).
