# reports-analytics — plan overview

> **MVP re-scope (2026-09-29): this whole feature is deferred post-MVP.** Do not plan its stories now. See `.squad/BACKLOG.md`, "MVP re-scope".

Entry point for the **Reports and Analytics** feature. This is **feature 11 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Reporting` (51; also 52–54). Read models are built only from other modules' integration events. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 51 | [`reporting-foundation-and-read-models`](../../stories/reports-analytics/reporting-foundation-and-read-models/intake.md) | Reporting foundation and read models | _not planned yet_ | CRM-15, CRM-19, CRM-33, CRM-50 |
| 52 | [`ticket-and-sla-performance-reports`](../../stories/reports-analytics/ticket-and-sla-performance-reports/intake.md) | Ticket and SLA performance reports | _not planned yet_ | CRM-51, CRM-33 |
| 53 | [`agent-performance-and-satisfaction-reports`](../../stories/reports-analytics/agent-performance-and-satisfaction-reports/intake.md) | Agent performance and satisfaction reports | _not planned yet_ | CRM-51, CRM-50, CRM-39, CRM-45, CRM-08 |
| 54 | [`management-dashboards-and-exports`](../../stories/reports-analytics/management-dashboards-and-exports/intake.md) | Management dashboards and exports | _not planned yet_ | CRM-51, CRM-52, CRM-53, CRM-26, CRM-36 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
