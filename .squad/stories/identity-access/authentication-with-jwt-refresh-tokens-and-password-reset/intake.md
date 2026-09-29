# Story 07 — Authentication users roles and permissions

Story intake for the **Identity and Access** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/identity-access/authentication-with-jwt-refresh-tokens-and-password-reset/intake.md`.

---

## Feature

- **Feature name (display):** Identity and Access
- **Feature slug (folder under `plans/`):** `identity-access`
- **Module (backend):** `Identity` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-07`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `identity-access`

---

## Title

```
Authentication users roles and permissions
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). **Absorbs stories 08 (user and role
management) and 09 (permission model)** in a reduced form: fixed system roles, a code-defined permission map, and
basic user administration.

---

## Description

Sign-in for staff, the token lifecycle every endpoint depends on, the users who sign in, and what each role may do.
The portal reuses this stack with a separate audience in story 47.

- The new `Identity` module (`Crm.Modules.Identity.*`, schema `identity`). `User` as an aggregate root with
  `EmailAddress`, display name, hashed password (ASP.NET Core `PasswordHasher<T>`, no full ASP.NET Identity UI),
  preferred locale, active flag, and lockout state.
- Memberships (department + role) stay in story 06's `Organisation` module; at login Identity reads them through
  `IOrganisationModuleApi.GetMembershipsAsync`. The seeded users reuse story 06's well-known user ids.
- Fixed system roles: **Administrator**, **Supervisor**, **Agent**. A code-defined permission catalogue
  (`tickets.read`, `tickets.write`, `tickets.assign`, `customers.read`, `customers.write`, `admin.organisation`,
  `admin.users`, `admin.branding`) and a static role → permission map.
- `RefreshTokenFamily` aggregate: rotating refresh tokens stored hashed, reuse of a rotated token revokes the
  family.
- Access token: short-lived JWT with `sub`, display name, and the `CrmClaimTypes` from story 03 (tenant, department
  ids) plus roles. Lifetimes from options, never literals.
- Endpoints: `POST login`, `POST refresh`, `POST logout`, `GET me` (profile, locale, memberships, effective
  permissions), `POST change-password`.
- Authorisation: `[RequirePermission]` on every MediatR request, enforced by the existing `AuthorizationBehavior`
  placeholder, plus a startup guard that refuses to start if any request in any module lacks a declaration.
  JWT bearer authentication on every Area except Portal (story 47) and Public.
- Deletes story 06's Development-only `X-Dev-User` authentication handler; JWT is now the only way in.
- User administration (Admin Area, `admin.users`): list with search and paging, create with an initial password,
  edit display name, deactivate and reactivate (deactivation revokes refresh tokens). The user screen manages
  memberships through story 06's membership endpoints.
- Lockout after 5 failed attempts for a configured window; one generic failure message that never reveals whether
  an account exists.
- Development seed: `admin@crm.local`, a supervisor, and two agents across the seeded departments.
- Frontend: login page, auth Pinia store, route middleware for authenticated/guest routes, the story 04
  `AuthSession` wired to the real refresh endpoint, locale preference from `GET /me` (story 05), a `useCan()`
  composable hiding actions the user cannot perform, and the users admin screens.
- `backend/requests/auth.http` and `backend/requests/users.http` covering every endpoint and failure mode.

## Acceptance criteria

- [ ] Valid credentials return access + refresh tokens; invalid credentials return 401 with a generic message.
- [ ] An expired access token is refreshed once and the original request retried; reusing a rotated refresh token
      revokes the family and forces re-login.
- [ ] Logout revokes the presented refresh token. Deactivating a user blocks sign-in and revokes their tokens.
- [ ] Five failed attempts lock the account for the configured window.
- [ ] `EmailAddress` rejects a malformed address at construction.
- [ ] Every request declares a permission; the app refuses to start if one does not.
- [ ] A request without the permission returns 403 naming it; without a token, 401. The handler never executes.
- [ ] A user holds different roles in different departments, and effective permissions follow the active scope.
- [ ] `GET /me` returns profile, locale, memberships, and permissions; the frontend hides actions via `useCan()`
      and the API still rejects them when called directly from `users.http`.
- [ ] Login and user screens work in `ar` RTL and `en`, at 375/768/1280, fully keyboard-navigable.
- [ ] Every path exercised from `auth.http` / `users.http` against a running API.

## Dependencies

- **Blocked by / related ids:** CRM-06.
- **Depends on code areas or other stories:** the organisation model and scope contract from story 06, the
  `AuthSession` abstraction and API client from story 04, `CrmClaimTypes` from story 03.

## Technical hints

- Store only a hash of the refresh token; keep a family id per login.
- The startup guard is the highest-value safeguard here. Do not skip it.
- This is the highest-risk story to ship without tests: walk every negative path in `auth.http` deliberately.

## Out of scope (deferred post-MVP)

- Forgot/reset password by email and two-factor (no email channel in the MVP; an admin resets passwords instead).
- Invite-by-email activation, custom roles, the permission matrix screen, agent availability and load, avatars,
  signatures (stories 08/09 full scope).
- Audit entries for identity changes (story 10). Portal accounts (story 47). SSO. Any test project.

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
