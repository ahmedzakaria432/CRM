---
description: Generate an implementation plan from a squad-kit story intake file.
---

You are generating an agent-executable implementation plan for this project using the squad-kit workflow.

## Inputs

- **Intake file path:** `$ARGUMENTS`
  - If empty, ask the user for the path to a story intake file (under `.squad/stories/`).
- **Meta-prompt:** `generate-plan.md` from the **installed squad-kit package** (`templates/prompts/` — not under `.squad/`; locate via `npm root -g`/your package manager under `squad-kit`, or run `squad new-plan <intake> --copy` to print the composed meta-prompt). Follow it exactly.
- **Project config:** `.squad/config.yaml` — read `project.projectRoots`, `tracker.type`, `naming.includeTrackerId`, `naming.globalSequence`.

## Steps

1. Read `generate-plan.md` from the installed squad-kit package completely. Treat it as your operating instructions for structure, tone, and output rules.
2. Read the intake file at `$ARGUMENTS`, plus any files in its `attachments/` directory that the intake references.
3. Read one or two existing plan files under `.squad/plans/` (if any) to match established tone. If none exist, use `story-skeleton.md` from the same `templates/prompts/` directory in the squad-kit package as the structural reference.
4. Determine the next sequence number by scanning `.squad/plans/**/NN-story-*.md` for the highest `NN` (global sequence) or the highest within the target feature folder (per-feature sequence), per `config.yaml.naming.globalSequence`.
5. Write the plan file to `.squad/plans/<feature-slug>/NN-story-<slug>[-<id>].md`.
6. Update `.squad/plans/<feature-slug>/00-overview.md` with the new story row.
7. If this is a new feature slug, add it to `.squad/plans/00-index.md`.

## Rules

- **Planning only.** Do not modify application source code in this session.
- **No scratch builds or probe runs.** Do not copy the app into a scratch folder, install packages, start a dev
  server or the API, run a build, or drive a browser. Plan from reading the repo, lockfiles, `package.json`/`.csproj`,
  and package docs. Anything that can only be proven by running it goes in the plan as a named risk with the
  verification step the implementation session must run first.
- **Concrete over clever.** File paths, line ranges, type names, function signatures, verification commands. No "consider" or "might".
- **Name the module.** State which backend module(s) the story touches (the intake's Module line, the catalogue in
  `CLAUDE.md`), put every file path under `backend/src/Modules/<Module>/`, and express cross-module work only as a
  Contracts API call or an integration event. A plan that needs a forbidden project reference is a wrong plan.
- **Respect existing conventions.** Read neighbouring plan files before inventing a new pattern.
- If a plan file already starts with `<!-- squad-kit:`, **never change or remove that first line** when editing. New plans start with `# Story NN — …` per `generate-plan.md`; do not invent the API metadata comment. In later implementation sessions, treat the plan file as read-only unless the user explicitly asks to revise the plan.
- Report back the path(s) you wrote and a one-line summary per file.

## Do not commit the plan

A plan is saved with its implementation, never committed on its own. Leave the plan file and the `00-overview.md`
/ `00-index.md` updates uncommitted in the working tree; `/squad-implement` commits them in the same commit as
the code. Record in the plan itself (not a commit body) what was checked during planning (tool and package
versions read from lockfiles or docs) and any decision a later story must not silently reverse (pinned versions,
licensing constraints, restated acceptance criteria).
