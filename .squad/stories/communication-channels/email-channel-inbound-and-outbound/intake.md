# Story 23 — Email channel inbound and outbound

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/communication-channels/email-channel-inbound-and-outbound/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`
- **Module (backend):** `Communication`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-23`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
Email channel inbound and outbound
```

---

## Description

Email is the channel most support volume arrives on, and the one with the most ways to go wrong: threading,
quoting, encodings, attachments, bounces, and loops.

- Inbound over Microsoft Graph or IMAP, configurable per department mailbox, with change-notification
  subscriptions where available and polling as a fallback, tracking a durable cursor so nothing is read twice or
  missed across a restart.
- MIME parsing that survives reality: multipart alternatives, inline images referenced by content id, nested
  forwards, non-UTF-8 charsets, and Arabic subjects and bodies in both quoted-printable and base64.
- Threading by `Message-Id`, `In-Reply-To`, and `References` first, falling back to a subject-plus-sender window,
  and a ticket-number token injected into outbound subjects and reply-to addresses as the most reliable anchor.
- Quote and signature trimming so a long chain does not re-append the entire history to every message.
- Outbound over SMTP or Graph with localised HTML and plain-text templates, the agent's signature from story 08,
  correct RTL rendering for Arabic mail, and attachments from story 12.
- Bounce, complaint, and auto-reply handling: parse delivery-status notifications, mark the message failed with a
  reason, flag a hard-bounced address on the contact, and suppress further sending to it.
- Loop protection: detect auto-responders and vacation replies, honour `Auto-Submitted` and precedence headers,
  and rate-limit per sender so two auto-responders cannot ping-pong forever.
- `backend/requests/email.http` plus a folder of real-world sample `.eml` files, including Arabic, inline images,
  a nested forward, and a bounce.

## Acceptance criteria

- [ ] An inbound email creates a ticket with subject, body, sender identified to a contact, and attachments intact.
- [ ] A reply to an existing ticket appends to that ticket's thread, matched by `In-Reply-To` and by the subject
      token independently.
- [ ] An Arabic subject and body survive round-trip in both quoted-printable and base64 without mojibake.
- [ ] An inline image referenced by content id renders in the thread rather than appearing as a stray attachment.
- [ ] Quoted history and signatures are trimmed, and the trimmed original is still recoverable from the raw
      payload.
- [ ] An outbound email renders correctly RTL in Arabic in a real mail client, and its plain-text alternative is
      readable.
- [ ] A hard bounce marks the message failed, flags the address on the contact, and suppresses further sends to it.
- [ ] An auto-reply is recognised and does not create a ticket or trigger another outbound reply.
- [ ] Restarting the service does not reprocess already-read mail and does not skip mail that arrived during the
      restart.
- [ ] A malformed MIME message is dead-lettered with its raw payload retained, and does not stall the pipeline.
- [ ] Every sample `.eml` in the fixtures folder is processed by hand and produces the expected outcome.

## Dependencies

- **Blocked by / related ids:** CRM-22.
- **Depends on code areas or other stories:** the channel pipeline from story 22, storage from story 12,
  signatures from story 08, and localisation from story 05.

## Technical hints

- Use MimeKit for parsing and MailKit for SMTP. Do not hand-roll MIME; it will fail on the first real forward.
- Put the ticket number in the reply-to address as well as the subject. Customers edit subjects; they rarely edit
  the address.
- Collect real messy emails as `.eml` fixtures as you go. On a project without tests they are the only regression
  net this channel will have.

## Out of scope

- Marketing or bulk email. This is transactional support mail only.
- Notification emails to agents. Those are story 36.
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
