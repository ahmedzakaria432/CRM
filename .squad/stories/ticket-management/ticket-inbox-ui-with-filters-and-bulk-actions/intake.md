# Story 20 — Ticket inbox UI with filters and bulk actions

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
Ticket inbox UI with filters and bulk actions
```

---

## Description

Where agents spend their day. Speed and information density matter more here than anywhere else in the product,
and it must be equally usable in Arabic RTL.

- A server-side paged, sorted, filtered ticket list with a density toggle, column chooser persisted per user, and
  a preview pane option.
- Filters for status, priority, category, assignee, team, department, branch, channel, tag, SLA state, date
  ranges, and custom fields, combinable and reflected in the URL.
- Built-in views from story 17's queues (my tickets, unassigned, my team, escalated, breaching soon, unanswered)
  plus user-defined saved views with a name, an order, and optional sharing to a team.
- Full-text search across subject, description, message bodies, customer name, and ticket number, with results
  ranked and highlighted, working in both Arabic and English.
- Bulk actions with a per-ticket result summary: assign, change status, change priority, add tag, and add a note,
  never aborting the whole batch on one rejection.
- Row-level SLA indicators: remaining time, at-risk, and breached, updating live without a full refresh.
- Keyboard-first operation: navigate rows, open, assign, change status, and move between views without a mouse.
- Real-time updates so a ticket claimed by a colleague changes state in place rather than after a manual refresh.

## Acceptance criteria

- [ ] Paging, sorting, and filtering all happen server-side; the client never filters a partial page.
- [ ] Every filter combination is encoded in the URL, survives a refresh, and is shareable with a colleague who
      has the same scope.
- [ ] Saved views persist per user, can be reordered, and can be shared to a team where permitted.
- [ ] Search returns ranked, highlighted matches for both an Arabic and an English query, including a match found
      only inside a message body.
- [ ] A bulk action on 100 tickets reports per-ticket success and failure, and one rejected ticket does not abort
      the rest.
- [ ] SLA countdown indicators update live and turn at-risk and then breached at the configured thresholds.
- [ ] A ticket updated by another agent updates in place within seconds, without the user losing scroll position
      or selection.
- [ ] The full inbox is operable by keyboard alone, with a discoverable shortcut list.
- [ ] The list renders 50 rows with no layout shift, and a cold load completes acceptably on a seeded set of
      50,000 tickets.
- [ ] The inbox is walked in `ar` RTL at 375px, 768px, and 1280px, with the preview pane on the correct side.
- [ ] Loading, empty (per view), error, and no-results-for-filter states are all distinct and actionable.

## Dependencies

- **Blocked by / related ids:** CRM-04, CRM-05, CRM-15, CRM-16, CRM-17.
- **Depends on code areas or other stories:** the UI kit from story 04, localisation from story 05, and the
  ticket, taxonomy, and assignment stories 15 to 17. Real-time transport is shared with story 26.

## Technical hints

- Keep all filter state in the URL query string. Saved views, sharing, and the back button then work for free.
- Virtualise the row list rather than raising the page size. Density is the point of this screen.
- Seed 50,000 tickets before judging performance. Without a test suite, a bad query plan here will be found by an
  agent on a Monday morning.

## Out of scope

- The ticket detail screen. That is story 21.
- The agent dashboard and its widgets. That is story 28.
- Any test project.


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
