# Story 08 — User and role management

Story intake for the **Identity and Access** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/identity-access/user-and-role-management/intake.md`.

---

## Feature

- **Feature name (display):** Identity and Access
- **Feature slug (folder under `plans/`):** `identity-access`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-08`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `identity-access`

---

## Title

```
User and role management
```

---

## Description

Administration of the people who use the CRM, and the roles that describe what they do.

- Backend: user CRUD with invite-by-email activation, deactivate and reactivate, force password reset, unlock,
  and assignment of departments, branches, and teams from story 06.
- Agent profile as part of the user aggregate: display name per locale as a `LocalizedText` value object, avatar,
  job title, outbound email signature per locale, skills or tags, languages spoken, and maximum concurrent load.
- Role as an aggregate root. System roles are seeded (Administrator, Supervisor, Agent, Read-only, Portal User)
  and are immutable; custom roles are editable. Role assignment is per department, so a user can be a supervisor
  in one department and an agent in another.
- Agent availability as a value object on the user aggregate: available, busy, away, or offline, with an
  out-of-office window and a delegate, consumed by auto-assignment in story 34.
- Frontend: user list with search and filters, user detail and edit drawer, invite flow, bulk deactivate, and
  role management screens.
- `backend/requests/users.http` covering invite, activate, deactivate, role change, and out-of-office.

## Acceptance criteria

- [ ] An admin can invite a user by email; the invite expires, can be resent, and the user sets their own
      password on activation.
- [ ] Deactivating a user immediately blocks sign-in and revokes their refresh tokens, without deleting history.
- [ ] A user can hold a different role in each department they belong to, and the effective role resolves from
      the active scope.
- [ ] System roles cannot be renamed or deleted; the aggregate rejects the attempt rather than the UI hiding it.
- [ ] Deleting a custom role with members is rejected until its members are reassigned.
- [ ] Setting an agent out of office with a delegate routes new assignments to the delegate.
- [ ] An out-of-office window ending before it starts is rejected at construction.
- [ ] Agent maximum concurrent load is stored and exposed to the auto-assignment engine.
- [ ] The user list supports search by name, email, department, branch, role, and status, and paginates
      server-side.
- [ ] Every mutation writes an audit entry naming actor, target, and before and after values.
- [ ] A non-admin cannot reach any user or role management endpoint or screen, verified from `users.http` with a
      non-admin token.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-07.
- **Depends on code areas or other stories:** the identity stack from story 07 and the organisation model from
  story 06. Stories 34 and 35 consume agent load and availability.

## Technical hints

- Model the department-scoped role as a membership row on the Membership aggregate, not as a flat user-role
  table. A flat table makes the scoped case impossible to express later.
- The email signature is per user and per locale. Store both from the start.

## Out of scope

- The permission catalogue and its enforcement. That is story 09.
- Agent performance metrics. Those are story 53.
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
