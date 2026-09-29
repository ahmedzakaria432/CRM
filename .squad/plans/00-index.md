# Plans index

One row per feature folder under `.squad/plans/`. `NN` is a **global execution sequence** across all features
(`naming.globalSequence: true` in `config.yaml`), so `NN` order is the build order for the whole project.

## Feature order — plan and build top to bottom

> **MVP re-scope (2026-09-29):** the 2-day MVP is customers, tickets, and the portal, plus bilingual RTL,
> responsive, multi-department/branch, and branding. Plan and build only the MVP stories, in this order:
> **05, 06, 07, 11, 14, 15, 18, 20, 47, 58**. Features marked **Deferred** are not planned now. Details and the
> merge map: [`.squad/BACKLOG.md`](../BACKLOG.md), "MVP re-scope".

Plan features in this order. Do not start planning a feature until the features above it are planned, because
every later feature's intake references contracts the earlier ones establish.

| # | Feature | Slug | Stories (NN) | Phase | Why it sits here |
|---|---------|------|--------------|-------|------------------|
| 1 | Platform Foundation | `platform-foundation` | 01–06 | MVP | Nothing compiles or runs before this. Every other story depends on the solution skeleton, persistence baseline, RTL shell, and org scoping. |
| 2 | Identity and Access | `identity-access` | 07 (08, 09 merged in; 10 deferred) | MVP | Every endpoint from here on declares a permission against story 09's catalogue and is scoped by story 06. |
| 3 | Customer Management | `customer-management` | 11, 14 (12 notes and 13 merged in) | MVP | A ticket cannot exist without a customer. The `Customer` aggregate is the first real domain model. |
| 4 | Ticket Management | `ticket-management` | 15, 18, 20 (16, 17, 19, 21 merged in) | MVP | The core aggregate of the product. Channels, SLA, AI, portal, and reports all read or write it. |
| 5 | Communication Channels | `communication-channels` | 22–27 | **Deferred** | Needs the ticket and message model to write into. Story 22's pipeline must land before any single channel. |
| 6 | Agent Workspace | `agent-workspace` | 28–31 | **Deferred** | The agent's daily surface, assembled over tickets, queues, and the realtime transport from story 26. |
| 7 | SLA and Automation | `sla-automation` | 32–36 | **Deferred** | Needs branch calendars (06), the ticket status machine (15), and channels (23, 25) to notify through. |
| 8 | Knowledge Base | `knowledge-base` | 37–40 | **Deferred** | Independent content model, but its agent surface plugs into the story 21 composer, so it follows tickets. |
| 9 | AI Features | `ai-features` | 41–46 | **Deferred** | Reads the ticket thread, the taxonomy, and the knowledge base. Nothing here works until 15–18, 16, and 37–38 exist. |
| 10 | Customer Portal | `customer-portal` | 47 (48, 49 merged in; 50 deferred) | **MVP** | Reuses identity (07), tickets (15–18), and the help centre (40). Its read model must be built after they settle. |
| 11 | Reports and Analytics | `reports-analytics` | 51–54 | **Deferred** | Projects over everything operational. Building it early means rebuilding it. |
| 12 | Integrations and Platform | `integrations-platform` | 58 only (55–57 deferred) | **MVP** (58) | Publishes a contract over the finished domain, and story 58 themes every surface the previous eleven features built. |

The full story-level map, with per-story dependencies and phases, is in [`.squad/BACKLOG.md`](../BACKLOG.md).

## Planning workflow

```
/squad-plan .squad/stories/<feature-slug>/<story-slug>/intake.md
```

Plan stories in `NN` order within a feature, and features in the order above. Each plan lands at
`.squad/plans/<feature-slug>/NN-story-<slug>.md` and its row is added to that feature's `00-overview.md`.

## Modular monolith

The backend is a modular monolith (see `CLAUDE.md`). Every plan names the backend module(s) its story touches,
using the Module line in the intake and the module catalogue. If the story creates a module, the plan's first task
scaffolds its five projects from `backend/src/Modules/Sample/` (or from the first real module once Story 11 deletes
Sample). Cross-module work must be a Contracts API call or an integration event, never a direct project reference.
