---
description: Show the squad-kit backlog state - stories, plans, and what is next in order.
---

Report the current state of the squad-kit workspace and what to plan next.

## Steps

1. Run `squad status` and `squad list`.
2. Read `.squad/plans/00-index.md` for the **feature planning order** (feature 1 of 12 through 12 of 12).
3. Read `.squad/BACKLOG.md` for the story map: global `NN` sequence, phase, and per-story dependencies.
4. Cross-check: for every story folder under `.squad/stories/<feature>/<slug>/`, report whether a matching plan
   exists under `.squad/plans/<feature>/NN-story-*.md`.
5. Output a compact table: `NN | feature (order #) | story | phase | intake | plan | blocked-by`.
6. End with **Next up**: the 3 lowest-`NN` unplanned stories whose dependencies are all already planned, and the
   exact `/squad-plan <path>` command for each.

## Rules

- Read-only. Do not create or modify stories, plans, or source.
- Respect the order. `NN` is a global sequence and features are planned in the `00-index.md` order - do not
  recommend a story whose dependencies are unplanned, even if it looks easy.
- If `.squad/BACKLOG.md`, the per-feature `00-overview.md` files, and the story folders disagree, say so
  explicitly instead of guessing which is right.
