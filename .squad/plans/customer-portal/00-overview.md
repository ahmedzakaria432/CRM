# customer-portal — plan overview

Entry point for the **Customer Portal** feature. This is **feature 10 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** no module of its own — the `Portal` Area over `Identity` (47), `Tickets` (48, 49), and a new `Feedback` module (50). See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 47 | [`portal-authentication-registration-and-account`](../../stories/customer-portal/portal-authentication-registration-and-account/intake.md) | Portal authentication registration and account | _not planned yet_ | CRM-07, CRM-11, CRM-04, CRM-36 |
| 48 | [`portal-ticket-submission-and-tracking`](../../stories/customer-portal/portal-ticket-submission-and-tracking/intake.md) | Portal ticket submission and tracking | _not planned yet_ | CRM-47, CRM-15, CRM-16, CRM-18, CRM-12, CRM-38 |
| 49 | [`portal-request-history-and-self-service-knowledge-access`](../../stories/customer-portal/portal-request-history-and-self-service-knowledge-access/intake.md) | Portal request history and self service knowledge access | _not planned yet_ | CRM-48, CRM-40, CRM-13, CRM-12 |
| 50 | [`customer-satisfaction-surveys-and-feedback`](../../stories/customer-portal/customer-satisfaction-surveys-and-feedback/intake.md) | Customer satisfaction surveys and feedback | _not planned yet_ | CRM-15, CRM-36, CRM-35, CRM-41 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
