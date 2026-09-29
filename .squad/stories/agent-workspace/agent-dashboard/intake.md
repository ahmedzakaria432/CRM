# Story 28 — Agent dashboard

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Agent Workspace** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/agent-workspace/agent-dashboard/intake.md`.

---

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`
- **Module (backend):** `Workspace` (new module); composes other modules' Contracts reads

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-28`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `agent-workspace`

---

## Title

```
Agent dashboard
```

---

## Description

The screen an agent lands on. It answers one question in the first two seconds: what do I work on next.

- A priority work list: tickets assigned to me, ordered by SLA urgency rather than by date, with the at-risk and
  breached ones surfaced first and a one-click start action.
- Widgets: my open tickets by status, breaching within the hour, unanswered by me for longer than a threshold,
  unassigned in my departments, my tasks and reminders due today from story 29, and mentions awaiting my reply
  from story 31.
- Personal performance for the current period: tickets resolved, average first response, average resolution, and
  satisfaction score, shown as the agent's own numbers only, never as a leaderboard against colleagues.
- A supervisor variant of the same screen, gated on permission: team workload by agent, unassigned queue depth,
  breaches today, and the oldest untouched ticket, with drill-through into a filtered inbox view.
- Live updates through story 26's SignalR transport, so a claimed ticket disappears from the unassigned widget
  without a refresh.
- Layout personalisation: widgets can be reordered, hidden, and resized, persisted per user, with a reset action.
- Every widget links into story 20's inbox with the equivalent filter applied, rather than being a dead end.

## Acceptance criteria

- [ ] The work list orders by SLA urgency, not creation date, and its first item is genuinely the most urgent
      ticket assigned to the agent.
- [ ] Every widget respects the caller's scope and the active department selection from story 06.
- [ ] Every widget links into the inbox with a filter that reproduces exactly the same set of tickets it counted.
- [ ] Widget counts match the inbox count for the equivalent filter, verified on a seeded data set.
- [ ] A ticket claimed by a colleague leaves the unassigned widget within seconds without a manual refresh.
- [ ] The supervisor variant is visible only with the supervisor permission and shows no cross-department data
      outside the caller's scope.
- [ ] Personal performance figures show only the signed-in agent's own numbers.
- [ ] Widget layout changes persist per user across sessions, and reset restores the default.
- [ ] Each widget loads independently; one failing widget shows an error state with retry inside itself and does
      not blank the dashboard.
- [ ] The dashboard renders correctly in `ar` RTL at 375px, 768px, and 1280px.
- [ ] Loading skeletons, empty states with a next action, and error states exist for every widget.

## Dependencies

- **Blocked by / related ids:** CRM-17, CRM-20, CRM-26, CRM-29, CRM-31.
- **Depends on code areas or other stories:** queues from story 17, the inbox from story 20, the realtime
  transport from story 26, tasks from story 29, and mentions from story 31. SLA state comes from story 33.

## Technical hints

- Compute each widget with its own query rather than one wide join. Independent panels are the point, and one slow
  widget must not take the page with it.
- Reuse story 17's queue specifications so a widget and its inbox link cannot drift apart.
- Deliberately avoid a cross-agent leaderboard. Story 53 serves management reporting; this screen serves the agent.

## Out of scope

- Management dashboards and cross-team analytics. Those are stories 53 and 54.
- SLA calculation itself. That is story 33.
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
