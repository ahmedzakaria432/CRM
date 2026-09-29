---
description: Execute a planned squad-kit story file end to end.
---

Implement the story plan at `$ARGUMENTS` (a file under `.squad/plans/<feature>/NN-story-*.md`).

## Inputs

- **Plan file:** `$ARGUMENTS`. If empty, ask which plan to execute.
- **Conventions:** `CLAUDE.md` at the repo root is binding for architecture, DDD rules, naming, and verification.

## Steps

1. Read the plan file completely. Treat it as read-only - do not edit the plan while implementing.
2. Read the story's `intake.md` (same feature/slug under `.squad/stories/`) for acceptance criteria.
3. Restate the plan's task list as a todo list, then work it in order.
4. Verify after each vertical slice, not once at the end.
5. Backend done means: `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration added if the
   schema moved, and every new endpoint exercised through `backend/requests/<feature>.http`.
6. Frontend done means: `pnpm typecheck`, `pnpm lint`, and `pnpm build` clean, and the screen walked in the
   browser in both `en` (LTR) and `ar` (RTL).

## Rules

- **No automated tests.** This project has no test projects and no Vitest suites - see the testing policy in
  `CLAUDE.md`. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not
  add a test framework to any project file. Verification is compiler + manual walkthrough.
- Never silence an analyzer, and never reach for `!`, `any`, or a ts-ignore to make something compile. With no
  test net, the compiler is the only automated check there is.
- Enforce invariants in the aggregate, not in the handler. That is where a unit test's assertions would have gone.
- Stay inside the plan's scope. Anything the plan did not ask for goes in a **Follow-ups** list at the end,
  not into the diff.
- Run the plan's named risks' verification steps first, before building on them.
- **Commit the plan with its implementation.** One commit per story holds the plan file, the `00-overview.md` /
  `00-index.md` updates, and the code: `git add .squad/plans/<feature-slug>/ <changed paths>` then
  `git commit -m "Implement Story NN — <title>"`. If the plan was already committed on its own, still commit the
  implementation — never rewrite history to merge them.
- Finish with: files changed, commands run and their results, what you verified by hand, and acceptance criteria
  met vs not met.
