# Story 07 — Authentication with JWT refresh tokens and password reset

Story intake for the **Identity and Access** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/identity-access/authentication-with-jwt-refresh-tokens-and-password-reset/intake.md`.

---

## Feature

- **Feature name (display):** Identity and Access
- **Feature slug (folder under `plans/`):** `identity-access`

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
Authentication with JWT refresh tokens and password reset
```

---

## Description

Sign-in for agents, supervisors, and admins, plus the token lifecycle every other endpoint depends on. The
customer portal reuses this stack with a separate audience in story 47.

- ASP.NET Core Identity over EF Core, with the CRM user as an aggregate root in the Identity bounded context,
  linked to the Organisation model from story 06 by strongly-typed id.
- `RefreshTokenFamily` as an aggregate: a rotating chain with reuse detection, where revoking is a method on the
  aggregate rather than a bulk update in a handler.
- Value objects: `EmailAddress`, `HashedPassword`, `TokenFamilyId`, `PasswordResetToken` with its expiry.
- Access token as a short-lived JWT carrying roles, permissions, department ids, and branch ids as claims, plus a
  long-lived rotating refresh token stored hashed.
- Endpoints: login, refresh, logout, logout-all-devices, forgot-password, reset-password, change-password, and
  `GET /me`.
- Password policy, lockout after repeated failures, and a generic failure message that never reveals whether an
  account exists.
- Optional two-factor by emailed or texted one-time code for admin roles.
- Frontend: login page, forgot-password and reset-password pages, an auth Pinia store, route middleware for
  authenticated and guest routes, silent refresh, and a session-expiry prompt.
- `backend/requests/auth.http` covering every endpoint and every failure mode, including the reuse-detection path.

## Acceptance criteria

- [ ] Valid credentials return an access token and a refresh token; invalid credentials return 401 with a generic
      message and no account-existence hint.
- [ ] An expired access token is refreshed silently once and the original request is retried transparently.
- [ ] Reusing an already-rotated refresh token revokes the whole family and forces re-login, and the revocation
      is performed by a method on the token-family aggregate.
- [ ] Logout revokes the presented refresh token; logout-all revokes every token for the user.
- [ ] Five failed attempts lock the account for the configured window and write an audit entry.
- [ ] A password reset token is single-use, expires after the configured window, and is invalidated by a
      successful reset or by a password change, with expiry enforced inside the value object.
- [ ] `EmailAddress` rejects a malformed address at construction, so no invalid address can reach the database.
- [ ] `GET /me` returns profile, roles, permissions, departments, branches, and locale preference.
- [ ] Every non-public endpoint returns 401 without a token and 403 with a valid token lacking the permission.
- [ ] Tokens carry no personally identifying data beyond user id and display name.
- [ ] The login page works in `ar` and RTL and is fully keyboard-navigable.
- [ ] Every listed path is exercised from `backend/requests/auth.http` against a running API.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-04, CRM-06.
- **Depends on code areas or other stories:** the user and membership aggregates from story 06 and the API client
  interceptors from story 04.

## Technical hints

- Store only a hash of the refresh token, and keep a family id per login so reuse detection can revoke a chain.
- Keep token lifetimes in configuration bound through the options pattern, never as literals in code.
- This is the highest-risk story to ship without tests. Walk every path in `auth.http` deliberately, including
  the negative ones, before calling it done.

## Out of scope

- Roles and permission management screens. Those are stories 08 and 09.
- Customer portal registration and self-service sign-up. That is story 47.
- Single sign-on and external identity providers.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10. Domain-Driven Design inside Clean Architecture (`Crm.Domain` <- `Crm.Application` <- `Crm.Infrastructure` / `Crm.Api`), CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server, minimal APIs, `Result<T>` returns, RFC 9457 problem details.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
