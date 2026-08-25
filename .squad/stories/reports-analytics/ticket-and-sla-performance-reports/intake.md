# Story 52 — Ticket and SLA performance reports

Story intake for the **Reports and Analytics** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/reports-analytics/ticket-and-sla-performance-reports/intake.md`.

---

## Feature

- **Feature name (display):** Reports and Analytics
- **Feature slug (folder under `plans/`):** `reports-analytics`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-52`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `reports-analytics`

---

## Title

```
Ticket and SLA performance reports
```

---

## Description

The operational reporting a support manager runs weekly: what came in, what went out, what is stuck, and which
promises were kept.

- Volume reporting: tickets created, resolved, closed, reopened, and backlog over time, broken by department,
  branch, category, priority, channel, and team, with period-over-period comparison.
- Backlog and ageing: open tickets by age band, the oldest untouched ticket, tickets with no agent response, and
  backlog trend, since a healthy weekly volume can still hide a growing tail.
- SLA compliance: first response and resolution compliance rates, breach counts and reasons, at-risk counts, time
  to breach distribution, and compliance by every dimension, all consistent with story 33.
- Distribution reporting rather than averages alone: median and 90th percentile response and resolution times
  alongside the mean, because an average hides the outliers a customer actually experiences.
- Reopen and first-contact-resolution analysis, with reopen reasons and the categories that reopen most.
- Channel reporting: volume, response time, resolution time, and satisfaction per channel, to inform where to
  invest.
- Report delivery: on-screen with drill-through, scheduled email delivery on a cron with a recipient list, and
  export to Excel, CSV, and PDF with the filters and generation time stamped on the output.
- A saved-report concept: a named filter and column set, private or shared to a team, so a manager's weekly report
  is one click rather than fifteen.

## Acceptance criteria

- [ ] Volume, backlog, ageing, and SLA reports each render for a chosen period with all filters applied.
- [ ] Every number drills through to the underlying ticket list, and the count matches.
- [ ] SLA compliance figures agree with the per-ticket SLA state from story 33 for a hand-checked sample.
- [ ] Median and 90th percentile are reported alongside the mean for every duration metric.
- [ ] Period-over-period comparison uses equivalent-length periods and states the comparison range explicitly.
- [ ] Reopen rate and first-contact resolution match a hand calculation on a seeded data set.
- [ ] A scheduled report is delivered on its schedule to its recipient list, with the filters applied and stamped
      on the output.
- [ ] Excel, CSV, and PDF exports contain the same figures as the screen, with the filters and generation
      timestamp included.
- [ ] Arabic exports render correctly RTL in Excel and PDF, including Arabic column headers.
- [ ] Saved reports persist, can be shared to a team, and reproduce their figures exactly when re-run.
- [ ] A report over a seeded 500,000-ticket set returns within the agreed budget.
- [ ] An agent-role user sees only their scope's figures, and a scheduled report respects the scope of its owner.

## Dependencies

- **Blocked by / related ids:** CRM-51, CRM-33.
- **Depends on code areas or other stories:** the reporting foundation from story 51 and SLA outcomes from
  story 33. Story 54 assembles these into management dashboards.

## Technical hints

- Report percentiles from the start. A manager asking why a customer waited three days cannot be answered with a
  mean.
- Generate the PDF server-side from the same data the screen uses, not by screenshotting the page, or Arabic will
  break.
- Stamp filters and generation time onto every export. Undated exports circulate for months and cause arguments.

## Out of scope

- Agent performance and satisfaction reporting. That is story 53.
- The management dashboard. That is story 54.
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
