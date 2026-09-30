# customer-management — plan overview

> **MVP re-scope (2026-09-29):** plan **11** (now "Customer aggregate profiles contact details and notes", absorbing the notes half of 12 and deleting `Sample`) and **14** (absorbing a minimal 13). 12 and 13 are not planned on their own.

Entry point for the **Customer Management** feature. This is **feature 3 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Customers` (11; also 12, 13) and deletes the `Sample` template module. 14 is frontend only. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 11 | [`customer-aggregate-profiles-and-contact-details`](../../stories/customer-management/customer-aggregate-profiles-and-contact-details/intake.md) | Customer aggregate profiles contact details and notes | [`11-story-customer-aggregate-profiles-contact-details-and-notes.md`](11-story-customer-aggregate-profiles-contact-details-and-notes.md) | CRM-06, CRM-07 |
| 12 | [`customer-notes-and-attachments`](../../../next-steps-stories/stories/customer-management/customer-notes-and-attachments/intake.md) | Customer notes and attachments | _not planned yet_ | CRM-11 |
| 13 | [`interaction-history-and-unified-customer-timeline`](../../../next-steps-stories/stories/customer-management/interaction-history-and-unified-customer-timeline/intake.md) | Interaction history and unified customer timeline | _not planned yet_ | CRM-11, CRM-02 |
| 14 | [`customer-360-workspace-ui`](../../stories/customer-management/customer-360-workspace-ui/intake.md) | Customer 360 workspace UI | _not planned yet_ | CRM-04, CRM-05, CRM-11, CRM-12, CRM-13 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
