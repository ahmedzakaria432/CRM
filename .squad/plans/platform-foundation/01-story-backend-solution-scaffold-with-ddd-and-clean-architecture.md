# Story 01 — Backend solution scaffold with DDD and Clean Architecture

## Prerequisites

- None. This is the first story in the global sequence (`.squad/BACKLOG.md`, line 22).
- **Verified present on this machine** (checked during planning, re-check before starting):
  - .NET SDK **10.0.301**, runtime **10.0.9** (`dotnet --list-sdks`, `dotnet --list-runtimes`)
  - `dotnet ef` **10.0.3** installed globally — **older than the EF Core packages story 02 will pin (10.0.11)**.
    Task 9 replaces it with a pinned local tool manifest so story 02 does not hit a version-skew error.
  - Docker **29.6.2**, Docker Compose **v5.3.1** (`docker --version`, `docker compose version`)
  - Node **v24.15.0**; **`pnpm` is NOT installed**. Not needed for this story — it blocks Story 04. Flag it there.
- No application source exists yet. `d:\CRM` currently contains only `CLAUDE.md`, `.gitignore`, `.claude/`,
  and `.squad/`. Every path in this plan is a **create**, not an edit.

---

## Story Goal

Produce the .NET 10 backend skeleton that stories 02–58 build on. When this story is done, the repository has a
solution that compiles clean with zero warnings, enforces the Clean Architecture dependency direction *at build
time* rather than by review, ships the DDD building blocks every aggregate will inherit, runs the MediatR
pipeline end to end, and answers a health probe that proves it can reach SQL Server in Docker.

Concretely:

1. `dotnet build backend/Crm.sln` succeeds with **zero warnings** (`TreatWarningsAsErrors` on).
2. `Crm.Domain` cannot reference outward — proven by a deliberate violation failing the build.
3. `Result` / `Result<T>`, `Error`, `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `IDomainEvent`, and the
   strongly-typed id base exist and are the only vocabulary later stories need for a new aggregate.
4. `GET /health` and `GET /health/ready` return 200 and report SQL Server reachability.
5. One sample query slice demonstrates the exact folder and file shape every future slice copies.
6. `backend/requests/health.http` exercises every endpoint this story adds.

**Not in scope:** any real aggregate, any `DbContext`, any migration, any authentication, any frontend file.
Story 02 owns persistence; story 03 owns Serilog, ProblemDetails, and OpenTelemetry; story 04 owns the frontend.
This story registers the *placeholders* those stories fill.

---

## Context — Read These Files First

1. `.squad/stories/platform-foundation/backend-solution-scaffold-with-ddd-and-clean-architecture/intake.md` —
   the whole file (110 lines). The **Acceptance criteria** block is the contract; **Task 12** below restates
   two of those criteria more precisely after a mechanism check (see **Edge Cases**).
2. `CLAUDE.md` — **read lines 21–68** (`## Backend — .NET 10, DDD + Clean Architecture`). Binding. Note in
   particular:
   - line 25–49 — the DDD rules. `AggregateRoot<TId>` in Task 4 exists to make these enforceable.
   - line 50–54 — CQRS folder layout. The sample slice in Task 8 **is** this layout.
   - line 55–56 — `Result` / `Result<T>`, business failures are results not exceptions.
   - line 57 — pipeline order: validation → authorisation → logging → transaction → caching.
3. `CLAUDE.md` — **read lines 82–104** (`## Testing policy`). **There is no test project in this repository and
   this story must not create one.** The `## Test Plan` section below is a manual verification matrix.
4. `.squad/BACKLOG.md` — **read lines 20–30**. Confirms stories 02, 03, and 04 depend on this one, and what each
   of them will add on top of the placeholders you create here.
5. `.squad/plans/00-index.md` — feature planning order. `platform-foundation` is feature 1 of 12.

---

## Backend Tasks

### 1 — Repository and solution layout

**Create directory tree** under `d:\CRM`:

