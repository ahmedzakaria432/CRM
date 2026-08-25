# Story 15 — Ticket aggregate and lifecycle

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-aggregate-and-lifecycle/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

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
Ticket aggregate and lifecycle
```

---

## Description

The core aggregate of the whole product. Everything else in the CRM is either feeding tickets, routing them, or
reporting on them, so the invariants here carry the most weight of any story in the backlog.

- `Ticket` as an aggregate root in `Crm.Domain/Tickets`, with `TicketNumber`, subject, description, customer id,
  contact id, channel of origin, category id, priority, status, owning department and branch, assigned agent and
  team, source reference, tags, and timestamps for created, first responded, resolved, and closed.
- Child entities inside the aggregate: `TicketMessage`, `TicketNote`, `TicketStatusChange`, `TicketAssignment`,
  and `SlaClock`. None of them has a `DbSet`; all are reached through the root.
- Value objects: `TicketNumber`, `Priority`, `TicketStatus`, `ResolutionCode`, and `Reopen` with its reason.
- The status machine expressed as domain behaviour, not as an updatable field: New, Open, Pending customer,
  Pending third party, On hold, Resolved, Closed, Cancelled, plus Reopened. Legal transitions are declared once
  and enforced by the aggregate.
- Intention-revealing methods only: `Open`, `AssignTo`, `Reassign`, `PutOnHold`, `AwaitCustomer`, `Escalate`,
  `Resolve`, `Close`, `Reopen`, `Cancel`, `Merge`, `LinkAsChildOf`. There is no public status setter anywhere.
- Domain events raised by those methods: created, assigned, status changed, escalated, resolved, closed, reopened,
  merged, first response recorded. Stories 13, 31, 33, 36, and 51 subscribe to them.
- Ticket relationships: merge a duplicate into another, link parent and child for a cascading incident, and mark
  related tickets, each with its own rules about what happens to the SLA and the customer's view.

## Acceptance criteria

- [ ] A ticket cannot be created without a customer, a subject, a category, a channel, and an owning department.
- [ ] `TicketNumber` is unique and gap-tolerant under concurrent creation, sourced from a database sequence.
- [ ] An illegal status transition, for example Closed straight to Pending customer, is rejected by the aggregate
      with a domain error mapped to 422, and the ticket is unchanged.
- [ ] There is no public setter for status, priority, or assignee anywhere on the aggregate; every change goes
      through a named method, and the build has no analyzer suppression hiding one.
- [ ] Resolving a ticket requires a resolution code and stamps resolved-at; closing stamps closed-at.
- [ ] Reopening a closed ticket requires a reason, restores it to Open, records the reopen, and restarts the SLA
      clock per policy.
- [ ] Closing a parent ticket with open children is rejected with a domain error naming the children.
- [ ] Merging ticket B into ticket A moves B's messages and attachments onto A, closes B with a merged
      resolution, points B at A, and never loses a message.
- [ ] First response is stamped exactly once, on the first outbound agent message, and is never overwritten by a
      later reply.
- [ ] `TicketMessage` and the other children have no `DbSet` and cannot be saved independently.
- [ ] Every method above and every rejected transition is exercised from `backend/requests/tickets.http`.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-06, CRM-09, CRM-11.
- **Depends on code areas or other stories:** the persistence baseline from story 02, scoping from story 06,
  permissions from story 09, and the customer aggregate from story 11.

## Technical hints

- Declare the legal transitions as a static map inside the aggregate and drive every method through it. Scattering
  `if (status == ...)` across handlers is how status machines rot.
- Raise the first-response event from inside the message-adding method so it cannot be forgotten by a caller.
- This aggregate has no test suite behind it. Keep the invariants in the domain where they are visible, put every
  transition in `tickets.http`, and walk the illegal ones deliberately.

## Out of scope

- Categories, priorities, and status configuration as admin data. That is story 16.
- Assignment strategy and queues. That is story 17.
- Messages as a channel-facing feature. That is story 18.
- The UI. Those are stories 20 and 21.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10. Domain-Driven Design inside Clean Architecture (`Crm.Domain` <- `Crm.Application` <- `Crm.Infrastructure` / `Crm.Api`), CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server, minimal APIs, `Result<T>` returns, RFC 9457 problem details.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
