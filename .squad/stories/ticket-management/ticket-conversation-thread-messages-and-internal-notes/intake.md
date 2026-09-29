# Story 18 — Ticket conversation thread messages internal notes and history

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ticket-management/ticket-conversation-thread-messages-and-internal-notes/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** `Tickets`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-18`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket conversation thread messages internal notes and history
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs story 19 (history)** as the unified
thread + activity feed. Plain-text bodies only; attachments and channel delivery are deferred.

---

## Description

The conversation itself: what the customer said, what the agent replied, and what the team said privately.

- `TicketMessage` child entity of `Ticket`: direction (inbound from customer, outbound from agent), author (contact
  id or agent id), plain-text body (max 10,000 chars, trimmed, no HTML), occurred-at.
- `TicketNote` child entity: internal-only, author agent id, body.
- Visibility enforced at the query layer: **public** (messages), **internal** (notes), **system** (the
  `TicketActivity` entries from story 15). Portal queries (story 47) can only ever return public entries.
- Aggregate methods: `ReplyAsAgent`, `AddInternalNote`, `ReceiveCustomerReply` (used by the portal in 47). An agent
  reply on a New/Open ticket records first-response time; a customer reply on Pending customer moves it back to Open;
  a reply to a Closed ticket is rejected, a reply to a Resolved ticket reopens it.
- A thread query returning messages, notes, and activity merged in chronological order, cursor-paged, with each
  activity entry as a template key + parameters that the frontend renders in the active locale.
- Integration event `TicketMessageAdded` (public messages only) in `Tickets.Contracts`.
- Endpoints in the Agent Area: get thread, reply, add note. `backend/requests/ticket-thread.http`.

## Acceptance criteria

- [ ] An agent reply, a customer reply, and an internal note each append exactly one entry of the right visibility.
- [ ] Internal notes never appear in the public-visibility query, verified from the `.http` file.
- [ ] First response time is recorded on the first agent reply only.
- [ ] Customer reply on Pending customer returns the ticket to Open; a reply on Closed is rejected with 422.
- [ ] The thread is chronological, cursor-paged, and adding an entry mid-read never duplicates or skips one.
- [ ] Activity entries are stored as template key + parameters and render in `en` and `ar` (keys added to the
      locale files).
- [ ] Bodies over the limit or empty are rejected by validation (400) with a field error.
- [ ] Every path exercised from `backend/requests/ticket-thread.http`.

## Dependencies

- **Blocked by / related ids:** CRM-15.
- **Depends on code areas or other stories:** the ticket aggregate and `TicketActivity` from story 15.

## Technical hints

- Render plain text with preserved line breaks on the frontend (`white-space: pre-wrap` via a utility), never
  `v-html`.

## Out of scope (deferred post-MVP)

- HTML bodies and sanitisation, attachments (story 12), outbound delivery over email/WhatsApp/SMS (22–27), delivery
  status, reply-all/forward, mentions, drafts that survive refresh, field-level change history for every attribute,
  derived durations. Any test project.

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
