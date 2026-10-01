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
| 13 | 47 | Customer portal accounts ticket submission tracking and history | **Customer Portal** | ✅ Done | [intake](stories/customer-portal/portal-authentication-registration-and-account/intake.md) | [plan](plans/customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md) | 07, 11, 15, 18 |
| 14 | 58 | Custom branding theming and tenant configuration | **Custom branding** | ✅ Done | [intake](stories/integrations-platform/custom-branding-theming-and-tenant-configuration/intake.md) | [plan](plans/integrations-platform/58-story-custom-branding-theming-and-tenant-configuration.md) | 06, 47 |
| 15 | 59 | Home and navigation flow | Ties the MVP together (owner feedback 2026-10-01) | ✅ Done | [intake](stories/platform-foundation/home-and-navigation-flow/intake.md) | [plan](plans/platform-foundation/59-story-home-and-navigation-flow.md) | 15, 20, 47, 58 |

**MVP: 0 stories remaining — MVP complete** (story 59 was added after the MVP and is done). Folder names keep the original slugs, so a
merged story's intake folder name can be narrower than its new title (e.g. story 20's folder is
`ticket-inbox-ui-…` but it now also covers the ticket detail page).


## Post-MVP build order (owner decision, 2026-10-01)

The deferred features came back from `next-steps-stories/`, **except AI (41–46) and Integrations (55–57)**, which
stay deferred. Run each row with `/squad-story NN` (or `next`). The order follows the intakes' real dependencies, so
it differs a little from the feature order: SLA comes before the Agent Workspace because tasks (29) and
collaboration (31) need notifications (36), and surveys (50) come before Reports because 51 reads them.
The **Depends on** column maps merged stories to their hosts (08/09→07, 12→11, 16/17→15, 19→18, 21→20, 48→47).
What a host did not deliver (for example attachment storage from 12, or custom fields from 16) is the planner's
call: build the minimum the story needs or name it as a risk. AI-dependent parts (41, 45) are out of scope.

| # | NN | Story | Covers | Status | Intake | Plan | Depends on |
|---|----|-------|--------|--------|--------|------|------------|
| 16 | 10 | Audit log | Audit | ⏳ To do | [intake](stories/identity-access/audit-log/intake.md) | — | 02, 03, 07 |
| 17 | 22 | Channel abstraction and inbound message pipeline | Communication | ⏳ To do | [intake](stories/communication-channels/channel-abstraction-and-inbound-message-pipeline/intake.md) | — | 11, 15, 18 |
| 18 | 23 | Email channel inbound and outbound | Communication | ⏳ To do | [intake](stories/communication-channels/email-channel-inbound-and-outbound/intake.md) | — | 22 |
| 19 | 24 | WhatsApp Business channel | Communication | ⏳ To do | [intake](stories/communication-channels/whatsapp-business-channel/intake.md) | — | 22 |
| 20 | 25 | SMS channel | Communication | ⏳ To do | [intake](stories/communication-channels/sms-channel/intake.md) | — | 22 |
| 21 | 26 | Live chat with realtime agent console | Communication | ⏳ To do | [intake](stories/communication-channels/live-chat-with-realtime-agent-console/intake.md) | — | 22 |
| 22 | 27 | Web form intake and public submission endpoint | Communication | ⏳ To do | [intake](stories/communication-channels/web-form-intake-and-public-submission-endpoint/intake.md) | — | 22 |
| 23 | 32 | SLA policies response and resolution targets | Sla | ⏳ To do | [intake](stories/sla-automation/sla-policies-response-and-resolution-targets/intake.md) | — | 06, 15 |
| 24 | 33 | SLA clock breach detection and at risk alerts | Sla | ⏳ To do | [intake](stories/sla-automation/sla-clock-breach-detection-and-at-risk-alerts/intake.md) | — | 32 |
| 25 | 34 | Automatic assignment and routing rules | Automation | ⏳ To do | [intake](stories/sla-automation/automatic-assignment-and-routing-rules/intake.md) | — | 15 |
| 26 | 36 | Notifications and alerts | Notifications | ⏳ To do | [intake](stories/sla-automation/notifications-and-alerts/intake.md) | — | 23, 25, 26, 33 |
| 27 | 29 | Tasks and reminders | Workspace | ⏳ To do | [intake](stories/agent-workspace/tasks-and-reminders/intake.md) | — | 15, 36 |
| 28 | 35 | Escalation and business rules engine | Automation | ⏳ To do | [intake](stories/sla-automation/escalation-and-business-rules-engine/intake.md) | — | 29, 33, 34, 36 |
| 29 | 30 | Quick replies and canned responses | Workspace | ⏳ To do | [intake](stories/agent-workspace/quick-replies-and-canned-responses/intake.md) | — | 18, 20 |
| 30 | 31 | Team collaboration mentions and watchers | Workspace | ⏳ To do | [intake](stories/agent-workspace/team-collaboration-mentions-and-watchers/intake.md) | — | 18, 26, 36 |
| 31 | 28 | Agent dashboard | Workspace | ⏳ To do | [intake](stories/agent-workspace/agent-dashboard/intake.md) | — | 20, 26, 29, 31 |
| 32 | 37 | Knowledge base content model and authoring workflow | KnowledgeBase | ⏳ To do | [intake](stories/knowledge-base/knowledge-base-content-model-and-authoring-workflow/intake.md) | — | 07, 15, 36 |
| 33 | 38 | Bilingual knowledge base search | KnowledgeBase | ⏳ To do | [intake](stories/knowledge-base/bilingual-knowledge-base-search/intake.md) | — | 37 |
| 34 | 39 | Agent facing knowledge base and reply insertion | KnowledgeBase | ⏳ To do | [intake](stories/knowledge-base/agent-facing-knowledge-base-and-reply-insertion/intake.md) | — | 20, 30, 37, 38 |
| 35 | 40 | Public help centre and FAQ experience | KnowledgeBase | ⏳ To do | [intake](stories/knowledge-base/public-help-centre-and-faq-experience/intake.md) | — | 27, 37, 38, 58 |
| 36 | 50 | Customer satisfaction surveys and feedback | Feedback | ⏳ To do | [intake](stories/customer-portal/customer-satisfaction-surveys-and-feedback/intake.md) | — | 15, 35, 36 |
| 37 | 51 | Reporting foundation and read models | Reporting | ⏳ To do | [intake](stories/reports-analytics/reporting-foundation-and-read-models/intake.md) | — | 15, 18, 33, 50 |
| 38 | 52 | Ticket and SLA performance reports | Reporting | ⏳ To do | [intake](stories/reports-analytics/ticket-and-sla-performance-reports/intake.md) | — | 33, 51 |
| 39 | 53 | Agent performance and satisfaction reports | Reporting | ⏳ To do | [intake](stories/reports-analytics/agent-performance-and-satisfaction-reports/intake.md) | — | 39, 50, 51 |
| 40 | 54 | Management dashboards and exports | Reporting | ⏳ To do | [intake](stories/reports-analytics/management-dashboards-and-exports/intake.md) | — | 26, 36, 51, 52, 53 |

**Remaining post-MVP: 25 stories.**

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
