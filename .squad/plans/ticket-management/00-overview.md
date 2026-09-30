# ticket-management — plan overview

> **MVP re-scope (2026-09-29):** plan **15** ("Ticket aggregate lifecycle taxonomy and assignment", absorbing 16 and 17), **18** ("… messages internal notes and history", absorbing 19), and **20** ("Ticket inbox and ticket detail workspace UI", absorbing 21). 16, 17, 19, 21 are not planned on their own.

Entry point for the **Ticket Management** feature. This is **feature 4 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Tickets` (15; also 16–19). 18 extends `Tickets` only (owned `TicketMessage`/`TicketNote`, the thread read model, `TicketMessageAddedIntegrationEvent`; no building-block change). 15 also adds read-only Contracts APIs to `Organisation` (department reference, membership check) and `Identity` (staff names), and the `ITransferableOrganisationScoped` write-guard exception in the building blocks. 20 (absorbing 21) is mostly frontend; it adds read-only Contracts reads to `Customers` (`GetCustomerNamesAsync`) and `Organisation` (`GetDepartmentMembersAsync`, `ListDepartmentsAsync`) and two Tickets Agent read endpoints (`…/agents`, `…/transfer-targets`); no command, schema or building-block change. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 15 | [`ticket-aggregate-and-lifecycle`](../../stories/ticket-management/ticket-aggregate-and-lifecycle/intake.md) | Ticket aggregate lifecycle taxonomy and assignment | [`15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md`](15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md) | CRM-06, CRM-07, CRM-11 (16 and 17 merged in, reduced) |
| 16 | [`ticket-taxonomy-categories-priorities-and-types`](../../../next-steps-stories/stories/ticket-management/ticket-taxonomy-categories-priorities-and-types/intake.md) | Ticket taxonomy categories priorities and types | _not planned yet_ | CRM-15, CRM-09 |
| 17 | [`ticket-assignment-ownership-and-queues`](../../../next-steps-stories/stories/ticket-management/ticket-assignment-ownership-and-queues/intake.md) | Ticket assignment ownership and queues | _not planned yet_ | CRM-15, CRM-08, CRM-06 |
| 18 | [`ticket-conversation-thread-messages-and-internal-notes`](../../stories/ticket-management/ticket-conversation-thread-messages-and-internal-notes/intake.md) | Ticket conversation thread messages internal notes and history | [`18-story-ticket-conversation-thread-messages-internal-notes-and-history.md`](18-story-ticket-conversation-thread-messages-internal-notes-and-history.md) | CRM-15 (19 history merged in; attachments of 12 deferred) |
| 19 | [`ticket-history-and-activity-timeline`](../../../next-steps-stories/stories/ticket-management/ticket-history-and-activity-timeline/intake.md) | Ticket history and activity timeline | _not planned yet_ | CRM-15, CRM-10, CRM-18 |
| 20 | [`ticket-inbox-ui-with-filters-and-bulk-actions`](../../stories/ticket-management/ticket-inbox-ui-with-filters-and-bulk-actions/intake.md) | Ticket inbox and ticket detail workspace UI | [`20-story-ticket-inbox-and-ticket-detail-workspace-ui.md`](20-story-ticket-inbox-and-ticket-detail-workspace-ui.md) | CRM-14, CRM-15, CRM-18 (21 detail workspace merged in; bulk actions deferred) |
| 21 | [`ticket-detail-workspace-ui`](../../../next-steps-stories/stories/ticket-management/ticket-detail-workspace-ui/intake.md) | Ticket detail workspace UI | merged into [20](20-story-ticket-inbox-and-ticket-detail-workspace-ui.md) | CRM-14, CRM-18, CRM-19, CRM-20 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