```
backend/
  Crm.sln
  Directory.Build.props
  Directory.Packages.props
  global.json
  .editorconfig
  docker-compose.yml
  README.md
  requests/health.http
  src/
    Crm.Domain/
    Crm.Application/
    Crm.Infrastructure/
    Crm.Api/
```

Commands, run from `d:\CRM\backend`:

```bash
dotnet new globaljson --sdk-version 10.0.301 --roll-forward latestFeature
dotnet new sln -n Crm
dotnet new classlib -n Crm.Domain         -o src/Crm.Domain         --framework net10.0
dotnet new classlib -n Crm.Application    -o src/Crm.Application    --framework net10.0
dotnet new classlib -n Crm.Infrastructure -o src/Crm.Infrastructure --framework net10.0
dotnet new web      -n Crm.Api            -o src/Crm.Api            --framework net10.0
dotnet sln add src/Crm.Domain src/Crm.Application src/Crm.Infrastructure src/Crm.Api
```

Delete the generated `Class1.cs` from each classlib.

**Template short names are verified** against `dotnet new list` on this machine: `globaljson`, `sln`, `classlib`,
`web`, `buildprops`, `editorconfig`, `tool-manifest`.

**Wire the dependency direction — this is the whole point of the layer structure:**

```bash
dotnet add src/Crm.Application    reference src/Crm.Domain
dotnet add src/Crm.Infrastructure reference src/Crm.Application
dotnet add src/Crm.Api            reference src/Crm.Infrastructure
```

**`Crm.Domain` gets zero `ProjectReference` elements. Ever.** Do not add a reference from `Crm.Api` directly to
`Crm.Domain` or `Crm.Application` — it compiles today and rots the boundary tomorrow. `Crm.Api` reaches the
inner layers transitively.

---

### 2 — `Directory.Build.props`

**Create file: `backend/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

`CS1591` (missing XML comment) is suppressed deliberately: `GenerateDocumentationFile` is on so analyzers see
doc comments, but requiring one on every public member would bury the real warnings. **Do not add any other
`NoWarn` entry** — on this project the compiler is the only automated check there is (`CLAUDE.md` line 95–96).

---

### 3 — `Directory.Packages.props` (central package management)

**Create file: `backend/Directory.Packages.props`**

All versions below were resolved from `api.nuget.org` during planning and are the current stable releases.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Pinned to 12.4.1 deliberately. See the licensing note below. -->
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="5.6.0" />
    <PackageVersion Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="7.0.2" />
  </ItemGroup>
</Project>
```

#### MediatR version — read before changing it

`CLAUDE.md` line 50 mandates MediatR. The latest version is **14.2.0**, but MediatR changed owner and licence
after 12.x. Verified from the package metadata during planning:

| Version | Licence |
|---------|---------|
| **12.4.1** | `Apache-2.0` (licence expression in the nuspec) |
| 13.x – 14.2.0 | `requireLicenseAcceptance=true`, licence file: **RPL-1.5**, *or* a paid Lucky Penny Software licence |

RPL-1.5 is a strong reciprocal licence that would require releasing this CRM's source. **12.4.1 is pinned so a
commercial closed-source product stays licence-clean without a purchase.** It ships `lib/net6.0`, which loads
fine on `net10.0` — confirmed from the package contents.

If the organisation buys a Lucky Penny licence, bumping to 14.x is a one-line change here; the
`IPipelineBehavior<,>` API used in Task 7 is source-compatible. **Do not bump it silently.**

---

### 4 — Domain building blocks

**Create files under `backend/src/Crm.Domain/Common/`:**

