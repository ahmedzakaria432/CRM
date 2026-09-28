# CRM backlog — story map

58 stories across 12 features, derived from `azm_squad_customer_support_crm.pdf`. `NN` is a global execution
sequence: **build in NN order.** Feature planning order is in [`plans/00-index.md`](plans/00-index.md).

- **Backend:** .NET 10 **modular monolith** — one module per bounded context (the Module column below; catalogue in
  `CLAUDE.md`), DDD + Clean Architecture inside each module, CQRS with MediatR, EF Core 10 on SQL Server with one
  schema per module. Modules talk only through their `Contracts` projects; the build enforces it (`CRM0001`).
- **Frontend:** Nuxt 4, Vue 3, TypeScript strict, Tailwind, bilingual `ar` / `en` with RTL.
- **No automated tests.** Deliberate project decision — see the testing policy in `CLAUDE.md`.

## Phases

| Phase | Stories | What it buys |
|-------|---------|--------------|
| **MVP** | 01–40 | A support team can actually run on it: customers, tickets, every channel, SLA, automation, agent workspace, knowledge base. |
| **Phase 2** | 41–54 | AI assistance, customer self-service portal, and the reporting layer. |
| **Phase 3** | 55–58 | Outward-facing contract: public API, webhooks, ERP, white-labelling. |

## Stories

| NN | Feature | Module | Story | Phase | Depends on |
|----|---------|--------|-------|-------|------------|
| 01 | platform-foundation | BuildingBlocks, `Sample` | Backend solution scaffold with DDD and Clean Architecture | MVP | — |
| 02 | platform-foundation | BuildingBlocks, `Sample` | Persistence baseline with auditing and soft delete | MVP | 01 |
| 03 | platform-foundation | BuildingBlocks | Cross cutting concerns error handling logging and caching | MVP | 01, 02 |
| 04 | platform-foundation | — (frontend) | Frontend scaffold with Nuxt TypeScript and Tailwind | MVP | 01 |
| 05 | platform-foundation | BuildingBlocks | Bilingual Arabic and English support with RTL layout | MVP | 03, 04 |
| 06 | platform-foundation | `Organisation` | Multi department and multi branch organisation scoping | MVP | 02, 04 |
| 07 | identity-access | `Identity` | Authentication with JWT refresh tokens and password reset | MVP | 02, 04, 06 |
| 08 | identity-access | `Identity` | User and role management | MVP | 06, 07 |
| 09 | identity-access | `Identity` | Permission model and policy based authorisation | MVP | 06, 08 |
| 10 | identity-access | `Audit` | Audit log | MVP | 02, 03, 09 |
| 11 | customer-management | `Customers` | Customer aggregate profiles and contact details | MVP | 02, 06, 09 |
| 12 | customer-management | `Customers` | Customer notes and attachments | MVP | 11 |
| 13 | customer-management | `Customers` | Interaction history and unified customer timeline | MVP | 02, 11 |
| 14 | customer-management | `Customers` | Customer 360 workspace UI | MVP | 04, 05, 11, 12, 13 |
| 15 | ticket-management | `Tickets` | Ticket aggregate and lifecycle | MVP | 02, 06, 09, 11 |
| 16 | ticket-management | `Tickets` | Ticket taxonomy categories priorities and types | MVP | 09, 15 |
| 17 | ticket-management | `Tickets` | Ticket assignment ownership and queues | MVP | 06, 08, 15 |
| 18 | ticket-management | `Tickets` | Ticket conversation thread messages and internal notes | MVP | 12, 15 |
| 19 | ticket-management | `Tickets` | Ticket history and activity timeline | MVP | 10, 15, 18 |
| 20 | ticket-management | `Tickets` | Ticket inbox UI with filters and bulk actions | MVP | 04, 05, 15, 16, 17 |
| 21 | ticket-management | `Tickets` | Ticket detail workspace UI | MVP | 14, 18, 19, 20 |
| 22 | communication-channels | `Communication` | Channel abstraction and inbound message pipeline | MVP | 11, 15, 16, 18 |
| 23 | communication-channels | `Communication` | Email channel inbound and outbound | MVP | 22 |
| 24 | communication-channels | `Communication` | WhatsApp Business channel | MVP | 22 |
| 25 | communication-channels | `Communication` | SMS channel | MVP | 22 |
| 26 | communication-channels | `Communication` | Live chat with realtime agent console | MVP | 12, 15, 18, 22 |
| 27 | communication-channels | `Communication` | Web form intake and public submission endpoint | MVP | 12, 16, 22 |
| 28 | agent-workspace | `Workspace` | Agent dashboard | MVP | 17, 20, 26, 29, 31 |
| 29 | agent-workspace | `Workspace` | Tasks and reminders | MVP | 08, 15, 36 |
| 30 | agent-workspace | `Workspace` | Quick replies and canned responses | MVP | 12, 18, 21 |
| 31 | agent-workspace | `Workspace` | Team collaboration mentions and watchers | MVP | 08, 18, 19, 26, 36 |
| 32 | sla-automation | `Sla` | SLA policies response and resolution targets | MVP | 06, 15, 16 |
| 33 | sla-automation | `Sla`, `Tickets` | SLA clock breach detection and at risk alerts | MVP | 15, 19, 32 |
| 34 | sla-automation | `Automation` | Automatic assignment and routing rules | MVP | 08, 16, 17 |
| 35 | sla-automation | `Automation` | Escalation and business rules engine | MVP | 29, 33, 34, 36 |
| 36 | sla-automation | `Notifications` | Notifications and alerts | MVP | 23, 25, 26, 33 |
| 37 | knowledge-base | `KnowledgeBase` | Knowledge base content model and authoring workflow | MVP | 09, 12, 16, 36 |
| 38 | knowledge-base | `KnowledgeBase` | Bilingual knowledge base search | MVP | 37 |
| 39 | knowledge-base | `KnowledgeBase` | Agent facing knowledge base and reply insertion | MVP | 21, 30, 37, 38 |
| 40 | knowledge-base | `KnowledgeBase` | Public help centre and FAQ experience | MVP | 27, 37, 38, 58 |
| 41 | ai-features | `Ai` | AI service abstraction prompt infrastructure and guardrails | Phase 2 | 03, 09 |
| 42 | ai-features | `Ai` | AI ticket summarisation | Phase 2 | 18, 21, 41 |
| 43 | ai-features | `Ai` | AI suggested replies | Phase 2 | 21, 30, 39, 41, 45 |
| 44 | ai-features | `Ai` | AI automatic categorisation priority and language detection | Phase 2 | 16, 22, 34, 41 |
| 45 | ai-features | `Ai` | AI suggested solutions with knowledge base retrieval | Phase 2 | 15, 37, 38, 41 |
| 46 | ai-features | `Ai` | AI chatbot for portal and chat channels | Phase 2 | 26, 40, 41, 45, 48 |
| 47 | customer-portal | `Identity` | Portal authentication registration and account | Phase 2 | 04, 07, 11, 36 |
| 48 | customer-portal | `Tickets` | Portal ticket submission and tracking | Phase 2 | 12, 15, 16, 18, 38, 47 |
| 49 | customer-portal | `Tickets` | Portal request history and self service knowledge access | Phase 2 | 12, 13, 40, 48 |
| 50 | customer-portal | `Feedback` | Customer satisfaction surveys and feedback | Phase 2 | 15, 35, 36, 41 |
| 51 | reports-analytics | `Reporting` | Reporting foundation and read models | Phase 2 | 15, 19, 33, 50 |
| 52 | reports-analytics | `Reporting` | Ticket and SLA performance reports | Phase 2 | 33, 51 |
| 53 | reports-analytics | `Reporting` | Agent performance and satisfaction reports | Phase 2 | 08, 39, 45, 50, 51 |
| 54 | reports-analytics | `Reporting` | Management dashboards and exports | Phase 2 | 26, 36, 51, 52, 53 |
| 55 | integrations-platform | `Integrations` | Public API versioning and API key management | Phase 3 | 03, 09, 11, 15, 37 |
| 56 | integrations-platform | `Integrations` | Outbound webhooks and integration events | Phase 3 | 02, 03, 55 |
| 57 | integrations-platform | `Integrations` | ERP and external system integration | Phase 3 | 03, 11, 32, 55 |
| 58 | integrations-platform | `Organisation` | Custom branding theming and tenant configuration | Phase 3 | 04, 05, 06, 40, 47 |

