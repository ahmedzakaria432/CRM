# Story 21 — Ticket detail workspace UI

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-detail-workspace-ui/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** none (frontend); reads `Tickets` and `Customers`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-21`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket detail workspace UI
```

---

## Description

The single screen where a ticket is actually worked. It has to hold the conversation, the customer context, the
ticket properties, and the available actions without becoming unusable, in both reading directions.

- A three-region layout: the conversation thread from story 18 in the centre, ticket properties and actions in a
  side panel, and customer context (profile summary, open tickets, recent interactions from story 13) in another,
  collapsible on narrow viewports and correctly mirrored in RTL.
- A header with ticket number, subject, status, priority, SLA countdown, assignee, and the primary actions,
  staying visible while the thread scrolls.
- Inline property editing with optimistic UI and rollback on failure, where an illegal transition rejected by the
  aggregate surfaces as a clear localised explanation of why, not as a generic error.
- A status and action bar exposing only the transitions the aggregate currently allows and the caller is
  permitted to perform, so an illegal action is not offered in the first place.
- A composer supporting reply, internal note, and forward, with channel selection, attachments, quick replies
  from story 30, AI-suggested replies from story 43, and a draft that survives navigation.
- Tabs for activity history from story 19, custom fields from story 16, related and child tickets, and
  attachments.
- Collision awareness: an indicator showing another agent is viewing or replying, to prevent duplicate answers.
- Keyboard shortcuts for the frequent actions, and a next-ticket action that keeps an agent in flow.

## Acceptance criteria

- [ ] The action bar offers only transitions that are currently legal for the ticket and permitted for the
      caller; an illegal transition is never offered.
- [ ] An action rejected by the aggregate shows a localised explanation naming the reason, and the optimistic UI
      rolls back cleanly.
- [ ] Inline edits to priority, category, assignee, and custom fields save individually without a page reload.
- [ ] The composer supports reply, internal note, and forward, retains a draft across navigation and refresh, and
      discards it on send.
- [ ] Internal notes are visually unmistakable from public replies, and the composer makes the current visibility
      obvious before sending.
- [ ] The SLA countdown in the header updates live and matches the value in the inbox for the same ticket.
- [ ] The collision indicator appears within seconds when a second agent opens the same ticket.
- [ ] Every tab loads independently; a failing tab shows an error state with retry inside itself.
- [ ] The whole screen is operable by keyboard, and next-ticket moves to the next ticket in the active view.
- [ ] The layout is walked in `ar` RTL at 375px, 768px, and 1280px with the panels mirrored correctly.
- [ ] Loading, empty, and error states exist for the thread, the customer panel, and every tab.

## Dependencies

- **Blocked by / related ids:** CRM-14, CRM-18, CRM-19, CRM-20.
- **Depends on code areas or other stories:** the conversation model from story 18, history from story 19, the
  customer panels from story 14, and the inbox from story 20. Stories 30, 31, and 43 plug into the composer.

## Technical hints

- Derive the action bar from a server-provided list of allowed transitions rather than reimplementing the status
  machine in TypeScript. Two copies of a state machine will diverge, and nothing here will catch it.
- Persist the composer draft in local storage keyed by ticket and visibility, so a public reply draft is never
  sent as an internal note by accident.
- Reuse story 20's real-time transport for collision awareness rather than adding a second channel.

## Out of scope

- Quick replies, AI suggestions, and collaboration features as features. Those are stories 30, 43, and 31; this
  story provides their attachment points.
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
