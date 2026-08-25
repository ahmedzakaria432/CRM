# Story 01 — Backend solution scaffold with DDD and Clean Architecture

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/backend-solution-scaffold-with-ddd-and-clean-architecture/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-01`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Backend solution scaffold with DDD and Clean Architecture
```

---

## Description

Stand up the .NET 10 solution that every later backend story builds on. This story delivers the empty-but-correct
skeleton: projects, dependency direction, the DDD building blocks, the CQRS pipeline, and a health endpoint that
proves the wiring works.

- Create `backend/Crm.sln` with `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, and `Crm.Api`.
- Enforce the dependency direction structurally: `Crm.Domain` declares zero project references, and a
  banned-API analyzer ruleset blocks the EF Core, MediatR, and ASP.NET namespaces inside it, so an outward
  reference fails the build rather than passing review.
- Domain building blocks in `Crm.Domain/Common`: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`,
  `IDomainEvent`, `IHasDomainEvents`, a strongly-typed id base, and a `DomainError` type. Bounded-context
  folders are created empty, one per feature area, so later stories have an obvious home.
- Register MediatR and wire the pipeline behaviours in order: validation, authorisation, logging, transaction.
- Add the shared `Result` and `Result<T>` types and the `Error` type used by every handler.
- Add `Directory.Build.props` with `net10.0`, nullable enabled, `TreatWarningsAsErrors`, and the shared analyzer
  set. With no test suite on this project the compiler is the only automated check, so these stay strict.
- Expose `GET /health` and `GET /health/ready`, plus one sample query slice that becomes the reference shape for
  every future slice, and `backend/requests/health.http` to exercise it.
- Add `docker-compose.yml` with SQL Server 2022 for local development.

## Acceptance criteria

- [ ] `dotnet build backend/Crm.sln` succeeds with zero warnings.
- [ ] `Crm.Domain.csproj` contains no `ProjectReference` at all and no package reference to EF Core, MediatR, or
      ASP.NET.
- [ ] Adding `using Microsoft.EntityFrameworkCore;` inside `Crm.Domain` fails the build via the analyzer ruleset.
- [ ] `AggregateRoot<TId>` exposes domain events as a read-only collection with a protected `Raise` method, and
      `ValueObject` implements structural equality.
- [ ] `GET /health` returns 200 and reports database reachability.
- [ ] The sample slice lives at `Crm.Application/Sample/Queries/GetSample/` with request, handler, and validator
      in three files, and is annotated as the canonical slice shape.
- [ ] A validation failure from any handler returns HTTP 400 with an RFC 9457 problem-details body listing the
      field errors, verified from `backend/requests/health.http`.
- [ ] `docker compose up` starts SQL Server and the API connects to it.
- [ ] `backend/README.md` documents how to run, how to verify, how to add a new slice, and where each bounded
      context lives.

## Dependencies

- **Blocked by / related ids:** none. This is the first story.
- **Depends on code areas or other stories:** nothing. Every other backend story depends on this one.

## Technical hints

- Keep the composition root in `Crm.Api/Program.cs` thin; each layer exposes one `AddApplication()` or
  `AddInfrastructure()` extension method.
- Use `Microsoft.CodeAnalysis.BannedApiAnalyzers` for the layer rule. It is a build-time check, not a test
  project, which is what this project wants.
- The sample slice is deleted by the story that lands the first real feature. Note that in a code comment.

## Out of scope

- Any real aggregate, database table, or migration. That is story 02 and the feature stories.
- Authentication and authorisation implementation. That is story 07; this story only registers the pipeline
  behaviour placeholder.
- All frontend work.
- Any test project. This project ships without automated tests by decision.


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