## Module notes

- A module is created by the first story that needs it; that story's plan scaffolds all five projects from the
  `Sample` template. Story 11 deletes `Sample`.
- **Story 03 delivers the integration-event outbox**, the only cross-module write path. Every story that reacts to
  another module's change (the customer timeline in 13, the audit log in 10, SLA in 33, reporting in 51, webhooks in
  56, and inbound channel messages becoming tickets in 22) depends on it. NN order already places 03 first.
- A story works in the module(s) its Module column names. In any other module it may only add a Contracts member or
  an integration-event handler that reacts to its events. Needing more than that is a sign to split the story.

## Known forward references

Three dependencies point forward in the sequence. They are deliberate, and each has a documented fallback so the
earlier story is never blocked:

- **40 → 58 (branding).** The help centre ships on story 04's default tokens; story 58 replaces them with no code
  change. If it needs a code change, story 04's tokens were bypassed.
- **43 → 45 (retrieval).** Build 45 before 43. If 43 is pulled forward, it must return no-confident-answer rather
  than an ungrounded suggestion.
- **46 → 48 (portal).** The chatbot ships anonymous-only on the help centre first; the authenticated
  own-ticket-status path lands after 48.

## Thin vertical slice

If a demo is needed before the MVP is complete, this is the shortest honest path to a working ticket:

`01 → 02 → 04 → 06 → 07 → 09 → 11 → 15 → 16 → 18 → 20 → 21 → 22 → 23`

A customer emails in, a ticket is created, an agent finds it in the inbox, opens it, and replies. Everything else
is depth on top of that line.

## Workflow

```
/squad-new-story <feature-slug> --title "..."          # scaffold an intake
/squad-plan .squad/stories/<feature>/<slug>/intake.md  # generate the executable plan
/squad-implement .squad/plans/<feature>/NN-story-*.md  # execute it in a fresh scoped session
/squad-status                                          # where the backlog stands
```
