# MVP stories — the only stories to build now

The 2-day MVP (owner decision, 2026-09-29): **Customer Management**, **Ticket Management**, and the **Customer
Portal**, plus **Arabic & English (RTL)**, **web and mobile friendly**, **multi-department**, **multi-branch**, and
**custom branding**. The full rationale and merge map are in [`BACKLOG.md`](BACKLOG.md), "MVP re-scope".

Everything that is not an MVP story now lives outside `.squad/`, in
[`../next-steps-stories/`](../next-steps-stories/README.md).

## Build order

Plan and implement top to bottom: `/squad-plan <intake>` → `/squad-implement <plan>`.

| # | NN | Story | Covers | Status | Intake | Plan | Depends on |
|---|----|-------|--------|--------|--------|------|------------|
| 1 | 01 | Backend solution scaffold with DDD and Clean Architecture | Foundation | ✅ Done | [intake](stories/platform-foundation/backend-solution-scaffold-with-ddd-and-clean-architecture/intake.md) | [plan](plans/platform-foundation/01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md) | — |
| 2 | 02 | Persistence baseline with auditing and soft delete | Foundation | ✅ Done | [intake](stories/platform-foundation/persistence-baseline-with-auditing-and-soft-delete/intake.md) | [plan](plans/platform-foundation/02-story-persistence-baseline-with-auditing-and-soft-delete.md) | 01 |
| 3 | 03 | Cross cutting concerns error handling logging and caching | Foundation | ✅ Done | [intake](stories/platform-foundation/cross-cutting-concerns-error-handling-logging-and-caching/intake.md) | [plan](plans/platform-foundation/03-story-cross-cutting-concerns-error-handling-logging-and-caching.md) | 01, 02 |
| 4 | 04 | Frontend scaffold with Nuxt TypeScript and Tailwind | Foundation, web & mobile | ✅ Done | [intake](stories/platform-foundation/frontend-scaffold-with-nuxt-typescript-and-tailwind/intake.md) | [plan](plans/platform-foundation/04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md) | 01 |
| 5 | 05 | Bilingual Arabic and English support with RTL layout | **Arabic & English**, **web & mobile** | ✅ Done | [intake](stories/platform-foundation/bilingual-arabic-and-english-support-with-rtl-layout/intake.md) | [plan](plans/platform-foundation/05-story-bilingual-arabic-and-english-support-with-rtl-layout.md) | 03, 04 |
| 6 | 06 | Multi department and multi branch organisation scoping | **Multi-department**, **multi-branch** | ✅ Done | [intake](stories/platform-foundation/multi-department-and-multi-branch-organisation-scoping/intake.md) | [plan](plans/platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md) | 02–05 |
| 7 | 07 | Authentication users roles and permissions | Sign-in (needed by everything) | ✅ Done | [intake](stories/identity-access/authentication-with-jwt-refresh-tokens-and-password-reset/intake.md) | [plan](plans/identity-access/07-story-authentication-users-roles-and-permissions.md) | 06 |
| 8 | 11 | Customer aggregate profiles contact details and notes | **Customer Management** | ✅ Done | [intake](stories/customer-management/customer-aggregate-profiles-and-contact-details/intake.md) | [plan](plans/customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md) | 06, 07 |
| 9 | 14 | Customer 360 workspace UI | **Customer Management** | ✅ Done | [intake](stories/customer-management/customer-360-workspace-ui/intake.md) | [plan](plans/customer-management/14-story-customer-360-workspace-ui.md) | 05, 07, 11 |
| 10 | 15 | Ticket aggregate lifecycle taxonomy and assignment | **Ticket Management** | ✅ Done | [intake](stories/ticket-management/ticket-aggregate-and-lifecycle/intake.md) | [plan](plans/ticket-management/15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md) | 06, 07, 11 |
| 11 | 18 | Ticket conversation thread messages internal notes and history | **Ticket Management** | ✅ Done | [intake](stories/ticket-management/ticket-conversation-thread-messages-and-internal-notes/intake.md) | [plan](plans/ticket-management/18-story-ticket-conversation-thread-messages-internal-notes-and-history.md) | 15 |
| 12 | 20 | Ticket inbox and ticket detail workspace UI | **Ticket Management** | ✅ Done | [intake](stories/ticket-management/ticket-inbox-ui-with-filters-and-bulk-actions/intake.md) | [plan](plans/ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md) | 14, 15, 18 |
| 13 | 47 | Customer portal accounts ticket submission tracking and history | **Customer Portal** | ⬜ Next | [intake](stories/customer-portal/portal-authentication-registration-and-account/intake.md) | — | 07, 11, 15, 18 |
| 14 | 58 | Custom branding theming and tenant configuration | **Custom branding** | ⬜ | [intake](stories/integrations-platform/custom-branding-theming-and-tenant-configuration/intake.md) | — | 06, 47 |

**Remaining: 2 stories** (47, 58). Folder names keep the original slugs, so a
merged story's intake folder name can be narrower than its new title (e.g. story 20's folder is
`ticket-inbox-ui-…` but it now also covers the ticket detail page).

## What each remaining story absorbed

| Story | Absorbed (now in `next-steps-stories/` for their deferred parts) |
|-------|------------------------------------------------------------------|
| 07 | 08 user and role management, 09 permission model — reduced to fixed roles, a code-defined permission map, basic user admin |
| 11 | 12 customer notes (attachments deferred) |
| 14 | 13 interaction history — minimal: a tickets panel on the customer page |
| 15 | 16 taxonomy (flat localised categories, fixed priorities), 17 assignment (assign, claim, transfer, queue queries) |
| 18 | 19 ticket history — activity entries in the thread |
| 20 | 21 ticket detail workspace UI |
| 47 | 48 portal submission and tracking, 49 portal request history — without KB, attachments, realtime |

## Parallelism

After 07: the backend chain 11 → 15 → 18 can overlap with the frontend 14 once 11's API exists; 20 and 47 can run
in parallel once 18 is done; 58 is last.

## Demo path

A customer registers on the branded portal and submits a ticket in Arabic; an agent in the right department and
branch finds it in the inbox, opens it with the customer's context, replies, adds an internal note, and resolves it;
the customer sees the reply and the new status on their phone.
