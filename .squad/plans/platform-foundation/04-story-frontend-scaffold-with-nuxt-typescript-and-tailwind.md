# Story 04 — Frontend scaffold with Nuxt TypeScript and Tailwind

## Prerequisites

- **Stories 01–03 are implemented and committed** (latest `0200bf6`, "Implement Story 03 — cross cutting concerns
  error handling logging and caching"). The API serves the four per-Area Swagger documents and the RFC 9457 error
  contract this story's client consumes. Read [`00-overview.md`](00-overview.md), "Story 02 — decisions carried
  forward" (per-Area Swagger, `/openapi/v1.json` is gone) and "Story 03 — decisions carried forward" (`ProblemTypes`
  URNs are never renamed).
- **Module:** none. This is a **frontend-only** story. It creates `frontend/` and **changes nothing under
  `backend/`**. It consumes the per-Area Swagger documents that merge every module's controllers.
- **Verified on this machine during planning (2026-09-28)**. Re-check before starting:
  - Node **24.15.0**, npm **12.0.1**, corepack **0.34.6** (all under `C:\Program Files\nodejs`).
  - **pnpm is not on PATH out of the box.** `corepack enable pnpm` **fails** with
    `EPERM: operation not permitted, open 'C:\Program Files\nodejs\pnpm.CMD'` (Program Files is not writable
    without elevation). **This command works** and was run during planning, so the shim already exists:
    ```bash
    corepack enable pnpm --install-directory "$APPDATA/npm"     # Git Bash; %APPDATA%\npm is already on PATH
    ```
    In PowerShell: `corepack enable pnpm --install-directory "$env:APPDATA\npm"`. The first `pnpm` call
    downloads **pnpm 12.6.0**. Set `COREPACK_ENABLE_DOWNLOAD_PROMPT=0` in non-interactive shells, or corepack waits on
    a prompt. Fallback if corepack is ever broken: `npm i -g pnpm@12.6.0` (global prefix is
    `C:\Users\user1\AppData\Roaming\npm`, user-writable).
  - **The npm registry is a mirror**: `registry=https://najizportalnpm.azurewebsites.net/` in `~/.npmrc`. It is slow
    (single metadata requests took 30–68 s on a cold store; a cold `pnpm install` took several minutes). Do not
    commit an `.npmrc` that overrides it.
  - Latest stable versions (npm registry, 2026-09-28), **pinned exactly** below: `nuxt` **4.5.2** (ships Nitro
    2.13.4, Vite 8.3.1), `vue` **3.5.43**, `vue-router` **5.3.1**, `typescript` **5.9.3** (not 7.0.2, see Probe 1),
    `vue-tsc` **3.3.11**, `@nuxt/eslint` **1.17.0**, `eslint` **10.11.0**, `eslint-config-prettier` **10.1.8**,
    `prettier` **3.9.9**, `tailwindcss` / `@tailwindcss/vite` **4.3.3**, `pinia` **4.0.3**, `@pinia/nuxt`
    **1.0.2**, `openapi-typescript` **7.13.0**, `openapi-fetch` **0.17.0**.
