# Story 25 — SMS channel

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/communication-channels/sms-channel/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`
- **Module (backend):** `Communication`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-25`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
SMS channel
```

---

## Description

SMS for short notifications and for customers on channels of last resort. It is the simplest adapter, which makes
it the right place to prove the pipeline abstraction actually abstracts.

- An SMS provider adapter behind the story 22 interfaces, with the concrete provider selected by configuration
  and at least two implementations to prove the seam holds.
- Outbound send with localised templates, Arabic Unicode handling, correct segment counting for both GSM-7 and
  UCS-2, and a cost-aware warning when a message will split into several segments.
- Inbound receipt where the provider and number type support it, threading onto an existing ticket by sender
  number and recency, since SMS carries no thread reference.
- Sender identity: an alphanumeric sender id where the region allows it, falling back to a long or short code,
  configurable per branch.
- Delivery receipts mapped to the story 18 delivery states, with provider error codes translated to localised
  reasons.
- Opt-out handling: recognise stop keywords in Arabic and English, mark the contact opted out, and block further
  non-critical sends to that number, with the block enforced in the domain rather than in the UI.
- Rate limiting and a per-department daily volume cap, because SMS costs money per message and a runaway loop is
  expensive.

## Acceptance criteria

- [ ] An outbound SMS sends through the configured provider and records queued, sent, and delivered states.
- [ ] Switching the configured provider requires no change outside `Crm.Modules.Communication.Infrastructure`.
- [ ] An Arabic message is encoded as UCS-2, and the segment count shown to the agent matches what the provider
      bills.
- [ ] A message that will split into multiple segments warns the agent before sending.
- [ ] An inbound SMS from a known number appends to that number's most recent open ticket, or opens a new one when
      there is none.
- [ ] Replying with a stop keyword in Arabic or in English marks the contact opted out.
- [ ] An attempt to send a non-critical message to an opted-out number is rejected by the domain, not merely
      hidden in the UI.
- [ ] Exceeding the per-department daily cap blocks further sends and raises an alert rather than failing silently.
- [ ] A provider failure surfaces a localised reason and retries per policy without duplicating the message.
- [ ] Provider credentials never appear in an API response or a log line.

## Dependencies

- **Blocked by / related ids:** CRM-22.
- **Depends on code areas or other stories:** the channel pipeline from story 22. Story 36 sends notifications
  through this adapter.

## Technical hints

- Get the segment counting right for Arabic before anything else; it is the difference between a correct invoice
  and a surprise.
- Enforce opt-out at the outbound adapter as well as in the domain. Notifications in story 36 will otherwise find
  a way around it.
- Implement two providers even if only one ships. It is the cheapest possible proof that story 22's abstraction is
  real.

## Out of scope

- OTP delivery for two-factor authentication, which story 07 owns, though it may reuse this adapter.
- Bulk SMS marketing.
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
