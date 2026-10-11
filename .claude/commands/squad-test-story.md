---
description: Plan and implement one backend unit-test story (UT-01..UT-05) end to end - planning subagent on the default model, implementation subagent on Sonnet, each in its own context.
---

Plan and implement unit-test story `$ARGUMENTS` (an id such as `UT-03`, or `next`).

You are the **orchestrator**. You do no planning and no coding yourself: you resolve the story, run two subagents
one after the other, check what each hands back, keep `.squad/UNIT-TEST-STORIES.md` current, and report. Keep
your own context small - do not read application or test source.

## 1. Resolve the story

1. Read `.squad/UNIT-TEST-STORIES.md` completely (status table, override rules, working-tree notes, session log).
2. Find the row whose **Id** is `$ARGUMENTS`, or the first row that is not `✅ Done` when `$ARGUMENTS` is `next` or
   empty.
3. Check every row in its **Depends on** column is `✅ Done`. If one is not, stop and say which.
4. Note the intake path (relative to `.squad/`), the id, the plan number `NN`, and the title.
5. If the row's **Plan** column links a plan that exists on disk, skip step 2 and implement that plan.
6. Set the row's status to `🔨 In progress` and add a Session log entry ("<date> — <id> started").

## 2. Plan - default model, own context

Launch one `Agent` (`subagent_type: general-purpose`, **no `model` override**, `run_in_background: false`):

> You are the planning session for <id> (Story NN — <title>) of the CRM repo at D:\CRM. Read `D:\CRM\CLAUDE.md`
> and `D:\CRM\.squad\UNIT-TEST-STORIES.md` first; the tracker's "Override rules" take precedence over CLAUDE.md's
> "no automated tests" policy for this story - writing unit tests is the task. Then follow
> `D:\CRM\.claude\commands\squad-plan.md` exactly, with `$ARGUMENTS` = `.squad/<intake path>`, using plan number
> NN and file `.squad/plans/backend-unit-tests/NN-story-<intake folder name>.md` (title line
> `# Story NN — <title>`). First read the existing story plans listed for this story in the tracker's "Existing plans to mine for test cases" and reuse their invariants, error codes and edge cases as test cases. Then enumerate every production type and handler the story must cover by reading the
> module's source, and list the test classes to write per file, grouped into slices of a manageable size
> (e.g. Domain, Commands, Validators, Queries/Consumers, Infrastructure). Planning is read-only: no builds,
> package installs, or test runs - anything that needs a run goes in the plan as a named risk. Do not commit.
> Finish with the plan file path, a one-paragraph summary, and the list of named risks.

When it returns, confirm the plan file exists and starts with `# Story NN`. If not, set the row to `⛔ Blocked`,
log why, and stop. Otherwise set the row to `📝 Planned` and link the plan in its **Plan** column.

## 3. Implement - Sonnet, own context

Launch one `Agent` (`subagent_type: general-purpose`, **`model: sonnet`**, `run_in_background: false`):

> You are the implementation session for <id> (Story NN — <title>) of the CRM repo at D:\CRM. Read
> `D:\CRM\CLAUDE.md` and `D:\CRM\.squad\UNIT-TEST-STORIES.md` first; the tracker's "Override rules" take
> precedence over the "no automated tests" rule in CLAUDE.md and in squad-implement.md for this story. Then follow
> `D:\CRM\.claude\commands\squad-implement.md`, with `$ARGUMENTS` = `<plan path>`. Verify the plan's named risks
> first. Where the plan points to an existing story plan, consult it for the intended behaviour; the code wins if they differ - log the difference as a Follow-up. Work slice by slice: after each slice, `dotnet test --project tests/<Project>` from `backend/` must pass
> before the next. Change nothing under `backend/src/` (UT-01 may change the build/config/docs files its plan
> names); record production bugs or untestable seams as Follow-ups. Before committing: run
> `dotnet build backend/Crm.sln` (zero warnings) and `dotnet test --solution Crm.sln` from `backend/`; set this
> story's row in `.squad/UNIT-TEST-STORIES.md` to `✅ Done` (or leave it `🔨 In progress` and say why if
> acceptance criteria are not met), decrement "Remaining", and add a Session log entry with test counts and
> Follow-ups. Commit the plan, the `00-overview.md`/`00-index.md` updates, the tracker, and the tests in one
> commit: `Implement Story NN — <title> (<id>)`. If you must stop early, do not commit; write exactly what is
> done and what is left into the Session log first. Finish with files changed, commands run and their results
> (test counts), acceptance criteria met vs not met, and Follow-ups.

## 4. Report

Tell the user, briefly:

- the plan path and the commit hash (`git log --oneline -1`), or that nothing was committed and why;
- build and test results (passed / failed / skipped counts);
- acceptance criteria not met and Follow-ups, verbatim from the implementer;
- the next row to run (`/squad-test-story next`).

If the implementer stopped without updating the tracker, update the row and Session log yourself from its report.

## Rules

- One story per run. Never start the next story.
- Never pass the planning subagent's transcript to the implementer - only the plan path. The plan is the handoff.
- If either subagent fails or stops early, record it in the tracker's Session log, report, and stop; do not
  finish its work in this context.
