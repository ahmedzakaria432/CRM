# Story 17 — Ticket assignment ownership and queues

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-assignment-ownership-and-queues/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** `Tickets`; reads agents and teams through `Identity.Contracts` / `Organisation.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-17`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket assignment ownership and queues
```

---

## Description

Getting a ticket to the right person, and making the current owner unambiguous at every moment. Manual and
supervisor-driven assignment lives here; the rules engine that automates it is story 34.

- Assignment as behaviour on the ticket aggregate: assign to an agent, assign to a team, claim from a queue,
  reassign with a reason, unassign back to the queue, and transfer to another department, each recording a
  `TicketAssignment` child entry with actor, from, to, reason, and timestamp.
- Queues as a query concept over the ticket aggregate rather than a physical table: unassigned in my departments,
  my team's tickets, my tickets, watched tickets, escalated, breaching soon, and unanswered by an agent.
- A claim operation that is safe under concurrency: two agents clicking claim on the same ticket produce one
  winner and one clear, localised "already claimed by" message.
- Workload visibility: open assigned count per agent, weighted by priority, against the maximum concurrent load
  from story 08, exposed for the supervisor's assignment picker and consumed by story 34.
- Cross-department transfer: the ticket leaves the sending department's scope and enters the receiving one,
  keeping full history, with the SLA behaviour on transfer defined explicitly rather than left implicit.
- Bulk assignment and bulk reassignment for supervisors, applied per ticket so one rejected ticket does not fail
  the batch, with a per-ticket result summary.
- Reassignment on agent deactivation or out-of-office from story 08, routing to the delegate.

## Acceptance criteria

- [ ] Assigning, reassigning, unassigning, claiming, and transferring each append an assignment record with
      actor, from, to, reason, and timestamp, and raise a domain event.
- [ ] Two concurrent claims on the same unassigned ticket result in exactly one assignment; the loser receives a
      localised already-claimed message, not a 500 and not a silent overwrite.
- [ ] Reassignment requires a reason when the ticket was already assigned, and the reason appears on the timeline.
- [ ] An agent cannot assign a ticket to someone outside its owning department unless the transfer path is used.
- [ ] Transferring a ticket to another department removes it from the sending department's queues and makes it
      visible in the receiving department's, with history intact.
- [ ] Assigning past an agent's maximum concurrent load is either blocked or warned about per configuration, and
      the choice is explicit rather than accidental.
- [ ] Deactivating an agent with open tickets reassigns them per the configured fallback and reports what moved.
- [ ] Setting an agent out of office routes new assignments to the delegate without touching existing ones.
- [ ] Every queue is scoped: an agent never sees a ticket outside their departments and branches in any queue.
- [ ] Bulk reassignment of 100 tickets reports per-ticket success or failure and does not abort on the first
      rejection.
- [ ] Every operation is exercised from `backend/requests/tickets-assignment.http`, including the concurrent
      claim path.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-08, CRM-06.
- **Depends on code areas or other stories:** the ticket aggregate from story 15, agent load and availability
  from story 08, and scoping from story 06. Story 34 automates what this story does by hand.

## Technical hints

- Use the aggregate's rowversion for the claim race. Optimistic concurrency turning into a 409 that the UI
  translates is the correct answer here, not a lock.
- Model queues as specifications over the ticket aggregate so story 20 can compose them with user filters.
- Decide and document what happens to the SLA clock on cross-department transfer. Leaving it implicit will make
  story 33 and the reports in story 52 disagree.

## Out of scope

- Automatic assignment rules and round-robin. That is story 34.
- Escalation rules. That is story 35.
- The inbox UI. That is story 20.
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
