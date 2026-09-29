# Story 32 — SLA policies response and resolution targets

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **SLA and Automation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/sla-automation/sla-policies-response-and-resolution-targets/intake.md`.

---

## Feature

- **Feature name (display):** SLA and Automation
- **Feature slug (folder under `plans/`):** `sla-automation`
- **Module (backend):** `Sla` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-32`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `sla-automation`

---

## Title

```
SLA policies response and resolution targets
```

---

## Description

The definitions behind every promise the business makes about how fast it will answer. This story models and
administers them; story 33 runs the clock.

- `SlaPolicy` as an aggregate root: a localised name, an owning department or organisation-wide scope, an active
  flag, an effective-from date, and an ordered set of targets.
- `SlaTarget` as a value object inside the policy: the metric (first response, next response, resolution, or
  update frequency), the priority it applies to, the duration, and whether it is measured in business hours or
  calendar hours.
- Policy matching by specificity, resolved once and explained: category, then priority, then channel, then
  customer tier, then department, with an explicit and inspectable precedence rather than a first-match accident.
- Business-hours calculation using the per-branch calendars and IANA timezone from story 06, including holidays,
  half days, and Ramadan hours, so a Thursday evening ticket is not judged against Friday.
- Pause rules declared on the policy: which statuses stop the clock (pending customer, pending third party, on
  hold) and whether a reopen restarts, resumes, or starts a fresh clock.
- Warning thresholds per target, expressed as a percentage of elapsed time, driving the at-risk state used in
  stories 20, 28, and 36.
- Customer-tier overrides so a premium tier gets tighter targets without duplicating the whole policy.
- Admin screens: policy list, a target matrix editor, a simulator that answers "what targets would this ticket
  get and why", and a versioned change history.

## Acceptance criteria

- [ ] A policy can be created with different targets per priority for first response, next response, and
      resolution, each in business or calendar hours.
- [ ] Policy matching is deterministic, and the simulator explains which rule matched and which were skipped for a
      given ticket.
- [ ] A target duration of zero or negative is rejected at value-object construction.
- [ ] Business-hours targets skip non-working hours, weekends, and configured holidays for the ticket's branch.
- [ ] A ticket created at 4pm Thursday with a four-business-hour target is due at the correct time on the next
      working day, verified by hand against a configured calendar.
- [ ] Two branches in different timezones running the same policy produce different absolute due times.
- [ ] Customer-tier overrides apply and are visible in the simulator's explanation.
- [ ] Changing a policy does not retroactively change targets already assigned to open tickets; the version in
      force at assignment is retained.
- [ ] Deactivating a policy leaves existing tickets governed by it and stops new assignments.
- [ ] Warning thresholds are configurable per target and drive the at-risk state consistently.
- [ ] The simulator is exercised for a matrix of category, priority, channel, and tier from
      `backend/requests/sla.http`.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-15, CRM-16.
- **Depends on code areas or other stories:** branch calendars from story 06, the ticket aggregate from story 15,
  and taxonomy from story 16. Story 33 executes what this story defines.

## Technical hints

- Snapshot the resolved targets onto the ticket at assignment. Recomputing them later against a changed policy is
  how SLA reports become indefensible.
- Put business-hours arithmetic in one value object with no dependencies and exercise it by hand across a
  daylight-saving change, a holiday, and a half day. It is the highest-risk arithmetic in the product and there is
  no test suite behind it.
- Build the simulator early. It is the only practical way to verify matching precedence without tests.

## Out of scope

- Running the clock, breach detection, and escalation. Those are stories 33 and 35.
- SLA reporting. That is story 52.
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
