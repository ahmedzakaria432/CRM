# Story 47 — Customer portal accounts ticket submission tracking and history

Story intake for the **Customer Portal** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-portal/portal-authentication-registration-and-account/intake.md`.

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`
- **Module (backend):** `Identity` (portal accounts); links to the customer through `Customers.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-47`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-portal`

---

## Title

```
Customer portal accounts ticket submission tracking and history
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs stories 48 (submission and
tracking) and 49 (request history)** without the knowledge base, attachments, verification codes, or realtime.
Moved from Phase 2 into the MVP.

---

## Description

The customer's own way in, and their view of what they asked for and where it stands.

- Portal identity in the `Identity` module as a separate audience on story 07's token stack: its own JWT audience
  and permission set (`portal.tickets`); a portal token can never satisfy an Agent or Admin endpoint, and a staff
  token never satisfies a Portal endpoint.
- `PortalAccount` aggregate bound to exactly one contact of one customer (through `Customers.Contracts`).
  Self-registration with email + password links to the existing contact with that email and portal access enabled;
  an email unknown to any customer is rejected with the same generic response as success-pending, so registration
  cannot enumerate known emails.
- An agent can enable portal access on a contact (story 11's flag) and set an initial password for them.
- Endpoints (Portal Area): register, login, refresh, logout, `GET me`, change password; rate limited per IP and
  per account (story 03's limiter extended to the Portal Area).
- Ticket submission: customer-visible categories (story 15), subject, description. The ticket is created in the
  customer's owning department with channel `Portal`.
- My tickets: a list scoped strictly to the customer's own tickets (all of the customer's tickets for a contact
  flagged account admin), customer-friendly status names, status filter and search, open vs closed tabs.
- Ticket detail: public thread only (story 18's public visibility — internal notes structurally unreachable),
  current status, reply, request closure (resolves the ticket), and reopen a resolved ticket.
- Frontend on the SSR `portal` layout: register, sign-in, my tickets, new ticket, ticket detail, account page (name,
  preferred language, change password). Bilingual RTL, mobile first.
- `backend/requests/portal.http` covering every endpoint, including cross-customer access attempts.

## Acceptance criteria

- [ ] A portal token is rejected (403) on every Agent/Admin endpoint, and a staff token on every Portal endpoint.
- [ ] Registering with an email on a portal-enabled contact creates an account bound to that contact; an unknown
      email gets the identical response and creates nothing.
- [ ] A customer sees only their own tickets; requesting another customer's ticket id returns 404.
- [ ] Internal notes never appear in any portal response, verified from `portal.http`.
- [ ] Submitting a ticket creates it with channel `Portal` in the customer's department, and it appears in the
      agent inbox (story 20).
- [ ] A customer reply appears in the agent's thread; an agent reply appears in the portal after refresh.
- [ ] Request closure resolves the ticket; reopen works on a resolved ticket and is rejected on a closed one.
- [ ] Portal login and registration are rate limited (429 + `Retry-After`).
- [ ] Every portal page walked in `en` and `ar` at 375px, 768px, and 1280px; server-rendered with the portal layout.

## Dependencies

- **Blocked by / related ids:** CRM-07, CRM-11, CRM-15, CRM-18.
- **Depends on code areas or other stories:** the token stack (07), contacts and the portal-access flag (11),
  tickets (15) and the public thread (18), the portal layout (04), i18n (05).

## Technical hints

- Keep portal controllers in each owning module's `Presentation/Controllers/Portal/` (Identity for accounts,
  Tickets for tickets), never in a separate portal module.
- The portal ticket DTOs are separate records from the agent DTOs, so an internal field cannot leak by reuse.

## Out of scope (deferred post-MVP)

- Email/SMS verification and password reset (no channels), CAPTCHA, invitations by email, attachments, KB
  deflection and embedded help centre (37–40), realtime updates, public tracking by code (27), announcements,
  exports, satisfaction surveys (50). Any test project.

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
