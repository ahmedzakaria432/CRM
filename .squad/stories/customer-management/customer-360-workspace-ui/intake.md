# Story 14 — Customer 360 workspace UI

Story intake for the **Customer Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-management/customer-360-workspace-ui/intake.md`.

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`
- **Module (backend):** none (frontend); reads `Customers`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-14`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-management`

---

## Title

```
Customer 360 workspace UI
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs story 13 in minimal form**: the 360
page shows contacts, notes, and (from story 20) the customer's tickets instead of a full interaction projection.

---

## Description

The agent-facing screens for customers: find one fast, then see everything about them on one page.

- Customer list (`/agent/customers`): server-side paged table with a debounced search box, filters for status,
  type, department, and branch, sortable columns, filters reflected in the URL.
- Customer detail (`/agent/customers/[id]`): a header (name, customer number, type, status, owning department), a
  contacts panel with add/edit/set-primary/remove, and a notes panel with add/pin/remove. Panels load independently.
- A "Tickets" panel placeholder with an empty state; story 20 fills it with the customer's tickets.
- Create and edit as a drawer form; 400 and 422 problem-details land on the right field through `useFormErrors()`.
- The duplicate warning from story 11 shown before create, with "create anyway".
- A reusable `CustomerPicker` component with async search, keyboard operable, used by ticket creation in story 20.
- Every panel has loading, empty, and error states with retry. All copy is `en` + `ar` keys.
- Mobile friendly: the list becomes stacked cards below 768px; the detail panels stack vertically.

## Acceptance criteria

- [ ] The list pages, sorts, and filters server-side; no client-side filtering of a partial page.
- [ ] Search is debounced, cancels in-flight requests, and never renders results from a stale keystroke.
- [ ] A failing panel shows an error state with retry inside that panel; the rest of the page stays usable.
- [ ] A 422 domain error lands on the specific field in the active locale.
- [ ] The duplicate warning appears before the record is created and offers "create anyway".
- [ ] `CustomerPicker` searches asynchronously and is keyboard and screen-reader operable.
- [ ] Every panel has a loading skeleton, an empty state with a next action, and an error state with retry.
- [ ] Walked in `en` and `ar` at 375px, 768px, and 1280px; no physical-direction utility in the diff.

## Dependencies

- **Blocked by / related ids:** CRM-05, CRM-07, CRM-11.
- **Depends on code areas or other stories:** the UI kit and `useApi` from story 04, i18n from story 05, auth and
  `useCan()` from story 07, the customer API from story 11 (regenerate with `pnpm gen:api`).

## Technical hints

- One composable per use case in `app/composables/customers/`.
- Hide write actions without `customers.write` via `useCan()`; the API still enforces it.

## Out of scope (deferred post-MVP)

- Interaction timeline and counters (story 13), attachments panel, merge UI, saved filter presets, CSV export,
  quick actions to email/WhatsApp/call, history tab (story 10). Any test suite.

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
