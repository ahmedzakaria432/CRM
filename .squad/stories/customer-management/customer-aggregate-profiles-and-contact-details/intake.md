# Story 11 — Customer aggregate profiles and contact details

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
Customer aggregate profiles and contact details
```

---

## Description

The Customer bounded context and its aggregate. Everything in the CRM eventually hangs off a customer, so the
model has to be right before tickets are built on top of it.

- `Customer` as an aggregate root in `Crm.Modules.Customers.Domain` (the new `Customers` module, with its own `customers` schema): an account that may be an individual or an
  organisation, with `CustomerNumber`, `LocalizedText` display name, type, tier or segment, status, preferred
  language, preferred contact channel, tax or commercial registration number, and an owning branch and department.
- `Contact` as a child entity inside the customer aggregate: a named person with a job title, one or more
  `EmailAddress` and `PhoneNumber` value objects, a primary flag, and a portal-access flag.
- Value objects: `CustomerNumber`, `EmailAddress`, `PhoneNumber` in E.164, `Address` with country, city, district,
  street, and postal code, and `TaxNumber`.
- Invariants enforced in the aggregate: exactly one primary contact when any contact exists, no duplicate email
  or phone within a customer, an organisation customer requires a legal name, and a customer cannot be
  deactivated while it has open tickets.
- Use cases: create, update, deactivate and reactivate, add, update and remove contact, set primary contact,
  change owning department, and merge duplicates. Merge moves tickets, interactions, notes, and attachments onto
  the surviving customer and leaves a redirect record so old links resolve. Only Customers-owned data moves inside
  the merge transaction; tickets and interactions owned by other modules move when those modules handle the
  `CustomerMerged` integration event published from `Customers.Contracts`.
- Duplicate detection on create and on demand: match on email, phone, tax number, and fuzzy name.
- Queries: get by id, search with paging, and a lookup projection for the customer picker used across the app.
- `backend/requests/customers.http` covering every use case, including merge and every rejected invariant.
- Delete the `Sample` template module (`backend/src/Modules/Sample/`, its `ModuleCatalog` line and host
  references, and its `sample` schema via a final migration or a documented drop) now that `Customers` is the first
  real module to copy from.

## Acceptance criteria

- [ ] Creating a customer without a display name, or an organisation customer without a legal name, is rejected by
      the aggregate with a domain error mapped to 422.
- [ ] Adding a second primary contact demotes the previous one in the same operation; the aggregate never allows
      two primary contacts.
- [ ] Adding a contact whose email already exists on the same customer is rejected.
- [ ] A malformed email or a phone number that is not valid E.164 is rejected at value-object construction.
- [ ] `Contact` cannot be loaded or saved except through its `Customer`; there is no `DbSet<Contact>`.
- [ ] Deactivating a customer with open tickets is rejected with a domain error naming the blocking tickets.
- [ ] Creating a customer matching an existing email, phone, or tax number returns a duplicate warning with the
      candidate matches, and can be confirmed through explicitly.
- [ ] Merging two customers moves every ticket, interaction, note, and attachment to the survivor, records the
      merge in the audit log, and leaves the merged id resolving to the survivor.
- [ ] Customer search matches on name in both Arabic and English, customer number, email, and phone, and
      paginates server-side.
- [ ] Every customer read and write is scoped to the caller's branch and department.
- [ ] Every use case and every rejected invariant is exercised from `backend/requests/customers.http`.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-06, CRM-09.
- **Depends on code areas or other stories:** the persistence baseline from story 02, organisation scoping from
  story 06, and the permission catalogue from story 09.

## Technical hints

- Map `Contact` as an owned collection or as a child entity with no `DbSet`, so the aggregate boundary is
  enforced by the model rather than by convention.
- Generate `CustomerNumber` from a database sequence, not from a count, or concurrent creates will collide.
- Arabic name search needs a collation that ignores diacritics and normalises alef and ya variants. Configure it
  on the column, not in application code.

## Out of scope

- Interaction history and the timeline. That is story 13.
- Notes and attachments. That is story 12.
- The customer 360 UI. That is story 14.
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
