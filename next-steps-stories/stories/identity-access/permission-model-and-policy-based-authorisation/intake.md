# Story 09 — Permission model and policy based authorisation

> **MVP re-scope (2026-09-29): merged into Story 07.** Its MVP-relevant scope now lives in that story's intake; the rest is deferred post-MVP. Do not plan this intake on its own. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Identity and Access** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/identity-access/permission-model-and-policy-based-authorisation/intake.md`.

---

## Feature

- **Feature name (display):** Identity and Access
- **Feature slug (folder under `plans/`):** `identity-access`
- **Module (backend):** `Identity`; the `[RequirePermission]` behaviour and permission catalogue are BuildingBlocks every module uses

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-09`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `identity-access`

---

## Title

```
Permission model and policy based authorisation
```

---

## Description

Fine-grained permissions, so a role is a bundle of capabilities rather than a hard-coded branch in code. This is
what makes department-level delegation safe.

- A permission catalogue defined in code as constants grouped by module: `tickets.read`, `tickets.assign`,
  `tickets.delete`, `customers.merge`, `kb.publish`, `reports.view_all`, `admin.users`, and so on.
- Role-to-permission mapping, editable for custom roles, with an effective-permission resolver that unions the
  caller's roles within the active scope.
- Authorisation enforced by a MediatR behaviour driven by a `[RequirePermission]` attribute on the request, so a
  handler cannot be reached without a declared permission, plus matching endpoint filters.
- A startup guard that enumerates every `IRequest` in every module's Application assembly (from the host's `ModuleCatalog`) and refuses to start the application if
  any of them lacks a permission declaration. On a project with no tests, a fail-fast startup check is the only
  way this stays complete as the backlog grows.
- Record-level rules layered on the scope filter from story 06: an agent sees only tickets assigned to them or
  their team unless they hold the department-wide read permission.
- Frontend: permissions returned from `GET /me`, a `useCan()` composable, a `v-can` directive, and navigation
  and action buttons that hide rather than fail.
- A permission matrix admin screen with roles as columns and permissions as rows, grouped by module.

## Acceptance criteria

- [ ] Every command and query declares a required permission, and the application refuses to start if one does not.
- [ ] A request without the required permission returns 403 with a problem-details body naming the permission,
      and the handler never executes.
- [ ] An agent without department-wide read cannot list tickets outside their own and their team's assignments.
- [ ] Changing a role's permissions takes effect on the user's next request, without requiring re-login.
- [ ] The frontend hides actions the user cannot perform, and the API still rejects them when invoked directly
      from `backend/requests/permissions.http`.
- [ ] The permission matrix screen saves changes atomically and marks system-locked roles as read-only.
- [ ] The full role-by-permission matrix for the ticket module is walked by hand and recorded in
      `backend/requests/permissions.http`, one request per cell that matters.
- [ ] Permission changes are written to the audit log with actor, role, and the permissions added and removed.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-08.
- **Depends on code areas or other stories:** the scope filter from story 06 and roles from story 08. Every
  feature story from here on declares its permissions against this catalogue.

## Technical hints

- Cache the effective-permission set per user and scope with a short TTL, and invalidate it on role change.
- Hiding UI is a convenience, never the control. The behaviour is the control.
- The startup guard is cheap to write and is the single highest-value safeguard in this story. Do not skip it.

## Out of scope

- Scope and tenancy filtering itself. That is story 06.
- API key and machine-to-machine authorisation. That is story 55.
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
