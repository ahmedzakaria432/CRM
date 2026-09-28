# Story 13 — Interaction history and unified customer timeline

Story intake for the **Customer Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-management/interaction-history-and-unified-customer-timeline/intake.md`.

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`
- **Module (backend):** `Customers`, fed by Tickets and Communication integration events

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-13`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-management`

---

## Title

```
Interaction history and unified customer timeline
```

---

## Description

A single chronological record of every touchpoint with a customer, assembled from every channel and every module.
This is the read model that makes the customer 360 page possible without querying eight tables from the UI.

- An `Interaction` record capturing type (ticket created, message received, message sent, call, note added, SLA
  breached, satisfaction submitted, portal login, field changed), channel, direction, actor, customer id,
  related ticket id, a localised summary, and the occurred-at timestamp.
- Interactions are written by subscribing to domain events rather than by callers remembering to log them. A new
  feature that raises the right domain event appears on the timeline for free.
- A denormalised read model optimised for reverse-chronological paging by customer, with a covering index. It is
  a projection, not an aggregate, and is rebuilt by a replay job rather than repaired by hand.
- Cursor-based paging, because a timeline that grows while the user reads it will duplicate or skip rows under
  offset paging.
- Filtering by type, channel, date range, and related ticket, plus a full-text search across summaries.
- Aggregate counters per customer: total tickets, open tickets, last contacted at, average satisfaction, and
  first-contact date, kept current by the same projection.
- A replay command that rebuilds the projection for one customer or for all, so a projection bug is recoverable
  without a data migration.

## Acceptance criteria

- [ ] Creating a ticket, receiving and sending a message, adding a note, breaching an SLA, and submitting a
      satisfaction score each append exactly one interaction, with no explicit logging call in the handler.
- [ ] The timeline returns newest-first and pages with a cursor; adding a new interaction mid-read never causes a
      duplicate or a skipped row.
- [ ] Filtering by type, channel, date range, and related ticket each narrow the result correctly, and the
      filters combine.
- [ ] Summaries render in the reader's locale, in both `en` and `ar`, without a second round trip.
- [ ] Customer counters match a direct count computed from source tables, verified for a seeded customer with a
      mixed history.
- [ ] Replaying the projection for a customer produces byte-identical output to the incrementally built version.
- [ ] The timeline loads within an acceptable time for a customer with 10,000 interactions on a seeded data set.
- [ ] Interactions inherit the customer's scope: an agent outside the customer's department sees nothing.
- [ ] Merging two customers in story 11 merges their timelines in correct chronological order.

## Dependencies

- **Blocked by / related ids:** CRM-11, CRM-02.
- **Depends on code areas or other stories:** domain-event dispatch from story 02 and the customer aggregate from
  story 11. Stories 15, 22, 33, and 50 raise events that this projection consumes.

## Technical hints

- Subscribe to domain events; never make interaction logging the caller's responsibility, or coverage will decay
  the moment a story is written in a hurry.
- Store the summary as a template key plus JSON parameters, not as pre-rendered text, or the timeline will be
  stuck in whichever language wrote it.
- Seed 10,000 interactions locally before judging the query plan. With no test suite, a missing index will
  otherwise be discovered by a user.

## Out of scope

- The timeline UI itself. That is story 14.
- Ticket-internal history and field-level audit, which is story 19.
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
