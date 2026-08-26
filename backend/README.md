# Crm backend

.NET 10, DDD inside Clean Architecture, CQRS with MediatR, EF Core on SQL Server. See `CLAUDE.md` at the
repository root for the full binding conventions — this file is the "how to run it" companion.

## Prerequisites

Verified on the development machine while this scaffold was built:

- .NET SDK **10.0.301** or later (`dotnet --list-sdks`)
- Docker with Compose **v2+** (`docker compose version`) — used for local SQL Server
- The EF Core CLI tool is **not** installed globally for this repo. It is pinned as a local tool (see below)
  to avoid a version mismatch with the EF Core packages the projects reference.

## Run it

```bash
# 1. Start SQL Server
cd backend
docker compose up -d

# 2. Restore the pinned local tools (dotnet-ef) — from the repository root
cd ..
dotnet tool restore

# 3. Build
dotnet build backend/Crm.sln

# 4. Run the API — migrates and seeds automatically in Development, no manual `dotnet ef` step needed
dotnet run --project backend/src/Crm.Api
```

The API listens on the URL printed at startup (Kestrel's default `http://localhost:5080` in Development
unless overridden). Swagger UI is at `/swagger` in Development — see "API surface: Areas and Swagger" below.

**Port 1433 may already be taken** by another project's SQL Server container on a shared dev machine.
`docker-compose.yml` here maps to **14330** instead (`appsettings.Development.json` and
`CrmDbContextFactory`'s design-time default both point at `localhost,14330`). If your machine is free on
1433, there is no need to change this — it is only a remap to coexist with another container, not a
requirement.

## Migrations

Use the wrapper — it fixes the `--project` / `--startup-project` pair so it does not have to be retyped (and
mistyped) in every future story:

```bash
./backend/scripts/ef.ps1 migrations add <Name> --output-dir Persistence/Migrations
./backend/scripts/ef.ps1 database update
```

Equivalent to, run from `backend/`:

```bash
dotnet ef migrations add <Name> --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api
```

`dotnet ef --version` should print **10.0.11** (the pinned local tool). If it prints something older, run
`dotnet tool restore` from the repository root first — the tool errors, rather than warns, on a version older
than the EF Core packages referenced here.

**Rollback:** `dotnet ef database update 0` drops every table this project has ever migrated. That is safe
**only** while there is no production data — true today, false from Story 11 onward. Do not treat it as a
general-purpose rollback once real aggregates exist.

## Verify

There is **no test project in this repository** — see "No automated tests" below. Verification is:

```bash
dotnet build backend/Crm.sln   # must be clean, zero warnings
```

...plus exercising the `.http` files under `backend/requests/` against a running API (VS Code's REST Client
extension, JetBrains' HTTP Client, or `curl` all work). Start with `backend/requests/health.http`, then
`backend/requests/persistence.http`.

## Add a new vertical slice

Every use case follows the shape of the **canonical sample slice** at `src/Crm.Application/Sample/`
(deleted, along with its controllers, once Story 11 lands the first real feature):

```
src/Crm.Application/<Context>/{Commands,Queries}/<UseCase>/
    <UseCase>{Command,Query}.cs            request record : ICommand<T> / ICommand / IRequest<Result<T>>
    <UseCase>{Command,Query}Handler.cs     handler, depends only on abstractions
    <UseCase>{Command,Query}Validator.cs   FluentValidation
```

Commands implement `ICommand` / `ICommand<T>` (`Crm.Application.Common.Messaging`) rather than a bare
`IRequest<Result>` — that marker is what tells `TransactionBehavior` to open a transaction around the
request. Queries implement `IRequest<Result<T>>` directly and never open one.

Handlers return `Result` / `Result<T>` (`src/Crm.Domain/Common/Results/`), never throw for expected business
failures. `Crm.Api.Common.ResultEndpointExtensions.ToActionResult()` translates a failed `Result` into an RFC
9457 problem-details `ActionResult` — a deliberately minimal mapper that Story 03 replaces with the global
exception handler and a static class of stable `type` URIs. Do not build that mapping framework out further
in the meantime.

## API surface: Areas and Swagger

`Crm.Api` uses **MVC controllers**, not minimal APIs, organised into one **Area per persona**:

```
src/Crm.Api/Areas/
  Agent/Controllers/     agent workspace       (stories 20, 21, 28-31)
  Portal/Controllers/    customer self-service  (stories 47-50)
  Admin/Controllers/     tenant admin + reports (stories 08-10, 51-54)
  Public/Controllers/    versioned external API (stories 55-57)
```

Every controller derives from `Crm.Api.Common.ApiControllerBase` and carries `[Area(CrmAreas.X)]` — the
`[area]` token in `ApiControllerBase`'s `[Route("api/[area]/[controller]")]` template expands from that
attribute. **A controller that forgets the `[Area]` attribute produces a literal `/api/[area]/...` route that
404s and appears in no Swagger document at all** — there is no compiler error, so double-check it.

Each Area publishes its own Swagger document — `/swagger/agent/swagger.json`, `/swagger/portal/swagger.json`,
`/swagger/admin/swagger.json`, `/swagger/public/swagger.json` — behind a Swagger UI with a document picker at
`/swagger`, Development-only. Adding a fifth Area is a one-line change to `Crm.Api.Areas.CrmAreas.All`.

No Area has `[Authorize]` yet — Stories 07 and 09 own authentication and the per-Area policies. Each
`Areas/<Area>/Controllers/_area.md` carries a `Story 09` note on which policy that Area will need.

## The layer rule

Dependency direction is strictly `Api -> Infrastructure -> Application -> Domain`. `Crm.Domain` has:

- **No project references at all.** Adding one is a design mistake, not a style nit.
- **No package reference to EF Core, MediatR, or ASP.NET.** Enforced two ways:
  - Structurally: there is nothing to reference, so `using Microsoft.EntityFrameworkCore;` fails with
    `CS0246` in the shipped state.
  - If the package *is* ever added, `Microsoft.CodeAnalysis.BannedApiAnalyzers` (wired via
    `BannedSymbols.txt` in each project + `.editorconfig`) fails the build with `RS0030` on first use of a
    banned namespace or member. `DateTime.Now` / `DateTime.Today` are banned solution-wide the same way —
    use `DateTimeOffset.UtcNow`, or `TimeProvider` where the read must be controllable (see below).

**`Crm.Application` carries no EF Core reference either.** `TransactionBehavior` (the MediatR pipeline
behaviour that opens a transaction, commits, and dispatches domain events after commit) lives in
`Crm.Application` but talks only to `Crm.Application.Common.Abstractions.IUnitOfWork` — an abstraction, not
`DbContext` directly. `Crm.Infrastructure`'s `CrmDbContext` is the only implementation, and it translates EF's
`DbUpdateConcurrencyException` into the Application-owned `ConcurrencyConflictException` at that boundary, so
`TransactionBehavior` can react to a concurrency conflict without ever referencing EF Core.

## Persistence: auditing, soft delete, concurrency

- **`AuditingSaveChangesInterceptor`** (`Crm.Infrastructure/Persistence/Interceptors/`) stamps
  created/modified-by and -at from `ICurrentUserService` and `TimeProvider`, and turns a delete of an
  `ISoftDeletable` aggregate into a flag update instead of a SQL `DELETE`. **If you touch this file, read its
  XML doc comments first** — the soft-delete recipe (`Deleted` → `Unchanged`, not `Deleted` → `Modified`) and
  the owned-reference check in `Audit()` both exist to work around real EF Core change-tracking behaviour that
  was reproduced and is not obvious from the EF Core docs.
- **Every aggregate root carries a `rowversion` column** (`IHasRowVersion`, mapped with `.IsRowVersion()`). A
  command that updates an aggregate the client already read must call
  `IRepository<T, TId>.SetOriginalRowVersion(aggregate, clientRowVersion)` before saving — see
  `RenameSampleCommandHandler` — or a stale write silently succeeds instead of returning **409**.
- **The soft-delete query filter is registered with EF Core 10's _named_ `HasQueryFilter` overload**
  (`CrmDbContext.SoftDeleteFilterName`). Story 06 adds the tenant/department/branch filter as a **second**
  named filter. **Never switch to the unnamed overload** — it silently replaces a prior filter instead of
  composing with it, which would resurrect soft-deleted rows product-wide with no build error.
- **Value objects need a constructor whose parameter name matches the mapped property name**
  (case-insensitive) — `SampleName`'s constructor takes `value` because the owned-type configuration maps
  `p.Value`. A mismatch fails at model build with `InvalidOperationException: No suitable constructor was
  found`, not at the call site. Applies to every later value object (`EmailAddress`, `PhoneNumber`, `Money`, …).
- **The clock is only reachable through `TimeProvider`**, injected wherever a timestamp is needed —
  `DateTimeOffset.UtcNow` is banned solution-wide (see "The layer rule" above) precisely so a handler or
  aggregate method cannot read it directly. A domain method that needs an event timestamp takes it as a
  parameter (see `SampleAggregate.Rename`) rather than reading the clock itself.

## No automated tests

This project ships without automated tests — a deliberate decision recorded in `CLAUDE.md` ("Testing
policy"). Do not create a `tests/` folder, a `*.Tests.csproj`, or add xUnit/NUnit/FluentAssertions/
Testcontainers to any project file. The compiler (`TreatWarningsAsErrors`, nullable, the banned-API
analyzer) and the `.http` request files under `backend/requests/` are what take a test suite's place.

## Notable pinned versions

- **MediatR 12.4.1**, not the latest. 13.x+ is licensed under RPL-1.5 or a paid Lucky Penny Software
  licence; RPL-1.5 would require releasing this CRM's source. 12.4.1 is the last `Apache-2.0` release.
  Do not bump without a licence decision.
- **EF Core 10.0.11** (`Microsoft.EntityFrameworkCore`, `.SqlServer`, `.Relational`, `.Design`) — kept in
  lockstep with the `dotnet-ef` tool below. A tool older than the packages errors rather than warns.
- **`dotnet-ef` 10.0.11** via the local tool manifest at `.config/dotnet-tools.json` (repository root).
  Run `dotnet tool restore` before `dotnet ef ...` commands — the globally installed version on this
  machine (10.0.3) predates the EF Core packages referenced here and will error on the mismatch.
- **Swashbuckle.AspNetCore 10.2.3** — this is OpenAPI.NET v2: `OpenApiInfo` lives in `Microsoft.OpenApi`, not
  `Microsoft.OpenApi.Models`. Every pre-2025 Swashbuckle sample online has the old import.
