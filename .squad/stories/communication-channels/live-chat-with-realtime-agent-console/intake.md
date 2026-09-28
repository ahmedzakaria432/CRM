# Story 26 — Live chat with realtime agent console

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/communication-channels/live-chat-with-realtime-agent-console/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`
- **Module (backend):** `Communication`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-26`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
Live chat with realtime agent console
```

---

## Description

Real-time chat between a website visitor and an agent, with the transport that the inbox and ticket detail
screens also use for their live updates.

- SignalR hub with authenticated agent connections and anonymous or authenticated visitor connections, connection
  scoping so a visitor can only ever reach their own conversation, and reconnection with message replay after a
  dropped connection.
- An embeddable chat widget as a small standalone bundle: a launcher, a pre-chat form capturing name, email or
  phone, and topic, a transcript view, typing indicators, file upload through story 12, and a post-chat
  satisfaction prompt feeding story 50.
- Chat routing: a waiting queue per department with position and estimated wait shown to the visitor, capacity per
  agent, and an offline path that turns the chat into a ticket with an acknowledgement rather than dropping it.
- The agent console: multiple concurrent conversations with unread indicators, visitor context (page URL,
  referrer, browser, locale, and prior tickets from story 13), transfer to another agent or department, and an
  invite-a-colleague action.
- Every chat produces a ticket in story 15 with the full transcript as messages in story 18, so nothing lives
  only in chat.
- Idle and abandonment handling: warn, then close with a transcript emailed to the visitor and the ticket left in
  the correct state.
- The widget must work in Arabic RTL and on a mobile viewport, and must not leak internal notes.

## Acceptance criteria

- [ ] A visitor can start a chat, exchange messages with an agent in real time, and the whole conversation lands
      on a ticket as messages.
- [ ] A visitor connection cannot read or join any conversation other than its own, verified by attempting it
      directly against the hub.
- [ ] A dropped connection reconnects and replays missed messages without duplicating them.
- [ ] Typing indicators and read markers work in both directions.
- [ ] A queued visitor sees their position and an estimated wait, and both update as the queue moves.
- [ ] Starting a chat outside business hours or with no agent available creates a ticket and tells the visitor
      what will happen next.
- [ ] An agent handles several concurrent chats with correct unread counts and no cross-talk between them.
- [ ] Transferring a chat to another department preserves the transcript and notifies the visitor.
- [ ] An abandoned chat closes after the configured idle period, and the transcript reaches the visitor by email.
- [ ] The widget renders correctly in Arabic RTL and on a 375px viewport, and passes a keyboard-only walkthrough.
- [ ] File upload from the widget honours the same content-type and size checks as story 12.
- [ ] Internal notes are never delivered to a visitor connection.

## Dependencies

- **Blocked by / related ids:** CRM-22, CRM-15, CRM-18, CRM-12.
- **Depends on code areas or other stories:** the channel pipeline from story 22, the ticket and message model
  from stories 15 and 18, and storage from story 12. Stories 20, 21, and 36 reuse this SignalR transport.

## Technical hints

- Keep the widget bundle small and dependency-free; it loads on the customer's site and its weight is visible.
- Authorise on the hub method, not only on connection. A connection-time check is not enough once a conversation
  can be transferred.
- Persist every chat message as it is sent rather than at conversation end. A dropped connection must not lose the
  transcript.

## Out of scope

- The AI chatbot handling chats before an agent. That is story 46, which plugs into this hub.
- Video and voice calling.
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
