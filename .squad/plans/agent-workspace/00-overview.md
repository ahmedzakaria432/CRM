# agent-workspace — plan overview

Entry point for the **Agent Workspace** feature. This is **feature 6 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Workspace` (whichever of 28–31 is implemented first). See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 28 | [`agent-dashboard`](../../stories/agent-workspace/agent-dashboard/intake.md) | Agent dashboard | _not planned yet_ | CRM-17, CRM-20, CRM-26, CRM-29, CRM-31 |
| 29 | [`tasks-and-reminders`](../../stories/agent-workspace/tasks-and-reminders/intake.md) | Tasks and reminders | _not planned yet_ | CRM-15, CRM-08, CRM-36 |
| 30 | [`quick-replies-and-canned-responses`](../../stories/agent-workspace/quick-replies-and-canned-responses/intake.md) | Quick replies and canned responses | _not planned yet_ | CRM-18, CRM-21, CRM-12 |
| 31 | [`team-collaboration-mentions-and-watchers`](../../stories/agent-workspace/team-collaboration-mentions-and-watchers/intake.md) | Team collaboration mentions and watchers | _not planned yet_ | CRM-18, CRM-19, CRM-26, CRM-36, CRM-08 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._
