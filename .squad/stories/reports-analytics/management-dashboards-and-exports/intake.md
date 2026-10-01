# Story 54 — Management dashboards and exports

Story intake for the **Reports and Analytics** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/reports-analytics/management-dashboards-and-exports/intake.md`.

---

## Feature

- **Feature name (display):** Reports and Analytics
- **Feature slug (folder under `plans/`):** `reports-analytics`
- **Module (backend):** `Reporting`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-54`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `reports-analytics`

---

## Title

```
Management dashboards and exports
```

---

## Description

The executive view: the state of support on one screen, in both languages, refreshing on its own, and readable on
a wall display as well as a laptop.

- A configurable dashboard: a widget catalogue over the story 51 metrics, drag-and-drop layout, per-widget filters
  inheriting from a global filter bar, and several saved dashboards per user.
- Widget types matched to the question rather than to fashion: KPI tiles with a trend and a comparison, time series,
  stacked bars for distribution, heatmaps for volume by hour and weekday (which is how staffing decisions are
  actually made), gauges for SLA compliance, leaderboards where appropriate, and tables with drill-through.
- Global filters: period with presets and a custom range, department, branch, team, channel, category, and
  priority, applied across every widget and reflected in the URL.
- Real-time operational widgets: current queue depth, agents online, chats waiting, tickets breaching within the
  hour, updating live through story 26's transport.
- Targets and thresholds per KPI with colour coding, and an alert through story 36 when a KPI crosses its
  threshold, so the dashboard is not something someone has to remember to look at.
- Presentation modes: a wall-display mode that rotates dashboards with no chrome, and a mobile layout that stacks
  into a readable single column rather than shrinking a grid.
- Export and share: PDF and Excel export of a whole dashboard, a scheduled emailed snapshot, and a tokenised
  read-only share link with an expiry.
- Full bilingual and RTL support: axes, legends, tooltips, number and date formatting, and correct chart mirroring
  in Arabic.

## Acceptance criteria

- [ ] A dashboard can be assembled from the widget catalogue, rearranged, and saved, and several dashboards can be
      kept per user.
- [ ] Global filters apply to every widget, are reflected in the URL, and survive a refresh and a share.
- [ ] Every widget's figures match the corresponding report from stories 52 and 53 for the same filters.
- [ ] Every widget drills through to the underlying records, and the count matches.
- [ ] Real-time widgets update live without a manual refresh and without a full dashboard reload.
- [ ] A KPI crossing its threshold changes colour and raises an alert through story 36 exactly once per crossing.
- [ ] Wall-display mode rotates dashboards without chrome and survives being left open overnight without leaking
      memory or stalling.
- [ ] The mobile layout stacks into a readable single column at 375px rather than scaling down the grid.
- [ ] PDF and Excel exports match the on-screen figures and are stamped with filters and generation time.
- [ ] A share link is read-only, expires, and grants no access beyond the dashboard it was issued for.
- [ ] Every chart renders correctly in Arabic RTL, including axis order, legends, tooltips, and number formatting.
- [ ] A dashboard of ten widgets loads within the agreed budget on a seeded 500,000-ticket data set.
- [ ] A user sees only data within their scope, and a shared link does not widen the sharer's scope.

## Dependencies

- **Blocked by / related ids:** CRM-51, CRM-52, CRM-53, CRM-26, CRM-36.
- **Depends on code areas or other stories:** the reporting foundation and reports from stories 51 to 53, realtime
  from story 26, and alerting from story 36.

## Technical hints

- Load the dashboard widget by widget rather than in one request. Ten independent queries render progressively;
  one combined query renders nothing until the slowest finishes.
- Mirror charts properly in RTL rather than flipping the container. A mirrored time axis running right to left is
  correct; mirrored digits are not.
- Verify that a share link cannot be widened by editing its parameters. A tokenised link that trusts its own query
  string is an open data export.

## Out of scope

- A general-purpose ad-hoc query builder or BI tool.
- Predictive forecasting.
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