| File | Contents |
|------|----------|
| `Entity.cs` | `public abstract class Entity<TId> where TId : notnull` — `public TId Id { get; protected set; }`, identity equality on `Id` and concrete type, `protected Entity()` for the ORM and `protected Entity(TId id)` |
| `AggregateRoot.cs` | `public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents where TId : notnull` |
| `IDomainEvent.cs` | `public interface IDomainEvent { DateTimeOffset OccurredOnUtc { get; } }` |
| `IHasDomainEvents.cs` | `IReadOnlyCollection<IDomainEvent> DomainEvents { get; }` + `void ClearDomainEvents()` |
| `ValueObject.cs` | `public abstract class ValueObject` — `protected abstract IEnumerable<object?> GetEqualityComponents()`, structural `Equals`/`GetHashCode`, `==`/`!=` |
| `StronglyTypedId.cs` | `public abstract record StronglyTypedId<TSelf>(Guid Value)` base for `CustomerId`, `TicketId`, … |
| `DomainError.cs` | `public sealed record DomainError(string Code, string Message)` |
| `DomainException.cs` | `public sealed class DomainException(DomainError error) : Exception(error.Message)` — thrown only from inside an aggregate when an invariant is violated |

`AggregateRoot<TId>` must expose events **read-only** and raise them **protected**:

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot() { }
    protected AggregateRoot(TId id) : base(id) { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**`Raise` is `protected`, not `public`.** A handler must never be able to raise a domain event on an aggregate's
behalf — that is the rule in `CLAUDE.md` line 40–41 made structural.

**Create the empty bounded-context folders** so story 11 and 15 have an obvious home, each with a
`_context.md` one-liner naming the context (an empty directory will not survive git):

```
src/Crm.Domain/Customers/     Tickets/     KnowledgeBase/     Sla/
                Organisation/ Identity/    Communication/
```

---

### 5 — Result type

**Create files under `backend/src/Crm.Domain/Common/Results/`:** `Error.cs`, `Result.cs`, `Result{T}.cs`.

```csharp
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error NotFound(string code, string description)   => new(code, description, ErrorType.NotFound);
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error Conflict(string code, string description)   => new(code, description, ErrorType.Conflict);
    public static Error Forbidden(string code, string description)  => new(code, description, ErrorType.Forbidden);
    public static Error Domain(string code, string description)     => new(code, description, ErrorType.Domain);
}

public enum ErrorType { Failure, Validation, NotFound, Conflict, Forbidden, Domain }
```

`Result` carries `IsSuccess`, `IsFailure`, `Error`; `Result<T>` adds `Value` and **throws on access when
`IsFailure`** — a silent `default!` would let a failure flow into a response body unnoticed.

`ErrorType.Domain` is separate from `Validation` on purpose. Story 03 maps `Validation` → **400** and `Domain`
→ **422**, which is what makes an aggregate's invariant distinguishable from a malformed request when you are
debugging from the outside with no test suite to consult.

---

### 6 — `Crm.Domain` layer enforcement

**Create file: `backend/src/Crm.Domain/BannedSymbols.txt`**

```
N:Microsoft.EntityFrameworkCore;Crm.Domain must not know about persistence. Move this to Crm.Infrastructure.
N:MediatR;Crm.Domain must not know about the mediator. Raise a domain event instead.
N:Microsoft.AspNetCore;Crm.Domain must not know about HTTP. Move this to Crm.Api.
N:Microsoft.Extensions.DependencyInjection;Crm.Domain must not resolve services. Pass the dependency in.
N:System.Data.Common;Crm.Domain must not open connections.
N:System.Net.Http;Crm.Domain must not make network calls. Use an abstraction in Crm.Application.
M:System.DateTime.get_Now;Use DateTimeOffset.UtcNow. Local time is banned across the solution.
M:System.DateTime.get_Today;Use DateTimeOffset.UtcNow. Local time is banned across the solution.
```

**Edit `backend/src/Crm.Domain/Crm.Domain.csproj`** — add the analyzer and register the file:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="BannedSymbols.txt" />
</ItemGroup>
```

`PrivateAssets=all` makes this a **build-time-only** dependency: it does not flow to consumers and is not a
runtime dependency, so the intake's "no package reference to EF Core, MediatR, or ASP.NET" criterion still holds.

**Create file: `backend/.editorconfig`** — force the analyzer to error even if `TreatWarningsAsErrors` is ever
relaxed:

```ini
root = true

[*.cs]
dotnet_diagnostic.RS0030.severity = error   # banned API used
dotnet_diagnostic.RS0031.severity = error   # duplicate entry in BannedSymbols.txt
```

Also add the `DateTime.Now` bans to `Crm.Application` and `Crm.Infrastructure` via their own
`BannedSymbols.txt` (the two `M:` lines only) — the intake for **story 02** requires `DateTime.Now` to fail the
build solution-wide.

> **This mechanism was verified during planning**, not assumed: a `net10.0` project with
> BannedApiAnalyzers 5.6.0, `TreatWarningsAsErrors`, and `N:System.Net.Http` in `BannedSymbols.txt` produced
> `error RS0030` on build. See **Edge Cases** for the one case it does *not* catch.

---

### 7 — MediatR pipeline

**Create file: `backend/src/Crm.Application/DependencyInjection.cs`**

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
```

**Registration order is the execution order and it matches `CLAUDE.md` line 57. Do not reorder it.**

**Create the four behaviours** under `backend/src/Crm.Application/Common/Behaviors/`:

| File | This story | Filled by |
|------|-----------|-----------|
| `ValidationBehavior.cs` | **Real.** Runs every `IValidator<TRequest>`, aggregates failures, returns a failed `Result` with `ErrorType.Validation`. Never throws. | — |
| `AuthorizationBehavior.cs` | **Placeholder.** Pass-through with a `// Story 09` comment. | Story 09 |
| `LoggingBehavior.cs` | **Real but minimal.** `ILogger` scope with request name, start/finish, elapsed ms, success/failure. Message templates only — no interpolation. | Story 03 enriches |
| `TransactionBehavior.cs` | **Placeholder.** Pass-through with a `// Story 02` comment — there is no `DbContext` to open a transaction on yet. | Story 02 |

`ValidationBehavior` must return a failed `Result`, not throw, for `TResponse` deriving from `Result`. Use a
`typeof(Result).IsAssignableFrom(typeof(TResponse))` guard and construct the failure by reflection over the
static factory. **A request whose `TResponse` is not a `Result` must still be validated** — throw
`ValidationException` in that case so the mistake is loud rather than silent.

**Add to `backend/src/Crm.Application/Crm.Application.csproj`:** `MediatR`, `FluentValidation`,
`FluentValidation.DependencyInjectionExtensions`, `Microsoft.CodeAnalysis.BannedApiAnalyzers`.

---

### 8 — The canonical sample slice

**Create files** — this is the shape every one of the 57 later stories copies. Get it right.

```
src/Crm.Application/Sample/Queries/GetSample/
    GetSampleQuery.cs          public sealed record GetSampleQuery(string Name) : IRequest<Result<SampleDto>>;
    GetSampleQueryHandler.cs   sealed class, returns Result<SampleDto>.Success(...)
    GetSampleQueryValidator.cs sealed class : AbstractValidator<GetSampleQuery> — NotEmpty, MaximumLength(100)
    SampleDto.cs               public sealed record SampleDto(string Name, DateTimeOffset ServerTimeUtc);
```

Head every file with:

```csharp
// CANONICAL SLICE — copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).
```

**Create file: `backend/src/Crm.Api/Endpoints/SampleEndpoints.cs`** implementing an `IEndpointGroup` interface
(`static abstract void Map(IEndpointRouteBuilder)` or an instance interface discovered by assembly scan — pick
one and document it in `backend/README.md`, because 57 stories will follow it).

Map `GET /api/sample?name={name}`. It must:
- take `ISender` by DI, send the query, and translate `Result<SampleDto>` to `Ok` or a ProblemDetails failure;
- return **400 with an RFC 9457 body listing field errors** when `name` is empty — this is the intake criterion
  and it is what proves `ValidationBehavior` is actually in the pipeline.

Use `Results.Problem(...)` / `Results.ValidationProblem(...)` with `AddProblemDetails()` registered in
`Program.cs`. Story 03 replaces this with the global exception handler and the stable `type` URIs; **write the
minimum here and do not build a mapping framework** — it will be thrown away in three stories.

---

### 9 — Health checks, `Program.cs`, and the EF tool manifest

**Create file: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** with
`AddInfrastructure(this IServiceCollection, IConfiguration)`:

```csharp
services.AddHealthChecks()
    .AddSqlServer(
        connectionString: configuration.GetConnectionString("Crm")!,
        name: "sql-server",
        tags: ["ready"]);
```

Use **`AspNetCore.HealthChecks.SqlServer` 9.0.0**, which talks to SQL Server through `Microsoft.Data.SqlClient`
directly. **Do not add EF Core to this story** — there is no `DbContext` until story 02, and pulling EF in now
just to answer a health probe drags an unused dependency through the composition root.

**Edit `backend/src/Crm.Api/Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();               // Story 04 generates its typed client from this document

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();                   // Story 03 replaces with the global handler
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapSampleEndpoints();

app.Run();
```

`/health` is **liveness** — the process is up; it must return 200 even when SQL Server is down.
`/health/ready` is **readiness** — filtered to the `ready` tag, so it returns 503 when SQL Server is
unreachable. Getting these the wrong way round means an orchestrator restarts a healthy API during a database
blip.

Add the connection string to `appsettings.Development.json` pointing at the compose instance (Task 10).
**Do not put a password in `appsettings.json`** — Development only, and note in `README.md` that deployment uses
environment variables or a secret store.

**Create the EF local tool manifest** now, so story 02 does not trip on the 10.0.3-vs-10.0.11 skew found during
planning. From `d:\CRM`:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.11
```

Story 02 then runs `dotnet ef …` and picks up the pinned local tool instead of the older global one.

---

### 10 — `docker-compose.yml`

**Create file: `backend/docker-compose.yml`**

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: crm-sqlserver
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:-Crm_Local_Dev_2026!}"
      MSSQL_PID: Developer
    ports:
      - "1433:1433"
    volumes:
      - crm-sqldata:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

volumes:
  crm-sqldata:
```

The SA password must satisfy SQL Server's complexity policy (8+ chars, three of four character classes) or the
container starts and immediately exits. `mssql-tools18` requires `-C` to trust the self-signed certificate.
Compose on this machine is **v5.3.1**, so use `docker compose`, not `docker-compose`.

---

### 11 — `.gitignore` and `backend/README.md`

**Edit `d:\CRM\.gitignore`** — append a .NET block **below** the existing squad-kit block. Do not touch the
squad-kit block; it is marked managed.

```gitignore
# .NET
bin/
obj/
*.user
.vs/
appsettings.*.local.json
```

**Create file: `backend/README.md`** covering: prerequisites and the versions verified above; `docker compose
up -d`; `dotnet build`; `dotnet run --project src/Crm.Api`; the OpenAPI URL; **how to add a new slice** (point
at the `Sample` folder and the `IEndpointGroup` convention); the layer rule and what `BannedSymbols.txt` does;
**and a "no test project — verify with `.http` files" note** pointing at `CLAUDE.md` lines 82–104, so the next
agent does not create one out of habit.

---

### 12 — `backend/requests/health.http`

**Create file: `backend/requests/health.http`**

```http
@host = http://localhost:5080

### Liveness — 200 even when SQL Server is stopped
GET {{host}}/health

### Readiness — 200 when SQL Server is reachable, 503 when it is not
GET {{host}}/health/ready

### Canonical slice — 200
GET {{host}}/api/sample?name=Ahmed

### Canonical slice — 400 with an RFC 9457 body listing field errors
GET {{host}}/api/sample?name=

### Arabic input round-trips unmangled
GET {{host}}/api/sample?name=أحمد
```

Every later story adds `backend/requests/<feature>.http`. This file is the pattern.

---

## Frontend Tasks

**No frontend changes required.** Story 04 creates `frontend/`. It consumes the OpenAPI document exposed in
Task 9 — do not hand-write any client here.

---

## Edge Cases & Failure Modes

- **A bare `using` does not trigger RS0030.** Verified during planning: a file containing only
  `using System.Net.Http;` with no symbol usage **built successfully**; RS0030 fires on symbol *use*, not on the
  directive. The intake's criterion "adding `using Microsoft.EntityFrameworkCore;` fails the build via the
  analyzer" is therefore imprecise. What is actually true, and what **Done Criteria** below asserts instead:
  - With no package reference (the shipped state), that `using` fails with **CS0246** — a *stronger* guarantee
    than the analyzer, because it needs no configuration.
  - If someone adds the package reference, RS0030 fires as soon as any symbol from the namespace is used.
  - Verify **both**, in that order. Enforced by `Crm.Domain.csproj` having no `ProjectReference` and by
    `src/Crm.Domain/BannedSymbols.txt`.

- **`TreatWarningsAsErrors` on a fresh `dotnet new web`.** The generated template may emit warnings that now
  break the build. Fix the code; **do not add a `NoWarn`.** If a warning is genuinely unfixable, record it in
  `backend/README.md` with the reason.

- **`Result<T>.Value` accessed on a failure.** Throws `InvalidOperationException` naming the error code.
  Enforced in `src/Crm.Domain/Common/Results/Result{T}.cs`. Returning `default!` would let a null flow into a
  200 response.

- **A request with no validator.** `ValidationBehavior` must pass through cleanly, not throw. Most queries will
  have no validator for a long time.

- **A request whose `TResponse` is not a `Result`.** Cannot be converted to a failed result, so
  `ValidationBehavior` throws `ValidationException`. Loud is correct here — silently skipping validation is how
  an unvalidated command reaches an aggregate.

- **SQL Server not yet accepting connections.** The container reports healthy well before SQL Server is ready.
  `/health` must still return **200** (liveness), `/health/ready` **503**. Confirm by starting the API before
  the container.

- **SA password rejected.** SQL Server exits on startup with a complexity error and the container restart-loops.
  Diagnose with `docker compose logs sqlserver`; the symptom looks like a network failure and is not one.

- **Port 1433 already in use** by a local SQL Server instance. Remap to `14330:1433` and update the connection
  string. Note whichever you chose in `backend/README.md`.

- **EF tool version skew.** `dotnet ef` 10.0.3 is installed globally and is older than the 10.0.11 packages
  story 02 will pin; EF errors rather than warns on that combination. Task 9's local tool manifest is the fix —
  **do not skip it** because this story has no `DbContext`.

- **Arabic input through the sample endpoint.** `?name=أحمد` must round-trip unmangled. If it does not, the
  response is not being written as UTF-8, and story 05 will inherit the bug across every screen.

- **`Directory.Build.props` picked up by the frontend.** It sits in `backend/`, not the repository root, so
  `frontend/` is unaffected. **Do not move it up a level.**

---

## Test Plan

**This project ships without automated tests** — `CLAUDE.md` lines 82–104, a recorded decision by the project
owner. **Do not create a test project, a `tests/` folder, a `*.Tests.csproj`, or add xUnit, NUnit,
FluentAssertions, or Testcontainers to any project file in this story.**

The verification matrix below replaces the test suite. Run every row and record the result in the story report.

| # | Check | How | Expected |
|---|-------|-----|----------|
| 1 | Clean build | `dotnet build backend/Crm.sln` | Succeeds, **0 warnings, 0 errors** |
| 2 | Domain isolation (structural) | Inspect `src/Crm.Domain/Crm.Domain.csproj` | No `ProjectReference`; only the analyzer `PackageReference`, with `PrivateAssets=all` |
| 3 | Domain isolation (no package) | Add `using Microsoft.EntityFrameworkCore;` to any `Crm.Domain` file, build | Fails **CS0246**. Revert. |
| 4 | Domain isolation (analyzer) | Temporarily add the EF Core package to `Crm.Domain`, use `DbContext` in a file, build | Fails **RS0030** with the message from `BannedSymbols.txt`. Revert both. |
| 5 | `DateTime.Now` ban | Add `var x = DateTime.Now;` to any project, build | Fails RS0030. Revert. |
| 6 | Pipeline order | Run the API at `Debug` log level, call `/api/sample?name=Ahmed` | Log shows validation → authorisation → logging → transaction, in that order |
| 7 | Validation → 400 | `requests/health.http` request 4 (`name=`) | **400**, `application/problem+json`, `errors` names the `name` field |
| 8 | Liveness vs readiness | `docker compose stop sqlserver`, call both endpoints | `/health` **200**, `/health/ready` **503**. Restart, both **200**. |
| 9 | Arabic round-trip | `requests/health.http` request 5 | Response contains `أحمد` unmangled |
| 10 | OpenAPI document | `GET /openapi/v1.json` | Valid document including `/api/sample` — story 04 depends on this |
| 11 | Compose from cold | `docker compose down -v && docker compose up -d`, wait, run the API | Connects with no manual step |

---

## Verification Steps

1. **Infrastructure starts:** `cd backend && docker compose up -d` — then `docker compose ps` shows
   `crm-sqlserver` healthy.
2. **Backend builds:** `dotnet build backend/Crm.sln` — **zero warnings, zero errors**. A warning is a failure
   on this project.
3. **Backend runs:** `dotnet run --project backend/src/Crm.Api` — starts with no unhandled exception.
4. **Endpoints answer:** run every request in `backend/requests/health.http` — matches rows 7–10 above.
5. **Layer rule bites:** perform matrix rows 3, 4, and 5, then **revert every deliberate violation** and re-run
   step 2 to confirm the tree is clean.
6. **Regression:** none — this is the first story and there is nothing to regress.

---

## Done Criteria

- [ ] `dotnet build backend/Crm.sln` succeeds with **zero warnings**.
- [ ] `Crm.Domain.csproj` has **no `ProjectReference`** and no runtime `PackageReference`; the only package is
      `Microsoft.CodeAnalysis.BannedApiAnalyzers` with `PrivateAssets=all`.
- [ ] `using Microsoft.EntityFrameworkCore;` in `Crm.Domain` fails the build with **CS0246** in the shipped
      state, **and** with **RS0030** once the package is referenced and a symbol used. *(Restated from the
      intake — see the first bullet under **Edge Cases**.)*
- [ ] `DateTime.Now` anywhere in the solution fails the build with RS0030.
- [ ] `AggregateRoot<TId>` exposes `DomainEvents` read-only with a **`protected` `Raise`**, and `ValueObject`
      implements structural equality.
- [ ] The seven bounded-context folders exist under `src/Crm.Domain/`.
- [ ] `Result` / `Result<T>` / `Error` / `ErrorType` exist, and `Result<T>.Value` throws on a failed result.
- [ ] The four pipeline behaviours are registered in the order at `CLAUDE.md` line 57; validation and logging
      are real, authorisation and transaction are commented placeholders naming stories 09 and 02.
- [ ] `GET /health` returns 200 with SQL Server stopped; `GET /health/ready` returns 503 and recovers to 200.
- [ ] The sample slice exists at `Crm.Application/Sample/Queries/GetSample/` as four files, is marked canonical,
      and is marked for deletion in story 11.
- [ ] `GET /api/sample?name=` returns **400** with an RFC 9457 body listing the field error.
- [ ] `GET /api/sample?name=أحمد` round-trips the Arabic value unmangled.
- [ ] `GET /openapi/v1.json` returns a valid document — story 04 generates its client from it.
- [ ] `docker compose up -d` from cold brings up SQL Server and the API connects with no manual step.
- [ ] `.config/dotnet-tools.json` pins `dotnet-ef` **10.0.11**.
- [ ] `backend/README.md` documents run, verify, add-a-slice, the layer rule, and the no-test-project policy.
- [ ] `backend/requests/health.http` contains all five requests and every one has been run against a live API.
- [ ] **No test project, no `tests/` folder, and no test framework package exists anywhere in the repository.**
- [ ] `MediatR` is pinned to **12.4.1** and `backend/Directory.Packages.props` carries the licensing note.
- [ ] `00-overview.md` updated with this story.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 02.**
