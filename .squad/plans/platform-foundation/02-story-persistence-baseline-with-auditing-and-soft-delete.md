# Story 02 — Persistence baseline with auditing and soft delete

> **Architecture update (2026-09-28):** implemented before the backend became a modular monolith. Paths below
> (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `CrmDbContext`) are historical; see
> [`00-overview.md`](00-overview.md), "Architecture change — modular monolith", for where each piece lives now.

## Prerequisites

- **Story 01 is implemented and present in the working tree, uncommitted** (`git status` shows `?? backend/`).
  Verified during planning by reading the real files, not the plan:
  [`01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md`](01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md).
  This story **edits that tree**. Confirm `dotnet build backend/Crm.sln` is clean before starting.
- Two placeholders story 01 deliberately left empty are filled here:
  - `backend/src/Crm.Application/Common/Behaviors/TransactionBehavior.cs` — pass-through with a `// Story 02`
    comment. **Task 8 replaces the body.**
  - `backend/src/Crm.Infrastructure/DependencyInjection.cs` — health checks only, **no EF Core package**.
    Task 1 adds them.
- **Verified on this machine during planning** (re-check before starting):
  - .NET SDK **10.0.301**.
  - **EF Core 10.0.11 is the latest stable** for `Microsoft.EntityFrameworkCore`, `.SqlServer`, `.Relational`,
    and `.Design` (queried from the `api.nuget.org` flat-container index). `.config/dotnet-tools.json` already
    pins `dotnet-ef` **10.0.11** with `"rollForward": false`. **Keep tool and packages on the same version** —
    EF errors, rather than warning, on skew.
  - `backend/Directory.Packages.props` already pins `Microsoft.AspNetCore.OpenApi` **10.0.11** and
    `Microsoft.Extensions.Logging.Abstractions` **10.0.11**. **Do not touch the MediatR `12.4.1` pin or its
    licensing comment** — story 01 recorded why.
  - **Swashbuckle.AspNetCore 10.2.3** is the current stable and works on `net10.0`.
- **Every mechanism in this plan was proven by a probe run**, not assumed — two throwaway `net10.0` apps built
  during planning: one on **EF Core 10.0.11** (SQLite provider, for speed) and one on **Swashbuckle 10.2.3** with
  four MVC areas. Results that changed the design are marked **Probe:** inline and collected in
  **Edge Cases & Failure Modes**.
- Coordination: **Story 06** extends the query filter added here with tenant / department / branch. Task 6's
  **named** filters are what make that possible without deleting this one. Read that task before changing it.

---

## Story Goal

Establish the data-layer conventions **and the API surface shape** every one of the 56 remaining stories
inherits, so no later story reinvents auditing, soft delete, concurrency, value-object mapping, domain-event
dispatch, the migration workflow, or where a controller goes.

Concretely, when this story is done:

1. `CrmDbContext` exists, discovers `IEntityTypeConfiguration<T>` by assembly scan, and has a migration that
   applies cleanly to the compose SQL Server.
2. A sample aggregate with a **value object** and a **child entity** round-trips: the value object persists as
   owned columns, the child has **no `DbSet`** and is reachable only through its root.
3. Saving stamps created-by/at; updating stamps modified-by/at — from `ICurrentUserService`, in an interceptor,
   with no handler writing an audit field by hand.
4. `Remove()` on an `ISoftDeletable` aggregate issues **no SQL `DELETE`** — it flips a flag.
5. A default query hides soft-deleted rows; `IgnoreQueryFilters()` shows them, and a **named** filter can be
   lifted on its own.
6. A concurrent update returns **HTTP 409**, not 500.
7. Domain events dispatch **after the transaction commits**, and not at all when it rolls back.
8. All timestamps persist as UTC, and the clock is only reachable through `TimeProvider`.
9. The seeder is idempotent.
10. **`Crm.Api` serves MVC controllers organised into four Areas — `Agent`, `Portal`, `Admin`, `Public` — one
    per persona, each with its own Swagger document, behind a Swagger UI with a document picker.**

**Not in scope:** the `Customer` and `Ticket` aggregates (stories 11 and 15) — only building blocks here. The
tenant/department/branch filter itself (**story 06** extends the filter this story adds). Serilog, the global
exception handler, and the full RFC 9457 `type` URIs (**story 03**). Authentication and the `[Authorize]`
policies that will guard each Area (**stories 07 and 09** — this story leaves the Areas open and marks them).
Any test project.

---

## Product rules (from story)

The intake describes persistence only. **Two rules were added by the project owner during planning** and are
binding on this story and every story after it:

| Rule | Current behaviour (story 01, as built) | New behaviour (this story) |
|---|---|---|
| API style | Minimal APIs. `IEndpointGroup` + `EndpointGroupExtensions.MapEndpointGroups()`, assembly-scanned. `SampleEndpoints` maps `GET /api/sample`. | **MVC controllers**, one **Area per persona**: `Agent`, `Portal`, `Admin`, `Public`. |
| API documentation | `builder.Services.AddOpenApi()` + `app.MapOpenApi()` — a JSON document at `/openapi/v1.json`, **no UI**. | **Swagger** — `AddSwaggerGen` with **one document per Area**, served under a Swagger UI at `/swagger`. |

Both replace what story 01 built. Task 14 carries the migration and Task 15 records it in `CLAUDE.md`.

---

## Context — Read These Files First

1. `.squad/stories/platform-foundation/persistence-baseline-with-auditing-and-soft-delete/intake.md` — the whole
   file (107 lines). The **Acceptance criteria** block is the contract. `attachments/` is empty.
2. `backend/src/Crm.Domain/Common/Results/Result.cs` and `ErrorType.cs` — **read both in full, they are short.**
   `Result` is a `class` with `protected Result(bool, Error)` and static `Success` / `Failure` / `Success<T>` /
   `Failure<T>`. `ErrorType` already has a **`Conflict`** member. **Task 9 reuses it — do not add a new error
   type.**
3. `backend/src/Crm.Api/Common/ResultEndpointExtensions.cs` — **read all 44 lines.** `ToProblem()` already maps
   `ErrorType.Conflict` → **409** and `Domain` → 422. It returns **`IResult`**, which is a minimal-API type;
   Task 14 adds the `ActionResult` sibling controllers need. **Keep the mapping logic identical** — story 03
   replaces both.
4. `backend/src/Crm.Api/Endpoints/` — `IEndpointGroup.cs`, `EndpointGroupExtensions.cs`, `SampleEndpoints.cs`.
   **This whole folder is deleted in Task 14.** Read `SampleEndpoints.cs` first: its `ISender` → `Result<T>` →
   `ToProblem()` shape is what the controller must reproduce.
5. `backend/src/Crm.Api/Program.cs` — all 35 lines. Task 14 rewrites the service registration and middleware;
   **leave the two `MapHealthChecks` calls and their liveness/readiness comments exactly as they are.**
6. `backend/src/Crm.Domain/Common/` — `AggregateRoot.cs`, `Entity.cs`, `ValueObject.cs`, `StronglyTypedId.cs`,
   `IDomainEvent.cs`, `IHasDomainEvents.cs`. The vocabulary Task 2 and Task 12 build on. Note `Raise` is
   `protected` — Task 8 harvests events through `IHasDomainEvents`, never by raising them itself.
