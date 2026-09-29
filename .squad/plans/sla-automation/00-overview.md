# sla-automation — plan overview

> **MVP re-scope (2026-09-29): this whole feature is deferred post-MVP.** Do not plan its stories now. See `.squad/BACKLOG.md`, "MVP re-scope".

Entry point for the **SLA and Automation** feature. This is **feature 7 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Sla` (32, 33), `Automation` (34, 35), and `Notifications` (36). The `SlaClock` stays inside the `Ticket` aggregate in `Tickets`. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 32 | [`sla-policies-response-and-resolution-targets`](../../stories/sla-automation/sla-policies-response-and-resolution-targets/intake.md) | SLA policies response and resolution targets | _not planned yet_ | CRM-06, CRM-15, CRM-16 |
| 33 | [`sla-clock-breach-detection-and-at-risk-alerts`](../../stories/sla-automation/sla-clock-breach-detection-and-at-risk-alerts/intake.md) | SLA clock breach detection and at risk alerts | _not planned yet_ | CRM-32, CRM-15, CRM-19 |
| 34 | [`automatic-assignment-and-routing-rules`](../../stories/sla-automation/automatic-assignment-and-routing-rules/intake.md) | Automatic assignment and routing rules | _not planned yet_ | CRM-17, CRM-08, CRM-16 |
| 35 | [`escalation-and-business-rules-engine`](../../stories/sla-automation/escalation-and-business-rules-engine/intake.md) | Escalation and business rules engine | _not planned yet_ | CRM-33, CRM-34, CRM-29, CRM-36 |
| 36 | [`notifications-and-alerts`](../../stories/sla-automation/notifications-and-alerts/intake.md) | Notifications and alerts | _not planned yet_ | CRM-23, CRM-25, CRM-26, CRM-33 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
