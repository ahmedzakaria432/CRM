# Story 15 — Ticket aggregate lifecycle taxonomy and assignment

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-aggregate-and-lifecycle/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** `Tickets` (new module); checks the customer through `Customers.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-15`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket aggregate lifecycle taxonomy and assignment
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs stories 16 (taxonomy) and 17
(assignment)** in reduced form: categories and fixed priorities, manual assignment and claim, and queue queries.
SLA, merge/link, custom fields, and bulk operations are deferred.

---

## Description

The core aggregate of the product.

- The new `Tickets` module (`Crm.Modules.Tickets.*`, schema `tickets`). `Ticket` aggregate root:
  `TicketNumber` (e.g. `T-000123`), subject, description, customer id and contact id (checked through
  `Customers.Contracts`), channel of origin (`Agent`, `Portal`), category id, `Priority` (Low, Normal, High,
  Urgent), status, owning branch and department (`IOrganisationScoped`), assigned agent id, and timestamps for
  created, first responded, resolved, closed.
- The status machine as domain behaviour: New, Open, Pending customer, On hold, Resolved, Closed, Cancelled, with
  Reopen. Legal transitions are declared once in the aggregate; there is no public status setter.
- Intention-revealing methods: `Open`, `AssignTo`, `Claim`, `Unassign`, `TransferTo(department)`, `ChangePriority`,
  `ChangeCategory`, `AwaitCustomer`, `PutOnHold`, `Resolve`, `Close`, `Reopen`, `Cancel`. Each raises a domain event
  and appends a `TicketActivity` child entry (actor, kind, from, to, timestamp) that story 18 renders.
- `Category` aggregate: `LocalizedText` name (`ar` + `en`), owning department, active flag, display order,
  customer-visible flag (the portal in 47 shows only these). Admin CRUD in the Admin Area.
- Claim is safe under concurrency: two agents claiming the same ticket produce one winner and a localised
  "already claimed by" 409 for the other (rowversion).
- Transfer to another department moves the ticket to the receiving department's scope and keeps its history.
- Queue queries (projections, not tables): my tickets, unassigned in my departments, all in scope; plus a paged
  search with filters (status, priority, category, assignee, department, branch, customer) and text search on
  number and subject.
- Integration events in `Tickets.Contracts` published through the story 03 outbox: `TicketCreated`,
  `TicketStatusChanged`, `TicketAssigned` (no subscribers yet in the MVP; they are the contract for later stories).
- Permissions: `tickets.read`, `tickets.write`, `tickets.assign`.
- `backend/requests/tickets.http` covering every transition, every rejected transition, claim contention, and
  transfer.

## Acceptance criteria

- [ ] Every legal transition succeeds and every illegal one is rejected by the aggregate with 422 `domain-rule`
      and a localised message naming the current status.
- [ ] No public status setter exists; `TicketActivity` has no `DbSet` and is reached only through `Ticket`.
- [ ] Creating a ticket for a customer outside the caller's scope, or for a nonexistent customer, is rejected.
- [ ] Two concurrent claims produce exactly one assignee; the loser gets a 409 naming the winner.
- [ ] Transfer moves the ticket out of the sender's scope and into the receiver's, with the transfer in the activity.
- [ ] Every transition appends exactly one `TicketActivity` entry and writes one outbox row.
- [ ] Queue queries and search page, sort, and filter server-side and are scoped (predicates visible in the SQL).
- [ ] Category names render in the active locale; a deactivated category disappears from pickers without breaking
      existing tickets.
- [ ] Every path exercised from `backend/requests/tickets.http`.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-07, CRM-11.
- **Depends on code areas or other stories:** scoping (06), permissions (07), `ICustomersModuleApi` (11), the
  outbox (03).

## Technical hints

- Declare the transition table once as data inside the aggregate; derive "allowed actions" from it so story 20's UI
  offers only legal transitions (expose them on the ticket DTO).
- Activity entries store a template key plus parameters, never pre-rendered text, so they read correctly in both
  languages.

## Out of scope (deferred post-MVP)

- SLA clock (32/33), merge and parent/child links, ticket types, custom fields, tags, resolution codes, category
  hierarchy, team assignment, workload/load balancing, bulk assign, auto-routing (34). Any test project.

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
