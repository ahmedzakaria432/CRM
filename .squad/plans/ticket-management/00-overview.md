# ticket-management — plan overview

> **MVP re-scope (2026-09-29):** plan **15** ("Ticket aggregate lifecycle taxonomy and assignment", absorbing 16 and 17), **18** ("… messages internal notes and history", absorbing 19), and **20** ("Ticket inbox and ticket detail workspace UI", absorbing 21). 16, 17, 19, 21 are not planned on their own.

Entry point for the **Ticket Management** feature. This is **feature 4 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Tickets` (15; also 16–19). 20 and 21 are frontend only. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 15 | [`ticket-aggregate-and-lifecycle`](../../stories/ticket-management/ticket-aggregate-and-lifecycle/intake.md) | Ticket aggregate and lifecycle | _not planned yet_ | CRM-02, CRM-06, CRM-09, CRM-11 |
| 16 | [`ticket-taxonomy-categories-priorities-and-types`](../../stories/ticket-management/ticket-taxonomy-categories-priorities-and-types/intake.md) | Ticket taxonomy categories priorities and types | _not planned yet_ | CRM-15, CRM-09 |
| 17 | [`ticket-assignment-ownership-and-queues`](../../stories/ticket-management/ticket-assignment-ownership-and-queues/intake.md) | Ticket assignment ownership and queues | _not planned yet_ | CRM-15, CRM-08, CRM-06 |
| 18 | [`ticket-conversation-thread-messages-and-internal-notes`](../../stories/ticket-management/ticket-conversation-thread-messages-and-internal-notes/intake.md) | Ticket conversation thread messages and internal notes | _not planned yet_ | CRM-15, CRM-12 |
| 19 | [`ticket-history-and-activity-timeline`](../../stories/ticket-management/ticket-history-and-activity-timeline/intake.md) | Ticket history and activity timeline | _not planned yet_ | CRM-15, CRM-10, CRM-18 |
| 20 | [`ticket-inbox-ui-with-filters-and-bulk-actions`](../../stories/ticket-management/ticket-inbox-ui-with-filters-and-bulk-actions/intake.md) | Ticket inbox UI with filters and bulk actions | _not planned yet_ | CRM-04, CRM-05, CRM-15, CRM-16, CRM-17 |
| 21 | [`ticket-detail-workspace-ui`](../../stories/ticket-management/ticket-detail-workspace-ui/intake.md) | Ticket detail workspace UI | _not planned yet_ | CRM-14, CRM-18, CRM-19, CRM-20 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
