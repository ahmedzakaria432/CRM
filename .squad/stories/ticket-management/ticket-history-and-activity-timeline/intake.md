# Story 19 — Ticket history and activity timeline

> **MVP re-scope (2026-09-29): merged into Story 18.** Its MVP-relevant scope now lives in that story's intake; the rest is deferred post-MVP. Do not plan this intake on its own. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-history-and-activity-timeline/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** `Tickets`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-19`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket history and activity timeline
```

---

## Description

A complete, readable account of what happened to a ticket and who did it. Support disputes, SLA arguments, and
post-incident reviews all resolve here, and on a project without automated tests it is also the fastest way to
understand why a ticket is in an unexpected state.

- Field-level change tracking for every ticket attribute that matters: status, priority, category, assignee,
  team, department, due date, tags, and custom field values, each with actor, before, after, and timestamp.
- A unified activity feed merging field changes, messages, notes, assignments, SLA events, escalations,
  automation actions, and AI actions into one chronological stream, distinguishing a human actor from a rule from
  an AI suggestion that a human accepted.
- Localised, template-driven entry rendering so the same history reads correctly in Arabic and English, stored as
  a template key plus parameters rather than as pre-rendered text.
- Filtering by entry type and actor, and a compact mode showing only status and assignment changes for a long
  ticket.
- Derived durations exposed on the ticket: time to first response, time in each status, total handling time, and
  waiting-on-customer time excluded from the SLA, all computed from the history rather than stored ad hoc.
- Automation transparency: an entry created by a rule names the rule and links to it, so a surprising change is
  traceable to its cause rather than to a mystery.

## Acceptance criteria

- [ ] Every tracked field change produces exactly one history entry with actor, before, after, and timestamp; a
      no-op save produces none.
- [ ] Changes made by an automation rule are attributed to the rule by name and link, not to a system user.
- [ ] Changes originating from an accepted AI suggestion are attributed to both the AI and the accepting agent.
- [ ] The activity feed interleaves all entry types in true chronological order, including entries produced
      inside the same transaction.
- [ ] Every entry renders correctly in `ar` and `en` from the same stored data, with no pre-rendered text.
- [ ] Time in status, time to first response, and waiting-on-customer time are derived from history and match a
      hand calculation on a seeded ticket with several status changes.
- [ ] Waiting-on-customer periods are excluded from SLA-relevant durations, consistent with story 33.
- [ ] Filtering by entry type and by actor narrows the feed, and compact mode shows only status and assignment
      entries.
- [ ] History is read-only through every API surface; no endpoint can amend or delete an entry.
- [ ] A ticket with 500 history entries renders the feed paged, without loading all of them.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-10, CRM-18.
- **Depends on code areas or other stories:** the ticket aggregate from story 15, the audit infrastructure and
  history component from story 10, and messages from story 18. Stories 33, 35, and 52 write entries here.

## Technical hints

- Derive durations from the history stream rather than maintaining counters on the ticket. Counters drift; a
  derivation can be recomputed.
- Reuse story 10's audit capture for field changes rather than writing a second change-tracking mechanism, and
  keep this story to the presentation projection over it.
- Attribute automation and AI actions explicitly from the start. Retrofitting attribution once rules are live is
  painful and the reports in story 52 will need it.

## Out of scope

- The general audit log across all entities. That is story 10.
- SLA calculation itself. That is story 33; this story only exposes the durations it needs.
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
