# Story 29 — Tasks and reminders

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Agent Workspace** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/agent-workspace/tasks-and-reminders/intake.md`.

---

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`
- **Module (backend):** `Workspace`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-29`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `agent-workspace`

---

## Title

```
Tasks and reminders
```

---

## Description

The small commitments that fall through the cracks: call back on Tuesday, chase the supplier, check whether the
fix held. Without this, agents keep them in their heads or in a personal notebook.

- `Task` as an aggregate root: title, description, owner, optional related ticket and customer, due date and time
  in the owner's timezone, priority, status (open, in progress, done, cancelled), and completion notes.
- Reminders on a task or directly on a ticket, with one or more notification times, delivered through story 36 as
  in-app, email, and optional push.
- Snooze and reschedule, with the original due date retained so chronic slippage is visible rather than hidden.
- Recurring tasks with a simple recurrence (daily, weekly, monthly, or every N days), where completing one
  instance creates the next and the series can be ended without deleting history.
- Ticket-linked follow-ups: a pending-customer ticket automatically gets a follow-up task at the configured
  interval, and resolving the ticket closes the open follow-ups rather than leaving orphans.
- Delegation: reassign a task to a colleague with a note, and reassign all open tasks when an agent goes out of
  office per story 08.
- Frontend: a today, upcoming, and overdue task list, a quick-add from any ticket, inline completion, a calendar
  month view, and a due-today badge in the shell.

## Acceptance criteria

- [ ] A task can be created standalone or from a ticket, and when created from a ticket it carries the ticket and
      customer link.
- [ ] A due date is stored in UTC and displayed in the owner's timezone, correct across a daylight-saving boundary.
- [ ] A reminder fires once at the configured time through story 36, and is not re-fired on service restart.
- [ ] Snoozing a task retains the original due date, and overdue counts reflect the original.
- [ ] Completing a recurring task creates exactly one next instance, and ending the series stops creation without
      deleting completed instances.
- [ ] Resolving a ticket closes its open follow-up tasks and records that it did so.
- [ ] Reassigning a task notifies the new owner and keeps the delegation visible in its history.
- [ ] Setting an agent out of office reassigns their open tasks to the delegate per story 08.
- [ ] Overdue tasks are visually distinct and sort first, and the shell badge count matches the list.
- [ ] A task is visible only to its owner, the delegating agent, and holders of the team-view permission.
- [ ] The task list and calendar render correctly in `ar` RTL, with an Arabic-locale calendar.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-08, CRM-36.
- **Depends on code areas or other stories:** the ticket aggregate from story 15, agent profiles and out-of-office
  from story 08, and notification delivery from story 36. Story 28 surfaces tasks on the dashboard.

## Technical hints

- Enforce fire-once for reminders with a persisted sent-at stamp checked inside the transaction. A scheduler that
  re-fires on restart is the usual failure here and there is no test to catch it.
- Keep recurrence simple. A full RRULE implementation is not what this product needs and it will not be exercised.
- Store the owner's timezone on the task at creation, so a later profile change does not silently move historical
  due dates.

## Out of scope

- A full calendar or meeting-scheduling product, and external calendar sync.
- SLA timers, which are story 33 and are a different mechanism from reminders.
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