7. `backend/src/Crm.Application/Common/Behaviors/` — all four behaviours, and `DependencyInjection.cs` for the
   registration order. **Task 8 changes `TransactionBehavior`'s body and nothing about its position.**
8. `backend/src/Crm.*/BannedSymbols.txt` — **all four exist already**, and all four already ban
   `DateTime.get_Now` and `DateTime.get_Today`. **Task 11 adds one line to each; it does not create them.**
9. `CLAUDE.md` — **read lines 58–66**. Binding, and the source of most of this story:
   - line 58–60 — `IEntityTypeConfiguration<T>` **only, never data annotations**; value objects as owned types
     or a `ValueConverter`; **migrations ship in the same change as the schema move**.
   - line 61–62 — `AuditableEntity`, soft delete via `ISoftDeletable` + a **global query filter**.
   - line 63–64 — everything scoped through `ICurrentUserService`. Story 06 builds on the seam Task 3 adds.
10. `CLAUDE.md` — **read lines 34–48** for the DDD rules persistence must not break, and **lines 82–104**
    (`## Testing policy`). **No test project. Do not create one.** The `## Test Plan` below is a manual matrix.
11. `.squad/BACKLOG.md` — **read lines 22–27**. Stories 03 and 06 both build directly on this one.

---

## Backend Tasks

### 1 — Packages and project wiring

**Edit `backend/Directory.Packages.props`** — add to the existing `<ItemGroup>`:

```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.11" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.11" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" />
<PackageVersion Include="Swashbuckle.AspNetCore" Version="10.2.3" />
```

**Edit `backend/src/Crm.Infrastructure/Crm.Infrastructure.csproj`** — reference
`Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Design`. Mark **Design** as
build-time-only, copying the `PrivateAssets` block already in `Crm.Api.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

**`Crm.Domain` gets no EF Core reference. `Crm.Application` gets none either** — it declares abstractions
(`IRepository<,>`, `IUnitOfWork`, `ICurrentUserService`) and `Crm.Infrastructure` implements them.
`Crm.Domain/BannedSymbols.txt` line 1 already errors on `N:Microsoft.EntityFrameworkCore`; adding the package to
`Crm.Application` would compile and silently rot the boundary.

---

### 2 — Domain-side persistence contracts

Plain interfaces with **no EF dependency**, so an aggregate can implement them without knowing about persistence.

**Create file: `backend/src/Crm.Domain/Common/IAuditable.cs`**

```csharp
namespace Crm.Domain.Common;

public interface IAuditable
{
    string? CreatedBy { get; }
    DateTimeOffset? CreatedAtUtc { get; }
    string? ModifiedBy { get; }
    DateTimeOffset? ModifiedAtUtc { get; }
}
```

**Create file: `backend/src/Crm.Domain/Common/AuditableEntity.cs`**

```csharp
public abstract class AuditableEntity<TId> : AggregateRoot<TId>, IAuditable
    where TId : notnull
{
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? CreatedAtUtc { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    protected AuditableEntity() { }
    protected AuditableEntity(TId id) : base(id) { }
}
```

**No public setters and no stamp methods.** `CLAUDE.md` line 36 bans public setters on entities, and an audit
field a handler can write is an audit field that can lie. The interceptor writes through the EF entry instead:

```csharp
entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = user;
```

**Probe: writing a private-setter property through `entry.Property(...).CurrentValue` works and needs no
accessor widening and no `InternalsVisibleTo`.**

**Create file: `backend/src/Crm.Domain/Common/ISoftDeletable.cs`**

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAtUtc { get; }
    string? DeletedBy { get; }
}
```

Deliberately **no `MarkDeleted` on the interface** — same reason. An aggregate that wants an intention-revealing
close exposes its own method (`ticket.Close(reason)`); that is a different concept from a row being soft-deleted.

**Create file: `backend/src/Crm.Domain/Common/IHasRowVersion.cs`**

```csharp
public interface IHasRowVersion
{
    byte[] RowVersion { get; }
}
```

---

### 3 — Repository and specification abstractions

**Create file: `backend/src/Crm.Application/Common/Abstractions/IRepository.cs`**

```csharp
public interface IRepository<TAggregate, in TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<TAggregate?> FirstOrDefaultAsync(Specification<TAggregate> spec, CancellationToken ct = default);
    Task<IReadOnlyList<TAggregate>> ListAsync(Specification<TAggregate> spec, CancellationToken ct = default);
    Task<int> CountAsync(Specification<TAggregate> spec, CancellationToken ct = default);
    void Add(TAggregate aggregate);
    void Remove(TAggregate aggregate);
}
```

**Aggregate roots only.** There is no `IRepository<TicketMessage, …>` and there never will be — `CLAUDE.md`
line 46. `Remove` is the soft-delete entry point; the interceptor turns it into an update (Task 5).

**Create file: `backend/src/Crm.Application/Common/Abstractions/IUnitOfWork.cs`**

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Create file: `backend/src/Crm.Application/Common/Specifications/Specification.cs`**

```csharp
public abstract class Specification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool AsNoTracking { get; private set; }
    public bool IgnoreSoftDeleteFilter { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria) => Criteria = criteria;
    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);
    protected void ApplyOrderBy(Expression<Func<T, object>> o) => OrderBy = o;
    protected void ApplyOrderByDescending(Expression<Func<T, object>> o) => OrderByDescending = o;
    protected void ApplyPaging(int skip, int take) { Skip = skip; Take = take; }
    protected void AsReadOnly() => AsNoTracking = true;
    protected void IncludeSoftDeleted() => IgnoreSoftDeleteFilter = true;
}
```

`IgnoreSoftDeleteFilter` is the **specification-level** escape hatch. Task 7's evaluator turns it into
`IgnoreQueryFilters([SoftDeleteFilterName])` — lifting **only** the soft-delete filter, so a spec cannot
accidentally lift story 06's tenant filter and leak another tenant's rows.

**Create file: `backend/src/Crm.Application/Common/Abstractions/ICurrentUserService.cs`**

```csharp
public interface ICurrentUserService
{
    string? UserId { get; }
    string UserIdOrSystem { get; }   // "system" when unauthenticated — seeder, background jobs, migrations
}
```

**Create file: `backend/src/Crm.Infrastructure/Identity/CurrentUserService.cs`** — a stub returning `"system"`,
with a `// Story 07 replaces this with the JWT principal.` comment. Register it as scoped.

---

### 4 — `CrmDbContext`

**Create file: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`**

```csharp
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string SoftDeleteFilterName = "SoftDelete";

    // Aggregate roots only. A child entity NEVER gets a DbSet — CLAUDE.md lines 30-32.
    public DbSet<SampleAggregate> Samples => Set<SampleAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();        // Task 6
        modelBuilder.ApplyUtcDateTimeOffsetConverter();   // Task 10
        base.OnModelCreating(modelBuilder);
    }
}
```

`ApplyConfigurationsFromAssembly` is the **only** registration path. **Never `modelBuilder.Entity<T>()` inline
in `OnModelCreating`, and never a data annotation** — `CLAUDE.md` lines 58–59.

**Create file: `backend/src/Crm.Infrastructure/Persistence/CrmDbContextFactory.cs`** implementing
`IDesignTimeDbContextFactory<CrmDbContext>`, reading the connection string from the `CRM_CONNECTION` environment
variable and falling back to the compose default. Without it, `dotnet ef` has to build and boot `Crm.Api` to
find the context, which breaks as soon as story 07 adds startup work needing a live database.

---

### 5 — The auditing interceptor

**Create file: `backend/src/Crm.Infrastructure/Persistence/Interceptors/AuditingSaveChangesInterceptor.cs`**

Override **`SavingChangesAsync`** only. Two passes, **in this order**:

```csharp
public sealed class AuditingSaveChangesInterceptor(
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not { } context)
        {
            return base.SavingChangesAsync(eventData, result, ct);
        }

        string user = currentUser.UserIdOrSystem;
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();

        SoftDelete(context, user, nowUtc);   // MUST run first — it changes entry states
        Audit(context, user, nowUtc);

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

