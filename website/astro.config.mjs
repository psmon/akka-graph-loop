// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// GitHub Pages project site: https://psmon.github.io/akka-graph-loop/
const GITHUB = 'https://github.com/psmon/akka-graph-loop';

export default defineConfig({
  site: 'https://psmon.github.io',
  base: '/akka-graph-loop/',
  integrations: [
    starlight({
      title: 'pdsa',
      tagline: 'Run your PDSA loop as graph memory.',
      description:
        'A CLI that backs Deming\'s Plan-Do-Study-Act loop with graph engineering — every cycle accumulates into a per-project Kùzu graph memory for AI agents.',
      logo: {
        src: './src/assets/pdsa-mark.svg',
        replacesTitle: false,
      },
      // White / light mode only — hide the theme switcher and force the light palette.
      components: {
        ThemeSelect: './src/components/EmptyThemeSelect.astro',
        ThemeProvider: './src/components/LightThemeProvider.astro',
      },
      customCss: ['./src/styles/theme.css'],
      social: [
        { icon: 'github', label: 'GitHub', href: GITHUB },
      ],
      editLink: {
        baseUrl: `${GITHUB}/edit/main/website/`,
      },
      lastUpdated: true,
      sidebar: [
        {
          label: 'Getting Started',
          items: [
            { label: 'Installation', slug: 'getting-started/installation' },
            { label: 'Quickstart', slug: 'getting-started/quickstart' },
            { label: 'Your First Cycle', slug: 'getting-started/first-cycle' },
          ],
        },
        {
          label: 'Concepts',
          items: [
            { label: 'The PDSA Loop', slug: 'concepts/pdsa-loop' },
            { label: 'Graph Memory (Kùzu)', slug: 'concepts/graph-memory' },
            { label: 'Expected → Verdict → Reinforce', slug: 'concepts/closed-loop' },
            { label: 'Recall (hit-rate)', slug: 'concepts/recall' },
          ],
        },
        {
          label: 'LLM & Auth',
          items: [
            { label: 'Providers & Auth Modes', slug: 'llm/providers' },
            { label: 'Language (EN / KO)', slug: 'llm/language' },
          ],
        },
        {
          label: 'Guides',
          items: [
            { label: 'Multi-project', slug: 'guides/multi-project' },
            { label: 'Graph Viewer', slug: 'guides/viewer' },
            { label: 'For AI Agents', slug: 'guides/ai-agents' },
          ],
        },
        {
          label: 'CLI Reference',
          items: [
            { label: 'Overview', slug: 'cli/overview' },
            { label: 'Cycle: plan · do · study · act', slug: 'cli/cycle' },
            { label: 'config · check · models', slug: 'cli/config' },
            { label: 'project · status · eval · view', slug: 'cli/project' },
            { label: 'init · guide · run · version', slug: 'cli/misc' },
          ],
        },
        {
          label: 'Under the Hood',
          items: [
            { label: 'Kùzu Interop', slug: 'internals/kuzu' },
            { label: 'Native AOT Build', slug: 'internals/aot' },
            { label: 'Distribution (npm)', slug: 'internals/distribution' },
          ],
        },
      ],
    }),
  ],
});
