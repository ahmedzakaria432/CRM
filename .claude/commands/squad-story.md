---
description: Plan and implement one MVP story end to end - planning subagent on the default model, implementation subagent on Sonnet, each in its own context.
---

Plan and implement story `$ARGUMENTS` (a story number such as `05`, or `next`).

You are the **orchestrator**. You do no planning and no coding yourself: you resolve the story, run two subagents
one after the other, check what each hands back, and report. Keep your own context small - do not read application
source.

## 1. Resolve the story

1. Read `.squad/MVP-STORIES.md`. Find the row whose `NN` is `$ARGUMENTS`, or the first row that is not `✅ Done`
   when `$ARGUMENTS` is `next` or empty.
2. Check every story in its **Depends on** column is `✅ Done`. If one is not, stop and say which.
3. Note the intake path (the row's **Intake** link, relative to `.squad/`), the story number `NN` and the title.
4. If the row already links a plan that exists on disk, skip step 2 and implement that plan.

## 2. Plan - default model, own context

Launch one `Agent` (`subagent_type: general-purpose`, **no `model` override**, `run_in_background: false`) with
this prompt, filled in:

> You are the planning session for Story NN — <title> of the CRM repo at D:\CRM. Read `D:\CRM\CLAUDE.md` first; it
> is binding. Then follow `D:\CRM\.claude\commands\squad-plan.md` exactly, with `$ARGUMENTS` =
> `.squad/<intake path>`. Planning is read-only: no scratch builds, package installs, dev servers, API runs, or
> browser probes - anything that needs a run to prove goes in the plan as a named risk. Do not commit. Finish with
> the plan file path, a one-paragraph summary, and the list of named risks.

When it returns, confirm the plan file exists under `.squad/plans/` and starts with `# Story NN`. If it does not,
stop and report - do not implement without a plan.

## 3. Implement - Sonnet, own context

Launch one `Agent` (`subagent_type: general-purpose`, **`model: sonnet`**, `run_in_background: false`) with this
prompt, filled in:

> You are the implementation session for Story NN — <title> of the CRM repo at D:\CRM. Read `D:\CRM\CLAUDE.md`
> first; it is binding. Then follow `D:\CRM\.claude\commands\squad-implement.md` exactly, with `$ARGUMENTS` =
> `<plan path>`. Verify the plan's named risks first. Before committing, set this story's row in
> `.squad/MVP-STORIES.md` to `✅ Done` (or leave it and say why if acceptance criteria are not met) and link the
> plan in its **Plan** column. Commit the plan, the overview/index updates, the MVP-STORIES row, and the code in
> one commit: `Implement Story NN — <title>`. Finish with files changed, commands run and their results, what you
> verified by hand, acceptance criteria met vs not met, and Follow-ups.

## 4. Report

Tell the user, briefly:

- the plan path and the commit hash (`git log --oneline -1`);
- build/typecheck/lint results and the manual verification the implementer did;
- acceptance criteria not met and Follow-ups, verbatim from the implementer;
- anything a subagent could not do (for example, a browser walkthrough it could not run), stated plainly.

## Rules

- One story per run. Never start the next story.
- Never pass the planning subagent's transcript to the implementer - only the plan path. The plan is the handoff.
- If either subagent fails or stops early, report what it said and stop; do not finish its work in this context.