Take `TimeProvider` by DI (`services.AddSingleton(TimeProvider.System)`), never `DateTimeOffset.UtcNow` inline —
it is the seam that makes the UTC criterion checkable by changing the machine timezone rather than by guessing.

#### `SoftDelete` — the part that is easy to get wrong

**Probe result — read this before writing the method.** The obvious implementation

```csharp
entry.State = EntityState.Modified;   // WRONG
```

**corrupts the row.** When EF cascades an aggregate root into `Deleted`, it also puts every **owned** entry —
the value object and the owned child collection — into `Deleted`. Flipping only the root back to `Modified`
leaves those owned entries deleted, and EF writes `NULL` over their columns. The probe failed with:

```
SqliteException: SQLite Error 19: 'NOT NULL constraint failed: Things.Email'
```

On SQL Server it is the same bug with a different message, and on a **nullable** value-object column **it does
not throw at all — it silently blanks the data.** Write it this way instead:

```csharp
private static void SoftDelete(DbContext context, string user, DateTimeOffset nowUtc)
{
    List<EntityEntry> roots = context.ChangeTracker.Entries()
        .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
        .ToList();

    if (roots.Count == 0)
    {
        return;
    }

    foreach (EntityEntry entry in roots)
    {
        // Unchanged, not Modified: resets the entry, then mark only the three columns dirty.
        entry.State = EntityState.Unchanged;
        entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
        entry.Property(nameof(ISoftDeletable.DeletedAtUtc)).CurrentValue = nowUtc;
        entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = user;
    }

    // Rescue everything EF cascaded into Deleted alongside those roots.
    foreach (EntityEntry owned in context.ChangeTracker.Entries().ToList())
    {
        if (owned.State == EntityState.Deleted && owned.Metadata.IsOwned())
        {
            owned.State = EntityState.Unchanged;
        }
    }
}
```

**Probe: with this recipe the generated SQL contained no `DELETE` and the owned columns survived intact.**

#### `Audit`

Runs **after** `SoftDelete`, because `SoftDelete` moves entries out of `Deleted` and those rows must get a
modified stamp:

```csharp
foreach (EntityEntry entry in context.ChangeTracker.Entries())
{
    if (entry.Entity is not IAuditable)
    {
        continue;
    }

    if (entry.State == EntityState.Added)
    {
        entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = user;
        entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = nowUtc;
    }
    else if (entry.State == EntityState.Modified)
    {
        entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = user;
        entry.Property(nameof(IAuditable.ModifiedAtUtc)).CurrentValue = nowUtc;
    }
}
```

**Do not put domain-event dispatch in `SavedChangesAsync`.** See Task 8.

---

### 6 — The global soft-delete query filter

**Create file: `backend/src/Crm.Infrastructure/Persistence/ModelBuilderExtensions.cs`**

```csharp
public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
{
    foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (entityType.IsOwned())                                        // owned types inherit the owner's filter
        {
            continue;
        }

        if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
        {
            continue;
        }

        ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
        UnaryExpression body = Expression.Not(
            Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

        modelBuilder.Entity(entityType.ClrType)
            .HasQueryFilter(CrmDbContext.SoftDeleteFilterName, Expression.Lambda(body, parameter));
    }
}
```

#### The filter must be **named**. This is the decision story 06 depends on.

EF Core 10 added **named** query filters. **Verified by probe on 10.0.11**, with two filters registered
(`"SoftDelete"` and a stand-in `"Tenant"`) over two rows, one soft-deleted and one belonging to another tenant:

| Query | Rows |
|---|---|
| Default | `0` — both filters apply, **ANDed** |
| `IgnoreQueryFilters()` | `2` — lifts **everything** |
| `IgnoreQueryFilters(["SoftDelete"])` | `1` — soft-delete lifted, **tenant filter still applied** |

Before EF Core 10, a second `HasQueryFilter` call **silently replaced** the first. Had this story used the
unnamed overload, story 06's tenant filter would have overwritten the soft-delete filter and deleted rows would
have quietly reappeared across the product, with no error anywhere. **Do not switch to the unnamed overload,
and do not rename `SoftDeleteFilterName` without updating story 06.**

`IgnoreQueryFilters()` with no arguments stays the admin/audit escape hatch the intake asks for — it lifts every
filter, so **it is correct only on explicitly authorised `Areas/Admin` endpoints** (`CLAUDE.md` lines 63–64).
Everywhere else use the named form.

---

### 7 — Repository implementation and the specification evaluator

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/Repository.cs`** implementing
`IRepository<TAggregate, TId>` over `Context.Set<TAggregate>()`, with `ApplySpecification` delegating to the
evaluator. Use plain `async`/`await` bodies.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/SpecificationEvaluator.cs`** applying, in
order: `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName])` when `spec.IgnoreSoftDeleteFilter`, `Criteria`,
each `Includes` entry, ordering, **then** paging, then `AsNoTracking()` when `spec.AsNoTracking`.

**Paging goes after ordering.** `Skip`/`Take` on an unordered query returns non-deterministic rows on SQL
Server, and with no test suite that surfaces as an intermittent, unreproducible bug report.

Register in `backend/src/Crm.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
services.AddSingleton(TimeProvider.System);
services.AddScoped<ICurrentUserService, CurrentUserService>();
services.AddScoped<AuditingSaveChangesInterceptor>();
services.AddDbContext<CrmDbContext>((sp, options) => options
    .UseSqlServer(configuration.GetConnectionString("Crm"))
    .AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>()));
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CrmDbContext>());
```

Enable `EnableSensitiveDataLogging()` **in Development only** — the intake's "Technical hints" ask for it, and
matrix rows 8–9 below depend on being able to read the generated SQL.

---

### 8 — `TransactionBehavior`: commit, then dispatch domain events

**File: `backend/src/Crm.Application/Common/Behaviors/TransactionBehavior.cs`** — currently a pass-through
placeholder. **Replace the body.** Leave its position in the registration order in
`backend/src/Crm.Application/DependencyInjection.cs` **exactly where it is** (`CLAUDE.md` line 57).

#### Why the dispatch does not go in the interceptor

