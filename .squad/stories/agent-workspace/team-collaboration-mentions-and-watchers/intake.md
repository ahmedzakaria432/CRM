# Story 31 — Team collaboration mentions and watchers

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Agent Workspace** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/agent-workspace/team-collaboration-mentions-and-watchers/intake.md`.

---

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`
- **Module (backend):** `Workspace`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-31`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `agent-workspace`

---

## Title

```
Team collaboration mentions and watchers
```

---

## Description

Hard tickets need more than one person. This story makes asking for help a first-class action instead of a tap on
the shoulder that leaves no record.

- Mentions in internal notes and in ticket comments: an at-mention picker over agents and teams within scope,
  which notifies the mentioned party through story 36 and creates an item in their dashboard.
- Watchers on a ticket: explicit follow and unfollow, automatic watching when an agent is mentioned, comments, or
  is the previous assignee, with watchers receiving activity notifications without owning the ticket.
- A structured internal discussion thread on the ticket, separate from the customer-facing conversation, with
  reactions and threaded replies so a long debate stays readable.
- Ask for help as a named action: request assistance from a colleague, a team, or another department with a reason
  and an optional deadline, tracked to a resolution so requests do not evaporate.
- Handover notes on reassignment: a required summary when transferring a ticket that has an active conversation,
  so the next agent starts informed rather than reading forty messages.
- Presence and availability: who is online and available now, drawn from story 26's realtime transport and story
  08's availability state, so help is asked of someone who can actually answer.
- Escalation to a supervisor as a collaboration action distinct from the automated escalation rules in story 35.

## Acceptance criteria

- [ ] Mentioning an agent notifies them, appears in their dashboard mentions widget, and adds them as a watcher.
- [ ] Mentioning a team notifies every member of that team within scope, and no one outside it.
- [ ] An agent cannot mention someone who has no access to the ticket's department; they do not appear in the picker.
- [ ] Watchers receive activity notifications and stop receiving them immediately on unfollow.
- [ ] Internal discussion content never appears in any customer-facing channel or portal response.
- [ ] An assistance request records requester, target, reason, and deadline, and is visibly open until resolved.
- [ ] An unresolved assistance request past its deadline is surfaced to the requester and the target, not silently
      forgotten.
- [ ] Reassigning a ticket with an active conversation requires a handover note.
- [ ] Presence reflects both connection state and the availability set in story 08, and updates within seconds.
- [ ] Every collaboration action appears on the ticket activity timeline from story 19 with correct attribution.
- [ ] The mention picker and discussion thread render correctly in `ar` RTL.

## Dependencies

- **Blocked by / related ids:** CRM-18, CRM-19, CRM-26, CRM-36, CRM-08.
- **Depends on code areas or other stories:** internal notes from story 18, the timeline from story 19, realtime
  presence from story 26, notifications from story 36, and availability from story 08.

## Technical hints

- Resolve the mention picker server-side against the caller's scope. A client-side filter over a full agent list
  leaks the org chart.
- Model the assistance request as its own small aggregate with an explicit lifecycle. Treating it as a note means
  it is never closed and never reportable.
- Reuse story 26's SignalR hub for presence rather than adding a second realtime channel.

## Out of scope

- Automated escalation rules. That is story 35.
- A general chat product between agents outside the ticket context.
- Any test project.

---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10 modular monolith. One module per bounded context under `backend/src/Modules/<Module>/` (`Domain` <- `Application` <- `Infrastructure` / `Presentation`, plus a public `Contracts` project), Domain-Driven Design with Clean Architecture inside each module, one host (`Crm.Api`). CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server with one DbContext and schema per module, MVC controllers in per-persona Areas, `Result<T>` returns, RFC 9457 problem details. The clock is read only through `IDateTime`; `System.DateTime` is banned.
- **Module boundaries:** a module reaches another only through that module's `Contracts` - a synchronous API interface for a read, an integration event (via the outbox) for "this happened". No cross-module project references, foreign keys, joins, or shared entities; one command, one module, one transaction. The build fails with `CRM0001` on a forbidden reference. The module catalogue in `CLAUDE.md` names the module each story belongs to.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events inside the module, and as integration events across modules; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
