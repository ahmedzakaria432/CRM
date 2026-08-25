# CRM backlog — story map

58 stories across 12 features, derived from `azm_squad_customer_support_crm.pdf`. `NN` is a global execution
sequence: **build in NN order.** Feature planning order is in [`plans/00-index.md`](plans/00-index.md).

- **Backend:** .NET 10, DDD inside Clean Architecture, CQRS with MediatR, EF Core 10 on SQL Server.
- **Frontend:** Nuxt 4, Vue 3, TypeScript strict, Tailwind, bilingual `ar` / `en` with RTL.
- **No automated tests.** Deliberate project decision — see the testing policy in `CLAUDE.md`.

## Phases

| Phase | Stories | What it buys |
|-------|---------|--------------|
| **MVP** | 01–40 | A support team can actually run on it: customers, tickets, every channel, SLA, automation, agent workspace, knowledge base. |
| **Phase 2** | 41–54 | AI assistance, customer self-service portal, and the reporting layer. |
| **Phase 3** | 55–58 | Outward-facing contract: public API, webhooks, ERP, white-labelling. |

## Stories

| NN | Feature | Story | Phase | Depends on |
|----|---------|-------|-------|------------|
| 01 | platform-foundation | Backend solution scaffold with DDD and Clean Architecture | MVP | — |
| 02 | platform-foundation | Persistence baseline with auditing and soft delete | MVP | 01 |
| 03 | platform-foundation | Cross cutting concerns error handling logging and caching | MVP | 01, 02 |
| 04 | platform-foundation | Frontend scaffold with Nuxt TypeScript and Tailwind | MVP | 01 |
| 05 | platform-foundation | Bilingual Arabic and English support with RTL layout | MVP | 03, 04 |
| 06 | platform-foundation | Multi department and multi branch organisation scoping | MVP | 02, 04 |
| 07 | identity-access | Authentication with JWT refresh tokens and password reset | MVP | 02, 04, 06 |
| 08 | identity-access | User and role management | MVP | 06, 07 |
| 09 | identity-access | Permission model and policy based authorisation | MVP | 06, 08 |
| 10 | identity-access | Audit log | MVP | 02, 03, 09 |
| 11 | customer-management | Customer aggregate profiles and contact details | MVP | 02, 06, 09 |
| 12 | customer-management | Customer notes and attachments | MVP | 11 |
| 13 | customer-management | Interaction history and unified customer timeline | MVP | 02, 11 |
| 14 | customer-management | Customer 360 workspace UI | MVP | 04, 05, 11, 12, 13 |
| 15 | ticket-management | Ticket aggregate and lifecycle | MVP | 02, 06, 09, 11 |
| 16 | ticket-management | Ticket taxonomy categories priorities and types | MVP | 09, 15 |
| 17 | ticket-management | Ticket assignment ownership and queues | MVP | 06, 08, 15 |
| 18 | ticket-management | Ticket conversation thread messages and internal notes | MVP | 12, 15 |
| 19 | ticket-management | Ticket history and activity timeline | MVP | 10, 15, 18 |
| 20 | ticket-management | Ticket inbox UI with filters and bulk actions | MVP | 04, 05, 15, 16, 17 |
| 21 | ticket-management | Ticket detail workspace UI | MVP | 14, 18, 19, 20 |
| 22 | communication-channels | Channel abstraction and inbound message pipeline | MVP | 11, 15, 16, 18 |
| 23 | communication-channels | Email channel inbound and outbound | MVP | 22 |
| 24 | communication-channels | WhatsApp Business channel | MVP | 22 |
| 25 | communication-channels | SMS channel | MVP | 22 |
| 26 | communication-channels | Live chat with realtime agent console | MVP | 12, 15, 18, 22 |
| 27 | communication-channels | Web form intake and public submission endpoint | MVP | 12, 16, 22 |
| 28 | agent-workspace | Agent dashboard | MVP | 17, 20, 26, 29, 31 |
| 29 | agent-workspace | Tasks and reminders | MVP | 08, 15, 36 |
| 30 | agent-workspace | Quick replies and canned responses | MVP | 12, 18, 21 |
| 31 | agent-workspace | Team collaboration mentions and watchers | MVP | 08, 18, 19, 26, 36 |
| 32 | sla-automation | SLA policies response and resolution targets | MVP | 06, 15, 16 |
| 33 | sla-automation | SLA clock breach detection and at risk alerts | MVP | 15, 19, 32 |
| 34 | sla-automation | Automatic assignment and routing rules | MVP | 08, 16, 17 |
| 35 | sla-automation | Escalation and business rules engine | MVP | 29, 33, 34, 36 |
| 36 | sla-automation | Notifications and alerts | MVP | 23, 25, 26, 33 |
| 37 | knowledge-base | Knowledge base content model and authoring workflow | MVP | 09, 12, 16, 36 |
| 38 | knowledge-base | Bilingual knowledge base search | MVP | 37 |
| 39 | knowledge-base | Agent facing knowledge base and reply insertion | MVP | 21, 30, 37, 38 |
| 40 | knowledge-base | Public help centre and FAQ experience | MVP | 27, 37, 38, 58 |
| 41 | ai-features | AI service abstraction prompt infrastructure and guardrails | Phase 2 | 03, 09 |
| 42 | ai-features | AI ticket summarisation | Phase 2 | 18, 21, 41 |
| 43 | ai-features | AI suggested replies | Phase 2 | 21, 30, 39, 41, 45 |
| 44 | ai-features | AI automatic categorisation priority and language detection | Phase 2 | 16, 22, 34, 41 |
| 45 | ai-features | AI suggested solutions with knowledge base retrieval | Phase 2 | 15, 37, 38, 41 |
| 46 | ai-features | AI chatbot for portal and chat channels | Phase 2 | 26, 40, 41, 45, 48 |
| 47 | customer-portal | Portal authentication registration and account | Phase 2 | 04, 07, 11, 36 |
| 48 | customer-portal | Portal ticket submission and tracking | Phase 2 | 12, 15, 16, 18, 38, 47 |
| 49 | customer-portal | Portal request history and self service knowledge access | Phase 2 | 12, 13, 40, 48 |
| 50 | customer-portal | Customer satisfaction surveys and feedback | Phase 2 | 15, 35, 36, 41 |
| 51 | reports-analytics | Reporting foundation and read models | Phase 2 | 15, 19, 33, 50 |
| 52 | reports-analytics | Ticket and SLA performance reports | Phase 2 | 33, 51 |
| 53 | reports-analytics | Agent performance and satisfaction reports | Phase 2 | 08, 39, 45, 50, 51 |
| 54 | reports-analytics | Management dashboards and exports | Phase 2 | 26, 36, 51, 52, 53 |
| 55 | integrations-platform | Public API versioning and API key management | Phase 3 | 03, 09, 11, 15, 37 |
| 56 | integrations-platform | Outbound webhooks and integration events | Phase 3 | 02, 03, 55 |
| 57 | integrations-platform | ERP and external system integration | Phase 3 | 03, 11, 32, 55 |
| 58 | integrations-platform | Custom branding theming and tenant configuration | Phase 3 | 04, 05, 06, 40, 47 |

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