`CLAUDE.md` line 40 says events are "dispatched after `SaveChanges`", and the intake sharpens that to "fire only
after the transaction commits, and not at all if it rolls back". **Those are not the same instant, and the probe
proves it:**

| Situation | Was an explicit transaction still open when `SavedChangesAsync` fired? |
|---|---|
| `SaveChangesAsync()` with no explicit transaction | `False` — already committed |
| `SaveChangesAsync()` inside `BeginTransactionAsync()` | **`True` — the transaction was still open** |

An interceptor dispatching in `SavedChangesAsync` would therefore publish events for work that is **about to be
rolled back**. Dispatch belongs after `CommitAsync()` returns, which is `TransactionBehavior`.

#### Shape

```csharp
public async Task<TResponse> Handle(
    TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    // Queries do not open a transaction. Only commands do.
    if (request is not ICommandMarker)
    {
        return await next();
    }

    await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

    try
    {
        TResponse response = await next();

        // A failed Result must not commit. Business failures are results, not exceptions
        // (CLAUDE.md line 55) — so this is the only place that can see them.
        if (response is Result { IsFailure: true })
        {
            await transaction.RollbackAsync(ct);
            return response;
        }

        List<IDomainEvent> events = context.ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (EntityEntry<IHasDomainEvents> entry in context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }

        await transaction.CommitAsync(ct);       // past this line the write is durable
        await dispatcher.DispatchAsync(events, ct);
        return response;
    }
    catch (DbUpdateConcurrencyException ex)
    {
        await transaction.RollbackAsync(ct);
        logger.LogWarning(ex, "Concurrency conflict handling {RequestName}", typeof(TRequest).Name);
        return ConcurrencyFailure<TResponse>();  // Task 9
    }
}
```

**Harvest and clear the events _before_ `CommitAsync`, dispatch _after_ it.** Harvesting after the commit is a
bug: a handler may have called `SaveChangesAsync` and then `ChangeTracker.Clear()`, and the events are gone.
**Probe: harvest-before / dispatch-after gave `1` event dispatched on commit and `0` on rollback** — exactly the
intake criterion.

**Create file: `backend/src/Crm.Application/Common/Messaging/ICommandMarker.cs`** — an empty marker interface,
plus `ICommand` / `ICommand<T>` deriving from it and `IRequest`. Every command from story 11 onward implements
it. Without the marker, every read query opens and commits a pointless SQL Server transaction on the hot path.

**Create file: `backend/src/Crm.Application/Common/Abstractions/IDomainEventDispatcher.cs`** and its
`Crm.Infrastructure` implementation, resolving handlers through MediatR's `IPublisher`. Domain events implement
`IDomainEvent` (`Crm.Domain`) and are wrapped in a `DomainEventNotification<T>` adapter in `Crm.Application` —
**`Crm.Domain` must not reference MediatR**, and `Crm.Domain/BannedSymbols.txt` line 2 errors with `RS0030` if
it does.

---

### 9 — Concurrency: rowversion to HTTP 409

**Every aggregate root gets a rowversion.** In its `IEntityTypeConfiguration<T>`:

```csharp
builder.Property(x => x.RowVersion).IsRowVersion();
```

On SQL Server this maps to the `rowversion` column type and is **server-generated** — no interceptor stamps it.

The `catch` in Task 8 returns a failed `Result` carrying `Error.Conflict(...)`. `Error.Conflict` and
`ErrorType.Conflict` **already exist** in `backend/src/Crm.Domain/Common/Results/`. **Do not add a new error
type.**

```csharp
Error.Conflict(
    "Persistence.ConcurrencyConflict",
    "The record was modified by someone else. Reload and try again.")
```

`ConcurrencyFailure<TResponse>()` builds the failed result by reflection over `Result.Failure<T>` — the same
technique `ValidationBehavior` already uses for its `TResponse` guard. **Read that method and match it** rather
than inventing a second approach.

`ResultEndpointExtensions.ToProblem()` **already maps `ErrorType.Conflict` → 409** (verified — line 39 of that
file). Task 14's `ActionResult` sibling inherits the same mapping, so **no new HTTP mapping is needed here.**

- **Probe: `DbUpdateConcurrencyException` was raised as expected, with `ex.Entries.Count == 2`** — the root
  **and its owned entry**. Log `typeof(TRequest).Name`, not `ex.Entries[0]`, or the log names an owned value
  object rather than the aggregate that actually conflicted.
- The `catch` must wrap `next()` **and** `CommitAsync`. `DbUpdateConcurrencyException` surfaces from whichever
  of the two runs `SaveChangesAsync`.

---

### 10 — UTC everywhere

**Add to `ModelBuilderExtensions`:**

```csharp
public static void ApplyUtcDateTimeOffsetConverter(this ModelBuilder modelBuilder)
{
    var converter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
        v => v.ToUniversalTime(),
        v => v.ToUniversalTime());

    foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
    {
        foreach (IMutableProperty property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(converter);
            }
        }
    }
}
```

**Use `DateTimeOffset`, not `DateTime`, on every persisted timestamp.** `DateTime` on SQL Server loses the
offset and reads back as `Unspecified`, which is the standard source of "the SLA clock is three hours off" bugs
on a product whose users are in `Asia/Riyadh`. Name every such property `…AtUtc` so a reviewer sees the contract
without opening the configuration.

---

### 11 — Force the clock through `TimeProvider`

**All four `BannedSymbols.txt` files already exist and already ban `DateTime.get_Now` and `DateTime.get_Today`**
— including `Crm.Api`. **Verify that, do not re-add it.** The intake's `DateTime.Now` criterion is already
satisfied by story 01; matrix row 17 confirms it across all four projects.

What is missing is the direct-clock-read ban that makes Task 5's `TimeProvider` seam meaningful. **Append one
line to each of the four files:**

```
M:System.DateTimeOffset.get_UtcNow;Inject TimeProvider so time is controllable. Direct clock reads are banned.
```

`AuditingSaveChangesInterceptor` takes `TimeProvider` and therefore never needs it. If a genuinely unavoidable
call site appears, suppress it at that line with a justification comment — **not by deleting the ban line.**

> **Note the analyzer's limit, verified in story 01:** `RS0030` fires on **symbol use**, not on a `using`
> directive. A file that merely imports a banned namespace still builds. That is fine here — a clock read is a
> symbol use by definition.

---

### 12 — Sample aggregate, migration, and seeder

#### The sample aggregate

The intake requires a round-trip proof "with a value object and a child entity". Story 01 created
`backend/src/Crm.Application/Sample/Queries/GetSample/` and marked the whole `Sample` surface **for deletion in
story 11**. Put this aggregate under the same marker so it leaves with it.

**Create files under `backend/src/Crm.Domain/Sample/`:**

| File | Contents |
|---|---|
| `SampleId.cs` | `public sealed record SampleId(Guid Value) : StronglyTypedId<SampleId>(Value);` |
| `SampleAggregate.cs` | `AuditableEntity<SampleId>`, `ISoftDeletable`, `IHasRowVersion`; private `List<SampleLine> _lines`; `Rename(SampleName)` raises `SampleRenamed` |
| `SampleLine.cs` | child entity — `Entity<Guid>`, **no `DbSet`, no repository** |
| `SampleName.cs` | value object — `ValueObject`, `Create(string)` factory, trims and rejects empty |
| `SampleRenamed.cs` | `IDomainEvent` |

