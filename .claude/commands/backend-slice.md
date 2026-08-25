---
description: Scaffold a backend DDD + CQRS vertical slice following the project's Clean Architecture conventions.
---

Create a vertical slice for `$ARGUMENTS` (e.g. `Tickets/AssignTicket` or `Customers/GetCustomerById`).

## Layout

For a use case named `<Context>/<UseCase>`:

- `src/Crm.Domain/<Context>/` - the aggregate root, its child entities, value objects, domain events, and
  domain errors. No EF Core, no MediatR, no ASP.NET.
- `src/Crm.Application/<Context>/Commands/<UseCase>/`
  - `<UseCase>Command.cs` - request record implementing `IRequest<Result<T>>`, with `[RequirePermission]`
  - `<UseCase>CommandHandler.cs` - loads the aggregate, calls one intention-revealing method on it, saves
  - `<UseCase>CommandValidator.cs` - FluentValidation, shape and format only
- Queries live under `Queries/<UseCase>/`, project straight to a DTO with `.Select(...)`, use `AsNoTracking()`,
  and bypass the aggregate entirely.
- `src/Crm.Infrastructure/<Context>/` - `IEntityTypeConfiguration<T>`, repository implementation, external
  service adapters that translate foreign models into domain types.
- `src/Crm.Api/Endpoints/<Context>Endpoints.cs` - minimal-API endpoints registered through `IEndpointGroup`.
- `backend/requests/<context>.http` - a worked request per new endpoint, committed.

## DDD rules

- The handler orchestrates; the aggregate decides. Business rules live in the domain, never in the handler.
- No public setters and no public parameterless constructors on entities. Mutate through named methods
  (`ticket.Escalate(reason, by)`), never through property assignment.
- One transaction touches one aggregate. Cross-aggregate effects go through a domain event.
- Reference other aggregates by strongly-typed id only, never by navigation property.
- Anything without identity is a value object: immutable, self-validating, equality by value.
- An aggregate is always valid. A factory or constructor that cannot satisfy the invariants fails outright.
- Name types and methods in the story's own words - the ubiquitous language beats a generic CRUD verb.

## Rules

- Dependency direction is Domain <- Application <- Infrastructure/Api. Never reference outward.
- Handlers return `Result`/`Result<T>`; do not throw for expected business failures.
- Every write is tenant, department, and branch scoped and writes an audit entry.
- Add the EF migration in the same change as the schema move.
- **No test project and no test files** - see the testing policy in `CLAUDE.md`. Verify with
  `dotnet build` (zero warnings) plus the `.http` file against a running API.
- Mirror the naming and folder shape of the nearest existing slice before inventing anything.
