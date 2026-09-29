# Story 35 — Escalation and business rules engine

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **SLA and Automation** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/sla-automation/escalation-and-business-rules-engine/intake.md`.

---

## Feature

- **Feature name (display):** SLA and Automation
- **Feature slug (folder under `plans/`):** `sla-automation`
- **Module (backend):** `Automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-35`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `sla-automation`

---

## Title

```
Escalation and business rules engine
```

---

## Description

What happens when a ticket is going wrong: nobody has answered, the SLA is about to breach, the customer has
replied three times, or a VIP is waiting. This is the generic rule engine, with escalation as its first consumer.

- `AutomationRule` as an aggregate root: a localised name, a trigger, conditions, an ordered set of actions, an
  active flag, a priority, and an execution audit trail.
- Triggers: ticket created, ticket updated, status changed, SLA at risk, SLA breached, no agent response for a
  duration, no customer response for a duration, customer replied N times, reopened, priority raised, and a
  scheduled time-based sweep.
- Conditions composable with and, or, and not over ticket fields, customer tier, elapsed times, business hours,
  assignment state, and channel.
- Actions: escalate to a supervisor or a named escalation level, reassign, change priority, change status, add a
  tag, add an internal note, notify a user, team, or role, send a customer message from a template, create a task
  from story 29, and call an outbound webhook from story 56.
- Multi-level escalation ladders with a level, a target, a wait duration, and a stop condition, so a ticket walks
  from agent to supervisor to manager and stops when someone responds.
- Loop protection: a rule cannot re-trigger itself, a per-ticket execution budget per window, and a global kill
  switch. A rule engine with actions that change tickets is a loop waiting to happen.
- A simulator and dry-run mode, a per-rule execution log with the reason it matched, and a rule-testing view
  against a real ticket.
- Every automated action is attributed to its rule on the story 19 timeline, so a surprised agent can see why.

## Acceptance criteria

- [ ] A rule triggered by SLA at risk notifies the configured supervisor once, and the action is attributed to the
      rule by name on the timeline.
- [ ] Conditions combine with and, or, and not, and the simulator explains which branch matched.
- [ ] Rules execute in priority order, and a rule's actions execute in their configured order.
- [ ] A rule whose action would re-trigger itself is prevented from looping, and the prevention is logged.
- [ ] Exceeding the per-ticket execution budget within the window stops further automation on that ticket and
      raises an alert.
- [ ] The kill switch halts all rule execution immediately without a deployment.
- [ ] A multi-level ladder escalates from level to level on the configured waits and stops as soon as the stop
      condition is met.
- [ ] A no-agent-response trigger fires only during business hours when the rule is configured that way.
- [ ] Dry-run reports every action a rule would take without taking any.
- [ ] Every execution is logged with the rule, the trigger, the matched conditions, the actions taken, and the
      outcome, including failures.
- [ ] A failing action does not abort the remaining actions unless the rule is configured to stop on failure.
- [ ] Deactivating a rule stops it immediately and leaves its execution history intact.

## Dependencies

- **Blocked by / related ids:** CRM-33, CRM-34, CRM-29, CRM-36.
- **Depends on code areas or other stories:** SLA events from story 33, assignment from story 34, tasks from
  story 29, and notifications from story 36. Story 56 provides the webhook action.

## Technical hints

- Build the condition tree as a small serialisable expression model rather than storing code or a script. It has
  to be editable in the admin UI and inspectable in the execution log.
- Loop protection is not optional. A rule that raises priority on update, plus a rule that notifies on priority
  change, is enough to melt a mailbox, and no test suite will catch it here.
- Make the execution log the primary debugging surface and design it before the engine.

## Out of scope

- Assignment strategies themselves, which are story 34 and are invoked as an action here.
- AI-driven decisions. Stories 42 to 45 may propose, but a rule executes.
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
