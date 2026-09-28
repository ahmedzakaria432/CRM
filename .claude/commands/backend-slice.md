---
description: Scaffold a backend DDD + CQRS vertical slice following the project's modular-monolith conventions.
---

Create a vertical slice for `$ARGUMENTS` (e.g. `Tickets/AssignTicket` or `Customers/GetCustomerById`).
The first segment is the **module** (see the module catalogue in `CLAUDE.md`); the second is the use case.

## Layout

For a use case named `<Module>/<UseCase>`, under `backend/src/Modules/<Module>/`:

- If the module does not exist yet, create it first by copying the shape of `backend/src/Modules/Sample/` - all
  five projects, `I<Module>UnitOfWork`, `<Module>DbContext` (own schema), the design-time factory, `<Module>Module :
  IModule`, and the two host lines (`Crm.Api.csproj` + `Crm.Api/Modules/ModuleCatalog.cs`). See
  `backend/README.md`, "Add a module".
- `Crm.Modules.<Module>.Domain/` - the aggregate root, its child entities, value objects, domain events, and
  domain errors. References only `Crm.SharedKernel`. No EF Core, no MediatR, no ASP.NET.
- `Crm.Modules.<Module>.Application/Commands/<UseCase>/`
  - `<UseCase>Command.cs` - request record implementing `ICommand<T>` / `ICommand`, with `[RequirePermission]`
  - `<UseCase>CommandHandler.cs` - loads the aggregate, calls one intention-revealing method on it, saves through
    `I<Module>UnitOfWork`; reads the clock only through `IDateTime`
  - `<UseCase>CommandValidator.cs` - FluentValidation, shape and format only
- Queries live under `Queries/<UseCase>/`, project straight to a DTO with `.Select(...)`, use `AsNoTracking()`,
  and bypass the aggregate entirely.
- `Crm.Modules.<Module>.Infrastructure/` - `IEntityTypeConfiguration<T>` under `Persistence/Configurations/`,
  `AddRepository<...>()` registration in `<Module>Module`, external service adapters that translate foreign models
  into domain types.
- `Crm.Modules.<Module>.Presentation/Controllers/<Area>/<Resource>Controller.cs` - MVC controller deriving from
  `ApiControllerBase` with `[Area(CrmAreas.X)]`. Controller names are unique within an Area across modules.
- `Crm.Modules.<Module>.Contracts/` - only if another module needs this: an API interface method or an
  integration event record. Primitives and contract DTOs only.
- `backend/requests/<feature>.http` - a worked request per new endpoint, committed.

## Module rules

- Another module is reachable only through its `Contracts` project - a synchronous API interface for a read, an
  integration event for "this happened". The build fails with `CRM0001` otherwise; never weaken
  `Directory.Build.targets`.
- One command, one module, one transaction. Never send another module's command from a handler.
- No foreign keys, joins, or queries into another module's schema. An id from another module is a plain value.
- Domain events stay inside the module; republish as an integration event if another module must react.

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

- Within a module: Presentation -> Application -> Domain, Infrastructure -> Application. Never reference outward.
- Handlers return `Result`/`Result<T>`; do not throw for expected business failures.
- `System.DateTime` is banned; use `DateTimeOffset` values and inject `IDateTime` to read the clock.
- Every write is tenant, department, and branch scoped and writes an audit entry.
- Add the EF migration in the same change as the schema move:
  `backend/scripts/ef.ps1 -Module <Module> migrations add <Name>`.
- **No test project and no test files** - see the testing policy in `CLAUDE.md`. Verify with
  `dotnet build` (zero warnings, no `CRM0001`) plus the `.http` file against a running API.
- Mirror the naming and folder shape of the nearest existing slice before inventing anything.
