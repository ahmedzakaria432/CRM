# Story 20 — Ticket inbox and ticket detail workspace UI

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-inbox-ui-with-filters-and-bulk-actions/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** none (frontend); reads `Tickets`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-20`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket inbox and ticket detail workspace UI
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs story 21 (ticket detail)**. Bulk
actions, realtime updates, saved views, and keyboard shortcuts are deferred.

---

## Description

Where agents spend their day: finding tickets and working them, equally usable in Arabic RTL and on a phone.

- Inbox (`/agent/tickets`): server-side paged, sorted, filtered list; built-in views **My tickets**, **Unassigned**,
  **All in scope** (story 15 queues); filters for status, priority, category, assignee, department, branch, and
  customer; text search on number and subject; filters and view in the URL; a status and priority badge per row.
- New ticket drawer: `CustomerPicker` (story 14) → contact → category → subject, description, priority.
- Ticket detail (`/agent/tickets/[id]`): a sticky header (number, subject, status, priority, assignee, primary
  actions); the thread from story 18 in the centre with public messages, internal notes, and activity entries
  visually distinct; a properties side panel (status actions, priority, category, assignee, department transfer);
  a customer context panel (name, contact, link to the customer 360 page). Side panels collapse into tabs below
  1024px and mirror correctly in RTL.
- The action bar offers only the transitions the ticket DTO says are allowed and the caller's permissions allow;
  a 422 from the aggregate shows its localised explanation inline.
- Composer with **Reply** and **Internal note** tabs.
- Claim / assign-to-me, assign to another agent (picker of agents in the ticket's department), and transfer.
- The customer 360 page (story 14) gets its **Tickets** panel filled with that customer's tickets.
- Every view has loading, empty, and error states. All copy is `en` + `ar`.

## Acceptance criteria

- [ ] The inbox pages, sorts, and filters server-side; the URL restores the exact view on refresh.
- [ ] Only legal, permitted actions are offered; an illegal action invoked anyway shows the aggregate's localised
      reason, not a generic error.
- [ ] A claim lost to a colleague shows "already claimed by <name>" and refreshes the ticket.
- [ ] Reply and internal note append to the thread without a full reload; notes are visibly internal.
- [ ] Creating a ticket from the drawer lands on its detail page; validation errors land on the fields.
- [ ] The customer 360 Tickets panel lists that customer's tickets with links.
- [ ] Walked in `en` and `ar` at 375px, 768px, and 1280px; the detail layout mirrors correctly in RTL.
- [ ] Every view has a loading skeleton, an empty state with a next action, and an error state with retry.

## Dependencies

- **Blocked by / related ids:** CRM-14, CRM-15, CRM-18.
- **Depends on code areas or other stories:** `CustomerPicker` (14), the ticket API (15) and thread API (18) —
  regenerate the client with `pnpm gen:api`.

## Technical hints

- One composable per use case under `app/composables/tickets/`.
- Status/priority badges map to design tokens, never literal colours.

## Out of scope (deferred post-MVP)

- Bulk actions, realtime updates, saved and shared views, column chooser, preview pane, SLA indicators, keyboard
  shortcuts, collision awareness, quick replies, AI suggestions, attachments, related tickets. Any test suite.

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
