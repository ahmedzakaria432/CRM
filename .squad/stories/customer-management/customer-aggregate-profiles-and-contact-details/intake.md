# Story 11 — Customer aggregate profiles contact details and notes

Story intake for the **Customer Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-management/customer-aggregate-profiles-and-contact-details/intake.md`.

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`
- **Module (backend):** `Customers` (new module); deletes the `Sample` template module

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-11`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-management`

---

## Title

```
Customer aggregate profiles contact details and notes
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs the notes half of story 12**;
attachments, merge, and fuzzy duplicate detection are deferred.

---

## Description

The Customer bounded context. Everything in the CRM hangs off a customer, so the model has to be right before
tickets are built on it.

- The new `Customers` module (`Crm.Modules.Customers.*`, schema `customers`), scaffolded from `Sample`.
- `Customer` aggregate root: `CustomerNumber`, `LocalizedText` display name, type (individual or organisation),
  legal name for organisations, status (active, inactive), preferred language, owning branch and department
  (`IOrganisationScoped`, so story 06's filter applies).
- `Contact` child entity: name, job title, `EmailAddress`, `PhoneNumber` (E.164), primary flag, portal-access flag.
- `CustomerNote` child entity: body, pinned flag, author id, timestamps. Internal only in the MVP.
- Value objects: `CustomerNumber` (generated, e.g. `C-000123`), `EmailAddress`, `PhoneNumber`.
- Invariants in the aggregate: exactly one primary contact when any contact exists; no duplicate email or phone
  within a customer; an organisation customer requires a legal name.
- Use cases: create, update, deactivate/reactivate, add/update/remove contact, set primary contact, add/pin/remove
  note.
- A simple duplicate warning on create: an exact match on contact email or phone in the caller's scope returns the
  candidates; the client can confirm through.
- Queries: get by id, paged search (name in `ar`/`en`, customer number, email, phone), and a lookup projection
  for the customer picker used by tickets.
- `Customers.Contracts`: `ICustomersModuleApi` with `GetCustomerSummaryAsync` and `FindContactByEmailAsync`
  (used by tickets in 15 and the portal in 47).
- Permissions: `customers.read`, `customers.write` (story 07).
- Delete the `Sample` template module (its projects, `ModuleCatalog` line, host references, and `sample` schema via a
  documented drop), and move `diagnostics.http`'s Sample-based requests onto Customers equivalents.
- `backend/requests/customers.http` covering every use case and every rejected invariant.

## Acceptance criteria

- [ ] An organisation customer without a legal name is rejected by the aggregate with 422 `domain-rule`.
- [ ] Adding a second primary contact demotes the previous one in the same operation.
- [ ] A duplicate contact email within one customer is rejected; a malformed email or non-E.164 phone is rejected
      at value-object construction.
- [ ] There is no `DbSet<Contact>` or `DbSet<CustomerNote>`; both are reached only through `Customer`.
- [ ] Creating a customer whose contact email/phone already exists in scope returns a duplicate warning with the
      candidates, and can be confirmed through.
- [ ] Search matches Arabic and English names, customer number, email, and phone, paged server-side.
- [ ] Every customer read and write is scoped to the caller's branch and department (story 06 filter visible in SQL).
- [ ] Pinned notes sort above unpinned ones.
- [ ] `Sample` is gone: `grep -r Sample backend/src` finds nothing, and the build is clean.
- [ ] Every use case and rejected invariant exercised from `backend/requests/customers.http`.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-07.
- **Depends on code areas or other stories:** scoping from story 06, permissions from story 07.

## Technical hints

- Keep `EmailAddress` and `PhoneNumber` in the Customers domain; other modules receive primitives via Contracts.
- Cacheable reads (`ICacheableQuery`) for get-by-id only; invalidate on every customer command.

## Out of scope (deferred post-MVP)

- Attachments and file storage (story 12 attachments half), customer merge, fuzzy name duplicate detection,
  addresses, tax numbers, tiers, the interaction timeline projection (story 13).
- Customer UI — story 14. Any test project.

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
