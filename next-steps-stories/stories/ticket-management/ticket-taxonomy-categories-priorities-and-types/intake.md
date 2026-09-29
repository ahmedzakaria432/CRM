# Story 16 — Ticket taxonomy categories priorities and types

> **MVP re-scope (2026-09-29): merged into Story 15.** Its MVP-relevant scope now lives in that story's intake; the rest is deferred post-MVP. Do not plan this intake on its own. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Ticket Management** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/ticket-management/ticket-taxonomy-categories-priorities-and-types/intake.md`.

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`
- **Module (backend):** `Tickets`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-16`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ticket-management`

---

## Title

```
Ticket taxonomy categories priorities and types
```

---

## Description

The configurable classification data behind every ticket. Getting this configurable rather than hard-coded is
what lets the CRM serve several departments without a code change per department.

- `Category` as an aggregate root, hierarchical to a bounded depth, with a `LocalizedText` name in Arabic and
  English, an owning department, a default priority, a default assignee or team, an SLA policy reference, an
  active flag, and a display order.
- `Priority` and `TicketType` as configurable reference data with localised names, colours, icons, and ordering,
  each mapping to an SLA target set in story 32.
- Custom fields per category: text, number, date, single select, multi select, boolean, and lookup, with a
  required flag, a default, a validation rule, a display order, and a visible-to-customer flag. Values are
  validated against the field definition when a ticket is saved.
- Resolution codes per category, so reporting can distinguish a fixed defect from a customer-error closure.
- Tags as a free-form but curated vocabulary, with merge and rename that update existing tickets.
- Admin screens: a category tree with drag-to-reorder and reparent, a custom-field designer with live preview,
  and priority and type management.
- Deactivation rather than deletion for anything referenced by a ticket, so history stays readable.

## Acceptance criteria

- [ ] A category tree can be created, reordered, reparented, and deactivated, and reparenting into a cycle is
      rejected by the aggregate.
- [ ] Every taxonomy name is stored and rendered in both Arabic and English, and a missing translation is
      surfaced in the admin screen rather than silently falling back.
- [ ] Deactivating a category removes it from pickers but leaves existing tickets readable and reportable.
- [ ] Deleting a category that is referenced by a ticket is rejected; only deactivation is possible.
- [ ] A required custom field blocks ticket save with a field-level error when empty.
- [ ] A custom field value that violates its validation rule is rejected against the field definition, not by
      ad-hoc handler code.
- [ ] Changing a ticket's category re-evaluates its custom fields and reports which values no longer apply,
      rather than dropping them silently.
- [ ] A field marked not visible to the customer never appears in any portal response.
- [ ] Renaming or merging a tag updates every ticket carrying it, in one transaction.
- [ ] Category defaults for priority, assignee, and SLA policy are applied on ticket creation and can still be
      overridden.
- [ ] The custom-field designer preview matches the rendered field on the real ticket form in both locales.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-09.
- **Depends on code areas or other stories:** the ticket aggregate from story 15 and permissions from story 09.
  Stories 32, 34, and 44 consume this taxonomy.

## Technical hints

- Store custom field values as typed columns in a value table keyed by field definition, not as a JSON blob, or
  reporting and filtering in story 52 become impossible.
- Model the category tree with a materialised path or `HierarchyId` so a subtree query is one statement.
- Seed a realistic taxonomy for two departments during development. A single flat category list will hide every
  interesting bug in this story.

## Out of scope

- SLA target values themselves. Those are story 32; this story only holds the reference.
- Automatic categorisation by AI. That is story 44.
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
