# CRM frontend

Nuxt 4 application for the Customer Support CRM: a public, SSR **portal** and an SPA **agent** workspace,
sharing one design system and one typed API client. See `CLAUDE.md` at the repo root for the binding
conventions (RTL, i18n, testing policy). This file is the runbook.

## Prerequisites

- Node 24+.
- pnpm, enabled without admin rights (Windows without elevation cannot write `pnpm.CMD` into
  `C:\Program Files\nodejs`):

  ```bash
  corepack enable pnpm --install-directory "$APPDATA/npm"   # Git Bash; %APPDATA%\npm is on PATH
  export COREPACK_ENABLE_DOWNLOAD_PROMPT=0                   # non-interactive shells
  pnpm --version                                             # -> 12.6.0
  ```

  If corepack is ever broken: `npm i -g pnpm@12.6.0`.
- The backend running in Development for `pnpm gen:api` and for `/_dev/api`'s live panel:
  `cd backend && docker compose up -d` then `dotnet run --project backend/src/Crm.Api --launch-profile http`
  (serves `http://localhost:5187`, migrates the database on start).

## Scripts

Run from `frontend/`, or from the repo root via `pnpm --dir frontend <script>`.

| Script | What it does |
|---|---|
| `pnpm install` | Installs dependencies. `postinstall` runs `nuxt prepare`. |
| `pnpm dev` | Dev server on `http://localhost:3000`, proxying `/api` to the backend on `:5187`. |
| `pnpm build` | Production build to `.output/`. Strips `/_dev/**`. |
| `pnpm preview` | Serves the production build (`.output/`) for a Lighthouse run. |
| `pnpm typecheck` | `nuxt typecheck` (vue-tsc). The only automated regression net — see "Testing policy". |
| `pnpm lint` / `pnpm lint:fix` | ESLint, including the physical-direction-utility and `any`/`ts-ignore`/`!` bans. |
| `pnpm format` / `pnpm format:check` | Prettier. |
| `pnpm gen:api` | Regenerates the typed API client from the backend's OpenAPI documents (below). |

## `pnpm gen:api`

Run it **online** (default) whenever a backend change touches a controller or DTO, with the API running in
Development:

```bash
pnpm gen:api
```

It fetches `/swagger/{agent,portal,admin,public}/swagger.json`, writes them to `openapi/*.json`, and
generates `app/api/generated/*.ts` with `openapi-typescript`. **Both `openapi/` and `app/api/generated/` are
committed** — a frontend-only change never needs the API or SQL Server running, and a backend change that
regenerates them ships the JSON diff in the same commit as the schema move.

Run it **offline** to regenerate types from the committed snapshots without the API:

```bash
pnpm gen:api --offline
```

If the API is unreachable and `--offline` was not passed, the script prints a message and exits 1 rather
than writing a half-updated set of files.

Never hand-write a DTO type — it comes from `app/api/generated/<area>.ts` only.

## The dev proxy

`nuxt.config.ts`'s `nitro.devProxy` forwards `/api/**` to `http://localhost:5187/api` in `pnpm dev`, so the
browser only ever calls same-origin `/api` and the backend needs no CORS policy in development. In
production, `/api` must be served from the same origin by a reverse proxy (or the backend needs a CORS
policy) — a deployment decision, not yet made.

## Auth token and theme

- **Theme**: `useTheme()` reads/writes a `crm-theme` cookie (`light` | `dark`, default `light`). It is read
  on the server, so SSR already emits the right `data-theme` with no flash. A tampered cookie value coerces
  to `light`. Story 58 (branding) overrides only the `--crm-*` variables in `app/assets/css/tokens.css`.
- **Auth**: `useAuthSession()` holds a module-level `AuthSession` (`accessToken`, `refresh`, `clear`,
  `isRefreshRequest`), currently `anonymousSession` (nobody signed in, refresh always fails). Story 07 calls
  `setAuthSession` from a `*.client.ts` plugin with the real implementation and replaces `app/pages/login.vue`.
  It **must not change `createCrmFetch`'s retry contract**: one single-flight refresh per 401, one retry with
  the new token and the same correlation id, then `clear()` and redirect to `/login?redirect=…` on a second
  401 or a failed refresh.

## The error contract

Every request goes through `useApi()`, never a raw `$fetch`/`fetch`. `useApi().call(request)`:

- Never throws — a network failure or a `{ error }` response both resolve to `{ ok: false, problem }`
  (`app/api/problem.ts`'s `Problem`, mirroring the backend's RFC 9457 body plus a client-only `network` type).
- Toasts automatically for anything that is not a `validation` (400) or `unauthorized` (401) problem —
  `crm-fetch` already redirects on 401, and a form shows its own validation errors.
- Backend field-error keys are PascalCase (`Name`, `Lines[0]`, `Address.City`); `toProblem` camel-cases each
  segment. A form binds `useFormErrors()`'s `fieldErrors`/`errorFor(field)` — **a 400 is a field error, never
  a toast.**

`app/api/problem.ts`'s `ProblemTypes` mirrors
`backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs` **verbatim**. Never
rename a value; add one when the backend adds one.

## Lint rules and why

- `@typescript-eslint/no-explicit-any`, `no-non-null-assertion`, `ban-ts-comment` (`@ts-ignore`,
  `@ts-expect-error`, `@ts-nocheck`) are errors. With no test suite, the compiler and the linter are the
  regression net — never silence them.
- Every physical-direction Tailwind utility (`ml-`, `mr-`, `pl-`, `pr-`, `left-`, `right-`,
  `text-left`/`text-right`, `border-l`/`border-r`, `rounded-l`/`rounded-r`, `float-left`/`float-right`, …) is
  a lint error, in templates and in script strings/template literals. Use the logical form: `ms-`, `me-`,
  `ps-`, `pe-`, `start-`, `end-`, `text-start`, `text-end`, `border-s`, `border-e`, `rounded-s`, `rounded-e`.
- Inside `app/components/ui/**`, bare `hover:`/`focus-visible:` are also errors — use `ui-hover:`/`ui-focus:`
  so `/_dev/ui` can force those states without a pointer (`data-force="hover"` / `data-force="focus"`).

## The review surface: `/_dev/ui` and `/_dev/api`

This project ships without automated tests (`CLAUDE.md`, "Testing policy"). In their place:

- **`/_dev/ui`**: every `app/components/ui/` component, in default, hover, focus, disabled, loading, error,
  and long-content states, in light and dark side by side, with a 375/768/1280 width switcher. Add a new kit
  component or a new state here in the same change that adds it.
- **`/_dev/api`**: exercises `useApi()`/`useFormErrors()`/`createCrmFetch` against the live API (a 500, a
  403, a 401, and a validation-erroring form) and against a scripted fake transport that proves the 401
  refresh/retry/redirect contract (scenarios A–E) without needing a backend.

Both exist only in `pnpm dev` — `modules/dev-only-pages.ts` removes `/_dev/**` from the page tree when
`nuxt.options.dev` is false, so neither route nor its code reaches `.output/`.

## Folder layout

See `.claude/commands/frontend-slice.md` for the layout every later frontend story follows
(`app/pages`, `app/components/<feature>`, `app/composables/use<Feature>.ts`, `app/stores`, reusing
`app/components/ui/`).