- **Probe run during planning.** A throwaway Nuxt app in the session scratchpad, **not in the repo**, with exactly
  the pins above, installed with pnpm 12.6.0 and run against the live API on 5187. Results that shaped the design
  are marked **Probe N:** inline:
  1. **TypeScript 7.0.2 is `latest` but `openapi-typescript` 7.13.0 peers `typescript ^5.x`.** TS 7 is the native
     compiler, and the Volar/vue-tsc stack is built on the TS 5 JS API. **Pin 5.9.3.** With it, `nuxt typecheck`
     (vue-tsc) runs clean and fails on a planted `const y: number = 'no'` (`TS2322`, exit 2).
  2. **`pnpm install` fails on the registry** with `ERR_PNPM_NO_MATCHING_VERSION cacheable@^2.5.1`.
     `flat-cache@6.1.24` (pulled in through eslint's `file-entry-cache`) depends on `cacheable@^2.5.1`, which is not
     published anywhere. `registry.npmjs.org` also tops out at 2.5.0. **Fix: a pnpm override `flat-cache: 6.1.23`**
     (depends on `cacheable ^2.5.0`). Install then succeeds.
  3. **pnpm 12 fails the install on unapproved build scripts** (`ERR_PNPM_IGNORED_BUILDS: esbuild@0.28.2,
     unrs-resolver@1.12.2`). Both must be listed under `allowBuilds:` in `pnpm-workspace.yaml`. pnpm 12 reads
     settings from `pnpm-workspace.yaml`, not from `package.json#pnpm`. With both set to `true` the install
     completes and `nuxt prepare` runs as `postinstall`. One harmless peer warning remains (`cac@7.0.0` vs `^6.7.14`
     wanted by nuxi's `@bomb.sh/tab`). Ignore it.
  4. **Tailwind v4 through `@tailwindcss/vite`** in `vite.plugins` works on Nuxt 4.5.2 / Vite 8. The
     `@nuxtjs/tailwindcss` module (6.14.0) is Tailwind v3-era. **Do not use it.** `ms-4` compiles to
     `margin-inline-start`.
  5. **Tokens must be declared with `@theme inline`.** With plain `@theme { --color-primary-500: var(--crm-primary-500) }`
     the utility compiles to `var(--color-primary-500)`. That value resolves once, on `:root`, so a nested
     `[data-theme=dark]` wrapper does not recolour it, and `/_dev/ui` needs light and dark side by side. With
     `@theme inline` the utility compiles to `background-color:var(--crm-primary-500)` and re-resolves per element.
  6. **`@custom-variant dark (&:where([data-theme=dark], [data-theme=dark] *))`** compiles `dark:text-white` to a
     `[data-theme=dark]` selector. Custom variants `ui-hover` (`@media (hover:hover){&:hover}` **plus**
     `&[data-force~="hover"]`) and `ui-focus` (`&:focus-visible, &[data-force~="focus"]`) and a `@utility focus-ring`
     all compile as intended. This is how `/_dev/ui` shows hover and focus states without a pointer.
  7. **ESLint on `@nuxt/eslint` 1.17 + `eslint-config-prettier/flat`** flags `any`, the non-null `!`, `@ts-ignore`
     (`@typescript-eslint/ban-ts-comment`), and physical-direction classes. `vue/no-restricted-class` catches static
     and `:class` object/array classes. `no-restricted-syntax` on `Literal`/`TemplateElement` catches class strings
     built in `<script>` (variant maps, template literals), including `hover:mr-1`. Logical forms (`ms-2 text-start
     border-s rounded-s-md`) pass. `app/components/ui/Button.vue` does **not** trip
     `vue/multi-word-component-names`.
  8. **Excluding `/_dev/**` from production:** `ignore: ['pages/_dev/**']` under `$production` in `nuxt.config.ts`
     did **not** remove the page (its text was still in `.output`). A local module in `modules/` that removes
     `/_dev*` routes in `pages:extend` when `!nuxt.options.dev` **does**: after `pnpm build`, `grep -r "dev ui"
     .output/` is empty. In `pnpm dev`, `/_dev/ui` returns 200.
  9. **`nitro.devProxy` `/api` → `http://localhost:5187/api`** works. `POST /api/Admin/Samples` through the dev
     server on 3100 returned the API's 400 `application/problem+json` with `x-correlation-id: via-proxy` echoed. No
     CORS policy is needed in development.
  10. **`routeRules: { '/agent/**': { ssr: false } }`**: `/agent` HTML carries no page content (SPA). `/` is
      server-rendered (content present in the HTML).
  11. **`openapi-typescript` 7.13.0 against the live API** produced `admin.ts` (526 lines), `agent.ts` (83),
      `public.ts` (73), and `portal.ts` (13: the Portal Area has **no paths yet**, `"paths": { }`). `openapi-fetch`
      `createClient<paths>({ fetch })` accepts `fetch: (input: Request) => Promise<Response>`, and a
      `client.POST('/api/Admin/Samples', { body: { name: '', lines: [] } })` call typechecks with `error` typed from
      the document.
  12. **Real error bodies**: validation keys are **PascalCase** (`"errors":{"Name":["'Name' must not be empty."]}`,
      `code: "Validation.Failed"`) while request DTO properties are **camelCase** (`name`, `lines`). A binding
      failure gives `"errors":{"Lines":["The Lines field is required."]}`. `GET /api/Admin/Diagnostics/unauthorized`
      returns a real **401** `urn:crm:problem:unauthorized` body in Development. Swagger declares error responses as
      `application/json` / `text/plain` with a `ProblemDetails` / `ValidationProblemDetails` schema. The wire
      `Content-Type` is `application/problem+json`.
  13. **Node 24 runs `scripts/gen-api.ts` directly** (native type stripping, no flag). With the API down,
      `openapi-typescript` throws `ResolveError: fetch failed`, which the script must catch (Task 5).
- **Coordination:**
  - **Story 05** (i18n + RTL) builds on this scaffold. It adds `@nuxtjs/i18n`, moves the shell copy in
    `app/copy/en.ts` (Task 7) into `en`/`ar` locale files, maps `ProblemTypes` URNs to messages, and flips
    arrow-key direction in `UiTabs`/`UiPagination` for RTL. This story ships **no i18n module** but writes no
    physical-direction utility anywhere (lint-enforced from day one, Task 3).
  - **Story 07** (authentication) fills the `AuthSession` abstraction (Task 6c) with a real token store and refresh
    call, and replaces the placeholder `/login` page. **It must not change `createCrmFetch`'s retry contract.**
  - **Story 58** (branding) rebrands by overriding the `--crm-*` CSS variables only (Task 4). No component names a
    raw colour.
  - **Every later frontend story** follows `.claude/commands/frontend-slice.md` and reuses `app/components/ui/`.

---

## Story Goal

Stand up `frontend/` so every later frontend story only adds pages, components, and composables.

When this story is done:

1. `pnpm install`, `pnpm dev`, `pnpm build`, `pnpm typecheck`, `pnpm lint` all work from `frontend/` (and via
   `pnpm --dir frontend <script>` from the repo root) with **zero** errors.
2. TypeScript `strict` is on. ESLint makes `any`, `@ts-ignore`/`@ts-expect-error`/`@ts-nocheck`, the non-null `!`,
   and every physical-direction Tailwind utility an **error**.
3. Tailwind v4 with design tokens (colour scales, semantic surfaces, spacing unit, radii, typography) driven by
   `--crm-*` CSS variables, and a light/dark theme switch persisted in a cookie, so SSR renders the right theme.
4. Two layouts: **`agent`** (persistent sidebar + topbar, SPA via `routeRules`) and **`portal`** (public, SSR).
5. `pnpm gen:api` fetches the four per-Area Swagger documents, commits snapshots, and generates typed
   `openapi-typescript` definitions, also committed. `openapi-fetch` clients per Area sit behind a `useApi`
   composable. Every request sends `X-Correlation-Id` and the bearer token. A **401 triggers one refresh and one
   retry**, and a second 401 redirects to `/login`. A **400 problem** becomes field-level form errors, never a toast.
6. A domain-free UI kit under `app/components/ui/` (15 components), a global toast store (Pinia), route-level
   loading and error boundaries.
7. `/_dev/ui` renders the whole kit in every state in both themes, and `/_dev/api` exercises the client against
   the live API and a fake transport. Both exist **only** in `pnpm dev`.

**Not in scope:** i18n and RTL flipping (Story 05), real authentication and the login screen (Story 07), any
backend change, and any test suite. **Do not install Vitest, Vue Test Utils, Playwright, Cypress, Storybook, or
Lighthouse CI.**

---

## Context — Read These Files First

1. `CLAUDE.md`: "Frontend — Nuxt + Vue 3 + TypeScript + Tailwind" (lines ~192–204), "Testing policy" (lines
   ~206–233), "Verification" (lines ~245–256). The three `pnpm --dir frontend …` commands there are this story's
   gate.
2. `.claude/commands/frontend-slice.md`: the folder layout every later story follows (`app/pages`,
   `app/components/<feature>`, `app/composables/use<Feature>.ts`, `app/stores`, "Reuse the existing design-system
   components under `app/components/ui/`"). This story creates the structure it describes.
3. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs` lines 10–45: the
   twelve URN constants. Task 6a mirrors them **verbatim** as a TS `const` object. Never rename one.
4. `backend/README.md` lines 228–250, "Error responses": status → `type` table, and the body fields (`title`,
   `instance`, `traceId`, `correlationId`, `code`, `errors` by field).
5. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Correlation/CorrelationIdMiddleware.cs` lines 12–14
   and 38–41: header `X-Correlation-Id`, accepted only if ≤ **64** chars of `[A-Za-z0-9._:-]`, otherwise replaced.
   `crypto.randomUUID()` (36 chars) passes.
6. `backend/src/Crm.Api/Program.cs` lines 94–103: Swagger and `/swagger/{area}/swagger.json` exist **only in
   Development**, one per `CrmAreas.All` entry, lower-cased. `backend/src/Crm.Api/Properties/launchSettings.json`
   line 8: `http://localhost:5187`.
7. `backend/src/Crm.Api/Diagnostics/Admin/DiagnosticsController.cs` lines 13–41: `[DevelopmentOnly]` endpoints
   `GET throw` (500), `GET forbidden` (403), `GET unauthorized` (401, line 41). `/_dev/api` (Task 12) drives them.
   `backend/src/Modules/Sample/Crm.Modules.Sample.Presentation/Controllers/Admin/SamplesController.cs`:
   `POST /api/Admin/Samples` with `{ "name": "" , "lines": [] }` yields the 400 with `errors.Name`.
8. `backend/requests/diagnostics.http`: the same failure modes as raw requests. Use it to compare bodies with
   what the client normalises.
9. [`03-story-cross-cutting-concerns-error-handling-logging-and-caching.md`](03-story-cross-cutting-concerns-error-handling-logging-and-caching.md),
   "Frontend Tasks" and Task 2/4: why every body carries `correlationId` and why the header is echoed on every
   response, including the 500.

---

## Frontend Tasks

All paths below are under `frontend/` unless they start with `backend/` or `.squad/`. Use the **Write tool** for
files, not Bash heredocs (heredocs with many apostrophes fail to parse on this machine).

### 1 — Enable pnpm (machine setup, once)

```bash
corepack enable pnpm --install-directory "$APPDATA/npm"
export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
pnpm --version        # → 12.6.0
```

If `pnpm` already resolves (planning left the shim in `%APPDATA%\npm`), skip the first line. Do not run plain
`corepack enable` (EPERM, see Prerequisites).

### 2 — Project files

Create these by hand. **Do not run `nuxi init`**: its template adds a README and unpinned `^` ranges this plan
replaces anyway.

**Create file: `package.json`**

```json
{
  "name": "crm-frontend",
  "type": "module",
  "private": true,
  "packageManager": "pnpm@12.6.0",
  "engines": { "node": ">=24.0.0" },
  "scripts": {
    "dev": "nuxt dev",
    "build": "nuxt build",
    "preview": "nuxt preview",
    "postinstall": "nuxt prepare",
    "typecheck": "nuxt typecheck",
    "lint": "eslint .",
    "lint:fix": "eslint . --fix",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "gen:api": "node scripts/gen-api.ts"
  },
  "dependencies": {
    "@pinia/nuxt": "1.0.2",
    "nuxt": "4.5.2",
    "openapi-fetch": "0.17.0",
    "pinia": "4.0.3",
    "vue": "3.5.43",
    "vue-router": "5.3.1"
  },
  "devDependencies": {
    "@nuxt/eslint": "1.17.0",
    "@tailwindcss/vite": "4.3.3",
    "eslint": "10.11.0",
    "eslint-config-prettier": "10.1.8",
    "openapi-typescript": "7.13.0",
    "prettier": "3.9.9",
    "tailwindcss": "4.3.3",
    "typescript": "5.9.3",
    "vue-tsc": "3.3.11"
  }
}
```

**Exact versions, no `^`.** There is no test suite to catch a minor bump that breaks something, so every upgrade
is a deliberate commit.

**Create file: `pnpm-workspace.yaml`** (Probes 2 and 3)

```yaml
# pnpm 12 reads its settings here. Not a monorepo: there is one package.
allowBuilds:
  esbuild: true        # Vite/Nuxt native binary
  unrs-resolver: true  # eslint-plugin-import-x resolver native binding
overrides:
  # flat-cache 6.1.24 depends on cacheable@^2.5.1, which was never published (npmjs tops out at 2.5.0).
  # Remove when a flat-cache release resolves again. See Story 04 plan, Probe 2.
  flat-cache: 6.1.23
```

**Create file: `.gitignore`**: `.output`, `.data`, `.nuxt`, `.nitro`, `.cache`, `dist`, `node_modules`, `*.log`,
`.env`, `.env.*`, `!.env.example`.

**Create file: `.env.example`**: `NUXT_API_BASE=http://localhost:5187` (server-side API origin for SSR, Task 6d)
and `CRM_API_URL=http://localhost:5187` (the `gen:api` source).

**Create file: `.prettierrc.json`**: `{ "semi": false, "singleQuote": true, "printWidth": 110, "trailingComma": "all" }`.

**Create file: `.prettierignore`**: `.nuxt`, `.output`, `node_modules`, `pnpm-lock.yaml`, `app/api/generated`,
`openapi`.

**Create file: `tsconfig.json`**: the Nuxt 4 project-references form (verified in the probe):

```json
{
  "files": [],
  "references": [
    { "path": "./.nuxt/tsconfig.app.json" },
    { "path": "./.nuxt/tsconfig.server.json" },
    { "path": "./.nuxt/tsconfig.shared.json" },
    { "path": "./.nuxt/tsconfig.node.json" }
  ]
}
```

**Create file: `nuxt.config.ts`**

```ts
import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@nuxt/eslint', '@pinia/nuxt'],
  css: ['~/assets/css/main.css'],
  vite: { plugins: [tailwindcss()] },
  typescript: { strict: true, typeCheck: false }, // typecheck is its own command; build stays fast
  runtimeConfig: {
    apiBase: 'http://localhost:5187', // server-side (SSR) API origin; NUXT_API_BASE overrides
    public: { apiBase: '' }, // browser: same-origin /api (dev proxy in dev, reverse proxy in production)
  },
  routeRules: {
    '/agent/**': { ssr: false }, // agent workspace is an SPA (Probe 10)
    '/_dev/**': { ssr: false },
  },
  nitro: {
    // Probe 9: the browser calls /api on the Nuxt origin, so the API needs no CORS policy in development.
    devProxy: { '/api': { target: 'http://localhost:5187/api', changeOrigin: true } },
  },
  app: { head: { htmlAttrs: { lang: 'en' } } }, // Story 05 makes lang/dir reactive
})
```

`modules/` at the project root is auto-registered by Nuxt 4 (the probe relied on it). **Do not** add
`modules/dev-only-pages` to `modules:`, or it registers twice.

Then run `pnpm install` in `frontend/`. Commit **`pnpm-lock.yaml`**.

### 3 — Lint and format

**Create file: `eslint.config.mjs`** (Probe 7; this exact rule set was proven)

```js
import withNuxt from './.nuxt/eslint.config.mjs'
import prettier from 'eslint-config-prettier/flat'

// RTL is first-class (CLAUDE.md): a physical-direction utility is a bug. Matched in templates
// (vue/no-restricted-class) and in any string or template literal in script (no-restricted-syntax),
// which is where variant maps build class lists.
const physical = String.raw`(^|\s|:)-?(ml|mr|pl|pr|left|right|border-l|border-r|rounded-l|rounded-r|rounded-tl|rounded-tr|rounded-bl|rounded-br|scroll-ml|scroll-mr|text-left|text-right|float-left|float-right|clear-left|clear-right)(-|\s|$)`
const message =
  'Physical direction utility: use the logical form (ms-/me-/ps-/pe-/start-/end-/border-s/border-e/rounded-s/rounded-e/text-start/text-end).'

export default withNuxt(
  { ignores: ['app/api/generated/**', 'openapi/**'] },
  {
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/ban-ts-comment': [
        'error',
        { 'ts-ignore': true, 'ts-nocheck': true, 'ts-expect-error': true },
      ],
      'vue/no-restricted-class': ['error', `/${physical}/`],
      'no-restricted-syntax': [
        'error',
        { selector: `Literal[value=/${physical}/]`, message },
        { selector: `TemplateElement[value.raw=/${physical}/]`, message },
      ],
    },
  },
  {
    // The kit uses ui-hover:/ui-focus: so /_dev/ui can force those states (Task 4, Probe 6).
    files: ['app/components/ui/**/*.vue'],
    rules: {
      'vue/no-restricted-class': ['error', `/${physical}/`, '/(^|:)hover:/', '/(^|:)focus-visible:/'],
    },
  },
).append(prettier)
```

`eslint.config.mjs` imports `.nuxt/eslint.config.mjs`, which `nuxt prepare` (the `postinstall`) writes. On a fresh
clone `pnpm install` must run before `pnpm lint`.

### 4 — Design tokens, Tailwind, theme switch

**Create file: `app/assets/css/tokens.css`**: every brand value, as `--crm-*` variables. Story 58 overrides this
layer only.

- `:root, [data-theme='light']`: colour scales **`primary`**, **`neutral`** (50, 100, 200, 300, 400, 500, 600, 700,
  800, 900, 950, as `oklch(...)`), status colours **`success`**, **`warning`**, **`danger`**, **`info`** (50, 500, 600,
  700), semantic roles `--crm-surface`, `--crm-surface-raised`, `--crm-surface-sunken`, `--crm-border`,
  `--crm-text`, `--crm-text-muted`, `--crm-text-inverse`, `--crm-focus`, `--crm-overlay`; `--crm-radius-sm|md|lg|xl|full`;
  `--crm-spacing: 0.25rem`; `--crm-font-sans` (system UI stack; Story 05 prepends an Arabic face);
  `--crm-font-mono`.
- `[data-theme='dark']`: redefine the semantic roles and status 500/600 values only. Scales stay put; roles point
  to different steps.
- **Contrast is part of the token set**: `--crm-text` on `--crm-surface` ≥ 7:1, `--crm-text-muted` ≥ 4.5:1,
  `--crm-primary-600` (button fill) against `--crm-text-inverse` ≥ 4.5:1, in **both** themes. The Lighthouse gate
  (Done Criteria) fails on contrast first.

**Create file: `app/assets/css/main.css`**

```css
@import 'tailwindcss';
@import './tokens.css';

/* Probe 6: dark: keys off data-theme, so a subtree can be themed independently (used by /_dev/ui). */
@custom-variant dark (&:where([data-theme=dark], [data-theme=dark] *));

/* Kit-only state variants: real hover/focus-visible, or forced by data-force="hover focus" on /_dev/ui. */
@custom-variant ui-hover {
  @media (hover: hover) {
    &:hover {
      @slot;
    }
  }
  &[data-force~='hover'] {
    @slot;
  }
}
@custom-variant ui-focus (&:focus-visible, &[data-force~='focus']);

/* Probe 5: inline, so utilities emit var(--crm-*) directly and re-resolve inside a nested data-theme. */
@theme inline {
  --color-primary-50: var(--crm-primary-50);
  /* … every primary/neutral step, success/warning/danger/info steps … */
  --color-surface: var(--crm-surface);
  --color-surface-raised: var(--crm-surface-raised);
  --color-surface-sunken: var(--crm-surface-sunken);
  --color-border: var(--crm-border);
  --color-fg: var(--crm-text);
  --color-fg-muted: var(--crm-text-muted);
  --color-fg-inverse: var(--crm-text-inverse);
  --color-focus: var(--crm-focus);
  --color-overlay: var(--crm-overlay);
  --radius-sm: var(--crm-radius-sm);
  --radius-md: var(--crm-radius-md);
  --radius-lg: var(--crm-radius-lg);
  --radius-xl: var(--crm-radius-xl);
  --spacing: var(--crm-spacing);
  --font-sans: var(--crm-font-sans);
  --font-mono: var(--crm-font-mono);
}

@utility focus-ring {
  outline: 2px solid var(--crm-focus);
  outline-offset: 2px;
}

@layer base {
  html {
    color-scheme: light;
  }
  html[data-theme='dark'] {
    color-scheme: dark;
  }
  body {
    @apply bg-surface text-fg font-sans antialiased;
  }
}
```

Utility names are therefore `bg-surface`, `text-fg`, `text-fg-muted`, `border-border`, `bg-primary-600`,
`rounded-md`, and so on. **Components never use a literal colour** (`bg-[#…]`, `text-blue-600`). They use the
token utilities only.

**Create file: `app/composables/useTheme.ts`**

```ts
export type Theme = 'light' | 'dark'

export function useTheme() {
  const cookie = useCookie<Theme>('crm-theme', { default: () => 'light', sameSite: 'lax', maxAge: 60 * 60 * 24 * 365 })
  const theme = computed<Theme>(() => (cookie.value === 'dark' ? 'dark' : 'light'))
  function setTheme(next: Theme): void {
    cookie.value = next
  }
  function toggleTheme(): void {
    setTheme(theme.value === 'dark' ? 'light' : 'dark')
  }
  return { theme, setTheme, toggleTheme }
}
```

**Create file: `app/plugins/theme.ts`**: `defineNuxtPlugin(() => { const { theme } = useTheme(); useHead({ htmlAttrs: { 'data-theme': theme } }) })`.
Because the cookie is read on the server, the SSR portal HTML already carries the right `data-theme`, with no
flash. Default is `light`. No `system` option in this story. Story 58 revisits it if needed.

### 5 — Typed API generation (`pnpm gen:api`)

**Decision: generate from the running API, commit both the raw documents and the generated types.**

- The documents exist only when the API runs in Development (`Program.cs` lines 94–103), and the API migrates SQL
  Server on start. A snapshot in `openapi/` lets a frontend-only change regenerate types (`--offline`) without SQL
  Server. It also makes every backend contract change a **reviewable JSON diff** in the PR that caused it.
- The generated `.ts` is committed (acceptance criterion), so `pnpm install && pnpm build` never needs the API.

**Create file: `scripts/gen-api.ts`** (Node 24 strips types natively, Probe 13. Use only erasable syntax: no
`enum`, no `namespace`, no parameter properties.)

- Areas: `const areas = ['agent', 'portal', 'admin', 'public'] as const` (lower-case `CrmAreas.All`).
- Online (default): for each area, `fetch(new URL(`/swagger/${area}/swagger.json`, process.env.CRM_API_URL ??
  'http://localhost:5187'))`. On a network error or non-200, print
  `gen:api: cannot reach <url>. Start the API in Development (dotnet run --project backend/src/Crm.Api) or use --offline.`
  and `process.exit(1)`. **Never leave a half-written set**: fetch all four first, then write.
- Write `openapi/<area>.json` as `JSON.stringify(doc, null, 2) + '\n'` (stable diffs).
- `--offline` flag: skip fetching; read `openapi/<area>.json`.
- For each area: `const ast = await openapiTS(pathToFileURL('openapi/<area>.json'))`, write
  `app/api/generated/<area>.ts` = banner `// Generated by pnpm gen:api from openapi/<area>.json. Do not edit.\n` +
  `astToString(ast)`.
- Log one line per area with the path count.

Run it once with the API up and commit `openapi/*.json` and `app/api/generated/*.ts`. **Probe 11:** `portal.ts` is
nearly empty (`paths` has no keys). That is correct until a Portal controller exists. The documents also include
the `[DevelopmentOnly]` Diagnostics endpoints, because generation runs against Development. They 404 in
production and only `/_dev/api` calls them.

### 6 — API client, error normalisation, auth abstraction

#### 6a — Problem details

**Create file: `app/api/problem.ts`**

```ts
/** Mirrors backend ProblemTypes.cs verbatim. Never rename a value; Story 05 maps them to en/ar messages. */
export const ProblemTypes = {
  Validation: 'urn:crm:problem:validation',
  BadRequest: 'urn:crm:problem:bad-request',
  Unauthorized: 'urn:crm:problem:unauthorized',
  Forbidden: 'urn:crm:problem:forbidden',
  NotFound: 'urn:crm:problem:not-found',
  MethodNotAllowed: 'urn:crm:problem:method-not-allowed',
  Conflict: 'urn:crm:problem:conflict',
  UnsupportedMediaType: 'urn:crm:problem:unsupported-media-type',
  DomainRule: 'urn:crm:problem:domain-rule',
  RateLimited: 'urn:crm:problem:rate-limited',
  Internal: 'urn:crm:problem:internal',
  DependencyUnavailable: 'urn:crm:problem:dependency-unavailable',
  /** Client-side only: the request never got an HTTP response (offline, DNS, aborted). */
  Network: 'urn:crm:problem:network',
} as const
export type ProblemType = (typeof ProblemTypes)[keyof typeof ProblemTypes]

export interface Problem {
  type: string // a ProblemType, or an unknown URN from a newer backend; never narrowed by assertion
  title: string
  status: number // 0 for Network
  detail: string | null
  code: string | null
  correlationId: string | null
  traceId: string | null
  /** Field errors keyed by the request's camelCase property path ("name", "lines[0]", "address.city"). */
  fieldErrors: Record<string, string[]>
  retryAfterSeconds: number | null
}

export function toProblem(status: number, body: unknown, headers: Headers): Problem
export function networkProblem(error: unknown): Problem
export function isProblem(value: unknown): value is Problem
```

`toProblem` rules:
- Reads `body` defensively with `typeof` checks. **No `as` casts to `any`.** Use a small `readString(obj, key)`
  helper over `Record<string, unknown>`, narrowed with `typeof body === 'object' && body !== null`.
- Missing `type` → `ProblemTypes` status mapping identical to `ProblemTypes.ForStatus` (400→bad-request,
  401→unauthorized, 403, 404, 405, 409, 415, 422, 429, 503, ≥500→internal, else bad-request). This covers a
  non-JSON body from a proxy.
- `correlationId` = body `correlationId` ?? response header `X-Correlation-Id`.
- `fieldErrors` = body `errors` normalised with `toFieldKey`: lower-case the first character of **each dot/bracket
  segment** (`Name`→`name`, `Lines[0]`→`lines[0]`, `Address.City`→`address.city`). An empty key (`""`, from a
  body-level binding error) maps to `_form`. **Probe 12:** keys arrive PascalCase.
- `retryAfterSeconds` = integer parse of `Retry-After` when numeric, else `null`.

#### 6b — Correlation id

**Create file: `app/api/correlation.ts`**: `export const CorrelationHeader = 'X-Correlation-Id'` and
`newCorrelationId(): string` returning `crypto.randomUUID()` (≤ 64 chars of `[0-9a-f-]`, accepted by
`CorrelationIdMiddleware.IsAcceptable`).

#### 6c — Auth session abstraction (Story 07 fills it)

**Create file: `app/api/auth-session.ts`**

```ts
export interface AuthSession {
  /** Current access token, or null when signed out. */
  accessToken(): string | null
  /** Exchange the refresh credential for a new access token. Resolves true on success. Must not throw. */
  refresh(): Promise<boolean>
  /** Forget all credentials (called before redirecting to login). */
  clear(): void
  /** True for the refresh call itself, which must never trigger another refresh. */
  isRefreshRequest(request: Request): boolean
}

/** Until Story 07: nobody is signed in, and refresh always fails. A 401 therefore goes straight to login. */
export const anonymousSession: AuthSession = {
  accessToken: () => null,
  refresh: async () => false,
  clear: () => {},
  isRefreshRequest: () => false,
}
```

**Create file: `app/composables/useAuthSession.ts`**: returns `{ session, setAuthSession }`, where `session`
is a module-level `shallowRef<AuthSession>(anonymousSession)`. **Do not use `useState`**: the session holds
functions, which cannot be serialised into the SSR payload. Story 07 calls `setAuthSession` from a client-only
plugin. A module-level ref is shared across SSR requests, so it must only ever hold `anonymousSession` on the
server. Story 07's plugin is `*.client.ts` for that reason.

#### 6d — The fetch wrapper (401 → one refresh → one retry → login)

**Create file: `app/api/crm-fetch.ts`**

```ts
export interface CrmFetchOptions {
  baseFetch: (input: Request) => Promise<Response>
  session: () => AuthSession
  onUnauthenticated: () => void | Promise<void>
}

export function createCrmFetch(options: CrmFetchOptions): (input: Request) => Promise<Response>
```

Behaviour, in this order, per request:
1. Clone the request (`input.clone()`) **before** sending. A body stream can be read once and the retry needs it.
2. Set `X-Correlation-Id` if absent (`newCorrelationId()`), and `Authorization: Bearer <token>` when
   `session().accessToken()` is non-null. Send via `baseFetch`.
3. If the status is not 401, or `session().isRefreshRequest(input)`, return the response.
4. On 401: `const ok = await refreshOnce()`. **Single-flight**: a module-level `let inflight: Promise<boolean> | null`,
   set to `session().refresh().finally(() => { inflight = null })`, so concurrent 401s share one refresh. If `ok`,
   rebuild the clone's headers with the **new** token and the **same** correlation id and send it once. **No loop,
   no second refresh.**
5. If refresh failed, **or** the retry is also 401: `session().clear()`, `await onUnauthenticated()`, and return
   the 401 response so the caller's `error` is the unauthorized problem. The caller does not toast it (Task 6e).

**Create file: `app/api/clients.ts`**: one `createClient<paths>` per Area with the imported `paths` types from
`./generated/<area>` and `fetch: crmFetch`. Export `type CrmClients = { agent: Client<AgentPaths>; portal: …;
admin: …; public: … }` and `createCrmClients(baseUrl: string, fetch: (input: Request) => Promise<Response>): CrmClients`.
`baseUrl` is `''` in the browser (same-origin `/api`, proxied in dev by Probe 9) and
`useRuntimeConfig().apiBase` on the server (SSR cannot use a relative URL).

**Create file: `app/plugins/api.ts`**: `defineNuxtPlugin` that builds `createCrmFetch({ baseFetch: (r) =>
globalThis.fetch(r), session: () => useAuthSession().session.value, onUnauthenticated: () =>
navigateTo({ path: '/login', query: { redirect: useRoute().fullPath } }) })`, then `createCrmClients(...)`, and
`provide`s `api`. Declare the `$api` type through the plugin's return value (`return { provide: { api } }`), which
Nuxt types automatically. **No hand-written `declare module` with `any`.**

#### 6e — `useApi` and form errors

**Create file: `app/composables/useApi.ts`**

```ts
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }

export interface CallOptions {
  /** Default true. Non-validation, non-401 problems raise an error toast. */
  toastOnError?: boolean
}

export function useApi(): {
  clients: CrmClients
  call<T>(request: Promise<{ data?: T; error?: unknown; response: Response }>, options?: CallOptions): Promise<ApiResult<T>>
}
```

`call` rules:
- A thrown `fetch` (network down, abort) → `networkProblem(error)`, never an unhandled rejection.
- `error` present → `toProblem(response.status, error, response.headers)`.
- **Toast policy:** `validation` (400) → **never** toasted, because the form shows it; `unauthorized` → never
  (`crm-fetch` already redirected); everything else → `useToasts().push({ tone: 'danger', title: problem.title,
  description: problem.detail, correlationId: problem.correlationId })` unless `toastOnError: false`.
- Success with `data === undefined` (204) is `ok: true` with `data` typed `T | undefined` only for that overload.
  Keep it simple: type `data` as `T` and let 204 endpoints use `T = undefined`.

**Create file: `app/composables/useFormErrors.ts`**

```ts
export function useFormErrors() {
  const fieldErrors = ref<Record<string, string[]>>({})
  const formError = ref<string | null>(null)
  function apply(problem: Problem): boolean // true if it was a validation problem and fields were set
  function errorFor(field: string): string | undefined // first message
  function clear(field?: string): void
  return { fieldErrors, formError, apply, errorFor, clear }
}
```

`apply` puts `fieldErrors` in place, and `_form` messages plus `detail` for a validation problem with no field
keys go into `formError`. Forms bind `:error="errorFor('name')"` on `UiInput`. **This is the acceptance
criterion "400 surfaces as field-level errors, not a raw toast"**, demonstrated on `/_dev/api` (Task 12).

### 7 — Shell copy placeholder (hand-off to Story 05)

**Create file: `app/copy/en.ts`**: `export const shellCopy = { appName: 'Customer Support CRM', skipToContent: 'Skip
to main content', nav: { home: 'Home', … }, theme: { toLight: 'Switch to light theme', toDark: 'Switch to dark
theme' }, error: { title: 'Something went wrong', retry: 'Try again', home: 'Go home', reference: 'Reference' },
toast: { dismiss: 'Dismiss' }, pagination: { label: 'Pagination', previous: 'Previous', next: 'Next', page: 'Page' },
login: { title: 'Sign in', pending: 'Sign-in arrives with Story 07.' } } as const`.

Layouts, `error.vue`, and the login placeholder read strings **only** from here. **UI kit components contain no
copy at all**: every visible label and every `aria-label` comes from a prop or a slot (e.g. `UiModal` has a
required `closeLabel` prop). Story 05 replaces `shellCopy.x` with `t('shell.x')` mechanically. `/_dev/**` pages
may use literal English: they are dev-only and never localised.

### 8 — Toast store

**Create file: `app/stores/toasts.ts`**: Pinia setup store `useToasts`:
`interface Toast { id: string; tone: 'info' | 'success' | 'warning' | 'danger'; title: string; description: string |
null; correlationId: string | null; timeoutMs: number }`, `push(input): string` (defaults: `timeoutMs` 5000, **0
for `danger`**, which stays until dismissed; max **5** visible, oldest dropped), `dismiss(id)`, `clear()`. Timers
start only on the client (`import.meta.client`).

**Create file: `app/components/ui/ToastRegion.vue`**: fixed at `bottom-4 end-4` (logical), renders
`UiToast` per item. Two live regions: `role="status" aria-live="polite"` for info/success/warning, `role="alert"`
for danger. Mount once in `app/app.vue`.

### 9 — UI kit (`app/components/ui/`)

Nuxt auto-imports `app/components/ui/Button.vue` as **`UiButton`** (Probe 7 confirmed the path lints). Rules for
**every** component:

- `<script setup lang="ts">`, typed `defineProps`/`defineEmits`, `defineModel` for v-model, `useId()` for ids.
- Classes only from token utilities. `ui-hover:`/`ui-focus:` instead of `hover:`/`focus-visible:` (lint-enforced).
  Focus = `ui-focus:focus-ring`, **visible in both themes**.
- Logical utilities only (`ps-`, `me-`, `start-0`, `border-s`, `rounded-e-md`, `text-start`).
- Keyboard operable with native semantics first (`<button>`, `<input>`, `<select>`, `<dialog>`, `<table>`).
- **No domain words** (no "ticket", "customer", "status"). `UiBadge` has tones, not ticket statuses.
- Disabled uses the native `disabled` attribute plus `disabled:opacity-50 disabled:cursor-not-allowed`. Loading
  sets `aria-busy="true"`.
- Long content: text truncates only where a `truncate` prop asks. Otherwise it wraps (`break-words`), and a
  container never overflows horizontally at 375 px.

| Component | Props (all typed) | Keyboard / a11y contract |
|---|---|---|
| `Button.vue` | `variant: 'primary' \| 'secondary' \| 'ghost' \| 'danger'` (default primary), `size: 'sm' \| 'md' \| 'lg'`, `type: 'button' \| 'submit' \| 'reset'` (default `button`), `disabled`, `loading`, `block` | Native button. `loading` → disabled + spinner (`aria-hidden`) + `aria-busy`; label stays in the DOM |
| `Input.vue` | `label` (required), `modelValue` via `defineModel<string>()`, `type`, `hint`, `error`, `disabled`, `required`, `placeholder` | `inheritAttrs: false`; `$attrs` (including `data-force`) go on the `<input>`. `<label for>`, `aria-invalid`, `aria-describedby` → hint + error ids; error text `role="alert"` |
| `Textarea.vue` | as Input + `rows` | as Input |
| `Select.vue` | as Input + `options: ReadonlyArray<{ value: string; label: string; disabled?: boolean }>`, `placeholder` | **Native `<select>`** (full keyboard for free); chevron is decorative and uses `end-3` |
| `Modal.vue` | `open` via `defineModel<boolean>('open')`, `title` (required), `closeLabel` (required), `size: 'sm' \| 'md' \| 'lg'`; slots `default`, `footer` | Native `<dialog>` + `showModal()`: focus trap, `Esc`, and inert background are native; `aria-labelledby` title; restores focus to the opener on close; backdrop click closes |
| `Drawer.vue` | as Modal + `side: 'start' \| 'end'` (default `end`) | Same `<dialog>` mechanics, pinned with `inset-y-0 start-0`/`end-0` (so it flips in RTL for free) |
| `Table.vue` | `generic="Row"`: `columns: ReadonlyArray<{ key: string; label: string; align?: 'start' \| 'end' }>`, `rows: ReadonlyArray<Row>`, `rowKey: (row: Row) => string`, `caption` (required, may be `sr-only`), `loading`; slots `cell-<key>` (scoped `{ row }`), `empty` | Real `<table>`/`<caption>`/`<th scope="col">`; horizontal scroll inside its own `overflow-x-auto` wrapper, never the page; `loading` → 5 skeleton rows + `aria-busy` |
| `Badge.vue` | `tone: 'neutral' \| 'primary' \| 'success' \| 'warning' \| 'danger' \| 'info'`, `size` | Text, not colour, carries meaning; contrast ≥ 4.5:1 per tone |
| `Avatar.vue` | `name` (required), `src?`, `size: 'sm' \| 'md' \| 'lg'` | `<img alt="">` + visible-to-AT name via `aria-label` on wrapper when no adjacent text; initials fallback from `name` (first letters of first two words; works for Arabic letters) when `src` absent or `@error` |
| `Tabs.vue` | `tabs: ReadonlyArray<{ id: string; label: string; disabled?: boolean }>`, `modelValue` (active id), `label` (required, `aria-label`); scoped slot `panel` `{ id }` | WAI-ARIA tabs: `role=tablist/tab/tabpanel`, roving `tabindex`, **ArrowLeft/ArrowRight/Home/End**, automatic activation. Story 05 swaps arrow direction under `dir=rtl` |
| `Toast.vue` | `tone`, `title`, `description?`, `correlationId?`, `dismissLabel` (required), `referenceLabel` (required); emits `dismiss` | Dismiss is a real button; shows `referenceLabel: <correlationId>` in `font-mono` so a user can quote it to support |
| `Skeleton.vue` | `variant: 'text' \| 'rect' \| 'circle'`, `lines` (text) | `aria-hidden="true"`; `motion-safe:animate-pulse` only |
| `EmptyState.vue` | `title` (required), `description?`; slots `icon`, `actions` | Heading level via `headingLevel: 2 \| 3` (default 2) |
| `ErrorState.vue` | `title` (required), `description?`, `correlationId?`, `referenceLabel`, `retryLabel?`; emits `retry` | `role="alert"`; retry button only when `retryLabel` is set |
| `Pagination.vue` | `page` via `defineModel<number>('page')`, `pageSize`, `total`, `label` (required nav label), `previousLabel`, `nextLabel`, `pageLabel` | `<nav aria-label>`; current page `aria-current="page"`; prev/next disabled at the bounds; windowed list (1 … 4 5 6 … 20) |

Add `app/components/ui/Spinner.vue` (used by Button and loading boundaries) and
`app/components/ui/ThemeToggle.vue` (a `UiButton` `ghost` with `aria-pressed`, labels from props) as internal
helpers. They count toward the kit and appear on `/_dev/ui`.

### 10 — Layouts, pages, boundaries

**Create file: `app/app.vue`**

```vue
<template>
  <NuxtLoadingIndicator color="var(--crm-primary-500)" />
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToastRegion />
</template>
```

**Create file: `app/layouts/portal.vue`**: public, SSR. A skip link (`sr-only focus:not-sr-only`, first focusable),
a `<header>` with app name and `UiThemeToggle`, `<main id="main" tabindex="-1">`, and a `<footer>`. The slot is
wrapped in `<NuxtErrorBoundary>` whose `#error="{ error, clearError }"` renders `UiErrorState` with a retry that
calls `clearError()`. Landmarks, one `<h1>` per page (the page owns it), max width `max-w-5xl mx-auto px-4`.

**Create file: `app/layouts/agent.vue`**: SPA workspace. `lg:` and up: a persistent sidebar (`w-64 border-e`,
`<nav aria-label>`) plus a topbar (`<header>`: menu button, app name, `UiThemeToggle`, a user-slot placeholder
`UiAvatar name="—"`). Below `lg`: the sidebar collapses into a `UiDrawer side="start"` opened by the topbar menu
button (`aria-expanded`, `aria-controls`). Same skip link and `NuxtErrorBoundary` as portal. Nav items come from a
typed `const agentNav: ReadonlyArray<{ to: string; label: string }>` (only "Home" → `/agent` in this story).

**Do not create `app/layouts/default.vue`.** Instead set `app/pages/*` explicitly with
`definePageMeta({ layout: 'portal' | 'agent' | false })`. A page without it renders with no layout, which is
visible immediately on review, rather than silently inheriting the wrong shell.

**Create file: `app/error.vue`**: the global Nuxt error page (`NuxtError` prop typed from `#app`). It renders
outside `app.vue`, so it wraps itself in `<NuxtLayout name="portal">`. Status 404 →
`UiEmptyState`; otherwise `UiErrorState` with `shellCopy.error.*` and `clearError({ redirect: '/' })`. **Never
render `error.stack` or `error.message` for a 5xx** in production. `import.meta.dev` may show the message.

**Create pages:**
- `app/pages/index.vue`: `definePageMeta({ layout: 'portal' })`, an `<h1>` with the app name and one paragraph.
  This page is the **Lighthouse target**.
- `app/pages/agent/index.vue`: `definePageMeta({ layout: 'agent' })`, `<h1>` plus a `UiEmptyState`.
- `app/pages/login.vue`: placeholder redirect target (`layout: 'portal'`), `shellCopy.login.*`. Reads
  `route.query.redirect` as a `string` only when `typeof === 'string'`. **Story 07 replaces this file.**

**Route-level loading:** `NuxtLoadingIndicator` covers navigation. Data-backed pages use
`useAsyncData`/`useApi().call` with `UiSkeleton` while pending, `UiEmptyState` when empty, and `UiErrorState` on
failure. `/_dev/api` shows all three (Task 12).

### 11 — Dev-only route exclusion

**Create file: `modules/dev-only-pages.ts`** (Probe 8)

```ts
import { defineNuxtModule } from 'nuxt/kit'
import type { NuxtPage } from 'nuxt/schema'

function prune(pages: NuxtPage[]): void {
  for (let i = pages.length - 1; i >= 0; i--) {
    const page = pages[i]
    if (page === undefined) continue
    if (page.path.startsWith('/_dev')) pages.splice(i, 1)
    else if (page.children) prune(page.children)
  }
}

/** /_dev/** is the review surface for a project without component tests. It never ships. */
export default defineNuxtModule({
  meta: { name: 'crm-dev-only-pages' },
  setup(_options, nuxt) {
    if (nuxt.options.dev) return
    nuxt.hook('pages:extend', prune)
  },
})
```

### 12 — `/_dev/ui` and `/_dev/api`

**Create file: `app/pages/_dev/ui.vue`**: `definePageMeta({ layout: false })`. A sticky header with an index of
anchors (one per component) and a global theme toggle. For **each** component, a `<section id>` with two columns
(`grid md:grid-cols-2`), **light** (`data-theme="light"`) and **dark** (`data-theme="dark"`), each on `bg-surface`
(Probe 5 makes the nested theme work). Each column renders a row per state, labelled with the state name:

| State | How it is produced |
|---|---|
| default | plain props |
| hover | `data-force="hover"` on the component (`ui-hover` variant) |
| focus | `data-force="focus"` (`ui-focus` variant); also Tab through the live row |
| disabled | `disabled` |
| loading | `loading` (Button, Table), `UiSkeleton` (for the rest, the loading representation the component uses) |
| error | `error="…"` (Input/Textarea/Select), `tone="danger"` (Badge/Toast), `UiErrorState` |
| long content | a 300-character unbroken token, an 80-word sentence, and a 40-character Arabic string (`"مرحبا بكم في نظام دعم العملاء متعدد الأقسام والفروع"`) as label/value/title/cell |

Where a state does not apply (hover on Skeleton, disabled on Avatar), render the row with a short "n/a" note, so
reviewers see it was considered. Modal/Drawer: an "open" button per theme plus an always-open inline preview
(`<dialog open>` markup without `showModal`, clearly labelled "static preview"). Toast: buttons that `push` each
tone into the real store, plus static renders. Add a **375 / 768 / 1280 width switcher** in the header that
constrains the content wrapper (`max-w-[375px]`, `max-w-[768px]`, `max-w-[1280px]`). Real breakpoint behaviour
is still checked in the DevTools device toolbar (Test Plan row 19), because `md:`/`lg:` key off the viewport,
not the wrapper.

**Create file: `app/pages/_dev/api.vue`**: `definePageMeta({ layout: false })`. Three panels:
1. **Live API** (backend running): buttons for `GET /api/Admin/Diagnostics/throw` (500 → danger toast with
   reference), `forbidden` (403 → toast), `unauthorized` (401 → redirect to `/login?redirect=/_dev/api`), and a small
   form (`UiInput label="Name"`, `UiTextarea` "Lines, one per line") that `POST`s `/api/Admin/Samples` through
   `useApi().call(clients.admin.POST(...))` + `useFormErrors().apply`. An empty name shows **"'Name' must not be
   empty."** under the Name field and **no toast**. A valid submit shows a success toast.
2. **Fake transport (401 flow without a backend)**: builds a private `createCrmFetch` with a scripted `baseFetch`
   (a queue of `Response` objects) and a fake `AuthSession` whose `refresh` counts calls. It renders counters
   *requests sent*, *refreshes*, *redirects*, *last Authorization header*. Scenarios:
   - A: `[401, 200]`, refresh → true ⇒ 2 requests, 1 refresh, 0 redirects, and the retry carries the new token and
     the same correlation id.
   - B: `[401, 401]`, refresh → true ⇒ 2 requests, 1 refresh, **1 redirect** (`onUnauthenticated` spy, not a real
     navigation).
   - C: `[401]`, refresh → false ⇒ 1 request, 1 refresh, 1 redirect.
   - D: three parallel calls, each `[401, 200]` ⇒ **1** refresh, 6 requests.
   - E: a request for which `isRefreshRequest` is true, `[401]` ⇒ 0 refreshes, returned as-is.
   Each scenario prints PASS/FAIL against its expected counters. **This page is the manual check for the 401
   acceptance criterion** until Story 07 exists.
3. **Data states**: a `useAsyncData` demo over `GET /api/Agent/Sample?name=…` that shows skeleton → data, an empty
   variant, and the error state (by pointing at a missing route → 404).

### 13 — Frontend runbook and conventions

**Create file: `frontend/README.md`** (a runbook, like `backend/README.md`): prerequisites (Node 24, the corepack
command from Task 1), scripts, `gen:api` online/offline and when to run it (**after every backend change that
touches a controller or DTO**; commit `openapi/` and `app/api/generated/` together with the backend change), the
dev proxy, the token/theme model, the error contract (`useApi` + `useFormErrors`), the lint rules and why, and
`/_dev/ui` + `/_dev/api` as the review surface.

**File: `CLAUDE.md`**, section "Frontend — Nuxt + Vue 3 + TypeScript + Tailwind" (lines ~192–204). Append bullets:
- API types come only from `pnpm gen:api` (`openapi/*.json` → `app/api/generated/*.ts`, both committed). Never
  hand-write a DTO type.
- All HTTP goes through `useApi()`. A 400 is shown with `useFormErrors()`, never a toast.
- Styling uses the `--crm-*` token utilities only. `ui-hover:`/`ui-focus:` inside `app/components/ui/`.
- Every new UI kit component, or new state, gets a section on `/_dev/ui`.
- pnpm is enabled with `corepack enable pnpm --install-directory "$APPDATA/npm"` on Windows without admin.

**File: `.claude/commands/frontend-slice.md`**. In "Layout", replace the `app/types/<feature>.ts` line with:
"API DTO types come from `app/api/generated/<area>.ts` (`pnpm gen:api`). Do not write them by hand; a view-model
type local to the feature may live in `app/types/<feature>.ts`."

---

## Backend Tasks

No backend changes required. The API is only **run** (Development) to generate types and to walk `/_dev/api`.

---

## Edge Cases & Failure Modes

- **`pnpm` not found.** Plain `corepack enable` hits EPERM on `C:\Program Files\nodejs`. Use the
  `--install-directory "$APPDATA/npm"` form (Task 1). In a non-interactive shell without
  `COREPACK_ENABLE_DOWNLOAD_PROMPT=0` the first call hangs on a download prompt.
- **`pnpm --dir frontend …` from the repo root.** The repo root has no `package.json`, so corepack falls back to its
  default pnpm. That is also 12.6.0 today. If the versions ever diverge, run from `frontend/` or add
  `COREPACK_ENABLE_STRICT=0`. Do not add a root `package.json` for this.
- **Registry mirror slowness/timeouts** (`najizportalnpm.azurewebsites.net`). A cold install takes minutes. Run
  it with a 10-minute timeout and **do not** switch the committed config to another registry. The lockfile stores
  integrity hashes, not the mirror URL.
- **`ERR_PNPM_NO_MATCHING_VERSION cacheable@^2.5.1`** → the `flat-cache` override is missing from
  `pnpm-workspace.yaml` (Probe 2).
- **`ERR_PNPM_IGNORED_BUILDS`** after an upgrade adds a new native dependency → add it to `allowBuilds` with a
  one-line reason. **Never** run with `--ignore-scripts` to get past it: esbuild then has no binary.
- **`pnpm lint` fails with "Cannot find module './.nuxt/eslint.config.mjs'"** on a fresh clone → run
  `pnpm install` (its `postinstall` is `nuxt prepare`).
- **Someone bumps `typescript` to 7.x** → openapi-typescript's peer range breaks, and vue-tsc's TS 5 API usage
  with it. Stays at 5.9.3 until both declare TS 7 support (Probe 1).
- **API not running when `pnpm gen:api` runs** → the script exits 1 with the "cannot reach" message and writes
  nothing (Task 5). `--offline` regenerates from `openapi/`.
- **Portal document has no paths** (Probe 11) → `paths` is empty and `clients.portal` has no callable routes.
  Correct until a Portal controller exists. Not an error.
- **Swagger declares problems as `application/json`** while the wire says `application/problem+json` (Probe 12).
  `openapi-fetch` parses JSON regardless of content type. `toProblem` never branches on content type, only on
  `response.ok`.
- **Non-JSON error body** (a proxy's HTML 502, an empty 401 from a future auth middleware) → `openapi-fetch` returns
  `error` as text or `undefined`. `toProblem` falls back to the status → `type` map with a generic title and keeps
  `X-Correlation-Id` from the header.
- **PascalCase / nested / indexed field keys** (`Name`, `Lines[0]`, `Address.City`, and `""`) → `toFieldKey`
  (Task 6a). A key with no matching form field lands in `formError` only when the form calls `apply` and finds no
  bound field. Forms render `formError` above the submit button.
- **Validation message contains `" | "`**: the backend's `ValidationErrorParser` quirk (Story 03 Edge Cases) can
  misattribute a field message. Status and `type` stay right. Not fixed here.
- **Concurrent 401s** → single-flight refresh (Task 6d, step 4), verified by `/_dev/api` scenario D.
- **Refresh request itself returns 401** → `isRefreshRequest` short-circuits, so there is no refresh recursion
  (scenario E). Story 07 must implement `isRefreshRequest` for its refresh URL.
- **Retry of a non-idempotent POST.** The retry after a 401 is safe because a 401 means the server rejected the
  request before running the handler (authentication middleware runs first). Do not extend the retry to any other
  status.
- **SSR calls during portal render** use `runtimeConfig.apiBase` (server) instead of the dev proxy. When the API is
  down, SSR `useAsyncData` resolves to an error state, and the page still renders (no 500).
- **Production `/api` routing.** `devProxy` exists only in `pnpm dev`. `pnpm preview` and production need a reverse
  proxy that serves `/api` on the same origin, **or** a backend CORS policy. That is a deployment decision, out of
  scope. Record it in `frontend/README.md`. The Lighthouse run targets `/`, which makes no API call.
- **Theme cookie tampered** (`crm-theme=purple`) → `useTheme` coerces anything but `dark` to `light`.
- **`/_dev/**` in production** → the route does not exist (404 via `error.vue`), and its code is absent from
  `.output` (Probe 8). Verify with `grep -r "Fake transport" frontend/.output` → empty.
- **`hover:` or `focus-visible:` written inside `app/components/ui/`** → lint error. Use `ui-hover:`/`ui-focus:`, or
  `/_dev/ui` cannot show the state.
- **False positives of the physical-direction regex**: a script string that is exactly `'left'`/`'right'` (e.g. a
  placement prop value) fails lint. Use `'start'`/`'end'`. User-visible prose lives in `shellCopy` today and in
  locale JSON after Story 05. A sentence such as "turn left" in a `.ts` literal would trip it; move it to copy
  under a key or reword it. **Do not weaken the rule.**
- **`<dialog>` and SSR**: `showModal()` only exists in the browser. Call it in a `watch` on `open` with
  `{ immediate: true }` guarded by `import.meta.client` and a template ref that may be `null` (check it, **no `!`**).
- **Arabic initials in `UiAvatar`**: take the first code point with `Array.from(word)[0]`, not `word[0]`, so
  surrogate pairs and combining marks do not split.
- **Lighthouse below 95** usually means contrast (fix tokens, not components), a missing `lang` (set in
  `nuxt.config.ts` `app.head`), a skip link that is not first, or a button without a name.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add Vitest, Vue Test
Utils, Playwright, Cypress, Storybook, `@nuxt/test-utils`, Lighthouse CI, or any `*.spec.ts` / `*.test.ts`
file.** The type checker, the linter, `/_dev/ui`, and `/_dev/api` replace the suite. Run every row and record the
result in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Install | `cd frontend && pnpm install` | Completes; no `ERR_PNPM_*`; `.nuxt/` generated |
| 2 | Typecheck | `pnpm --dir frontend typecheck` | exit 0 |
| 3 | Typecheck bites | Temporarily add `const n: number = 'x'` to `app/pages/index.vue` | `TS2322`, exit ≠ 0; revert |
| 4 | Lint | `pnpm --dir frontend lint` | exit 0 |
| 5 | Lint bites | Temporarily add a `.vue` with `: any`, `x!`, `// @ts-ignore`, `class="ml-2 text-left"`, a script string `'pl-3'`, and `hover:bg-surface` in a `ui/` component | Each is an error; delete the file |
| 6 | Grep guard | `grep -rnE "(:\s*any\b|as any|@ts-ignore|@ts-expect-error|@ts-nocheck)" frontend/app frontend/modules frontend/scripts` | No matches |
| 7 | Build | `pnpm --dir frontend build` | Succeeds; `.output/` exists |
| 8 | Dev pages stripped | `grep -rl "Fake transport\|static preview" frontend/.output` | No matches |
| 9 | Dev serve | `pnpm --dir frontend dev` → `http://localhost:3000/`, `/agent`, `/_dev/ui`, `/_dev/api` | All 200; `/` HTML contains the h1 (SSR); `/agent` HTML has no page content (SPA) |
| 10 | Gen API | API running; `pnpm --dir frontend gen:api` | 4 JSON + 4 TS written; `git diff --stat` empty on a second run (deterministic) |
| 11 | Gen API offline | API stopped; `gen:api` → exit 1 with message; `gen:api --offline` → exit 0 | as stated |
| 12 | Correlation | `/_dev/api` → any live call; DevTools Network | Request has `X-Correlation-Id` (UUID); response echoes the same value; API log line carries it |
| 13 | 400 → fields | `/_dev/api` form, empty Name | "'Name' must not be empty." under Name; **no toast**; `aria-invalid="true"` on the input |
| 14 | 500 → toast | `/_dev/api` "throw" | Danger toast, `Reference: <correlationId>` matches the response header; no stack text |
| 15 | 401 live | `/_dev/api` "unauthorized" | Exactly **one** request to `/unauthorized` (anonymous session: refresh returns false) then navigation to `/login?redirect=%2F_dev%2Fapi` |
| 16 | 401 scenarios | `/_dev/api` Fake transport A–E | All PASS with the counters in Task 12 |
| 17 | Kit coverage | `/_dev/ui`, each section | 15 kit components + Spinner + ThemeToggle, each with the 7 state rows in light **and** dark |
| 18 | Keyboard | `/_dev/ui` with keyboard only | Every interactive component reachable by Tab, visible focus ring in both themes; Modal/Drawer trap focus, close on Esc, return focus; Tabs arrow/Home/End; Select opens with Alt+Down / Space |
| 19 | Responsive | `/`, `/agent`, `/_dev/ui` at 375 / 768 / 1280 (DevTools device toolbar) | No horizontal page scroll; agent sidebar becomes a drawer below `lg` |
| 20 | Theme persistence | Toggle to dark on `/`, reload, view-source | `<html data-theme="dark">` in the **server** HTML; no flash |
| 21 | Lighthouse | `pnpm --dir frontend build && pnpm --dir frontend preview`, Edge DevTools → Lighthouse → Accessibility, Desktop **and** Mobile, on `http://localhost:3000/` in light and dark | Score **≥ 95** in all four runs (Chrome is not installed; Edge at `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe` has the same panel) |
| 22 | Error page | `http://localhost:3000/does-not-exist` in preview | Styled 404 via `error.vue`, no stack |
| 23 | Regression | `dotnet build backend/Crm.sln`; `git status backend/` | 0 warnings; no backend file changed |

---

## Verification Steps

1. **Machine:** `pnpm --version` → `12.6.0` (Task 1).
2. **Frontend installs:** `cd frontend && pnpm install` → no errors (row 1).
3. **Frontend gates:** from `D:\CRM`: `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`,
   `pnpm --dir frontend build` → all exit 0 (rows 2, 4, 7). Then rows 3, 5, 6, 8.
4. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d` (SQL Server must be up, because the API
   migrates on start), then `dotnet run --project backend/src/Crm.Api` → `Now listening on: http://localhost:5187`.
5. **Types generated:** `pnpm --dir frontend gen:api` → rows 10–11. Commit `frontend/openapi/` and
   `frontend/app/api/generated/`.
6. **Frontend runs:** `pnpm --dir frontend dev` → rows 9, 12–20.
7. **Production preview:** rows 21–22.
8. **Regression:** row 23.

---

## Done Criteria

- [ ] `pnpm install` then `pnpm dev` serves the app, and `pnpm build` produces `.output/`.
- [ ] `pnpm typecheck` and `pnpm lint` pass with zero errors, from `frontend/` and via `pnpm --dir frontend`.
- [ ] TypeScript strict is on. ESLint errors on `any`, ts-ignore / ts-expect-error / ts-nocheck, non-null `!`, and
      physical-direction utilities. The grep in Test Plan row 6 is empty.
- [ ] `pnpm gen:api` is a single command producing `openapi/{agent,portal,admin,public}.json` and
      `app/api/generated/{agent,portal,admin,public}.ts`. Both are committed, and no DTO type is hand-written.
- [ ] A 401 triggers exactly one refresh and one retry; a second 401 (or failed refresh) redirects to `/login`.
      Proven by `/_dev/api` scenarios A–E and the live `unauthorized` call.
- [ ] A 400 problem from `POST /api/Admin/Samples` appears as a field error under "Name" on the `/_dev/api` form,
      with no toast.
- [ ] Every request carries `X-Correlation-Id`. Error toasts and `UiErrorState` show the correlation id.
- [ ] Tailwind v4 tokens come from `--crm-*` variables in `app/assets/css/tokens.css`. No component uses a
      literal colour. The light/dark switch persists via cookie and renders server-side.
- [ ] `agent` layout (sidebar + topbar, SPA) and `portal` layout (public, SSR) exist, with skip link, landmarks,
      and `NuxtErrorBoundary`. `error.vue` and `NuxtLoadingIndicator` are in place.
- [ ] `app/components/ui/` holds Button, Input, Select, Textarea, Modal, Drawer, Table, Badge, Avatar, Tabs, Toast,
      Skeleton, EmptyState, ErrorState, Pagination (+ Spinner, ThemeToggle, ToastRegion), free of domain concepts
      and of hardcoded copy.
- [ ] `/_dev/ui` renders every kit component in default, hover, focus, disabled, loading, error, and long-content
      states in light and dark. `/_dev/**` is absent from the production build (row 8).
- [ ] Every kit component is keyboard operable with a visible focus ring (row 18).
- [ ] Lighthouse accessibility on the portal layout (`/`) is ≥ 95, desktop and mobile, light and dark (row 21).
- [ ] `frontend/README.md`, the `CLAUDE.md` frontend bullets, and `.claude/commands/frontend-slice.md` are
      updated (Task 13).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere. `grep -rE
      "vitest|playwright|@vue/test-utils|cypress|@nuxt/test-utils" frontend/package.json` is empty.
- [ ] No file under `backend/` changed.
- [ ] `00-overview.md` updated with this story.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 05.**
