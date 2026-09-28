# Story 06 — Multi department and multi branch organisation scoping

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/multi-department-and-multi-branch-organisation-scoping/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`
- **Module (backend):** `Organisation` (new module); the scope filter and scope ids are BuildingBlocks / SharedKernel so every module applies them

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-06`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Multi department and multi branch organisation scoping
```

---

## Description

The CRM serves multiple departments across multiple branches. Data isolation and visibility must be enforced in
one place, at the data layer, so no handler can leak across a boundary by omission.

- The Organisation bounded context as the new `Organisation` module (`Crm.Modules.Organisation.*`, schema `organisation`): `Organisation`, `Branch`, `Department`, and
  `Team` as aggregate roots, with `BusinessHours`, `HolidayCalendar`, and `TimeZoneId` as value objects.
- Membership as its own aggregate linking a user to a department, branch, and team with a role, so one user can
  belong to several departments holding a different role in each. Membership references user and department by
  strongly-typed id only.
- `ICurrentUserService` exposing user id, roles, permissions, department ids, branch ids, and the scope the user
  has selected in the UI.
- The scope ids (`TenantId`, `BranchId`, `DepartmentId`) and `IOrganisationScoped` live in `Crm.SharedKernel`, and the
  scope filter is registered by `ModuleDbContext` in `Crm.BuildingBlocks.Infrastructure`, so every module's DbContext
  applies it without opting in. `ICurrentUserService` resolves memberships through `Organisation.Contracts`, never by
  reading the `organisation` schema from another module.
- An `IOrganisationScoped` marker plus a global query filter restricting reads to the caller's accessible
  branches and departments, layered on the soft-delete filter from story 02.
- A write-side guard: a pipeline behaviour rejecting a command whose target falls outside the caller's scope,
  returning 403 rather than an accidental 404.
- A scope switcher in the agent shell; the active branch and department drive every list, dashboard, and report.
- Admin CRUD for branches, departments, and teams, including business hours and holiday calendars per branch,
  consumed later by the SLA clock in story 33.

## Acceptance criteria

- [ ] An agent in department A cannot read, list, or update an entity in department B; the attempt returns 403
      and is written to the audit log.
- [ ] A user who belongs to two departments sees the union only when the all-my-departments scope is active, and
      a single department's data when that department is active.
- [ ] Removing a user's department membership removes their access immediately, with no cached-token grace period.
- [ ] Scope filtering is automatic: a newly written naive query is still scoped, confirmed by reading the
      EF Core generated SQL for that query and seeing the branch and department predicates.
- [ ] Branch business hours and holiday calendars are stored per branch with an IANA timezone and are queryable.
- [ ] `BusinessHours` rejects an invalid range at construction, so no branch can be saved with a malformed schedule.
- [ ] Admin can create, rename, deactivate, and reparent departments and teams; deactivating removes them from
      assignment pickers without breaking historical records.
- [ ] Reparenting a department that would create a cycle is rejected by the aggregate, not by the UI.
- [ ] The scope switcher persists per user and is reflected in the ticket inbox, dashboard, and reports.
- [ ] The cross-boundary access matrix for every role is walked by hand and every request recorded in
      `backend/requests/organisation.http`.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-04.
- **Depends on code areas or other stories:** the query filter and interceptor from story 02. Story 09 composes
  its permission checks with the scope check delivered here.

## Technical hints

- Resolve scope from the token plus a per-request active-scope header. Never treat a client-supplied department
  id as the authorisation source.
- Store the IANA timezone id per branch, not a UTC offset, so daylight saving is handled correctly.
- The scope filter is the single most security-relevant piece of code in the system and there is no test suite
  behind it. Keep it in one file, keep it short, and log every rejection.

## Out of scope

- Role and permission definitions. Those are stories 08 and 09.
- SLA calculation over business hours. Story 33 consumes the calendars stored here.
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
