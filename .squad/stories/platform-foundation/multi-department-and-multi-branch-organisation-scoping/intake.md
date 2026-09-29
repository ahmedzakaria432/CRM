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

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). Delivers **multi-department** and
**multi-branch**. Teams, business hours, and holiday calendars are deferred (their only consumer, SLA, is out of the
MVP).

---

## Description

The CRM serves multiple departments across multiple branches of one organisation. Data isolation must be enforced
once, at the data layer, so no handler can leak across a boundary by omission.

- The new `Organisation` module (`Crm.Modules.Organisation.*`, schema `organisation`), scaffolded from the `Sample`
  template: `Organisation` (the tenant), `Branch`, and `Department` as aggregate roots with `LocalizedText` names
  (`ar` + `en`), an active flag, and a code. A department belongs to one branch.
- The scope ids (`TenantId`, `BranchId`, `DepartmentId`) and an `IOrganisationScoped` marker live in
  `Crm.SharedKernel`. `ModuleDbContext` adds a second **named** query filter (`ScopeFilterName`, next to
  `SoftDeleteFilterName`) restricting reads of `IOrganisationScoped` entities to the caller's tenant and accessible
  branches/departments. Every module gets it without opting in.
- `ICurrentUserService` (from story 03) gains the caller's accessible department and branch ids and the **active
  scope** chosen in the UI (sent as an `X-Crm-Scope` header, validated against the caller's memberships — never
  trusted on its own).
- A write-side guard: a pipeline behaviour rejects a command targeting an entity outside the caller's scope with
  403 `urn:crm:problem:forbidden`, never an accidental 404.
- `Membership` as its own aggregate in this module: a user id (a plain `Guid` — users arrive in story 07), a
  department id, and a system role (`Administrator`, `Supervisor`, `Agent`). One user can belong to several
  departments with a different role in each. Admin endpoints add, change, and remove memberships.
  `Organisation.Contracts` exposes `IOrganisationModuleApi.GetMembershipsAsync(userId)`; story 07 reads it at login
  to put department ids and roles on the token.
- **Development-only dev login:** until story 07, an authentication handler active only in Development accepts an
  `X-Dev-User: <user id>` header for the seeded well-known users and builds the principal from their memberships.
  Story 07 deletes it.
- Admin endpoints and screens (Admin Area): list, create, rename, deactivate branches and departments.
- A scope switcher in the agent shell (all my departments, or one department); the choice persists per user in a
  cookie and drives every list.
- Development seed: one organisation, two branches (Riyadh, Jeddah), three departments, and memberships for four
  well-known user ids (an admin in every department, a supervisor, two agents in different departments) that story
  07's user seed reuses.

## Acceptance criteria

- [ ] An agent in department A cannot read, list, or update an entity in department B; the attempt returns 403.
- [ ] A user in two departments sees the union with "all my departments" active and one department's data when
      that department is active.
- [ ] Scope filtering is automatic: a naive new query is still scoped, confirmed by reading the EF Core SQL and
      seeing the tenant/branch/department predicates.
- [ ] The scope filter is a second **named** filter; soft-deleted rows stay hidden when scope is lifted in the one
      permitted admin path.
- [ ] Admin can create, rename, and deactivate branches and departments; deactivated ones disappear from pickers
      without breaking existing records.
- [ ] A forged `X-Crm-Scope` naming a department the caller does not belong to returns 403.
- [ ] Branch and department names render in the active locale; screens walked in `en` and `ar` at 375/768/1280.
- [ ] Every endpoint and the cross-department matrix recorded in `backend/requests/organisation.http`.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-03, CRM-04, CRM-05.
- **Depends on code areas or other stories:** the named soft-delete filter from story 02, `ICurrentUserService` and
  `CrmClaimTypes` from story 03, the agent shell from story 04, i18n from story 05.

## Technical hints

- Resolve scope from the token claims plus the active-scope header; the header can only narrow, never widen.
- The scope filter is the most security-relevant code in the system and has no test suite behind it. Keep it in
  one file, keep it short, log every rejection.
- Do not use the unnamed `HasQueryFilter` overload — it replaces the soft-delete filter (story 02 decision).

## Out of scope (deferred post-MVP)

- Teams, business hours, holiday calendars, IANA timezone per branch, department reparenting.
- Audit-log entries for rejected access (story 10 is deferred; rejections are logged).
- Real authentication and the permission catalogue — story 07. Any test project.

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
