# Story 51 — Reporting foundation and read models

Story intake for the **Reports and Analytics** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/reports-analytics/reporting-foundation-and-read-models/intake.md`.

---

## Feature

- **Feature name (display):** Reports and Analytics
- **Feature slug (folder under `plans/`):** `reports-analytics`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-51`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `reports-analytics`

---

## Title

```
Reporting foundation and read models
```

---

## Description

The query layer every report and dashboard sits on. Reporting against the operational tables will either be slow
or will lock the agents' inbox, so the read side gets its own shape.

- Denormalised reporting read models built as projections from domain events: a ticket fact table with its
  dimensions resolved, an SLA outcome table, an agent activity table, a satisfaction table, and a channel volume
  table.
- Pre-aggregated daily and hourly rollups per department, branch, agent, category, priority, and channel, so the
  common dashboard query reads a small table rather than scanning millions of rows.
- Explicit metric definitions, written down once and used everywhere: first response time, resolution time,
  handling time, backlog, reopen rate, first-contact resolution, SLA compliance, deflection, and satisfaction.
  Each states its clock, its exclusions (paused time, out-of-hours), and its attribution rule.
- A shared query layer taking a single filter contract (date range, department, branch, team, agent, category,
  priority, channel, customer tier) so every report filters identically and no two reports disagree.
- Timezone and calendar handling: reports render in the caller's timezone while the underlying data stays UTC, and
  a business-day report uses the branch calendar from story 06.
- Incremental projection maintenance with a bounded lag, a documented freshness indicator shown on every report,
  and a rebuild command for one day, one department, or everything.
- Scope enforcement in the reporting layer identical to the operational one: an agent's report never includes a
  department they cannot see.
- Row-count and drill-through support, so every aggregate number can be expanded to the underlying tickets. A
  number nobody can drill into will not be trusted.

## Acceptance criteria

- [ ] Every metric has a written definition naming its clock, exclusions, and attribution rule, and every report
      uses that one definition.
- [ ] The reporting projection reproduces the same figures as a direct query against the operational tables for a
      seeded month, verified per metric.
- [ ] Rebuilding a projection produces identical results to the incrementally maintained version.
- [ ] A dashboard query over a seeded 500,000-ticket data set returns within the agreed budget.
- [ ] Reporting queries do not block or slow the ticket inbox under concurrent load.
- [ ] Every report exposes its data freshness, and the value is accurate.
- [ ] Filters behave identically across every report; the same filter set yields the same ticket population.
- [ ] Reports render in the caller's timezone, and a business-day report respects the branch calendar.
- [ ] An agent's report contains no data from outside their scope, verified by comparing two users' output.
- [ ] Every aggregate number drills through to the underlying ticket list, and the drill-through count matches the
      aggregate.
- [ ] Paused and out-of-hours time is excluded exactly as story 33 excludes it, so SLA figures agree with the
      ticket header.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-19, CRM-33, CRM-50.
- **Depends on code areas or other stories:** ticket domain events from story 15, history from story 19, SLA
  outcomes from story 33, and satisfaction from story 50. Stories 52 to 54 are built on this layer.

## Technical hints

- Agree the metric definitions with the business before building. Every reporting dispute is a definition dispute,
  and rework here is expensive.
- Verify the projection against a direct operational query for a seeded month. With no test suite, this
  reconciliation is the only proof the numbers are right, so script it as a repeatable command.
- Make drill-through a requirement of every metric from the start. It is also the fastest way to find a projection
  bug.

## Out of scope

- Individual reports and dashboards. Those are stories 52 to 54.
- A data warehouse or external BI integration beyond a documented export.
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
