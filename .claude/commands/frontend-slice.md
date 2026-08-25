---
description: Scaffold a Nuxt feature module following the project's Vue + TypeScript + Tailwind conventions.
---

Create a frontend feature module for `$ARGUMENTS` (e.g. `tickets/inbox` or `customers/profile`).

## Layout

- `app/pages/<feature>/...` - route pages, thin. Data fetching and layout only.
- `app/components/<feature>/` - presentational components, `PascalCase.vue`, `<script setup lang="ts">`.
- `app/composables/use<Feature>.ts` - data access and local state, one composable per use case.
- `app/types/<feature>.ts` - DTO types mirroring the backend contract, generated where possible.
- `app/stores/<feature>.ts` - Pinia store only when state is shared across routes.
- `i18n/locales/{en,ar}.json` - every user-visible string, keyed `<feature>.<key>`. No hardcoded copy.

## Rules

- TypeScript strict. No `any`, no ts-ignore, no non-null `!` to silence the compiler. With no test suite on this
  project, the type checker is the only automated safety net - do not blunt it.
- Tailwind utility classes only - no scoped CSS unless there is no utility for it.
- Every layout must work in both LTR and RTL: use logical utilities (`ms-`/`me-`/`ps-`/`pe-`/`text-start`),
  never `ml-`/`mr-`/`text-left`.
- Mobile-first and responsive; verify at 375px, 768px, and 1280px.
- Loading, empty, and error states are required for every data-backed view, not optional polish.
- Reuse the existing design-system components under `app/components/ui/` before adding a new one.
- **No test files** - see the testing policy in `CLAUDE.md`. Verify with `pnpm typecheck`, `pnpm lint`,
  `pnpm build`, then walk the screen in the browser in both `en` and `ar`.
