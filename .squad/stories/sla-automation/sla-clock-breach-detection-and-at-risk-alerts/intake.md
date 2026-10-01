# Story 33 — SLA clock breach detection and at risk alerts

Story intake for the **SLA and Automation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/sla-automation/sla-clock-breach-detection-and-at-risk-alerts/intake.md`.

---

## Feature

- **Feature name (display):** SLA and Automation
- **Feature slug (folder under `plans/`):** `sla-automation`
- **Module (backend):** `Sla` + `Tickets` (the `SlaClock` lives inside the `Ticket` aggregate; targets come from `Sla.Contracts`)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-33`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `sla-automation`

---

## Title

```
SLA clock breach detection and at risk alerts
```

---

## Description

The engine that turns policies into live countdowns and fires when a promise is about to be broken. It has to be
right after a restart, right across a timezone boundary, and right when a ticket has been paused four times.

- `SlaClock` as a child entity of the ticket aggregate, one per active target: the resolved target, started-at,
  due-at, accumulated paused duration, the current state (running, paused, met, breached), and the satisfied-at
  stamp.
- Clock lifecycle driven by domain events from story 15: created starts the first-response and resolution clocks,
  the first outbound reply satisfies first response, an inbound customer reply starts a next-response clock, a
  pausing status pauses, a resuming status resumes, resolution stops the resolution clock, and a reopen applies
  the policy's configured reopen behaviour.
- A background worker that evaluates due clocks on a schedule, marks at-risk at the warning threshold and
  breached at the due time, and raises the corresponding domain events for stories 35 and 36.
- Restart safety: state is derived from persisted timestamps, never from in-memory timers, so a redeploy neither
  double-fires nor misses a breach.
- Idempotent firing: at-risk and breach events fire exactly once per clock, guaranteed by a persisted stamp
  checked inside the transaction.
- Catch-up on restart: clocks whose due time passed while the service was down are evaluated on startup and fire
  once with the correct historical timestamp.
- Live remaining time exposed to the API as an absolute due timestamp plus a paused duration, so the UI can count
  down locally without polling and without clock drift.
- Recalculation when a ticket's priority, category, or department changes mid-life, per an explicit configured
  rule rather than an accident.

## Acceptance criteria

- [ ] Creating a ticket starts first-response and resolution clocks whose due times match the story 32 simulator.
- [ ] The first outbound public reply satisfies the first-response clock exactly once; a later reply does not
      re-satisfy or reopen it.
- [ ] Moving a ticket to pending customer pauses the clock, and returning to open resumes it with paused time
      excluded from the elapsed total.
- [ ] Pausing and resuming three times accumulates paused duration correctly, verified by hand against a seeded
      ticket.
- [ ] At-risk fires once at the configured threshold and breach fires once at the due time, never twice.
- [ ] Restarting the service does not re-fire an already-fired at-risk or breach event.
- [ ] A clock whose due time passed while the service was stopped fires once on startup with the correct
      historical timestamp.
- [ ] A resolved ticket's clocks stop and report met or breached correctly, and reopening applies the policy's
      configured reopen behaviour.
- [ ] Changing a ticket's priority mid-life recalculates or retains targets per the configured rule, and the
      choice is recorded on the timeline.
- [ ] The API returns an absolute due timestamp and paused duration, and the UI countdown matches the server's
      breach decision to within a second.
- [ ] Breach state is consistent between the inbox, the ticket header, the dashboard, and the reports for the
      same ticket.

## Dependencies

- **Blocked by / related ids:** CRM-32, CRM-15, CRM-19.
- **Depends on code areas or other stories:** SLA policies from story 32, the ticket aggregate and its domain
  events from story 15, and the history timeline from story 19. Stories 35 and 36 consume the events raised here.

## Technical hints

- Derive everything from persisted timestamps. An in-memory timer is the classic way this feature breaks on a
  Friday deploy.
- Make the fired stamp part of the same transaction as the event dispatch, or a retry will double-notify a
  supervisor.
- Return an absolute due time rather than a remaining duration. Remaining time computed server-side is stale
  before it reaches the browser.

## Out of scope

- What happens on breach beyond raising the event. Escalation is story 35 and notification is story 36.
- SLA performance reporting. That is story 52.
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
