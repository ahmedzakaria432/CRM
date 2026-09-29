# Story 48 — Portal ticket submission and tracking

> **MVP re-scope (2026-09-29): merged into Story 47.** Its MVP-relevant scope now lives in that story's intake; the rest is deferred post-MVP. Do not plan this intake on its own. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Customer Portal** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/customer-portal/portal-ticket-submission-and-tracking/intake.md`.

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`
- **Module (backend):** `Tickets` (Portal Area endpoints)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-48`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-portal`

---

## Title

```
Portal ticket submission and tracking
```

---

## Description

The customer's own view of what they have asked for and where it stands. Done well, it removes the "any update?"
email that consumes a disproportionate share of agent time.

- Ticket submission from the portal: category selection from the customer-visible taxonomy in story 16, subject,
  description, the customer-visible custom fields, attachments through story 12, and priority only where the
  organisation allows a customer to set it.
- Pre-submission deflection: as the customer types the subject, suggest knowledge-base articles from story 38 and,
  where enabled, the story 46 chatbot, with a recorded signal when a customer abandons submission after reading an
  article.
- A ticket list scoped strictly to the customer, showing status in customer-friendly language rather than internal
  status names, with filters and search.
- A ticket detail view showing only the public conversation, the customer's own attachments and the agent's, the
  current status, and the expected response time where the organisation chooses to expose it. Internal notes and
  internal fields must be structurally unreachable.
- Customer actions: reply, add an attachment, request closure, reopen within a configured window, cancel a request,
  and approve a proposed resolution where that workflow is enabled.
- Realtime and notification updates: a reply from an agent appears without a manual refresh and notifies the
  customer per their story 36 preferences.
- Draft persistence, so a long description survives a mobile network drop.
- Public tracking by ticket number plus a verification code for a customer who submitted through the web form in
  story 27 and has no account.

## Acceptance criteria

- [ ] A customer can submit a ticket with attachments and receives a ticket number immediately.
- [ ] Only customer-visible categories and custom fields appear on the form; an internal field is never rendered
      nor accepted if injected into the request.
- [ ] Typing a subject surfaces relevant knowledge-base suggestions, and abandonment after reading one is recorded.
- [ ] The ticket list shows only tickets the signed-in contact is entitled to see, per the account-administrator
      rule from story 47.
- [ ] Requesting another customer's ticket by id returns 404 and writes an audit entry, and never reveals that the
      ticket exists.
- [ ] Internal notes, internal fields, and agent-only history never appear in any portal response, verified by
      inspecting the raw API response rather than the rendered page.
- [ ] Status names are shown in customer-friendly localised language in both `ar` and `en`.
- [ ] A customer reply appends to the ticket thread as a public message and notifies the assigned agent.
- [ ] An agent reply appears in the portal without a manual refresh and notifies the customer per their preferences.
- [ ] Reopen is possible only within the configured window and requires a reason.
- [ ] A long description survives a network drop and a page reload as a draft.
- [ ] Public tracking by ticket number plus verification code shows status without exposing the conversation, and
      cannot be brute-forced.
- [ ] The whole flow works in Arabic RTL on a 375px viewport.

## Dependencies

- **Blocked by / related ids:** CRM-47, CRM-15, CRM-16, CRM-18, CRM-12, CRM-38.
- **Depends on code areas or other stories:** portal identity from story 47, tickets and messages from stories 15
  and 18, taxonomy from story 16, storage from story 12, and knowledge search from story 38.

## Technical hints

- Build the portal read model as its own projection with only customer-safe fields, rather than reusing the agent
  DTO and hoping every field is stripped. On a project with no tests, a shared DTO will eventually leak an internal
  note.
- Return 404 rather than 403 for another customer's ticket. A 403 confirms the ticket exists.
- Make the public tracking code long enough and rate-limited, or it becomes an enumeration oracle over the ticket
  number sequence.

## Out of scope

- Request history and knowledge access as separate surfaces. That is story 49.
- Satisfaction feedback. That is story 50.
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
