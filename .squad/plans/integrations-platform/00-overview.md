# integrations-platform — plan overview

> **MVP re-scope (2026-09-29):** only **58** (branding, reduced to organisation-level branding of the agent workspace and portal) is in the MVP, depending on 06 and 47. 55–57 are deferred.

Entry point for the **Integrations and Platform** feature. This is **feature 12 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Integrations` (55–57); 58 extends `Organisation`. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 55 | [`public-api-versioning-and-api-key-management`](../../stories/integrations-platform/public-api-versioning-and-api-key-management/intake.md) | Public API versioning and API key management | _not planned yet_ | CRM-09, CRM-03, CRM-11, CRM-15, CRM-37 |
| 56 | [`outbound-webhooks-and-integration-events`](../../stories/integrations-platform/outbound-webhooks-and-integration-events/intake.md) | Outbound webhooks and integration events | _not planned yet_ | CRM-55, CRM-02, CRM-03 |
| 57 | [`erp-and-external-system-integration`](../../stories/integrations-platform/erp-and-external-system-integration/intake.md) | ERP and external system integration | _not planned yet_ | CRM-11, CRM-32, CRM-03, CRM-55 |
| 58 | [`custom-branding-theming-and-tenant-configuration`](../../stories/integrations-platform/custom-branding-theming-and-tenant-configuration/intake.md) | Custom branding theming and tenant configuration | _not planned yet_ | CRM-04, CRM-05, CRM-06, CRM-40, CRM-47 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
