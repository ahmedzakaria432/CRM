# Story 22 — Channel abstraction and inbound message pipeline

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/communication-channels/channel-abstraction-and-inbound-message-pipeline/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-22`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
Channel abstraction and inbound message pipeline
```

---

## Description

One pipeline that every channel plugs into, so adding SMS later is an adapter rather than a rewrite. This is the
strategy pattern doing real work, and it is the anti-corruption layer keeping provider payloads out of the domain.

- `IInboundChannelAdapter` and `IOutboundChannelAdapter` per channel, registered by channel type and resolved by
  a factory. The adapter's only job is translating between the provider's payload and the domain's message model.
- A single inbound pipeline every channel funnels through: receive, verify authenticity, persist the raw payload,
  deduplicate, identify the customer and contact, decide new-ticket-versus-append, apply the channel's default
  category and department, create or append, then raise domain events.
- Identity resolution: match the sender's email, phone, or channel handle to a contact from story 11, fall back to
  creating a provisional customer flagged for review rather than dropping the message or guessing.
- Threading: append to an existing open ticket when the provider's thread reference or in-reply-to matches, or
  when the same sender writes within a configurable window on the same subject; otherwise open a new ticket. A
  reply arriving on a closed ticket either reopens it or spawns a linked child, per configuration.
- Durable processing: an inbox table with received, processing, processed, and failed states, bounded retries with
  backoff, a dead-letter queue, and a replay command. A provider outage must not lose a customer's message.
- Outbound path: an outbox with retry, per-channel rate limiting, and delivery-status callbacks written back to
  the message from story 18.
- Channel configuration per department and branch, so two departments can use different WhatsApp numbers and
  mailboxes, with secrets held outside the database and never returned by any API.
- `backend/requests/channels.http` with a realistic sample webhook payload per channel, replayable by hand.

## Acceptance criteria

- [ ] A new channel can be added by implementing the two adapter interfaces and registering them, with no change
      to the pipeline.
- [ ] No provider-specific type appears anywhere in `Crm.Domain` or `Crm.Application`; translation happens only
      in the adapter.
- [ ] An inbound message with an unverified signature is rejected and recorded, never processed.
- [ ] The same provider message id delivered twice creates exactly one ticket or message.
- [ ] An unknown sender produces a provisional customer flagged for review, and the message is never dropped.
- [ ] A reply matching an open ticket's thread reference appends to it; an unrelated message from the same sender
      opens a new ticket.
- [ ] A reply to a closed ticket reopens it or opens a linked child per configuration, and the choice is
      configurable rather than hard-coded.
- [ ] A failing downstream step retries with backoff, lands in the dead-letter queue after the configured
      attempts, and can be replayed successfully once the cause is fixed.
- [ ] Every inbound payload is persisted raw before processing, so a mis-parse is diagnosable after the fact.
- [ ] Channel credentials are never returned by any API response and never appear in logs.
- [ ] Two departments can run different accounts on the same channel type simultaneously.
- [ ] Every sample payload in `channels.http` can be replayed against a running API and produces the expected
      ticket or message.

## Dependencies

- **Blocked by / related ids:** CRM-11, CRM-15, CRM-16, CRM-18.
- **Depends on code areas or other stories:** the customer aggregate from story 11, the ticket aggregate from
  story 15, taxonomy from story 16, and the message model from story 18. Stories 23 to 27 are adapters over this
  pipeline.

## Technical hints

- Persist the raw payload before anything else. With no test suite, the raw payload is the only way to
  reconstruct what a provider actually sent.
- Make deduplication a unique index on channel plus provider message id, not a lookup-then-insert.
- Do the provider work in a background worker reading the inbox table, and keep the webhook endpoint to accept,
  verify, persist, and return 200 quickly. Providers retry aggressively on slow responses.

## Out of scope

- Individual channel implementations. Those are stories 23 to 27.
- AI categorisation of inbound messages. That is story 44.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10. Domain-Driven Design inside Clean Architecture (`Crm.Domain` <- `Crm.Application` <- `Crm.Infrastructure` / `Crm.Api`), CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server, minimal APIs, `Result<T>` returns, RFC 9457 problem details.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