Head every file with the same two-line canonical/delete-in-story-11 comment already at the top of
`backend/src/Crm.Api/Endpoints/SampleEndpoints.cs`.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/SampleAggregateConfiguration.cs`**

```csharp
builder.HasKey(x => x.Id);
builder.Property(x => x.Id)
    .HasConversion(id => id.Value, value => new SampleId(value))
    .ValueGeneratedNever();

builder.OwnsOne(x => x.Name, name =>
    name.Property(p => p.Value).HasColumnName("Name").HasMaxLength(200).IsRequired());

builder.OwnsMany(x => x.Lines, lines =>
{
    lines.ToTable("SampleLines");
    lines.WithOwner().HasForeignKey("SampleId");
    lines.HasKey(l => l.Id);
});
builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

builder.Property(x => x.RowVersion).IsRowVersion();
builder.HasIndex(x => x.IsDeleted);
```

- **Probe: a value object needs a constructor whose parameter names match its mapped property names**
  (case-insensitive). A private `SampleName(string v)` fails at **model build** with
  `InvalidOperationException: No suitable constructor was found for the type 'SampleName'`. Name the parameter
  `value`, or give the value object a private parameterless constructor. This will bite again on
  `EmailAddress`, `PhoneNumber`, and `Money` in stories 11 and 15 — **document it in `backend/README.md`.**
- **Probe: `UsePropertyAccessMode(PropertyAccessMode.Field)` on the navigation makes the private
  `List<SampleLine>` backing field work** with no public setter and no public parameterless constructor.
- **Probe: the child is reachable only through the root** — the probe context exposed no `DbSet` for the child
  and `Model.FindEntityType(childType).IsOwned()` returned `True`.
- Index `IsDeleted`: every query carries `WHERE IsDeleted = 0` from Task 6's filter, so without the index every
  read is a scan from story 11 onward.

#### The migration

From `d:\CRM\backend`:

```bash
dotnet tool restore
dotnet ef migrations add Initial \
  --project src/Crm.Infrastructure \
  --startup-project src/Crm.Api \
  --output-dir Persistence/Migrations
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api
```

`dotnet ef` resolves to the **local tool pinned at 10.0.11** in `.config/dotnet-tools.json` (verified present,
`"rollForward": false`). If it resolves to the global **10.0.3** instead, run `dotnet tool restore` — **do not
downgrade the packages to match.**

**Create file: `backend/scripts/ef.ps1`** wrapping the two commands so the `--project` / `--startup-project`
pair is not retyped, and mistyped, in 56 later stories. Document it in `backend/README.md`.

#### The seeder

**Create file: `backend/src/Crm.Infrastructure/Persistence/DatabaseSeeder.cs`** — idempotent, keyed on a stable
well-known id, **never on a generated `Guid`**:

```csharp
if (await context.Samples.AnyAsync(s => s.Id == WellKnownIds.SampleSeed, ct))
{
    return;
}
```

Call it from `Program.cs` **in Development only**, inside a scope, **after** `MigrateAsync()`. Do not call
`EnsureCreated()` anywhere — it bypasses migrations and leaves a database no later migration can apply to.

---

### 13 — `backend/requests/persistence.http`

**Create file: `backend/requests/persistence.http`** — the project's stand-in for an integration test
(`CLAUDE.md` lines 102–104). Routes are the Task 14 Area routes.

```http
@host = http://localhost:5080

### Create a sample — 201, echoes createdBy/createdAtUtc
POST {{host}}/api/Admin/Samples
Content-Type: application/json

{ "name": "Round trip", "lines": ["first", "second"] }

### Read it back — value object and child both present
GET {{host}}/api/Admin/Samples/{{sampleId}}

### Update — populates modifiedBy/modifiedAtUtc, bumps rowVersion.
### rowVersion is a base64 byte[]; send back exactly what the GET returned.
PUT {{host}}/api/Admin/Samples/{{sampleId}}
Content-Type: application/json

{ "name": "Renamed", "rowVersion": "{{rowVersion}}" }

### Stale rowVersion — 409, NOT 500
PUT {{host}}/api/Admin/Samples/{{sampleId}}
Content-Type: application/json

{ "name": "Conflicting", "rowVersion": "AAAAAAAAB9E=" }

### Soft delete — 204, no SQL DELETE in the EF log
DELETE {{host}}/api/Admin/Samples/{{sampleId}}

### Gone from the default query — 404
GET {{host}}/api/Admin/Samples/{{sampleId}}

### Arabic value object round-trips unmangled
POST {{host}}/api/Admin/Samples
Content-Type: application/json

{ "name": "عميل تجريبي", "lines": ["سطر أول"] }

### Canonical query slice, now on a controller — 200
GET {{host}}/api/Agent/Sample?name=Ahmed

### Validation still returns RFC 9457 with field errors — 400
GET {{host}}/api/Agent/Sample?name=
```

---

### 14 — Controllers, Areas, and Swagger

This replaces story 01's minimal-API surface. **It is a rewrite of the API layer, not an addition beside it.**

#### 14a — Delete the minimal-API surface

**Delete** `backend/src/Crm.Api/Endpoints/` entirely — `IEndpointGroup.cs`, `EndpointGroupExtensions.cs`, and
`SampleEndpoints.cs`. Remove the `app.MapEndpointGroups();` call from `Program.cs`. Nothing else references
them; `MapEndpointGroups` is called from exactly one line.

#### 14b — The four Areas

**Create directories:**

```
backend/src/Crm.Api/Areas/
  Agent/Controllers/       agent workspace          (stories 20, 21, 28-31)
  Portal/Controllers/      customer self-service    (stories 47-50)
  Admin/Controllers/       tenant admin + reports   (stories 08-10, 51-54)
  Public/Controllers/      versioned external API   (stories 55-57)
```

Add an `_area.md` one-liner in each naming the persona and the stories that fill it — an empty directory does
not survive git, and the next agent needs to know which Area a new controller belongs in.

**Create file: `backend/src/Crm.Api/Areas/CrmAreas.cs`**

```csharp
public static class CrmAreas
{
    public const string Agent = "Agent";
    public const string Portal = "Portal";
    public const string Admin = "Admin";
    public const string Public = "Public";

