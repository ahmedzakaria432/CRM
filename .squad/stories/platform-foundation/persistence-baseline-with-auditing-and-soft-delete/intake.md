# Story 02 — Persistence baseline with auditing and soft delete

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/persistence-baseline-with-auditing-and-soft-delete/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-02`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Persistence baseline with auditing and soft delete
```

---

## Description

Establish the data-layer conventions every aggregate inherits, so no later story reinvents auditing, soft delete,
concurrency, value-object mapping, or the migration workflow.

- `CrmDbContext` in `Crm.Infrastructure` with `IEntityTypeConfiguration<T>` discovery by assembly scan.
- Persistence conventions for the DDD model: aggregate roots get their own `DbSet`, child entities do not and are
  reached only through their root; value objects map as owned types or through a `ValueConverter`; strongly-typed
  ids map via a `ValueConverter`; private setters and backing fields are mapped explicitly.
- Generic `IRepository<TAggregate, TId>` exposing aggregate roots only, plus the specification base type that
  later stories use to express reusable query intent.
- A `SaveChangesInterceptor` that stamps created and modified by and at from `ICurrentUserService`, converts
  deletes of `ISoftDeletable` into flag updates, and dispatches domain events after a successful commit.
- A global query filter excluding soft-deleted rows, with an explicit `IgnoreQueryFilters()` escape hatch for
  admin and audit reads.
- Optimistic concurrency via a rowversion column on every aggregate root, surfaced as HTTP 409.
- UTC conversion so all timestamps persist as UTC, plus an analyzer rule banning `DateTime.Now`.
- Migration and seeding workflow: `Crm.Infrastructure/Persistence/Migrations`, an idempotent seeder, and a
  `dotnet ef` wrapper script.

## Acceptance criteria

- [ ] `dotnet ef migrations add Initial` and `dotnet ef database update` both succeed against the compose SQL Server.
- [ ] A sample aggregate with a value object and a child entity round-trips correctly, with the value object
      persisted as owned columns and the child reachable only through its root.
- [ ] `DbContext` exposes a `DbSet` for the aggregate root only; the child entity has no `DbSet`.
- [ ] Saving an entity populates created-by and created-at; updating it populates modified-by and modified-at.
- [ ] Removing an `ISoftDeletable` entity sets the deleted flag and timestamp and issues no SQL DELETE, confirmed
      from the EF Core generated-SQL log.
- [ ] A default query does not return soft-deleted rows; `IgnoreQueryFilters()` does.
- [ ] A concurrent update to the same row returns HTTP 409, not a 500.
- [ ] Domain events raised inside an aggregate method fire only after the transaction commits, and not at all if
      it rolls back, confirmed from the log output of a deliberately failing save.
- [ ] All persisted timestamps are UTC when written from a machine in a non-UTC timezone.
- [ ] `DateTime.Now` anywhere in the solution fails the build.
- [ ] The seeder is idempotent: running it twice produces the same row counts.

## Dependencies

- **Blocked by / related ids:** CRM-01.
- **Depends on code areas or other stories:** `Crm.Infrastructure` and the `Crm.Domain` building blocks from
  story 01.

## Technical hints

- Use `ValueConverter` for strongly-typed ids rather than exposing primitive Guid properties on aggregates.
- Map private setters with `UsePropertyAccessMode(PropertyAccessMode.Field)`; the domain model must not gain
  public setters to satisfy EF Core.
- Turn on EF Core sensitive-data logging in Development only, and use it to verify the interceptor behaviour.

## Out of scope

- Feature aggregates such as customer and ticket. Only building blocks and infrastructure here.
- The tenant, department, and branch filter itself. Story 06 extends the filter added here.
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
