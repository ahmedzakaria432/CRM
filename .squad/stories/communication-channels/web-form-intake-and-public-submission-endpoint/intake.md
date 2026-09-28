# Story 27 — Web form intake and public submission endpoint

Story intake for the **Communication Channels** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/communication-channels/web-form-intake-and-public-submission-endpoint/intake.md`.

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`
- **Module (backend):** `Communication`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-27`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `communication-channels`

---

## Title

```
Web form intake and public submission endpoint
```

---

## Description

Public, unauthenticated ticket submission from a website form or a QR code, which means it is the most exposed
write endpoint in the system and needs the most defensive treatment.

- A form designer in admin: named forms, each with configured fields drawn from story 16's custom fields, plus
  required flags, help text, and validation, all localised in Arabic and English.
- Per-form routing: target department, branch, category, priority, and an optional default assignee, so a
  complaints form and a technical form land in different queues.
- A public submission endpoint: unauthenticated, rate-limited by IP and by email, CAPTCHA-protected, with a
  honeypot field and a minimum fill-time check, accepting attachments through story 12's validation.
- Anti-abuse in depth: per-IP and per-email throttles, a duplicate-submission window, an attachment count and
  size cap, a blocklist, and an audit entry per rejection so abuse is visible rather than invisible.
- Customer resolution: match the submitted email or phone to an existing contact, otherwise create a provisional
  customer for review, never silently merging on a weak match.
- Acknowledgement: an immediate localised confirmation showing the ticket number, plus an email or SMS
  acknowledgement, and a tracking link into the portal from story 48.
- An embeddable form: both a hosted page and a snippet, styled from the branding tokens in story 58, working in
  Arabic RTL and on mobile.

## Acceptance criteria

- [ ] A submitted form creates a ticket with the configured department, branch, category, and priority, and all
      custom field values populated.
- [ ] Required and validation rules are enforced server-side, and the failure response drives field-level errors
      on the form in the active locale.
- [ ] A missing or failed CAPTCHA is rejected, and so is a filled honeypot and an implausibly fast submission.
- [ ] Exceeding the per-IP or per-email rate limit returns 429 and writes an audit entry.
- [ ] Submitting the same content twice within the duplicate window is rejected with a pointer to the existing
      ticket rather than creating a second one.
- [ ] Attachments are validated by magic bytes and size, exactly as in story 12, before storage.
- [ ] A submission from a known email is attached to that contact; an unknown one produces a provisional customer
      flagged for review.
- [ ] The submitter receives an immediate on-screen confirmation with the ticket number, and an acknowledgement
      message on their preferred channel.
- [ ] The form renders and submits correctly in Arabic RTL on a 375px viewport.
- [ ] No endpoint on this surface can be used to enumerate customers, tickets, or agents; error responses are
      deliberately uninformative.
- [ ] Every abuse path is exercised from `backend/requests/webforms.http`.

## Dependencies

- **Blocked by / related ids:** CRM-22, CRM-16, CRM-12.
- **Depends on code areas or other stories:** the channel pipeline from story 22, custom fields from story 16, and
  storage from story 12. Story 48 provides the tracking link.

## Technical hints

- Treat this as the hostile edge of the system. It is unauthenticated, it accepts files, and it has no test suite
  behind it, so every check must be verified by hand against a deliberately abusive request set.
- Return the same generic response for an unknown and a known email. Distinguishing them turns the form into a
  customer-enumeration oracle.
- Reuse story 16's custom field definitions rather than inventing a second field model for forms.

## Out of scope

- Authenticated portal submission. That is story 48.
- Public knowledge-base search on the form. That is story 40.
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
