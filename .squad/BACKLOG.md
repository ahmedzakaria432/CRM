# CRM backlog — story map

58 stories across 12 features, derived from `azm_squad_customer_support_crm.pdf`. `NN` is a global execution
sequence: **build in NN order.** Feature planning order is in [`plans/00-index.md`](plans/00-index.md).

- **Backend:** .NET 10 **modular monolith** — one module per bounded context (the Module column below; catalogue in
  `CLAUDE.md`), DDD + Clean Architecture inside each module, CQRS with MediatR, EF Core 10 on SQL Server with one
  schema per module. Modules talk only through their `Contracts` projects; the build enforces it (`CRM0001`).
- **Frontend:** Nuxt 4, Vue 3, TypeScript strict, Tailwind, bilingual `ar` / `en` with RTL.
- **No automated tests.** Deliberate project decision — see the testing policy in `CLAUDE.md`.

## MVP re-scope (2026-09-29, owner decision)

The MVP must ship in **2 days** and covers only:

- **Customer Management**, **Ticket Management**, and the **Customer Portal**;
- **Arabic & English** with RTL, **web and mobile friendly** (375 / 768 / 1280), **multi-department**,
  **multi-branch**, and **custom branding**.

Sign-in and permissions (story 07) are included because the agent workspace and the portal cannot exist without
them. Everything else — channels, agent workspace extras, SLA and automation, knowledge base, AI, reports,
integrations, and the audit log — is **deferred post-MVP**. Their intakes now live outside `.squad/`, in [`../next-steps-stories/`](../next-steps-stories/README.md), carry a "deferred" banner, and
are not planned now.

To fit two days, related stories were **merged** into the story that hosts them, each cut to an MVP scope with an
explicit "Out of scope (deferred post-MVP)" list. Story numbers were **kept**, not renumbered: gaps in `NN` are
expected, and every existing link and plan filename stays valid. A merged intake carries a banner pointing at its
host story.

**Remaining MVP work: 10 stories** — 05, 06, 07, 11, 14, 15, 18, 20, 47, 58 (01–04 are done). The build list is
[`MVP-STORIES.md`](MVP-STORIES.md). The build list is [`MVP-STORIES.md`](MVP-STORIES.md).

## Phases

| Phase | Stories | What it buys |
|-------|---------|--------------|
| **MVP — done** | 01–04 | Backend scaffold, persistence, cross-cutting concerns, frontend scaffold. |
| **MVP — remaining** | 05, 06, 07, 11, 14, 15, 18, 20, 47, 58 | Bilingual RTL, org scoping, sign-in, customers, tickets, the portal, and branding. |
| **Post-MVP** | 10, 22–46, 50–57 (and the deferred halves of merged stories) | Channels, workspace extras, SLA/automation, knowledge base, AI, surveys, reports, integrations, audit log. |

## MVP stories — build in this order

| NN | Feature | Module | Story (MVP scope) | Absorbs | Depends on |
|----|---------|--------|-------------------|---------|------------|
| 01 | platform-foundation | BuildingBlocks, `Sample` | Backend solution scaffold with DDD and Clean Architecture — **done** | — | — |
| 02 | platform-foundation | BuildingBlocks, `Sample` | Persistence baseline with auditing and soft delete — **done** | — | 01 |
| 03 | platform-foundation | BuildingBlocks | Cross cutting concerns error handling logging and caching — **done** | — | 01, 02 |
| 04 | platform-foundation | — (frontend) | Frontend scaffold with Nuxt TypeScript and Tailwind — **done** | — | 01 |
| 05 | platform-foundation | BuildingBlocks | Bilingual Arabic and English support with RTL layout (+ responsive shell) | — | 03, 04 |
| 06 | platform-foundation | `Organisation` | Multi department and multi branch organisation scoping (+ memberships, dev login) | — | 02, 03, 04, 05 |
| 07 | identity-access | `Identity` | Authentication users roles and permissions | 08, 09 | 06 |
| 11 | customer-management | `Customers` | Customer aggregate profiles contact details and notes (deletes `Sample`) | 12 (notes) | 06, 07 |
| 14 | customer-management | — (frontend) | Customer 360 workspace UI | 13 (minimal) | 05, 07, 11 |
| 15 | ticket-management | `Tickets` | Ticket aggregate lifecycle taxonomy and assignment | 16, 17 | 06, 07, 11 |
| 18 | ticket-management | `Tickets` | Ticket conversation thread messages internal notes and history | 19 | 15 |
| 20 | ticket-management | — (frontend) | Ticket inbox and ticket detail workspace UI | 21 | 14, 15, 18 |
| 47 | customer-portal | `Identity`, `Tickets` | Customer portal accounts ticket submission tracking and history | 48, 49 | 07, 11, 15, 18 |
| 58 | integrations-platform | `Organisation` | Custom branding theming and tenant configuration (org-level, agent + portal) | — | 06, 47 |

