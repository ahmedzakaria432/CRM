# identity-access — plan overview

> **MVP re-scope (2026-09-29):** only **07** is planned — "Authentication users roles and permissions", absorbing 08 and 09 in reduced form. 08 and 09 are merged into 07; 10 (audit log) is deferred.

Entry point for the **Identity and Access** feature. This is **feature 2 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Identity` (07; also 08, 09) and `Audit` (10). See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 07 | [`authentication-with-jwt-refresh-tokens-and-password-reset`](../../stories/identity-access/authentication-with-jwt-refresh-tokens-and-password-reset/intake.md) | Authentication with JWT refresh tokens and password reset | _not planned yet_ | CRM-02, CRM-04, CRM-06 |
| 08 | [`user-and-role-management`](../../../next-steps-stories/stories/identity-access/user-and-role-management/intake.md) | User and role management | _not planned yet_ | CRM-06, CRM-07 |
| 09 | [`permission-model-and-policy-based-authorisation`](../../../next-steps-stories/stories/identity-access/permission-model-and-policy-based-authorisation/intake.md) | Permission model and policy based authorisation | _not planned yet_ | CRM-06, CRM-08 |
| 10 | [`audit-log`](../../../next-steps-stories/stories/identity-access/audit-log/intake.md) | Audit log | _not planned yet_ | CRM-02, CRM-03, CRM-09 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
