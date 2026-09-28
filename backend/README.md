# Crm backend

.NET 10 **modular monolith**: one ASP.NET host, one module per bounded context, DDD + Clean Architecture inside
each module, CQRS with MediatR, EF Core on SQL Server with one schema per module. See `CLAUDE.md` at the
repository root for the full binding conventions (including the module catalogue) — this file is the "how to run
it" companion.

## Prerequisites

Verified on the development machine while this scaffold was built:

- .NET SDK **10.0.301** or later (`dotnet --list-sdks`)
- Docker with Compose **v2+** (`docker compose version`) — used for local SQL Server
- The EF Core CLI tool is **not** installed globally for this repo. It is pinned as a local tool (see below)
  to avoid a version mismatch with the EF Core packages the projects reference.

## Run it

```bash
# 1. Start SQL Server, Redis, and the telemetry dashboard (http://localhost:18888)
cd backend
docker compose up -d

# 2. Restore the pinned local tools (dotnet-ef) — from the repository root
cd ..
dotnet tool restore

# 3. Build
dotnet build backend/Crm.sln

# 4. Run the API — migrates every module and seeds automatically in Development, no manual `dotnet ef` step
dotnet run --project backend/src/Crm.Api
```

The API listens on the URL printed at startup (`http://localhost:5187`, from `src/Crm.Api/Properties/launchSettings.json`
unless overridden). Swagger UI is at `/swagger` in Development — see "API surface: Areas and Swagger" below.