**Parallelism.** After 07, the backend pair 11 → 15 → 18 and the frontend 14 can overlap: 14 starts as soon as 11's
API exists. 47 and 20 can run in parallel once 18 is done. 58 is last.

## Post-MVP stories (deferred)

Intakes for every row below have moved to [`../next-steps-stories/`](../next-steps-stories/README.md).

| NN | Feature | Module | Story | Notes |
|----|---------|--------|-------|-------|
| 08 | identity-access | `Identity` | User and role management | **Merged into 07** (basic user admin, fixed roles); invites, custom roles, availability deferred |
| 09 | identity-access | `Identity` | Permission model and policy based authorisation | **Merged into 07** (code-defined permissions + startup guard); matrix screen, record-level rules deferred |
| 10 | identity-access | `Audit` | Audit log | Deferred |
| 12 | customer-management | `Customers` | Customer notes and attachments | **Notes merged into 11**; attachments and file storage deferred |
| 13 | customer-management | `Customers` | Interaction history and unified customer timeline | **Merged into 14** as a tickets panel; projection deferred |
| 16 | ticket-management | `Tickets` | Ticket taxonomy categories priorities and types | **Merged into 15** (flat localised categories, fixed priorities); custom fields, types, tags deferred |
| 17 | ticket-management | `Tickets` | Ticket assignment ownership and queues | **Merged into 15** (assign, claim, transfer, queue queries); bulk, workload deferred |
| 19 | ticket-management | `Tickets` | Ticket history and activity timeline | **Merged into 18** (activity entries in the thread); durations, field history deferred |
| 21 | ticket-management | `Tickets` | Ticket detail workspace UI | **Merged into 20** |
| 22–27 | communication-channels | `Communication` | Channel pipeline, email, WhatsApp, SMS, live chat, web form | Deferred |
| 28–31 | agent-workspace | `Workspace` | Dashboard, tasks, quick replies, collaboration | Deferred |
| 32–36 | sla-automation | `Sla`, `Automation`, `Notifications` | SLA, routing, escalation, notifications | Deferred |
| 37–40 | knowledge-base | `KnowledgeBase` | Content, search, agent KB, help centre | Deferred |
| 41–46 | ai-features | `Ai` | AI abstraction, summaries, replies, categorisation, solutions, chatbot | Deferred |
| 48 | customer-portal | `Tickets` | Portal ticket submission and tracking | **Merged into 47**; KB deflection, attachments, realtime, tracking code deferred |
| 49 | customer-portal | `Tickets` | Portal request history and self service knowledge access | **Merged into 47** (history); KB, announcements, exports deferred |
| 50 | customer-portal | `Feedback` | Customer satisfaction surveys and feedback | Deferred |
| 51–54 | reports-analytics | `Reporting` | Reporting foundation, reports, dashboards, exports | Deferred |
| 55–57 | integrations-platform | `Integrations` | Public API, webhooks, ERP | Deferred |

When the MVP ships, re-plan the deferred work by restoring these rows into the main table and trimming each
merged intake's deferred list into a follow-up story.

## Module notes

- A module is created by the first story that needs it; that story's plan scaffolds all five projects from the
  `Sample` template (`Organisation` in 06, `Identity` in 07, `Customers` in 11, `Tickets` in 15). **Story 11 deletes
  `Sample`**; 15 copies from `Customers` instead.
- Story 03's integration-event outbox stays the only cross-module write path. In the MVP, tickets publish
  `TicketCreated`/`TicketStatusChanged`/`TicketAssigned`/`TicketMessageAdded` with no subscribers yet; they are the
  contract the deferred stories (timeline, audit, SLA, notifications) subscribe to later.
- Memberships (user ↔ department + role) live in `Organisation` (06); `Identity` (07) reads them through
  `Organisation.Contracts` at login.
- A story works in the module(s) its Module column names. In any other module it may only add a Contracts member or
  an integration-event consumer. Needing more than that is a sign to split the story.

## MVP demo path

`05 → 06 → 07 → 11 → 14 → 15 → 18 → 20 → 47 → 58`

A customer registers on the branded portal and submits a ticket in Arabic; an agent in the right department and
branch finds it in the inbox, opens it with the customer's context, replies, adds an internal note, and resolves it;
the customer sees the reply and the new status on their phone.

## Workflow

```
/squad-new-story <feature-slug> --title "..."          # scaffold an intake
/squad-plan .squad/stories/<feature>/<slug>/intake.md  # generate the executable plan
/squad-implement .squad/plans/<feature>/NN-story-*.md  # execute it in a fresh scoped session
/squad-status                                          # where the backlog stands
```