    public static readonly IReadOnlyList<string> All = [Agent, Portal, Admin, Public];
}
```

`All` drives both the Swagger document registration and the UI picker, so adding an Area is a one-line change.

#### 14c — The controller base and its route template

**Create file: `backend/src/Crm.Api/Common/ApiControllerBase.cs`**

```csharp
[ApiController]
[Route("api/[area]/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ActionResult<T> ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();

    protected ActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : result.Error.ToActionResult();
}
```

**Probe: `[Route("api/[area]/[controller]")]` on an `[Area("Agent")]` controller resolves to
`/api/Agent/Tickets`**, and the `area` route value is populated — which is what 14d's document split keys on.
A derived controller **must** carry its own `[Area(CrmAreas.Agent)]` attribute; the token expands from that
attribute, and a controller that forgets it produces a literal `/api/[area]/…` route that 404s.

**Probe: routing is case-insensitive** — `/api/agent/tickets` and `/api/Agent/Tickets` both returned 200 — **but
the generated Swagger document uses the declared casing** (`/api/Agent/Tickets`). Story 04 generates its typed
client from that document, so **declare Areas in `CrmAreas` with the casing you want in the URLs** and use the
same casing in every `.http` file.

**Add to `backend/src/Crm.Api/Common/ResultEndpointExtensions.cs`** an `ActionResult` sibling of the existing
`ToProblem()`:

```csharp
public static ActionResult ToActionResult(this Error error)
```

**Reuse the existing status-code `switch` — do not fork the mapping.** Extract the `error.Type` → status-code
decision into one private method that both `ToProblem` (minimal-API `IResult`) and `ToActionResult` call, so a
Conflict is 409 on both paths and story 03 has one place to replace. Once `Endpoints/` is deleted nothing calls
`ToProblem` any more; **keep it only if health-check mapping needs it, otherwise delete it in the same edit.**

#### 14d — Swagger, one document per Area

**Add to `backend/src/Crm.Api/Crm.Api.csproj`:** `<PackageReference Include="Swashbuckle.AspNetCore" />`.

**Rewrite the registration block in `backend/src/Crm.Api/Program.cs`:**

```csharp
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    foreach (string area in CrmAreas.All)
    {
        options.SwaggerDoc(area.ToLowerInvariant(), new OpenApiInfo
        {
            Title = $"CRM {area} API",
            Version = "v1",
        });
    }

    // Route each action into the document for its Area.
    options.DocInclusionPredicate((docName, apiDescription) =>
        apiDescription.ActionDescriptor.RouteValues.TryGetValue("area", out string? area)
        && string.Equals(area, docName, StringComparison.OrdinalIgnoreCase));

    options.IncludeXmlComments(Path.Combine(
        AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});
```

> **`using Microsoft.OpenApi;` — not `Microsoft.OpenApi.Models`.** Swashbuckle 10.x moved to OpenAPI.NET v2 and
> `OpenApiInfo` left the `.Models` namespace. **Probe: the `.Models` import fails with `CS0234`.** Every
> Swashbuckle sample written before 2025 has the old import.

`IncludeXmlComments` works because story 01 already sets `GenerateDocumentationFile` in
`Directory.Build.props` and suppresses `CS1591`.

**Middleware, replacing `app.MapOpenApi()`:**

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (string area in CrmAreas.All)
        {
            options.SwaggerEndpoint($"/swagger/{area.ToLowerInvariant()}/swagger.json", $"CRM {area} API");
        }
    });
}
```

**Drop `builder.Services.AddOpenApi()` and the `Microsoft.AspNetCore.OpenApi` `PackageReference`.** Running both
generators produces two documents that drift, and story 04 would have no single answer about which to generate
its client from. Leave the `Microsoft.AspNetCore.OpenApi` line in `Directory.Packages.props` — a central version
pin with no consumer is harmless, and story 55 may want it back.

**Leave the two `MapHealthChecks` calls and their liveness/readiness comments untouched.** Add
`app.MapControllers();` where `app.MapEndpointGroups();` was.

**Probe: this exact configuration produced four documents that partitioned cleanly**, one route each, and a
Swagger UI at `/swagger/index.html` returning **200** with a working document picker:

```
doc agent:  paths=[/api/Agent/Tickets]
doc portal: paths=[/api/Portal/MyTickets]
doc admin:  paths=[/api/Admin/Users]
doc public: paths=[/api/Public/v1/Customers]
```

#### 14e — Port the canonical slice to a controller

**Create file: `backend/src/Crm.Api/Areas/Agent/Controllers/SampleController.cs`** — the read slice, carrying
over the canonical/delete-in-story-11 header comment from the deleted `SampleEndpoints.cs`:

```csharp
[Area(CrmAreas.Agent)]
public sealed class SampleController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SampleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SampleDto>> Get(
        [FromQuery] string? name, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetSampleQuery(name ?? string.Empty), cancellationToken));
}
```

**Create file: `backend/src/Crm.Api/Areas/Admin/Controllers/SamplesController.cs`** with the `POST` / `GET{id}` /
`PUT` / `DELETE` actions that Task 13's `.http` file exercises. These exist to prove the persistence baseline and
**leave with the rest of the `Sample` surface in story 11.**

Every action gets `[ProducesResponseType…]` attributes — without them each Area's Swagger document claims every
action returns 200 with an untyped body, and story 04's generated client inherits that lie.

**Do not add `[Authorize]` anywhere yet.** Stories 07 and 09 own authentication and the per-Area policies. Put a
`// Story 09: this Area requires policy "…"` comment in each `_area.md` so the seam is visible.

---

### 15 — Record the convention change in `CLAUDE.md`

`CLAUDE.md` line 12 currently reads `Crm.Api/  minimal API endpoints, auth, DI composition root`, and line 100
points verification at "Swagger UI or the feature's `.http` file". **Edit the repository layout block** so line
12 reads:

```
    Crm.Api/             MVC controllers in per-persona Areas, Swagger, auth, DI composition root
```

**Add to the `## Backend` bullet list**, after the CQRS bullet:

> - **API surface:** MVC controllers, not minimal APIs. One **Area per persona** — `Agent`, `Portal`, `Admin`,
>   `Public` — under `Crm.Api/Areas/<Area>/Controllers/`. Every controller derives from `ApiControllerBase`,
>   carries `[Area(CrmAreas.X)]`, and inherits the `api/[area]/[controller]` route template. Each Area gets its
>   own Swagger document at `/swagger/{area}/swagger.json`; the UI at `/swagger` is Development-only.

This is the only file outside `backend/` and `.squad/` that this story touches. **A later story that finds a
minimal API in this codebase should delete it, not follow it.**

---

## Frontend Tasks

**No frontend changes required.** `frontend/` does not exist until story 04. Story 04 generates its typed client
from the **four per-Area Swagger documents** created in Task 14d, not from `/openapi/v1.json` — that endpoint is
gone. Flag this when planning story 04.

---

## Edge Cases & Failure Modes

- **Soft delete nulls a value object.** Trigger: setting `entry.State = EntityState.Modified` on a deleted root.
  EF leaves the cascaded owned entries in `Deleted` and writes `NULL` over their columns — on a **non-nullable**
  column it throws, on a **nullable** one it **silently blanks the data**. Expected: no `DELETE`, all columns
  intact. Enforced by the two-pass `SoftDelete` in
  `backend/src/Crm.Infrastructure/Persistence/Interceptors/AuditingSaveChangesInterceptor.cs` (Task 5).
  **Reproduced during planning; this is the single most likely way to get this story wrong.**

- **Domain events fire for rolled-back work.** Trigger: dispatching from `SavedChangesAsync`. Verified: with an
  explicit transaction open, `SavedChangesAsync` runs **before** the commit. Expected: dispatch only after
  `CommitAsync` returns. Enforced in `TransactionBehavior.Handle` (Task 8).

- **Events lost before dispatch.** Trigger: a handler calls `ChangeTracker.Clear()` after saving. Expected:
  events already harvested. Enforced by harvesting **before** `CommitAsync` (Task 8).

- **Story 06 silently disables soft delete.** Trigger: a later story calls the **unnamed** `HasQueryFilter`
  overload, replacing this one. Expected: named filters compose and AND together. Enforced by
  `CrmDbContext.SoftDeleteFilterName` and `ApplySoftDeleteQueryFilter` (Task 6). **Verified on EF 10.0.11.**

- **A spec lifts the tenant filter.** Trigger: bare `IgnoreQueryFilters()` inside `SpecificationEvaluator`,
  leaking other tenants' rows once story 06 lands. Expected: only the soft-delete filter lifts. Enforced by
  `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName])` (Task 7).

- **Value object has no usable constructor.** Trigger: a private constructor whose parameter name does not match
  the mapped property name. Fails at **model build**, before any query runs, with `InvalidOperationException:
  No suitable constructor was found`. Expected: parameter named `value`, or a private parameterless constructor.
  Reproduced during planning.

- **A controller forgets `[Area]`.** Trigger: deriving from `ApiControllerBase` without `[Area(CrmAreas.X)]`.
  The `[area]` token does not expand, the route is a literal `/api/[area]/…` that 404s, **and the action
  appears in no Swagger document at all** — `DocInclusionPredicate` finds no `area` route value. Expected: every
  controller carries the attribute. Symptom is a missing endpoint, not an error.

- **Swashbuckle namespace.** `using Microsoft.OpenApi.Models;` fails with **`CS0234`** on Swashbuckle 10.x —
  OpenAPI.NET v2 moved `OpenApiInfo` to `Microsoft.OpenApi`. Every pre-2025 sample has the old import.
  Reproduced during planning.

- **Two OpenAPI generators.** Trigger: leaving `AddOpenApi()`/`MapOpenApi()` in place alongside `AddSwaggerGen`.
  Both serve documents, they drift, and story 04 generates its client from whichever it finds first. Expected:
  Swashbuckle only (Task 14d).

- **Casing drift between URL and document.** Routing is case-insensitive, but the Swagger document emits the
  **declared** casing. A `.http` file written in lowercase still works; a generated client built from the
  document will use `/api/Agent/…`. Keep one casing everywhere (Task 14c).

- **`dotnet ef` version skew.** Trigger: the global **10.0.3** tool resolving instead of the pinned local
  **10.0.11**. EF **errors**, it does not warn. Fix with `dotnet tool restore`.

- **Queries open transactions.** Trigger: `TransactionBehavior` with no `ICommandMarker` guard. Every read pays
  for a transaction. Enforced by the marker check at the top of `Handle` (Task 8).

- **`DbUpdateConcurrencyException` reported against the wrong entity.** `ex.Entries` contains the root **and its
  owned entries** (probe: `2`). Log the request name, not `ex.Entries[0]` (Task 9).

- **Rowversion reformatted by a client.** `byte[]` serialises as base64. A client that round-trips it verbatim
  works; one that re-encodes it produces a spurious 409. The contract is noted in `persistence.http`.

- **`EnsureCreated()` alongside migrations.** It creates the schema **without** writing `__EFMigrationsHistory`,
  so the next `database update` fails on already-existing objects. Expected: `MigrateAsync()` only (Task 12).

- **Seeder not idempotent.** Trigger: keying the existence check on a freshly generated `Guid`, which never
  matches. Expected: a stable well-known id (Task 12).

- **Non-UTC dev machine.** Trigger: a machine in `Asia/Riyadh` (+03:00) writing a local `DateTimeOffset`.
  Expected: stored as UTC. Enforced by the converter in Task 10. Verify by changing the OS timezone, not by
  reading the code.

- **`TreatWarningsAsErrors` and EF's generated migration.** The scaffolded migration may trip
  `GenerateDocumentationFile` or a nullability analyzer. Fix it, or scope a suppression **to the
  `Persistence/Migrations` folder in `.editorconfig`**. **Do not add a solution-wide `NoWarn`** — on this
  project the compiler is the only automated check there is (`CLAUDE.md` lines 96–97).

---

## Test Plan

**This project ships without automated tests** — `CLAUDE.md` lines 82–104, a recorded decision by the project
owner. **Do not create a test project, a `tests/` folder, a `*.Tests.csproj`, or add xUnit, NUnit,
FluentAssertions, or Testcontainers to any project file in this story.**

This matrix replaces the suite. Run every row and record the result in the story report. Rows 8–9 need
`EnableSensitiveDataLogging()` in Development so the generated SQL is readable.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Clean build | `dotnet build backend/Crm.sln` | Succeeds, **0 warnings, 0 errors** |
| 2 | Migration applies | `docker compose up -d`, then the Task 12 commands | Both succeed; `__EFMigrationsHistory` has one row |
| 3 | Migration from cold | `docker compose down -v && docker compose up -d`, `dotnet ef database update` | Succeeds on an empty server |
| 4 | Round-trip | `persistence.http` requests 1–2 | Value object in its own columns; both child lines returned |
| 5 | Child is not addressable | Inspect `CrmDbContext` | `DbSet<SampleAggregate>` only; **no `DbSet<SampleLine>`** |
| 6 | Created stamps | `persistence.http` request 1 | `createdBy` / `createdAtUtc` set; modified fields null |
| 7 | Modified stamps | `persistence.http` request 3 | `modifiedBy` / `modifiedAtUtc` set; created fields unchanged |
| 8 | Soft delete emits no DELETE | `persistence.http` request 5, watch the EF SQL log | An `UPDATE` setting `IsDeleted`; **no `DELETE`** anywhere |
| 9 | Owned columns survive the delete | `SELECT * … WHERE IsDeleted = 1` in SSMS | `Name` still populated, **not `NULL`** |
| 10 | Filter hides the row | `persistence.http` request 6 | **404** |
| 11 | Escape hatch shows it | Temporary admin read using `IgnoreQueryFilters()` | Row returned |
| 12 | Named filter lifts alone | `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName])` | Soft-deleted rows returned; other filters still applied |
| 13 | Concurrency → 409 | `persistence.http` request 4, stale rowVersion | **409**, `application/problem+json`. **Not 500** |
| 14 | Events after commit | `Debug` logging, run request 1 | Handler → commit → dispatch, **in that order** |
| 15 | Events not after rollback | Force a failure between `SaveChangesAsync` and `CommitAsync` | **No** dispatch log line; no row written |
| 16 | UTC | Set Windows to `(UTC+03:00) Riyadh`, restart the API, run request 1 | Stored value is UTC; SSMS shows `+00:00` |
| 17 | `DateTime.Now` ban — all four projects | Add `var x = DateTime.Now;` to one file in **each** project, build each | All four fail **RS0030**. Revert all four. |
| 18 | `DateTimeOffset.UtcNow` ban | Add `var x = DateTimeOffset.UtcNow;` outside the interceptor, build | Fails **RS0030**. Revert. |
| 19 | Seeder idempotent | Run the API twice against the same database, `SELECT COUNT(*)` after each | Identical counts |
| 20 | Queries open no transaction | `Debug` logging, run request 2 | **No** `BEGIN TRANSACTION` for the read |
| 21 | Arabic round-trip | `persistence.http` request 7 | `عميل تجريبي` unmangled in the response **and in SSMS** |
| 22 | Four Swagger documents | `GET /swagger/{agent,portal,admin,public}/swagger.json` | Four valid documents; each lists **only** its Area's routes |
| 23 | Swagger UI | Browse `/swagger` | Loads; picker lists all four documents; "Try it out" works on request 8 |
| 24 | Swagger is Development-only | Run with `ASPNETCORE_ENVIRONMENT=Production`, browse `/swagger` | **404** |
| 25 | Minimal APIs are gone | `grep -r "IEndpointGroup\|MapEndpointGroups\|MapOpenApi" backend/src` | **No matches** |
| 26 | Canonical slice still works | `persistence.http` requests 8–9 | **200**, and **400** with an RFC 9457 body listing the `name` field |
| 27 | Regression — health | `backend/requests/health.http` requests 1–2 | Unchanged: `/health` 200, `/health/ready` 200/503 |

**`backend/requests/health.http` requests 3–5 target `/api/sample` and are superseded by Task 14e.** Update
those three lines to `/api/Agent/Sample` in the same change, or the file lies to the next agent.

---

## Migration / Rollback

- `Initial` is the **first** migration. If it is wrong, `dotnet ef migrations remove`, fix the configuration,
  and re-add. **Do not hand-edit a generated migration file** to patch a modelling mistake — the model snapshot
  drifts from the migration and the *next* story's `migrations add` produces nonsense.
- Rollback: `dotnet ef database update 0` drops everything this story created. Safe here and **only** here,
  because there is no production data. Say so in `backend/README.md` before story 11 makes it untrue.
- **Half-applied state:** if `database update` fails partway, SQL Server has some objects and
  `__EFMigrationsHistory` has no row. Recovery on a dev box is `docker compose down -v && docker compose up -d`
  then a clean `database update`. Nothing here is worth a repair script.
- **The API-surface rewrite (Task 14) has no rollback and does not need one** — story 01 is uncommitted, and the
  only consumer of the deleted routes is `backend/requests/health.http`, updated in the same change. Commit
  Task 14 and Tasks 1–13 together so the tree is never in a state where the `.http` files do not match the
  routes.

---

## Verification Steps

1. **Infrastructure starts:** `cd backend && docker compose up -d` — `docker compose ps` shows `crm-sqlserver`
   healthy.
2. **Tooling resolves:** `dotnet tool restore` from `d:\CRM`, then `dotnet ef --version` — prints **10.0.11**.
3. **Backend builds:** `dotnet build backend/Crm.sln` — **zero warnings, zero errors**. A warning is a failure
   on this project.
4. **Schema applies:** the Task 12 `dotnet ef` commands, from `d:\CRM\backend`.
5. **Backend runs:** `dotnet run --project backend/src/Crm.Api` — starts, migrates, seeds, no unhandled
   exception.
6. **Swagger answers:** matrix rows 22–24 — four documents, the UI picker, and a 404 in Production.
7. **Endpoints answer:** run every request in `backend/requests/persistence.http` — matches matrix rows 4–13,
   21, and 26.
8. **Interceptor behaviour:** matrix rows 8, 9, 14, 15, with sensitive-data logging on. **Read the SQL, do not
   infer it** — this is the story's whole point.
9. **Bans bite:** matrix rows 17 and 18, then **revert every deliberate violation** and re-run step 3 to confirm
   the tree is clean.
10. **UTC:** matrix row 16, with the machine timezone actually changed.
11. **Regression:** matrix rows 25 and 27 — no minimal-API remnants, health checks unchanged.

---

## Done Criteria

- [ ] `dotnet build backend/Crm.sln` succeeds with **zero warnings**.
- [ ] `dotnet ef migrations add Initial` and `dotnet ef database update` both succeed against the compose SQL
      Server, and succeed again from a cold `docker compose down -v`.
- [ ] `CrmDbContext` registers configurations **only** via `ApplyConfigurationsFromAssembly`; no data
      annotations and no inline `modelBuilder.Entity<T>()` mapping anywhere.
- [ ] The sample aggregate round-trips: `SampleName` persists as **owned columns**, `SampleLine` is reachable
      only through the root, and both survive a save/load cycle.
- [ ] `CrmDbContext` exposes `DbSet<SampleAggregate>` and **no `DbSet<SampleLine>`**.
- [ ] Saving populates created-by/at; updating populates modified-by/at — written by the interceptor, **not by
      any handler**.
- [ ] Removing an `ISoftDeletable` sets the flag and timestamp and issues **no SQL `DELETE`**, confirmed from
      the EF Core generated-SQL log — **and the owned value-object columns are still populated afterwards.**
- [ ] A default query does not return soft-deleted rows; `IgnoreQueryFilters()` does; and
      `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName])` lifts **only** the soft-delete filter.
- [ ] The soft-delete filter uses the **named** `HasQueryFilter` overload, so story 06 can add the tenant filter
      without replacing it.
- [ ] A concurrent update returns **HTTP 409** with an `application/problem+json` body, not a 500.
- [ ] Domain events dispatch **after** `CommitAsync` returns and **not at all** on rollback, confirmed from the
      log of a deliberately failing save.
- [ ] `TransactionBehavior` opens a transaction for commands only — a query logs no `BEGIN TRANSACTION`.
- [ ] All persisted timestamps are UTC when written from a machine set to `(UTC+03:00) Riyadh`.
- [ ] `DateTime.Now` fails the build with **RS0030** in all four projects, and `DateTimeOffset.UtcNow` fails
      outside `AuditingSaveChangesInterceptor`.
- [ ] Running the seeder twice produces identical row counts.
- [ ] `Crm.Domain` still has **no `ProjectReference`** and no EF Core, MediatR, or ASP.NET package.
- [ ] **`Crm.Api/Endpoints/` is deleted** and `grep -r "IEndpointGroup\|MapEndpointGroups\|MapOpenApi"
      backend/src` returns nothing.
- [ ] **Four Areas exist** under `Crm.Api/Areas/` — `Agent`, `Portal`, `Admin`, `Public` — each with an
      `_area.md` naming its persona and the stories that fill it.
- [ ] **Every controller derives from `ApiControllerBase`, carries `[Area(CrmAreas.X)]`**, and has
      `[ProducesResponseType…]` on every action.
- [ ] **Four Swagger documents** serve at `/swagger/{area}/swagger.json`, each containing only its Area's
      routes, and the UI at `/swagger` lists all four and is **404 in Production**.
- [ ] `Error.Conflict` maps to 409 through **one** shared status-code mapping used by both result translators.
- [ ] `backend/requests/persistence.http` contains all nine requests and every one has been run against a live
      API, including the Arabic round-trip.
- [ ] `backend/requests/health.http` requests 3–5 updated to the new `/api/Agent/Sample` route and re-run.
- [ ] `CLAUDE.md` line 12 and the `## Backend` bullets record the controllers/Areas/Swagger convention.
- [ ] `backend/README.md` documents the `ef.ps1` wrapper, the value-object constructor-parameter-name rule, the
      Areas layout, and the `database update 0` rollback caveat.
- [ ] **No test project, no `tests/` folder, and no test framework package exists anywhere in the repository.**
- [ ] `00-overview.md` updated with this story.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
