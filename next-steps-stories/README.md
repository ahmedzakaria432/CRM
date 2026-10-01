# Next steps — stories deferred past the MVP

These story intakes and plan overviews were moved out of `.squad/` on 2026-09-29, when the backlog was re-scoped to
a 2-day MVP (customers, tickets, the portal, bilingual RTL, responsive, multi-department/branch, branding). They are
**not planned or built now**. The MVP list is [`../.squad/MVP-STORIES.md`](../.squad/MVP-STORIES.md); the rationale
and merge map are in [`../.squad/BACKLOG.md`](../.squad/BACKLOG.md), "MVP re-scope".

The layout mirrors `.squad/` so a story can be moved back unchanged:

```
next-steps-stories/
  stories/<feature>/<slug>/intake.md   44 intakes
  plans/<feature>/00-overview.md       overviews of the six fully deferred features
```

## Two kinds of story here

- **Merged** — part of the story's scope was folded into an MVP story; the rest waits here. Each intake starts with
  a "merged into Story NN" banner. Before building one later, trim it to only what its host story did not deliver
  (the host intake's "Out of scope (deferred post-MVP)" list says what that is).

  | NN | Story | Merged into |
  |----|-------|-------------|
  | 08 | User and role management | 07 |
  | 09 | Permission model and policy based authorisation | 07 |
  | 12 | Customer notes and attachments | 11 (notes) |
  | 13 | Interaction history and unified customer timeline | 14 |
  | 16 | Ticket taxonomy categories priorities and types | 15 |
  | 17 | Ticket assignment ownership and queues | 15 |
  | 19 | Ticket history and activity timeline | 18 |
  | 21 | Ticket detail workspace UI | 20 |
  | 48 | Portal ticket submission and tracking | 47 |
  | 49 | Portal request history and self service knowledge access | 47 |

> **2026-10-01:** 10, 22–40, 50 and 51–54 moved back to `.squad/` (see `.squad/MVP-STORIES.md`, "Post-MVP build order"). Still here: AI 41–46, Integrations 55–57, and the merged stories.

- **Deferred** — untouched by the MVP: 10 (audit log), 22–27 (channels), 28–31 (agent workspace), 32–36 (SLA and
  automation), 37–40 (knowledge base), 41–46 (AI), 50 (surveys), 51–54 (reports), 55–57 (integrations). Each
  intake starts with a "deferred post-MVP" banner.

## Bringing a story back

1. `git mv next-steps-stories/stories/<feature>/<slug> .squad/stories/<feature>/<slug>` (and its plan overview
   folder from `plans/` if the whole feature returns).
2. Remove the banner, trim a merged intake to its remaining scope, and update its "Plan it with" line back to
   `.squad/stories/...`.
3. Add the row back to `.squad/BACKLOG.md` and `.squad/MVP-STORIES.md` (or a post-MVP list), then `/squad-plan` it.
