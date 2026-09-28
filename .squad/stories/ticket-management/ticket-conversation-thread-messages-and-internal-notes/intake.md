# Story 18 — Ticket conversation thread messages and internal notes

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
Ticket conversation thread messages and internal notes
```

---

## Description

The conversation itself: everything the customer said, everything the agent replied, and everything the team said
privately. This is the model the channel adapters in stories 22 to 27 write into.

- `TicketMessage` as a child entity of the ticket aggregate: direction (inbound or outbound), channel, author as
  either a contact or an agent, body in both plain text and sanitised HTML, attachments referencing story 12's
  storage, delivery status, external message id, in-reply-to, and occurred-at.
- `TicketNote` as a child entity: internal-only, never visible on any portal or outbound channel, with mentions.
- A visibility model with exactly three values, enforced at the query layer rather than in the UI: public
  (customer-visible), internal (staff only), and system (generated activity entries).
- HTML sanitisation on the way in for inbound provider content and on the way out for rendering, with a strict
  allow-list. Quoted-reply and signature trimming so a five-deep email chain does not re-render the whole history.
- Outbound composition: reply, reply-all, forward, and a private note, with attachment support, the agent's
  localised signature from story 08, and a draft that survives navigation and a browser refresh.
- Delivery state per outbound message: queued, sent, delivered, read, failed with a provider reason, and a
  bounded retry, surfaced in the thread rather than hidden in logs.
- Idempotent inbound handling: the same provider message id delivered twice appends one message.
- Frontend: a threaded conversation view with public and internal turns visually distinct, collapsed quoted
  history, attachment previews, and a composer with channel selection.

## Acceptance criteria

- [ ] An internal note is never returned by any portal endpoint or included in any outbound channel payload,
      verified by requesting the thread with a portal token.
- [ ] A public reply and an internal note are visually unmistakable in the thread, in both locales.
- [ ] Inbound HTML containing a script tag, an inline event handler, or a javascript URL is stored and rendered
      inert.
- [ ] The same provider message id delivered twice produces exactly one message.
- [ ] An outbound message shows queued, then sent, then delivered, and a failure shows the provider reason with a
      retry action.
- [ ] The first outbound public reply stamps the ticket's first-response timestamp exactly once.
- [ ] Quoted history and signatures are trimmed from inbound email so the thread stays readable at ten replies.
- [ ] A composer draft survives navigating away and a browser refresh, and is discarded on send.
- [ ] Attachments on a message reuse story 12's storage, honour the same content-type and size checks, and are
      downloadable only by callers with scope and permission.
- [ ] Messages cannot be edited after sending; a correction is a new message, and the aggregate enforces it.
- [ ] A thread with 200 messages pages without loading all of them at once.
- [ ] Both the XSS payload case and the duplicate-delivery case are exercised from
      `backend/requests/tickets-messages.http`.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-12.
- **Depends on code areas or other stories:** the ticket aggregate from story 15 and file storage from story 12.
  Stories 22 to 27 write into this model; stories 42 and 43 read from it.

## Technical hints

- Sanitise on write and on render. Provider content changes shape without warning, and there is no test suite to
  catch a regression in the sanitiser.
- Store both the raw provider payload and the sanitised body. Debugging a channel bug without the raw payload is
  guesswork.
- Make inbound handling idempotent on the provider message id with a unique index, not with a lookup-then-insert.

## Out of scope

- Channel transport itself. Those are stories 22 to 27.
- Quick replies and canned responses. That is story 30.
- AI-suggested replies. That is story 43.
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
