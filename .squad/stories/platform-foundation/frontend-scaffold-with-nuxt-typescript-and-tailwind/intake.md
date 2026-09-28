# Story 04 — Frontend scaffold with Nuxt TypeScript and Tailwind

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/frontend-scaffold-with-nuxt-typescript-and-tailwind/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`
- **Module (backend):** none (frontend); consumes the per-Area Swagger documents that merge every module's controllers

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-04`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Frontend scaffold with Nuxt TypeScript and Tailwind
```

---

## Description

Stand up the Nuxt 4 application and its shared shell so feature stories only add pages and components.

- A `frontend/` Nuxt 4 project: TypeScript strict, pnpm, ESLint and Prettier.
- Tailwind with design tokens for colour scale, spacing, radii, and typography, driven by CSS variables so
  story 58 can rebrand without touching components, plus a light and dark theme switch.
- Two layouts: `agent` with a persistent sidebar and topbar running as an SPA, and `portal`, public and SSR.
- A typed API client generated from the backend OpenAPI document, with interceptors for the auth token, the
  correlation id, and normalisation of problem-details errors into field-level form errors.
- A minimal UI kit under `app/components/ui/`: Button, Input, Select, Textarea, Modal, Drawer, Table, Badge,
  Avatar, Tabs, Toast, Skeleton, EmptyState, ErrorState, Pagination.
- Route-level loading and error boundaries, a global toast store, and a `useApi` composable.
- A `/_dev/ui` route rendering the whole kit in every state and both themes. On a project with no component
  tests this page is the review surface, so it must cover disabled, loading, error, and long-content states.

## Acceptance criteria

- [ ] `pnpm install` then `pnpm dev` serves the app, and `pnpm build` produces a production bundle.
- [ ] `pnpm typecheck` and `pnpm lint` pass with zero errors.
- [ ] TypeScript strict is on, and the repository contains no `any`, no ts-ignore, and no non-null `!` used to
      silence the compiler.
- [ ] The generated API client is produced by a single `pnpm gen:api` command and is committed.
- [ ] An API 401 triggers one token refresh and one retry; a second 401 redirects to login.
- [ ] An API 400 problem-details response surfaces as field-level errors on the calling form, not a raw toast.
- [ ] `/_dev/ui` renders every UI kit component in default, hover, focus, disabled, loading, error, and
      long-content states, in both light and dark theme, and is excluded from production builds.
- [ ] Every UI kit component is keyboard operable and has visible focus styling.
- [ ] The Lighthouse accessibility score on the portal layout is 95 or higher.

## Dependencies

- **Blocked by / related ids:** CRM-01, for the OpenAPI document.
- **Depends on code areas or other stories:** none in the frontend. Every frontend story depends on this one.

## Technical hints

- Use `openapi-typescript` with `openapi-fetch`; do not hand-write DTO types.
- Keep the UI kit free of domain concepts. A ticket status badge belongs to the ticket story, not here.
- Do not install Vitest, Vue Test Utils, or Playwright. `/_dev/ui` plus the type checker is the agreed
  verification path for this project.

## Out of scope

- Localisation and RTL. Story 05 builds on this scaffold.
- Authentication screens. Story 07 delivers them.
- Any component, unit, or end-to-end test suite.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10 modular monolith. One module per bounded context under `backend/src/Modules/<Module>/` (`Domain` <- `Application` <- `Infrastructure` / `Presentation`, plus a public `Contracts` project), Domain-Driven Design with Clean Architecture inside each module, one host (`Crm.Api`). CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server with one DbContext and schema per module, MVC controllers in per-persona Areas, `Result<T>` returns, RFC 9457 problem details. The clock is read only through `IDateTime`; `System.DateTime` is banned.
- **Module boundaries:** a module reaches another only through that module's `Contracts` - a synchronous API interface for a read, an integration event (via the outbox) for "this happened". No cross-module project references, foreign keys, joins, or shared entities; one command, one module, one transaction. The build fails with `CRM0001` on a forbidden reference. The module catalogue in `CLAUDE.md` names the module each story belongs to.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events inside the module, and as integration events across modules; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
