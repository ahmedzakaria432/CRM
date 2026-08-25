# Story 53 — Agent performance and satisfaction reports

Story intake for the **Reports and Analytics** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/reports-analytics/agent-performance-and-satisfaction-reports/intake.md`.

---

## Feature

- **Feature name (display):** Reports and Analytics
- **Feature slug (folder under `plans/`):** `reports-analytics`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-53`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `reports-analytics`

---

## Title

```
Agent performance and satisfaction reports
```

---

## Description

Reporting about people, which needs more care than reporting about tickets: a metric that is unfair will be gamed,
and a metric that is opaque will be resented.

- Per-agent metrics: tickets handled, resolved, and reopened, first response and resolution times as median and
  percentile, handling time, backlog, touches per ticket, and satisfaction, all with the attribution rule stated.
- Attribution handled explicitly for reassigned tickets, using the rule agreed in story 50, and shown on the
  report so a number is never mysterious.
- Fairness adjustments made visible rather than hidden: volume normalised by working hours from the story 06
  calendar, out-of-office excluded, and complexity acknowledged by reporting per category and priority instead of
  one blended figure.
- Team and department rollups, with distribution rather than only a top-and-bottom ranking, so a manager sees the
  spread instead of a league table.
- Satisfaction reporting from story 50: CSAT, CES, and NPS by agent, team, department, channel, and category, with
  the response rate always shown next to the score, because a 100% score from two responses means nothing.
- Comment themes from story 50's analysis, grouped and drillable to the raw comments.
- Knowledge and deflection contribution: articles authored, article usage, and deflection attributed from stories
  39 and 45, so the agents who reduce future tickets are visible alongside the ones who close current ones.
- Quality assurance support: a sampled review of tickets against a scorecard, with the reviewer, score, and notes,
  reported alongside the quantitative metrics.
- Agent self-view: every agent can see their own figures, matching what their supervisor sees, so the numbers are
  never a surprise in a review.

## Acceptance criteria

- [ ] Every agent metric states its attribution rule on the report, and reassigned tickets are attributed
      consistently with story 50.
- [ ] Response and resolution times are reported as median and percentile, not as a bare mean.
- [ ] Volume is normalised by the agent's working hours, and out-of-office periods are excluded.
- [ ] Metrics can be broken down by category and priority so complexity is visible.
- [ ] Satisfaction always displays the response rate beside the score, and a score below the minimum response
      count is marked as not statistically meaningful rather than shown as a number.
- [ ] Comment themes drill through to the raw comments, respecting scope.
- [ ] Article authorship, usage, and deflection are attributed and reported per agent.
- [ ] QA scorecard reviews appear alongside the quantitative metrics with reviewer and notes.
- [ ] An agent can see their own figures and cannot see another agent's individual figures without the permission.
- [ ] A supervisor sees only agents within their scope.
- [ ] Every figure drills through to the underlying tickets, and the count matches.
- [ ] Every metric definition is documented in one place shared with story 51.

## Dependencies

- **Blocked by / related ids:** CRM-51, CRM-50, CRM-39, CRM-45, CRM-08.
- **Depends on code areas or other stories:** the reporting foundation from story 51, satisfaction from story 50,
  knowledge usage from stories 39 and 45, and agent profiles and calendars from stories 08 and 06.

## Technical hints

- Suppress a satisfaction score below a minimum response count rather than displaying a misleading number. It is
  the single most important piece of statistical honesty in this story.
- Never ship a raw ranked leaderboard as the default view. Report the distribution; a manager who wants a ranking
  can sort.
- Reconcile every per-agent figure against a drill-through count by hand before release. Performance numbers get
  challenged, and there is no test suite to appeal to.

## Out of scope

- Payroll, bonus calculation, and HR processes.
- Ticket volume and SLA reporting. That is story 52.
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
