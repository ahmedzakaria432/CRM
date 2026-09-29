# Story 24 — WhatsApp Business channel

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/communication-channels/whatsapp-business-channel/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`
- **Module (backend):** `Communication`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-24`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
WhatsApp Business channel
```

---

## Description

WhatsApp is the primary channel for many Saudi customers, and it has rules no other channel has: a 24-hour
customer service window, pre-approved templates outside it, and a strict media model.

- WhatsApp Cloud API integration: webhook receipt with signature verification, and sending text, media, document,
  location, template, and interactive reply-button messages.
- The 24-hour service window enforced as a domain rule: a free-form reply is only allowed inside an open window,
  and outside it only an approved template can be sent. The composer must make this state visible rather than
  failing at send time.
- Template management: sync approved templates and their variables from the provider, store localised Arabic and
  English versions, validate variable counts before sending, and surface template rejection reasons.
- Media handling: download inbound media before its provider URL expires, store it through story 12, and honour
  the per-type size limits on send.
- Status callbacks mapped to the delivery states from story 18: sent, delivered, read, and failed with the
  provider's error code translated into a human-readable, localised reason.
- Contact identity: the WhatsApp phone number in E.164 matched to a contact from story 11, with the profile name
  captured for a provisional customer.
- Multiple numbers per organisation, mapped to departments and branches, so each team owns its own line.
- Voice notes and images are common on this channel; both must be playable and viewable inline in the thread.

## Acceptance criteria

- [ ] An inbound WhatsApp text message creates or appends to a ticket, with the sender matched to a contact by
      E.164 number.
- [ ] The webhook rejects a payload whose signature does not verify, and records the rejection.
- [ ] A free-form reply inside the 24-hour window sends successfully; outside it the API rejects the attempt with
      a domain error and the UI offers the template path instead.
- [ ] The composer shows the remaining service-window time and switches to template-only mode when it lapses,
      before the agent writes a message they cannot send.
- [ ] A template send validates the variable count and types against the synced definition and reports a mismatch
      as a field error.
- [ ] Inbound media is downloaded and stored before the provider URL expires, and remains available afterwards.
- [ ] An inbound voice note is playable inline, and an inbound image previews inline in the thread.
- [ ] Delivery status transitions to delivered and read, and a failure surfaces a localised reason rather than a
      raw provider code.
- [ ] Two departments can operate two different WhatsApp numbers simultaneously.
- [ ] Provider credentials never appear in an API response or a log line.
- [ ] Every message type is exercised from `backend/requests/whatsapp.http` with recorded sample payloads.

## Dependencies

- **Blocked by / related ids:** CRM-22.
- **Depends on code areas or other stories:** the channel pipeline from story 22, storage from story 12, and the
  contact model from story 11.

## Technical hints

- Model the service window as a value object on the conversation with an explicit expiry, so both the API and the
  UI read the same rule rather than each implementing it.
- Download inbound media in the webhook worker immediately. Provider media URLs expire and there is no second
  chance.
- Keep the provider error-code to localised-message mapping in one table. Raw codes in the UI are unusable for an
  agent.

## Out of scope

- WhatsApp marketing campaigns and broadcast lists.
- The AI chatbot answering on WhatsApp. That is story 46, which reuses this adapter.
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