**Port 1433 may already be taken** by another project's SQL Server container on a shared dev machine.
`docker-compose.yml` here maps to **14330** instead (`appsettings.Development.json` and
`ModuleDesignTimeDbContextFactory`'s design-time default both point at `localhost,14330`).

**Upgrading a dev database from before the modular-monolith move:** the pre-module `Initial` migration created
`dbo.Samples`, `dbo.SampleLines`, and `dbo.__EFMigrationsHistory`. They are no longer used — the Sample module now
lives in the `sample` schema. Drop them, or reset the volume with `docker compose down -v`. No production data
exists yet, so nothing is lost.

## Solution layout

```
src/
  Crm.Api/                                 host - Program.cs, Modules/ModuleCatalog.cs, Swagger, health checks
  BuildingBlocks/
    Crm.SharedKernel/                      Entity, AggregateRoot, ValueObject, StronglyTypedId, IDomainEvent, Result/Error
    Crm.BuildingBlocks.Application/        behaviours, ICommand, IRepository, IUnitOfWork, IUnitOfWorkResolver, IDateTime, Specification
    Crm.BuildingBlocks.Infrastructure/     ModuleDbContext, AddModuleDbContext, AddRepository, IModule, interceptor, SystemDateTime
    Crm.BuildingBlocks.Presentation/       ApiControllerBase, CrmAreas, AREAS.md, Result -> ProblemDetails
  Modules/
    Sample/                                canonical template module - deleted in Story 11
      Crm.Modules.Sample.Domain/
      Crm.Modules.Sample.Application/
      Crm.Modules.Sample.Contracts/
      Crm.Modules.Sample.Infrastructure/
      Crm.Modules.Sample.Presentation/
```

## Add a module

A module is created by the first story that needs it (the catalogue in `CLAUDE.md` says which). Copy the shape
of `src/Modules/Sample/`:

1. Five projects named `Crm.Modules.<Module>.{Domain,Application,Contracts,Infrastructure,Presentation}` under
   `src/Modules/<Module>/`, with the same `ProjectReference`s as the Sample projects (Presentation keeps
   `DisableTransitiveProjectReferences`). Add them to `Crm.sln` in the `src/Modules/<Module>` solution folder.
2. `Application`: `I<Module>UnitOfWork : IUnitOfWork` and an `AssemblyReference`.
3. `Infrastructure`: `<Module>DbContext : ModuleDbContext, I<Module>UnitOfWork` with `SchemaName = "<module>"`,
   a `<Module>DbContextFactory : ModuleDesignTimeDbContextFactory<...>`, and `<Module>Module : IModule`, whose
   `AddServices` calls `AddModuleDbContext`, `AddRepository` per aggregate, registers seeders
   (`IModuleSeeder`) and the Contracts implementations.
4. `Presentation`: an `AssemblyReference` and `Controllers/<Area>/`.
5. Host: add the Infrastructure and Presentation `ProjectReference`s to `Crm.Api.csproj` and one line to
   `Crm.Api/Modules/ModuleCatalog.cs`.
6. First migration: `./backend/scripts/ef.ps1 -Module <Module> migrations add Initial`.

## Module boundaries — enforced by the build

`Directory.Build.targets` checks every project's declared `ProjectReference`s before compilation and fails with
**`CRM0001`** when one breaks a rule:

| Project | May reference |
|---------|---------------|
| `Crm.SharedKernel` | nothing |
| `Crm.BuildingBlocks.*` | shared kernel and other building blocks — never a module |
| `Crm.Modules.X.Domain`, `Crm.Modules.X.Contracts` | `Crm.SharedKernel` only |
| `Crm.Modules.X.Application` | own Domain + Contracts, `Crm.BuildingBlocks.Application`, **other modules' Contracts** |
| `Crm.Modules.X.Infrastructure` | own Application, `Crm.BuildingBlocks.Infrastructure`, other modules' Contracts |
| `Crm.Modules.X.Presentation` | own Application, `Crm.BuildingBlocks.{Application,Presentation}`, shared kernel — never Domain |
| `Crm.Api` | building blocks, each module's Infrastructure + Presentation |

Every module project must be named `Crm.Modules.<Module>.<Layer>` — the rules key off the name. The Presentation
projects also set `DisableTransitiveProjectReferences`, so the module's Domain types are not even visible to a
controller (`CS0234` if you try).

## Migrations — one set per module

Use the wrapper; it takes the module name and fixes the `--project` / `--startup-project` pair:

```bash
./backend/scripts/ef.ps1 -Module Sample migrations add <Name>      # lands in Persistence/Migrations
./backend/scripts/ef.ps1 -Module Sample database update
```

Equivalent to, run from `backend/`:

```bash
dotnet ef migrations add <Name> --project src/Modules/Sample/Crm.Modules.Sample.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

Each module's migrations create its tables in its own schema and record themselves in
`<schema>.__EFMigrationsHistory`, so modules migrate independently. In Development the host applies every
module's migrations at startup (`MigrateAndSeedModulesAsync`), then runs every `IModuleSeeder`.

`dotnet ef --version` should print **10.0.11** (the pinned local tool). If it prints something older, run
`dotnet tool restore` from the repository root first.

**Rollback:** `ef.ps1 -Module X database update 0` drops that module's tables. Safe **only** while there is no
production data — true today, false from Story 11 onward.

## Verify

There is **no test project in this repository** — see "No automated tests" below. Verification is:

```bash
dotnet build backend/Crm.sln   # must be clean, zero warnings, no CRM0001
```

...plus exercising the `.http` files under `backend/requests/` against a running API (VS Code's REST Client
extension, JetBrains' HTTP Client, or `curl` all work). Start with `backend/requests/health.http`, then
`backend/requests/persistence.http`.

## Add a new vertical slice

Every use case follows the shape of the **canonical Sample module** at `src/Modules/Sample/` (deleted once Story 11
lands the first real feature):

```
src/Modules/<Module>/Crm.Modules.<Module>.Application/{Commands,Queries}/<UseCase>/
    <UseCase>{Command,Query}.cs            request record : ICommand<T> / ICommand / IRequest<Result<T>>
    <UseCase>{Command,Query}Handler.cs     handler, depends only on abstractions (I<Module>UnitOfWork, IRepository, IDateTime)
    <UseCase>{Command,Query}Validator.cs   FluentValidation
src/Modules/<Module>/Crm.Modules.<Module>.Presentation/Controllers/<Area>/<Resource>Controller.cs
```

Commands implement `ICommand` / `ICommand<T>` (`Crm.BuildingBlocks.Application.Messaging`) rather than a bare
`IRequest<Result>` — that marker is what tells `TransactionBehavior` to open a transaction, on the DbContext of the
module the command's assembly belongs to (`IUnitOfWorkResolver`). Queries implement `IRequest<Result<T>>` directly
and never open one.

Handlers return `Result` / `Result<T>` (`Crm.SharedKernel.Results`), never throw for expected business failures.
`ApiControllerBase.ToActionResult()` translates a failed `Result` into an RFC 9457 problem through
`ProblemDetailsFactory`, with a stable `type` from `ProblemTypes`; exceptions go through `GlobalExceptionHandler`.
See "Error responses" below.

## Talking to another module

- **Read it now:** call an interface from its Contracts project (see `ISampleModuleApi`). The owning module
  implements it in its Application project by sending its own query, so validation, authorisation, and scoping
  still apply.
- **Tell it something happened:** declare an integration event record in your Contracts and `IEventBus.Publish` it
  from the command handler; it is written to your outbox in your transaction and delivered after commit. The other
  module subscribes with an `IIntegrationEventConsumer<T>` in its Application project and handles it in its own
  transaction. See "Integration events and the outbox" below and `SampleRenamedIntegrationEvent` for the shape.
- **Never** reference another module's Domain/Application/Infrastructure, read its schema, or mutate its state
  inside your own transaction.

## API surface: Areas and Swagger

MVC controllers, not minimal APIs, organised into one **Area per persona** (`Agent`, `Portal`, `Admin`, `Public`
— see `Crm.BuildingBlocks.Presentation/AREAS.md`). Controllers live in module Presentation projects under
`Controllers/<Area>/`; the host adds each Presentation assembly as an MVC application part through
`ModuleCatalog`.

Every controller derives from `Crm.BuildingBlocks.Presentation.ApiControllerBase` and carries
`[Area(CrmAreas.X)]` — the `[area]` token in `[Route("api/[area]/[controller]")]` expands from that attribute.
**A controller that forgets the `[Area]` attribute produces a literal `/api/[area]/...` route that 404s and appears
in no Swagger document at all** — there is no compiler error, so double-check it. Controller names must be unique
within an Area across all modules.

Each Area publishes its own Swagger document — `/swagger/agent/swagger.json`, `/swagger/portal/swagger.json`,
`/swagger/admin/swagger.json`, `/swagger/public/swagger.json` — merging every module's controllers for that Area,
behind a Swagger UI at `/swagger`, Development-only. XML doc comments are read from every `Crm.*.xml` in the output.

No Area has `[Authorize]` yet — Stories 07 and 09 own authentication and the per-Area policies.

## Time

The clock is read only through **`IDateTime`** (`Crm.BuildingBlocks.Application.Abstractions`), implemented once by
`SystemDateTime` in `Crm.BuildingBlocks.Infrastructure`. Enforced by the banned-API analyzer (`RS0030`):

- `build/BannedSymbols.Clock.txt` (every project): `System.DateTime` (the type), `DateTimeOffset.UtcNow`,
  `DateTimeOffset.Now`.
- `build/BannedSymbols.TimeProvider.txt` (every project except `Crm.BuildingBlocks.Infrastructure`):
  `System.TimeProvider`.
- `build/BannedSymbols.Pure.txt` (SharedKernel, every Domain and Contracts project): EF Core, MediatR, ASP.NET,
  DI, `System.Data.Common`, `System.Net.Http`.

All three lists are wired once in `Directory.Build.props`, so a new project cannot forget them. A domain method
that needs a timestamp takes it as a parameter (see `SampleAggregate.Rename`) rather than reading the clock.

## Persistence: auditing, soft delete, concurrency

- **`AuditingSaveChangesInterceptor`** (`Crm.BuildingBlocks.Infrastructure/Persistence/Interceptors/`) is attached
  to every module DbContext. It stamps created/modified-by and -at from `ICurrentUserService` and `IDateTime`, and
  turns a delete of an `ISoftDeletable` aggregate into a flag update instead of a SQL `DELETE`. **If you touch this
  file, read its XML doc comments first** — the soft-delete recipe (`Deleted` → `Unchanged`, not `Deleted` →
  `Modified`) and the owned-reference check in `Audit()` both work around real EF Core change-tracking behaviour.
- **Every aggregate root carries a `rowversion` column** (`IHasRowVersion`, mapped with `.IsRowVersion()`). A
  command that updates an aggregate the client already read must call
  `IRepository<T, TId>.SetOriginalRowVersion(aggregate, clientRowVersion)` before saving — see
  `RenameSampleCommandHandler` — or a stale write silently succeeds instead of returning **409**.
- **The soft-delete query filter is registered with EF Core 10's _named_ `HasQueryFilter` overload**
  (`ModuleDbContext.SoftDeleteFilterName`), for every module. Story 06 adds the tenant/department/branch filter as
  a **second** named filter in `ModuleDbContext`. **Never switch to the unnamed overload** — it silently replaces a
  prior filter instead of composing with it.
- **Value objects need a constructor whose parameter name matches the mapped property name**
  (case-insensitive) — `SampleName`'s constructor takes `value` because the owned-type configuration maps
  `p.Value`. A mismatch fails at model build with `InvalidOperationException: No suitable constructor was found`.

## Error responses

Every error is RFC 9457 `application/problem+json` with a stable `type` (constants in
`Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs`), a fixed English `title`, `instance`, `traceId`,
`correlationId`, and — for Result failures and domain rules — the error `code`. The frontend maps `type` (and
`code`) to localised messages. Never rename a type; add one.

| Status | `type` | When |
|---|---|---|
| 400 | `urn:crm:problem:validation` | A validator rejected the input, or the body could not be bound (`errors` by field) |
| 400 | `urn:crm:problem:bad-request` | The request could not be read at all |
| 401 | `urn:crm:problem:unauthorized` | Not authenticated |
| 403 | `urn:crm:problem:forbidden` | `Error.Forbidden` |
| 404 | `urn:crm:problem:not-found` | `Error.NotFound`, or no such route |
| 405 | `urn:crm:problem:method-not-allowed` | Wrong HTTP method |
| 409 | `urn:crm:problem:conflict` | `Error.Conflict`, including the concurrency clash (`code: Persistence.ConcurrencyConflict`) |
| 415 | `urn:crm:problem:unsupported-media-type` | Wrong `Content-Type` |
| 422 | `urn:crm:problem:domain-rule` | An aggregate threw `DomainException` (`code` = the `DomainError` code) |
| 429 | `urn:crm:problem:rate-limited` | Public-Area rate limit; see `Retry-After` |
| 500 | `urn:crm:problem:internal` | Anything unhandled. The detail is fixed text — **never** the exception message |
| 503 | `urn:crm:problem:dependency-unavailable` | An outbound call failed, timed out, or its circuit is open |

`backend/requests/diagnostics.http` produces every row above on demand (Development only).

## Observability

`docker compose up -d` also starts **Redis** on `16379` and the **Aspire dashboard** at `http://localhost:18888`
(OTLP ingest on `14317`, `OpenTelemetry:OtlpEndpoint` in `appsettings.Development.json`). The dashboard shows
traces, metrics, and the structured logs.

- **Logs** are compact JSON on the console in every environment, plus OTLP when an endpoint is configured. Levels
  live in the `Serilog` section of `appsettings*.json` — there is no `Logging` section.
- **Correlation:** send `X-Correlation-Id` (1–64 chars of `A-Z a-z 0-9 . _ : -`) or get one generated (equal to the
  trace id). It comes back on every response, is on every log line, is forwarded on outbound HTTP calls, and is
  stored on outbox rows so a delivery logs under the request that caused it. To find a request: search the
  dashboard's structured logs for `CorrelationId`, or its traces for `crm.correlation_id`.
- **Traces** cover HTTP in (not `/health`), HTTP out (one span per attempt, so retries are visible), EF Core, every
  MediatR request, and every outbox delivery (parented to the publishing request's trace).
- **Metrics:** ASP.NET Core, HttpClient, runtime, Polly, and `crm.request.duration` per MediatR request.
- **Redaction** is by property name, for objects logged with `{@X}` and for top-level properties. Free text inside an
  exception message is **not** redacted — a connection string inside a `SqlException` reaches the log (never the
  response). EF sensitive-data logging (Development only) prints SQL parameter values.

## Caching

`ICacheService` over Redis (`ConnectionStrings:Redis`); with no Redis configured, an in-process cache and a startup
warning — correct on **one** instance only. A query implementing `ICacheableQuery` is cached by `CachingBehavior`
under the caller's tenant/department/branch scope; a command implementing `ICacheInvalidatingCommand` invalidates its
tags after commit, in every scope. If Redis goes down, the first cache call times out after 2 s and the cache is then
bypassed for 30 s (uncached reads, Warning log); an invalidation that cannot reach Redis is logged at Error and the
entry stays stale until its TTL (`Caching:DefaultExpiration`, 5 min).

## Integration events and the outbox

Every module has `{schema}.outbox_messages` and `{schema}.outbox_message_consumers`, mapped by `ModuleDbContext` —
a new module's Initial migration includes them. A command handler calls `IEventBus.Publish`; the row is written in
the command's transaction. `OutboxDispatcher` polls every `Outbox:PollingInterval`, delivers to each
`IIntegrationEventConsumer<T>` in its own module's transaction, and records one consumer row per delivery.

- **Retry:** a failing consumer is retried with backoff 2, 4, 8, 16 s…; consumers that already succeeded are
  skipped. After `Outbox:MaxAttempts` (5) the row is **dead-lettered**: `ProcessedAtUtc IS NULL AND Attempts >= 5`,
  with `LastError`. It is never deleted automatically.
- **Replay** a dead letter: `UPDATE {schema}.outbox_messages SET Attempts = 0, NextAttemptAtUtc = NULL WHERE Id = …`.
  Consumers that already applied it skip it. (With `sqlcmd`, pass `-I` — the filtered index needs QUOTED_IDENTIFIER.)
- **Single instance assumed.** Two API instances would both deliver a message; the consumer row's primary key stops
  the second from committing, but a consumer's side effects outside its transaction could double-fire. Add a
  row-claim (`UPDLOCK, READPAST`) before running more than one instance.
- **Rate limiting behind a proxy:** the Public-Area limiter partitions on `RemoteIpAddress`; add
  `UseForwardedHeaders` with known proxies before deploying behind one.

## No automated tests

This project ships without automated tests — a deliberate decision recorded in `CLAUDE.md` ("Testing policy").
Do not create a `tests/` folder, a `*.Tests.csproj`, or add xUnit/NUnit/FluentAssertions/Testcontainers/
NetArchTest to any project file. The compiler (`TreatWarningsAsErrors`, nullable, the banned-API analyzer), the
`CRM0001` module boundary rules, and the `.http` request files under `backend/requests/` take a test suite's place.

## Notable pinned versions

- **MediatR 12.4.1**, not the latest. 13.x+ is licensed under RPL-1.5 or a paid Lucky Penny Software
  licence; RPL-1.5 would require releasing this CRM's source. 12.4.1 is the last `Apache-2.0` release.
  Do not bump without a licence decision.
- **EF Core 10.0.11** (`Microsoft.EntityFrameworkCore`, `.SqlServer`, `.Relational`, `.Design`) — kept in
  lockstep with the `dotnet-ef` tool below. A tool older than the packages errors rather than warns.
- **`dotnet-ef` 10.0.11** via the local tool manifest at `.config/dotnet-tools.json` (repository root).
- **Swashbuckle.AspNetCore 10.2.3** — this is OpenAPI.NET v2: `OpenApiInfo` lives in `Microsoft.OpenApi`, not
  `Microsoft.OpenApi.Models`. Every pre-2025 Swashbuckle sample online has the old import.
- **Story 03 observability stack:** `Serilog.AspNetCore` 10.0.0, `Serilog.Sinks.OpenTelemetry` 4.2.0, OpenTelemetry
  1.19.x, `Microsoft.Extensions.Http.Resilience` 10.10.0, `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.11,
  `Polly.Core` 8.6.6. **`OpenTelemetry.Instrumentation.EntityFrameworkCore` is pinned to `1.19.1-beta.1`** — it has
  never shipped a stable release; it is the only prerelease package in the solution.
