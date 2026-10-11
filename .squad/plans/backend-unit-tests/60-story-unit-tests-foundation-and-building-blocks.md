# Story 60 — Unit tests — foundation and building blocks

## Prerequisites

- **Tracker id UT-01.** Row 1 of [`.squad/UNIT-TEST-STORIES.md`](../../UNIT-TEST-STORIES.md). Blocks UT-02..UT-05 (plans 61–64).
- **All implemented stories (01–07, 11, 14, 15, 18, 20, 47, 58, 59) are committed** (HEAD `c931351`). This story changes **no production
  code**: it only adds tests, test-only packages/config, and documentation.
- **The setup session's scaffolding is already in the working tree, uncommitted, and belongs to this story's commit.** Keep and build on it:
  - `backend/Directory.Packages.props` — `Label="Tests"` group (`xunit.v3` 4.0.2, `NSubstitute` 6.2.0, `NSubstitute.Analyzers.CSharp` 1.0.17,
    `Microsoft.EntityFrameworkCore.InMemory` 10.0.11).
  - `backend/global.json` — `"test": { "runner": "Microsoft.Testing.Platform" }` (xunit.v3 4.x refuses VSTest on the .NET 10 SDK).
  - `backend/tests/Directory.Build.props` — imports the parent props, `OutputType Exe`, `IsTestProject`, `GenerateDocumentationFile false`,
    `PackageReference`s `xunit.v3`, `NSubstitute`, `NSubstitute.Analyzers.CSharp`, global usings `Xunit` + `NSubstitute`.
  - `backend/Directory.Build.targets` — `Tests` accepted as a module layer in the CRM0001 name check; header comment already mentions
    `Crm.Modules.<Module>.Tests` (task D3 says what is left).
  - Six test projects in `backend/tests/` and in `backend/Crm.sln` (solution folder `tests`): `Crm.SharedKernel.Tests` (references SharedKernel
    only; has `Results/ResultTests.cs`, 3 tests), `Crm.BuildingBlocks.Tests` (references the three BuildingBlocks projects),
    `Crm.Modules.{Organisation,Identity,Customers,Tickets}.Tests` (each references its module's Domain, Application, Infrastructure; **no
    tests yet - UT-02..UT-05 fill them**).
- **Modules.** No module is touched (nothing under `backend/src/` changes). `backend/src/BuildingBlocks/*` is **read only**; all new code goes
  under `backend/tests/Crm.SharedKernel.Tests` and `backend/tests/Crm.BuildingBlocks.Tests`. The four module test projects are only built.
- **Checked during planning (2026-10-11, from files only - nothing was built or run):** .NET SDK `10.0.301` (`backend/global.json`, rollForward
  `latestFeature`); `Directory.Packages.props`: EF Core 10.0.11, MediatR **12.4.1** (licence pin, never bump), FluentValidation 12.1.1 + the test
  group above. `backend/Directory.Build.props`: `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, banned-API
  lists `BannedSymbols.Clock.txt` (`System.DateTime`, `DateTimeOffset.UtcNow/Now`), `.Logging.txt` (`System.Console`, `System.Diagnostics.Debug`),
  `.TimeProvider.txt` (`System.TimeProvider`; applies to every project except `Crm.BuildingBlocks.Infrastructure`, so **tests may not use
  `TimeProvider`/`FakeTimeProvider` either**). `BannedSymbols.Pure.txt` applies to SharedKernel/Domain/Contracts only - not to test projects.
  `Crm.BuildingBlocks.Presentation.csproj`: `FrameworkReference Microsoft.AspNetCore.App`, `MediatR`, `Polly.Core`;
  `Crm.BuildingBlocks.Infrastructure.csproj`: `FrameworkReference`, Serilog.AspNetCore, OpenTelemetry, EF SqlServer, Http.Resilience, Redis cache.
  Grep confirmed there is **no `InternalsVisibleTo`** anywhere in `backend/`.
- **Decisions later stories must not silently reverse (restated from the tracker):** xUnit v3 + NSubstitute + plain `Assert` only; **never
  FluentAssertions** (8+ is a paid licence); EF Core InMemory only for persistence logic that cannot be isolated; tests touch only
  `backend/tests/**` (plus the docs/config this plan names); a bug or untestable seam found while testing is **recorded in Follow-ups and in the
  tracker Session log, never fixed under `backend/src/`**; no analyzer suppression, no `!`, no clock reads; PascalCase test names without
  underscores (CA1707); one test class per production type, folders mirror the production project.
- **Environment.** No Python, no `pnpm` (irrelevant here). A developer's `Crm.Api` may be running and lock `backend/src/Crm.Api/bin`: do **not**
  kill it; build test projects on their own (`dotnet build tests/<Project>`) and say in the report if the full solution build could not run.

---

## Story Goal

Turn the "no automated tests" policy off for the backend and prove it on the foundation every module sits on.

1. **Finish and document the test infrastructure** (packages, MTP opt-in, the `Tests` build-rule layer, run commands) and rewrite the policy text in
   `CLAUDE.md`, `.claude/commands/squad-implement.md`, `backend/Directory.Build.targets`, `backend/README.md` (and one sentence in the root
   `README.md` that states the opposite).
2. **`Crm.SharedKernel.Tests`** — every type in `Crm.SharedKernel`.
3. **`Crm.BuildingBlocks.Tests`** — every unit-testable type in `Crm.BuildingBlocks.Application`, `.Infrastructure` and `.Presentation`: the six
   MediatR pipeline behaviours, the authorisation guard and permission catalogue, `OrganisationScope`, specifications, caching, the outbox,
   `ModuleDbContext` (scope filter, write guard, unit-of-work members), the auditing/soft-delete interceptor, repositories, log redaction,
   correlation id, current-user resolution, error-to-`ProblemDetails` mapping, authorisation policies/conventions, middleware, rate limiting.
4. Leave `Crm.Modules.{Organisation,Identity,Customers,Tickets}.Tests` empty-but-building for UT-02..UT-05.

**Out of scope:** module tests, frontend tests, integration tests against SQL Server / Testcontainers, API end-to-end tests, coverage gates in CI,
the Audit module (no code yet), any change under `backend/src/`.

**Types deliberately not covered** (list them in the final report as "not unit-testable here"): `ModuleDatabaseExtensions.MigrateAndSeedModulesAsync`
(needs a relational provider; InMemory `MigrateAsync` throws), `ModuleDesignTimeDbContextFactory`, `ModuleSqlServer` (SQL Server wiring),
`IModule`/`IModuleSeeder` (interfaces), pure interfaces and markers (`ICacheService`, `IRepository`, `IUnitOfWork`, `IDomainEventDispatcher`,
`IHasRowVersion`, `ISoftDeletable`, `IAuditable`, `IDomainEvent`, `IHasDomainEvents`), `PagedResult<T>` (positional record), `CrmDiagnostics` (static
instruments; exercised by `LoggingBehaviorTests`), string-constant classes `CrmClaimTypes`/`CrmAuthenticationSchemes`/`CrmRoles` (exercised by the
tests that use them) and `CrmMessageKeys` (covered by the resource parity test in slice 2).

---

## Context — Read These Files First

The planner read every file below; the implementer re-reads the ones named in each slice before writing its tests.

1. `CLAUDE.md` — lines 6–38 (repository layout block), 255–281 (**Testing policy**, rewritten by D1), 301–312 (**Verification**, gets the test
   command). Everything else binds unchanged (analyzers, banned APIs, `IDateTime`, no `!`).
2. `.squad/UNIT-TEST-STORIES.md` — override rules (lines 38–63) and working-tree notes (lines 65–79); this story edits row 1 (line 26),
   "Remaining" (line 35) and the Session log (line 81+).
3. `backend/tests/Directory.Build.props`, `backend/tests/Crm.SharedKernel.Tests/Crm.SharedKernel.Tests.csproj`,
   `backend/tests/Crm.BuildingBlocks.Tests/Crm.BuildingBlocks.Tests.csproj`, `backend/tests/Crm.SharedKernel.Tests/Results/ResultTests.cs` — house style:
   `public sealed class XTests`, `[Fact]`, PascalCase names, `Assert.*`, file-scoped namespace `Crm.<Project>.Tests.<Folder>`.
   `Crm.BuildingBlocks.Tests.csproj` has **no** `Microsoft.EntityFrameworkCore.InMemory` reference yet (task 1.2).
4. `backend/src/BuildingBlocks/Crm.SharedKernel/` — whole project (15 small files; `Results/`, `Scoping/`).
5. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/` — `Behaviors/*.cs` (six), `Abstractions/OrganisationScope.cs` (198 lines),
   `Authorization/*.cs`, `Caching/*.cs`, `Messaging/*.cs`, `Modules/*.cs`, `Specifications/Specification.cs`, `Localization/*.cs` and the
   `Localization/Resources/CrmMessages*.resx` files (the parity test in slice 2 reads them), `DependencyInjection.cs`, `Exceptions/*.cs`.
6. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/` — `Persistence/ModuleDbContext.cs` (150 lines) and
   `ModuleDbContext.OrganisationScope.cs` (134), `Persistence/Interceptors/AuditingSaveChangesInterceptor.cs`, `Persistence/Repositories/*.cs`,
   `Persistence/ModelBuilderExtensions.cs`, `Outbox/*.cs` (`OutboxJson` is `internal`; `ModuleDbContext.GetDueOutboxMessagesAsync/HasConsumedAsync/
   RecordConsumer` are `internal` - the dispatcher is therefore tested end-to-end through `BackgroundService.StartAsync`), `Caching/DistributedCacheService.cs`,
   `Identity/*.cs`, `Logging/*.cs` (`SensitiveData` is `internal` - tested through the public policy/enricher), `Correlation/*.cs`,
   `Http/HttpClientBuilderExtensions.cs`, `Time/SystemDateTime.cs`, `Modules/ModuleServiceCollectionExtensions.cs`,
   `Observability/ObservabilityServiceCollectionExtensions.cs` (**not read during planning** - risk R12), `DependencyInjection.cs`.
7. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/` — `ApiControllerBase.cs`, `Authorization/*.cs`, `ErrorHandling/*.cs`,
   `Localization/*.cs`, `Scoping/OrganisationScopeMiddleware.cs`, `RateLimiting/RateLimitingServiceCollectionExtensions.cs`, `CrmAreas.cs`.
8. `.claude/commands/squad-implement.md` — lines 18–20 (backend-done definition) and 25–29 (the "No automated tests" rule replaced by D2).
9. `backend/Directory.Build.targets` — header comment lines 3–14 (already updated in the working tree) and the `Tests` layer condition (line ~28).
10. `backend/README.md` — line 149 ("There is **no test project**…"), lines 380–385 (section "No automated tests"), "Solution layout" block (~lines 59–78),
    "Notable pinned versions" (line 387+). Root `README.md` lines 233–242 ("Quality gates").
11. Plan-style precedent: `.squad/plans/customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md`.

---

## Test conventions (apply to every slice; UT-02..UT-05 copy these)

- **Naming / layout:** `Crm.BuildingBlocks.Tests/<Layer>/<folder mirroring src>/<ProductionType>Tests.cs`, `public sealed class <Type>Tests`, namespace
  `Crm.BuildingBlocks.Tests.<Layer>.<Folder>`. Names are PascalCase sentences without underscores (`DeniesWhenPermissionMissing`). Helpers shared by
  several classes live in `Crm.BuildingBlocks.Tests/Support/` (namespace `Crm.BuildingBlocks.Tests.Support`). Make fixture/helper types
  `public sealed` (avoids CA1812 "never instantiated"; the existing `ResultTests` shows this builds clean).
- **Asserts:** `Assert.Equal/True/False/Null/NotNull/Empty/Single/Collection/Throws<T>/ThrowsAsync<T>/IsType<T>`. No FluentAssertions, no Shouldly.
- **Cancellation:** xunit.v3's analyzer (xUnit1051) wants `TestContext.Current.CancellationToken` on every call that accepts a token - do it everywhere.
- **Time:** fixed `DateTimeOffset` constants and `Substitute.For<IDateTime>()` with `.UtcNow.Returns(...)`. Never `DateTime`, `DateTimeOffset.UtcNow/Now`,
  `TimeProvider`, `FakeTimeProvider`, `Thread.Sleep`.
- **Logging:** `NullLogger<T>.Instance` unless the test asserts on log output; then a hand-written `RecordingLogger<T> : ILogger<T>` in `Support/`
  (an NSubstitute `ILogger` cannot see `[LoggerMessage]` state reliably). Never `System.Console`.
- **Scopes:** never mock `OrganisationScope` (sealed class with a public constructor and `ForPortalCustomer`); build real ones in `Support/Scopes.cs` and
  hand them to an `ICurrentUserService` substitute from `Support/CurrentUsers.cs`.
- **No `!`, no `#pragma warning disable`, no `[SuppressMessage]`, no `NoWarn` edit.** If an analyzer objects, change the test code.
- **EF Core InMemory** only for `ModuleDbContext`-derived behaviour (slice 5): a unique database name per test (`Guid.NewGuid().ToString("N")`) because
  xUnit runs test classes in parallel.

---

## Implementation tasks

### Task group 1 — Test project changes

#### 1.1 — Confirm the scaffolding builds (run first; risks R1, R2)

From `D:\CRM\backend`: `dotnet build tests/Crm.SharedKernel.Tests`, `dotnet test --project tests/Crm.SharedKernel.Tests` (expect 3 passing), then
`dotnet build tests/Crm.BuildingBlocks.Tests`. If a running API locks `src/Crm.Api/bin`, do not build the solution; say so in the report.

#### 1.2 — **File: `backend/tests/Crm.BuildingBlocks.Tests/Crm.BuildingBlocks.Tests.csproj`**

Add a new `<ItemGroup>` with `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />` (version from `Directory.Packages.props`). If, after R3 is
checked, ASP.NET types (`DefaultHttpContext`, `IProblemDetailsService`, `AreaAttribute`, `ControllerModel`) do not resolve through the project references,
add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to **this** csproj only (not to `tests/Directory.Build.props`; `Crm.SharedKernel.Tests` must stay
free of it). Add no other package; if the compiler reports a missing transitive package (`Serilog`, `FluentValidation`, `MediatR`), add its `PackageReference`
(no `Version` attribute - central package management) and record it in Follow-ups.

#### 1.3 — **Create files** under `backend/tests/Crm.BuildingBlocks.Tests/Support/` (namespace `Crm.BuildingBlocks.Tests.Support`)

| File | Contents |
|------|----------|
| `Clock.cs` | `static class Clock { public static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero); public static IDateTime Fixed(DateTimeOffset? at = null) }` returning an `IDateTime` substitute. |
| `ScopeFixtures.cs` | Named constants `TenantA`, `TenantB`, `BranchA1`, `BranchA2`, `BranchB1`, `DeptA1`, `DeptA2`, `DeptB1` as `TenantId`/`BranchId`/`DepartmentId` (fixed GUIDs, plus raw `Guid` forms). |
| `Scopes.cs` | Factories building **real** `OrganisationScope`s: `Agent(params ScopeMembership[])`, `AgentIn(dept, branch, role)`, `Narrowed(memberships, activeDepartment)`, `Rejected(memberships)`, `Portal(tenant, customerId)` (via `OrganisationScope.ForPortalCustomer`). |
| `CurrentUsers.cs` | `ICurrentUserService` substitute factories `Agent(...)`, `Portal(...)`, `Anonymous()`, `Unrestricted()` whose `Scope`, `UserId`, `UserIdOrSystem`, `TenantId`, `DepartmentId`, `BranchId` are consistent with the real scope passed in. |
| `RecordingLogger.cs` | `RecordingLogger<T> : ILogger<T>` capturing `(LogLevel, EventId, string, Exception?)`; configurable `IsEnabled` minimum level (so `LoggingBehavior`'s Debug branch is testable). |
| `InMemoryCacheService.cs` | Hand-written `ICacheService` that really stores values by key (for caching-behaviour hit/miss tests). |
| `TestRequests.cs` | MediatR fixtures: `ReadQuery : IRequest<Result<int>>` `[RequirePermission(CrmPermissions.TicketsRead)]`; `WriteCommand : ICommand` `[RequirePermission(CrmPermissions.TicketsWrite)]`; `AssignCommand : ICommand` `[RequirePermission(CrmPermissions.TicketsAssign)]`; `ValueCommand : ICommand<int>`; `AuthenticatedOnlyQuery : IRequest<Result<int>>` `[RequireAuthenticatedUser]`; `AnonymousCommand : ICommand` `[AllowAnonymousRequest]`; `PortalTicketsQuery` `[RequirePermission(CrmPermissions.PortalTickets)]`; `NonResultRequest : IRequest<string>`; plus cacheable (`ICacheableQuery`) and invalidating (`ICacheInvalidatingCommand`) variants. The behaviours keep **per-closed-generic statics** (`AuthorizationBehavior.Requirement`, `CachingBehavior.HandleCachedMethod`), so each scenario gets its own request type. |
| `TestEvents.cs` | `public sealed record SampleIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc, string Payload) : IntegrationEvent(Id, OccurredOnUtc);` a second event record, `SampleDomainEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent`, and consumer fixtures (`SampleConsumer : IIntegrationEventConsumer<SampleIntegrationEvent>`, `FailingConsumer`, `PublishingConsumer`). |
| `Persistence/TestDbContext.cs` | `public sealed class TestDbContext : ModuleDbContext` (schema `"test"`) with `DbSet`s for the fixture entities; ctor `(DbContextOptions<TestDbContext>, ICurrentUserService)`; static `Create(string databaseName, ICurrentUserService user, IDateTime? clock = null, params IInterceptor[] extra)` building options with `UseInMemoryDatabase(databaseName)`, `ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))` (InMemory throws on `BeginTransaction` otherwise - R4) and `AddInterceptors(new AuditingSaveChangesInterceptor(user, clock))`. **No `IEntityTypeConfiguration` classes in the test assembly**: `ModuleDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly(GetType().Assembly)`, so any such class would apply to every test context; rely on conventions (`Id` key, `DbSet`s). Also `public interface ITestUnitOfWork : IUnitOfWork` implemented by `TestDbContext` (for `AddModuleDbContext` tests). |
| `Persistence/Fixtures.cs` | Entities with private parameterless ctors for EF: `TenantRow : AggregateRoot<Guid>, ITenantScoped`; `DepartmentRow : AuditableEntity<Guid>, IOrganisationScoped, ISoftDeletable, IHasRowVersion` (private setters, `Create(...)`, `Rename`); `TransferableRow : AggregateRoot<Guid>, ITransferableOrganisationScoped` with one `TransferTo(BranchId, DepartmentId)`; `OwnedRow : AggregateRoot<Guid>, IPortalCustomerOwned`; `UnscopedRow : AggregateRoot<Guid>, IHasRowVersion` (no scope marker); `EventfulRow : AggregateRoot<Guid>` raising a `SampleDomainEvent` from `Touch()`. |
| `Persistence/ThrowingInterceptor.cs` | `SaveChangesInterceptor` that throws `DbUpdateConcurrencyException` from `SavingChangesAsync` (InMemory never raises concurrency conflicts itself). |
| `Http/HttpTestHelpers.cs` | `FakeLease : RateLimitLease`, `StubHandler : HttpMessageHandler` capturing requests, `HttpContexts.Authenticated(claims, scheme)` / `WithHeader(name, value)` builders. |

Add a helper only when two test classes need it; otherwise inline it.

---

### Test classes to write, grouped in slices

Every slice ends with the named `dotnet test --project` run from `D:\CRM\backend`; it must pass (zero failures, zero skipped, zero warnings) before the next slice.
"≈N" is a rough test count so the implementer can tell a thin class from a complete one.

#### Slice 1 — `Crm.SharedKernel.Tests` (≈ 60 tests)

Read all of `backend/src/BuildingBlocks/Crm.SharedKernel/`. Files are **created under `backend/tests/Crm.SharedKernel.Tests/`**.

| File | Covers | Cases (all required) |
|---|---|---|
| `Results/ResultTests.cs` (extend; keep the three existing tests) | `Result` | success has `Error.None`; failure carries its error; `IsFailure` mirrors; `Result.Failure(Error.None)` throws; a `Result` subclass fixture calling `base(true, someError)` throws `InvalidOperationException`; `Success<T>`/`Failure<T>` return typed results. |
| `Results/ResultOfTTests.cs` | `Result<TValue>` | `Value` on success; `Value` on failure throws `InvalidOperationException` whose message contains `Error.Code`; implicit conversion from `TValue` is a success; reference type and `default` value successes. |
| `Results/ErrorTests.cs` | `Error`, `ErrorType` | `[Theory]` over the seven factories (`Failure`, `NotFound`, `Validation`, `Conflict`, `Forbidden`, `Domain`, `Unauthorized`): `Type`, `Code`, `Description`; `Error.None` is empty strings + `ErrorType.Failure`; value equality; `Error.None` equals `new Error("", "", ErrorType.Failure)`. |
| `EntityTests.cs` | `Entity<TId>` | same id + type equal; different id not equal; same id different concrete type not equal; `null` not equal; reference short-circuit; `==`/`!=` incl. null/null and null/instance; `GetHashCode` equal for equal entities and different across types with the same id; `Equals(object?)` with a non-entity. Two fixture entities in the test file. |
| `AggregateRootTests.cs` | `AggregateRoot<TId>` | new root has no events; protected `Raise` (via a fixture method) adds events in order; `DomainEvents` is a read-only view; `ClearDomainEvents` empties, twice is safe; implements `IHasDomainEvents`. |
| `AuditableEntityTests.cs` | `AuditableEntity<TId>` | all four audit properties null on a new instance; the property setters are **not public** (reflection: `GetSetMethod(false)` is null) - the "an audit field a handler can write can lie" rule; implements `IAuditable`; is an `AggregateRoot<TId>`. |
| `ValueObjectTests.cs` | `ValueObject` | equal components equal with equal hash; different components not equal; different types with equal components not equal; null components; order matters; `==`/`!=` incl. null; `Equals(object?)`; usable as dictionary key. |
| `StronglyTypedIdTests.cs` | `StronglyTypedId` | `ToString` is the guid string; same record type + same `Value` equal; **different record types with the same guid are not equal**; dictionary key; `with` creates a new id. |
| `DomainExceptionTests.cs` | `DomainException`, `DomainError` | `Message == error.Message`; `Error` is the instance passed; `DomainError` value equality; class is sealed and derives from `Exception`. |
| `IntegrationEventTests.cs` | `IntegrationEvent` | a concrete record exposes `Id`/`OccurredOnUtc`; value equality; `with` copy; implements `IIntegrationEvent`. |
| `Scoping/ScopeIdsTests.cs` | `TenantId`, `BranchId`, `DepartmentId` | `TenantId.None` is `Guid.Empty`; `BranchId.New()`/`DepartmentId.New()` give distinct non-empty values; `new TenantId(g) != new BranchId(g)` (as `StronglyTypedId` base values: not equal); base behaviour. |
| `Scoping/ScopeMarkersTests.cs` | `ITenantScoped`, `IOrganisationScoped`, `ITransferableOrganisationScoped`, `IPortalCustomerOwned` | interface hierarchy via reflection (`IOrganisationScoped : ITenantScoped`, `ITransferableOrganisationScoped : IOrganisationScoped`, `IPortalCustomerOwned : IOrganisationScoped` exposing `Guid CustomerId`) - the write guard and query filters depend on it. |

**Verify:** `dotnet test --project tests/Crm.SharedKernel.Tests`.

#### Slice 2 — `Crm.BuildingBlocks.Tests`: Application, non-pipeline (≈ 90 tests)

Folder `backend/tests/Crm.BuildingBlocks.Tests/Application/`. Read first: `Abstractions/OrganisationScope.cs`, `Authorization/*`, `Messaging/*`, `Modules/*`,
`Specifications/Specification.cs`, `Localization/*` + resx, `DependencyInjection.cs`.

| File | Covers | Cases |
|---|---|---|
| `Abstractions/OrganisationScopeTests.cs` | `OrganisationScope` | **No active scope:** `Departments`/`Branches` = distinct memberships ordered by `Value`; `Roles` distinct ordinal-sorted; `EffectiveRoles` = roles in effective departments. **Narrowed:** `Departments == [active]`, `Branches` = that department's branches only, `EffectiveRoles` only that department's roles while `Roles` lists all. **Rejected:** `Departments`, `Branches`, `EffectiveRoles` empty. `Unrestricted`: `IsUnrestricted`, `Allows(anything)` true. `None`: not unrestricted, `Allows(tenant row)` false. `Allows`: other tenant false; `ITenantScoped`-only same tenant true; `IOrganisationScoped` needs **both** branch and department in scope (4-case table); narrowed scope refuses a row of another own department; portal customer: own customer's `IPortalCustomerOwned` row true, another customer's false, plain `ITenantScoped` row of its tenant true, other tenant false; `null` throws. `ResolveWriteTarget`: requested department in scope → target with that membership's branch + tenant; requested outside scope → `null`; none requested + active department → it; none requested + exactly one department → it; none requested + several → `null`. `ForPortalCustomer`: `IsPortalCustomer`, roles `[CrmRoles.PortalCustomer]`, empty `Departments`/`Branches`, `ArgumentException` for `TenantId.None`, for an empty customer id, for an empty contact id, null args throw. Constants `HeaderName == "X-Crm-Scope"`, `AllDepartments == "all"`. |
| `Authorization/CrmPermissionsTests.cs` | `CrmPermissions` | `Staff` has the eight staff permissions; `All == Staff + [PortalAccount, PortalTickets]`; `ForRoles`: `Agent` = tickets.read/write + customers.read/write (no `tickets.assign`, no admin); `Supervisor` = Agent + `tickets.assign`; `Administrator` = all `Staff`; `PortalCustomer` = both portal permissions and **no** staff permission; union across roles distinct and ordinal-sorted; unknown role contributes nothing; empty → empty; `IsKnown` true for every member of `All`, false for `""`, wrong case, unknown. |
| `Authorization/RequestAuthorizationGuardTests.cs` | `For`, `EnsureEveryRequestDeclares` | `For`: `[AllowAnonymousRequest]` → `(true, null)`; `[RequirePermission(x)]` → `(false, x)`; `[RequireAuthenticatedUser]` or no attribute → `(false, null)`; attributes are **not inherited** (derived request type). `EnsureEveryRequestDeclares([typeof(CrmCultures).Assembly])` (the Application assembly declares no requests) does not throw; failing scenarios use `System.Reflection.Emit` dynamic assemblies (`AssemblyBuilder.DefineDynamicAssembly`, R6) with one offending type each: no attribute; two attributes; unknown permission; non-`Result` response; a good `ICommand`/`ICommand<T>` passes; abstract/interface/open-generic ignored; the message names the type and lists every problem; null throws. **Never pass the test assembly** (it contains deliberately undeclared fixtures). |
| `Specifications/SpecificationTests.cs` | `Specification<T>` | fixture subclass exposing each protected method: defaults (`Criteria` null, `Includes` empty, flags false, `Skip/Take` null); `Where`, `AddInclude` (accumulates), `ApplyOrderBy`, `ApplyOrderByDescending`, `ApplyThenBy`, `ApplyPaging`, `AsReadOnly`, `IncludeSoftDeleted`, `IgnoreOrganisationScope` set the matching property; `Where` twice replaces. |
| `Messaging/OutboxEventBusTests.cs` | `OutboxEventBus` | `Publish` → `HasPending`; `DequeueAll` returns events in order and empties; second `DequeueAll` empty; the returned list is a copy; `Publish(null)` throws; one instance serves `IEventBus` and `IPendingIntegrationEvents`. |
| `Messaging/IntegrationEventConsumerRegistryTests.cs` | `IntegrationEventConsumerRegistry` | `Scan` finds the `SampleConsumer` fixture; `TryGet(typeof(SampleIntegrationEvent).FullName)` returns its `EventType` and handler; two consumers of one event both listed; a class implementing two event interfaces appears under both; abstract classes/interfaces skipped; unknown name → `false`; `HandlerTypes` is the distinct union; lookup is ordinal; `IIntegrationEventConsumer.HandleAsync(IIntegrationEvent, …)` default interface method casts and forwards (wrong event type throws `InvalidCastException`). |
| `Messaging/DomainEventNotificationTests.cs` | `DomainEventNotification<T>` | wraps the event; record equality; is an `INotification`. |
| `Modules/UnitOfWorkResolverTests.cs` | `UnitOfWorkResolver`, `ModuleUnitOfWork` | request from a registered module assembly resolves the unit of work from the provider; unregistered assembly → `InvalidOperationException` naming type and assembly; two modules for the same assembly → `InvalidOperationException` (`SingleOrDefault`); resolved instance is the provider's (scoped per scope). |
| `Localization/ResourceLocalizationServiceTests.cs` | `ResourceLocalizationService`, `CrmCultures` | with the real resource manager (`new ResourceManager("Crm.BuildingBlocks.Application.Localization.Resources.CrmMessages", typeof(CrmCultures).Assembly)`): `Get(CrmMessageKeys.ProblemTitle.Validation)` under UI culture `en` and `ar` returns different non-key strings (set `CultureInfo.CurrentUICulture` inside the test, restore in `finally`); unknown key returns the key; first resource that has the key wins (two `LocalizationResource`s); `CurrentLanguage` is `"ar"` for `ar`/`ar-SA`, `"en"` for `en`, `fr`, invariant; `CrmCultures.All == [en, ar]`, `Default == en`. |
| `Localization/CrmMessageKeysResourceParityTests.cs` | `CrmMessageKeys` ↔ resx | reflect over nested classes `ProblemTitle` and `ProblemDetail`, collect every `const string`; each key resolves to a **non-empty, non-key** value under both `en` and `ar` (a key added without both resx entries fails here). No Arabic literal in test source. |
| `Exceptions/ApplicationExceptionsTests.cs` | `ConcurrencyConflictException`, `OrganisationScopeViolationException` | inner exception and fixed message kept; scope violation exposes `EntityType`, `Operation` and a message containing both. |
| `Caching/CacheContractsTests.cs` | `ICacheableQuery` defaults, `CachingSettings` | `CacheDuration` default null, `VaryByUser` default false (fixture implementing only required members); `CachingSettings.Default.DefaultExpiration == 5 minutes`; record equality. |
| `DependencyInjectionTests.cs` | `AddBuildingBlocksApplication` | called with `[typeof(CrmCultures).Assembly]`: open-generic `IPipelineBehavior<,>` registrations appear **in the documented order** (validation, authorisation, organisation scope, logging, transaction, caching); `IUnitOfWorkResolver` scoped; `ILocalizationService` singleton resolves; `IEventBus` and `IPendingIntegrationEvents` resolve to the **same** `OutboxEventBus` within a scope and different instances across scopes; `IntegrationEventConsumerRegistry` singleton registered; an assembly with an undeclared request (dynamic assembly) makes the call throw. |

**Verify:** `dotnet test --project tests/Crm.BuildingBlocks.Tests`.

#### Slice 3 — `Crm.BuildingBlocks.Tests`: pipeline behaviours (≈ 85 tests)

Folder `Application/Behaviors/`. Read first: the six `Behaviors/*.cs`, `Messaging/IEventBus.cs`, `Caching/ICacheableQuery.cs`. Invoke each behaviour directly:
`await behaviour.Handle(request, () => Task.FromResult(response), TestContext.Current.CancellationToken)` (`RequestHandlerDelegate<T>` is a plain delegate).

| File | Covers | Cases |
|---|---|---|
| `ValidationBehaviorTests.cs` | `ValidationBehavior<,>` | no validators → `next` called once; all valid → passthrough; one failing validator, `Result` response → `Error.Validation("Validation.Failed", "Prop: message")`, **`next` not called**; several failures joined with `" | "` in order (FluentValidation `InlineValidator<T>`); `Result<T>` response gets a typed failure; non-`Result` response (`NonResultRequest`) throws FluentValidation `ValidationException` with all failures; the cancellation token reaches the validators. |
| `AuthorizationBehaviorTests.cs` | `AuthorizationBehavior<,>` | `[AllowAnonymousRequest]` → `next` with no user; unrestricted scope → `next` without permission; no `UserId` → `Unauthorized "Authorization.Unauthenticated"`, `next` not called; signed-in **portal** principal on a `[RequireAuthenticatedUser]` request → `Forbidden "Authorization.StaffOnly"`; staff on it → `next`; permission present through a role → `next`; missing (Agent on `tickets.assign`) → `Forbidden "Authorization.PermissionDenied"` naming the permission; permission judged on `EffectiveRoles` **only** (Agent in the active department but Administrator elsewhere does not get admin permissions); portal customer passes a `portal.tickets` request and fails a staff-permission one; `Result<T>` response → typed failure; `next` never invoked on denial; log levels via `RecordingLogger` (`Information` unauthenticated, `Warning` staff-only/denied). |
| `OrganisationScopeBehaviorTests.cs` | `OrganisationScopeBehavior<,>` | passthrough; `OrganisationScopeViolationException` from `next` with a `Result` response → `Forbidden "Organisation.OutOfScope"` + `Warning` log; typed for `Result<T>`; **non-`Result` response: exception propagates** (the `when` filter); other exceptions propagate. |
| `LoggingBehaviorTests.cs` | `LoggingBehavior<,>` | success: `Information` "Handling"/"Handled" lines; metric `crm.request.duration` recorded with `crm.outcome=success` and `crm.request.kind` `command` (`ICommandMarker`) vs `query` (use `System.Diagnostics.Metrics.MeterListener` on `CrmDiagnostics.Meter`, filter by the unique request name for parallel safety); failed `Result` → `Warning` with the error code, outcome `failure`, activity status `Error` + `crm.error.code` tag (use an `ActivityListener` on `CrmDiagnostics.SourceName`); exception → rethrown, outcome `exception`, `Warning`, activity has the exception event; the Debug payload line only when `IsEnabled(Debug)` (two `RecordingLogger` settings); activity tags `crm.request.name`/`crm.request.kind`. Dispose listeners in `finally`. |
| `TransactionBehaviorTests.cs` | `TransactionBehavior<,>` | **Query:** no transaction (`IUnitOfWorkResolver.ForRequest` not called), passthrough; a query that leaves a pending integration event → `InvalidOperationException` ("a query") and the queue is drained. **Command success:** call order on the unit of work `BeginTransactionAsync` → handler → `GetAndClearDomainEvents` → `CommitTransactionAsync` → cache invalidation → `DispatchAsync` (`Received.InOrder`); no integration events → no `AddToOutbox`, no pre-commit `SaveChangesAsync`; with events → `AddToOutbox(events, correlationId)` then `SaveChangesAsync` **before** commit, same instances, correlation id from `ICorrelationIdAccessor`; **failed `Result`** → `RollbackTransactionAsync`, no commit, pending events discarded, no dispatch, no invalidation; `ICacheInvalidatingCommand` → `InvalidateTagsAsync(tags)` after commit; non-invalidating command leaves the cache alone; `ConcurrencyConflictException` → rollback, events dropped, `Error.Conflict("Persistence.ConcurrencyConflict")` (typed for `Result<T>`), warning logged; `OrganisationScopeViolationException` → rollback, events dropped, **rethrown**; an integration event published from a domain-event handler (simulate: `IDomainEventDispatcher` substitute calls `bus.Publish`) → `InvalidOperationException` mentioning "domain-event handler" and the event is dropped; any other exception from the handler propagates (record whether rollback is called - pin current behaviour only if the code comments show it is intended, else Follow-up). Use a real `OutboxEventBus` for `IPendingIntegrationEvents`, substitutes for the rest. |
| `CachingBehaviorTests.cs` | `CachingBehavior<,>` | non-cacheable request → `next`, cache untouched; cacheable request with a non-`Result<T>` response → `InvalidOperationException` naming both types; **miss:** `next` runs, success stored as `CachedValue<T>` and returned as `Result.Success(value)`; **hit:** second call does not run `next`; **failure never cached** (original failure returned; a later call runs `next` again); key `t={tenant}|d={department}|b={branch}:{CacheKey}` with `-` for nulls (capture the key argument with an NSubstitute `ICacheService`); `VaryByUser` appends `|u={UserIdOrSystem}` and otherwise does not; two tenants → different keys; `CacheDuration` override else `CachingSettings.DefaultExpiration`; `CacheTags` passed in `CacheEntrySettings`; a cache that returns `null` with no failure captured → `InvalidOperationException`; handler exception propagates. Use `InMemoryCacheService` for hit/miss flows and an NSubstitute `ICacheService` for argument capture. |

**Verify:** `dotnet test --project tests/Crm.BuildingBlocks.Tests`.

#### Slice 4 — `Crm.BuildingBlocks.Tests`: Infrastructure without a database (≈ 85 tests)

Folder `Infrastructure/`. Read first: `Identity/*`, `Correlation/*`, `Logging/*`, `Caching/DistributedCacheService.cs`, `Http/*`, `Time/*`, `DependencyInjection.cs`,
`Observability/ObservabilityServiceCollectionExtensions.cs`.

| File | Covers | Cases |
|---|---|---|
| `Identity/CurrentUserServiceTests.cs` | `CurrentUserService` | no `HttpContext` → `Scope == OrganisationScope.Unrestricted`, `UserId` null, `UserIdOrSystem == "system"`, ids null; unauthenticated → `OrganisationScope.None`; authenticated staff with `tenant_id` + `membership` claims `"{dept}|{branch}|{role}"` → memberships parsed, `TenantId` string, `DepartmentId`/`BranchId` comma-joined effective ids; **malformed membership claims skipped, never thrown** (wrong part count, bad guid); missing/garbage `tenant_id` → `None`; `X-Crm-Scope`: absent/empty/`"all"` (any case) → no narrowing; a department the user belongs to → narrowed; a department the user does **not** belong to → `ActiveScopeRejected`, `Departments` empty (never widened); non-guid → rejected; **portal** principal (`AuthenticationType == CrmAuthenticationSchemes.Portal`) with the five portal claims → `IsPortalCustomer` + grant fields; any portal claim missing/malformed/empty-guid or tenant `None` → `None`; a principal with both a staff and a portal authenticated identity → `None`; `UserId`/`SessionId` from `sub`/`sid` only when authenticated; scope cached per `HttpContext.User` reference and re-resolved when `User` is replaced. `DefaultHttpContext` + `ClaimsIdentity(claims, authenticationType)` + substitute `IHttpContextAccessor`. |
| `Identity/SystemCurrentUserTests.cs` | `SystemCurrentUser` | singleton `Instance`; `UserIdOrSystem == "system"`; `Scope == OrganisationScope.Unrestricted`; ids null. |
| `Correlation/CorrelationIdAccessorTests.cs` | `CorrelationIdAccessor` | none → null; `HttpContext.Items[CorrelationIdMiddleware.ItemKey]` returned; `BeginScope` overrides and **restores** on dispose; nested scopes restore in order; override flows across `await`; separate async flows do not leak. |
| `Correlation/CorrelationIdMiddlewareTests.cs` | `CorrelationIdMiddleware` | acceptable header (`[A-Za-z0-9._:-]{1,64}`) kept; absent → generated (the `Activity.Current` trace id when one exists, else 32-hex guid); 65 chars, empty, illegal characters (space, `/`, newline, non-ASCII) → replaced; stored in `Items[ItemKey]`; `Activity.Current` tagged `crm.correlation_id`; response header `X-Correlation-Id` written when the response starts (a custom `IHttpResponseFeature` that records `OnStarting` callbacks and the test invokes them); `next` called. |
| `Correlation/CorrelationIdDelegatingHandlerTests.cs` | `CorrelationIdDelegatingHandler` | adds `X-Correlation-Id` when the accessor has one; leaves an explicit header; adds nothing when null; null request throws (`HttpMessageInvoker` over `new CorrelationIdDelegatingHandler(accessor) { InnerHandler = new StubHandler() }`). |
| `Logging/SensitiveDataDestructuringPolicyTests.cs` | `SensitiveDataDestructuringPolicy` (+ `SensitiveData`) | local Serilog `Logger` with `.Destructure.With<SensitiveDataDestructuringPolicy>()` and a delegate sink: `{@X}` of an object with `Password`, `RefreshToken`, `ApiKey`, `ClientSecret`, `Authorization`, `ConnectionString`, `Credentials`, `pass_word`, `API-KEY` → `"***"` (separator/case insensitive); other properties preserved; nested object redacted; primitives, strings, enums and `System.*` types return `false`; null args throw; indexers skipped; `TypeTag` is the type name. Never assign `Log.Logger`. |
| `Logging/SensitivePropertyRedactionEnricherTests.cs` | `SensitivePropertyRedactionEnricher` | properties named `Password`/`apiKey`/`Bearer-Token` → `"***"`; others untouched; no properties safe; null args throw. |
| `Logging/CrmLogEnricherTests.cs` | `CrmLogEnricher` | correlation id added when present, none when absent; unauthenticated → only correlation; authenticated → `UserId` from `sub`; `TenantId`/`DepartmentId`/`BranchId` from an `ICurrentUserService` in `RequestServices`; none registered → only `UserId`; an existing `CorrelationId` property is not overwritten; null args throw. |
| `Time/SystemDateTimeTests.cs` | `SystemDateTime` | `UtcNow.Offset == TimeSpan.Zero`; `UtcNow > DateTimeOffset.UnixEpoch`; two reads non-decreasing. |
| `Caching/DistributedCacheServiceTests.cs` | `DistributedCacheService` | backed by `new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))`: miss → factory once, value stored; second call hit (factory not run); `null` factory result not stored; after `InvalidateTagsAsync(["t"])` the entry is stale (factory runs again); an unrelated tag keeps the hit; an entry with several tags is stale if any one is invalidated; `RemoveAsync` forces a miss; an NSubstitute `IDistributedCache` wrapper proves `SetAsync` receives `AbsoluteExpirationRelativeToNow == settings.Expiration`; **outage degradation:** `GetAsync` throws → `GetOrCreateAsync` still returns the factory value, and the **next** call does not touch the cache (`DidNotReceive` - the 30 s bypass uses `Environment.TickCount64`; no sleeping); failed `SetAsync` degrades the same way; `InvalidateTagsAsync` against a throwing cache does not throw and later invalidations are skipped while bypassed; a cancelled token is **not** swallowed; null args throw; log lines via `RecordingLogger` (`Warning` outage, `Error` failed invalidation). One service instance per test (bypass state is per instance). |
| `Http/HttpClientBuilderExtensionsTests.cs` | `AddCrmResilience` | `ServiceCollection` + `AddLogging()` + `AddTransient<CorrelationIdDelegatingHandler>()` + substitute `ICorrelationIdAccessor` + in-memory configuration: `AddHttpClient("x").AddCrmResilience(config)` does not throw and the client's pipeline stamps `X-Correlation-Id` (primary handler replaced with `StubHandler` via `ConfigurePrimaryHttpMessageHandler`); null args throw. Anything needing a real network is **not tested** (R7). |
| `DependencyInjectionTests.cs` | `AddBuildingBlocksInfrastructure`, `UsesInProcessCache` | with in-memory configuration `ConnectionStrings:Crm` and a substitute `IHostEnvironment`: `IDateTime` → `SystemDateTime`; `PersistenceSettings(EnableSensitiveDataLogging)` true only in `Development`; `ICurrentUserService` scoped; `OutboxSettings` from the `Outbox` section else `OutboxSettings.Default`; `CachingSettings` from `Caching` else default; `ICacheService` → `DistributedCacheService`; a hosted service of type `OutboxDispatcher` registered; **no `ConnectionStrings:Crm` → `InvalidOperationException`**; no Redis string → `UsesInProcessCache` true and `IDistributedCache` in-memory; with it → false (do not resolve the Redis cache or connect to SQL Server). |
| `Observability/ObservabilityServiceCollectionExtensionsTests.cs` | `AddCrmObservability` | **Decide after reading the file (R12):** if it only registers Serilog/OpenTelemetry from configuration, assert it can be called with minimal configuration and registers the expected services without starting exporters; if it needs a host builder or exports, test only pure option binding or list the type as "not unit-testable here". |

**Verify:** `dotnet test --project tests/Crm.BuildingBlocks.Tests`.

#### Slice 5 — `Crm.BuildingBlocks.Tests`: persistence and outbox (≈ 100 tests)

Folders `Infrastructure/Persistence/` and `Infrastructure/Outbox/`. Read first: `ModuleDbContext*.cs`, `AuditingSaveChangesInterceptor.cs`, `Repository.cs`,
`SpecificationEvaluator.cs`, `ModelBuilderExtensions.cs`, `Outbox/*.cs`, `Modules/ModuleServiceCollectionExtensions.cs`. Uses `Support/Persistence/TestDbContext`.
Two requests over one store = two contexts with the same database name and different `ICurrentUserService` substitutes. Seed through an `Unrestricted` user.

| File | Covers | Cases |
|---|---|---|
| `Persistence/ModuleDbContextScopeFilterTests.cs` | the named `OrganisationScope` filter + `SoftDelete` filter | agent in A1 sees only A1 rows of tenant A (tenant B hidden; other department hidden; other branch hidden); two departments see both; narrowed sees one; rejected sees none; `None` sees none; `Unrestricted` sees all; a `TenantRow` is visible to every department of its tenant, not to another tenant; `UnscopedRow` visible to everyone; **portal customer** sees its own customer's `OwnedRow` and `TenantRow`s of its tenant, never another customer's or tenant's; soft-deleted row hidden by default, visible with `IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName])` **while the scope filter still applies**; `IgnoreQueryFilters([ModuleDbContext.ScopeFilterName])` lifts only scope, not soft delete; the constants are `"OrganisationScope"` and `"SoftDelete"`. (R5) |
| `Persistence/ModuleDbContextWriteGuardTests.cs` | `EnsureWritesInScope` via `SaveChangesAsync` | add in scope saves; add in another department/branch/tenant → `OrganisationScopeViolationException(<type name>, "Write")`, **nothing persisted**; modify in scope saves; modify so it leaves scope (non-transferable) throws; unrestricted bypasses; `None` refuses any scoped write; unscoped entities ignored; **transfer:** a modified `TransferableRow` moved by `TransferTo` from the caller's department to another department of the same tenant saves; by a caller who did not own the original department throws; to another **tenant** throws; an **added** transferable row is judged like any other; portal customer writing an `OwnedRow` for its own customer allowed, for another customer refused; `Deleted` entries are not guarded (pin current behaviour). |
| `Persistence/ModuleDbContextUnitOfWorkTests.cs` | `ModuleDbContext` as `IUnitOfWork` | `GetAndClearDomainEvents` returns events of all tracked `IHasDomainEvents` entities and clears them (second call empty); `AddToOutbox` adds one `OutboxMessage` per event (`Type` = event full name, `CorrelationId`, `TraceParent` = `Activity.Current?.Id` with an `ActivityListener`-started activity, `Id` = event id), persisted by `SaveChangesAsync`; null throws; `ThrowingInterceptor` → `SaveChangesAsync` throws `ConcurrencyConflictException` with the original inner exception; Begin/Commit/Rollback complete under the InMemory transaction-ignored setup (R4) and Commit/Rollback with no transaction are no-ops; the model has `outbox_messages` and `outbox_message_consumers` in schema `"test"`; `DateTimeOffset` round-trips as UTC. |
| `Persistence/AuditingSaveChangesInterceptorTests.cs` | `AuditingSaveChangesInterceptor` | added `AuditableEntity` gets `CreatedBy` = `UserIdOrSystem` and `CreatedAtUtc` = the substituted clock; modified gets `ModifiedBy/At` and keeps `CreatedBy/At`; unchanged untouched; `"system"` for no user; **soft delete:** `Remove` of an `ISoftDeletable` becomes `IsDeleted = true` + `DeletedAtUtc` + `DeletedBy` and the row still exists in the store (`IgnoreQueryFilters`); a non-soft-deletable entity is physically deleted; whether `ModifiedAtUtc` is also set on a soft-deleted row (soft delete runs first, entry becomes `Unchanged`) - run it, pin the observed behaviour with a comment, Follow-up if surprising. |
| `Persistence/ModelBuilderExtensionsTests.cs` | `ApplySoftDeleteQueryFilter`, `ApplyUtcDateTimeOffsetConverter` | from `TestDbContext.Model`: every `ISoftDeletable` entity has the `SoftDelete` filter, others none; owned types skipped (add one owned fixture); every `DateTimeOffset`/`DateTimeOffset?` property has a value converter and a `+03:00` value converts to UTC. |
| `Persistence/RepositoryTests.cs` | `Repository<TAggregate,TId,TContext>` (derive `Repository<DepartmentRow, Guid, TestDbContext>`) | `GetByIdAsync`: in scope returns; missing id `null`; **exists outside scope → `OrganisationScopeViolationException("DepartmentRow","Read")`** (never a silent `null`); soft-deleted id → `null`; `FirstOrDefaultAsync(spec)` same three outcomes; `ListAsync` applies criteria, ordering, paging and scope; `CountAsync` same filters; `Add` + save persists, `Remove` soft-deletes; `SetOriginalRowVersion` writes `OriginalValue` of `RowVersion`; a non-tenant-scoped aggregate (`UnscopedRow`) with a missing id → `null`, never throws. |
| `Persistence/SpecificationEvaluatorTests.cs` | `SpecificationEvaluator.GetQuery` | criteria filters; `OrderBy` + `ThenBy`; `OrderByDescending` + `ThenBy`; `OrderBy` wins when both set; `Skip`/`Take` after ordering; `AsReadOnly` → untracked; default tracked; `IncludeSoftDeleted` shows deleted rows but keeps scope; `IgnoreOrganisationScope` shows other departments but still hides deleted; `Includes` assertion only if a navigation fixture exists (otherwise assert empty); an empty specification leaves the query unchanged. |
| `Persistence/DomainEventDispatcherTests.cs` | `DomainEventDispatcher` | each event published as `DomainEventNotification<TRuntimeType>` in order (substitute `IPublisher`, inspect `GetType().GetGenericArguments()[0]`); token forwarded; empty → no publish; publisher exception propagates and later events are not published. |
| `Modules/ModuleServiceCollectionExtensionsTests.cs` | `AddModuleDbContext`, `AddRepository` | `AddRepository<DepartmentRow, Guid, TestDbContext>()` registers `IRepository<DepartmentRow, Guid>` scoped → `Repository<…>` (descriptor assertions, no provider build); `AddModuleDbContext<TestDbContext, ITestUnitOfWork>` with `ConnectionStrings:Crm` registers the context, a scoped `ITestUnitOfWork` forwarding to it, a singleton `ModuleUnitOfWork(moduleName, assembly, typeof(ITestUnitOfWork))` and a singleton `ModuleDatabase(moduleName, schema, typeof(TestDbContext))`; **missing `ConnectionStrings:Crm` → `InvalidOperationException`**. Do not resolve the context (it is configured for SQL Server). |
| `Outbox/OutboxMessageTests.cs` | `OutboxMessage`, `OutboxMessageConsumer` | `From` copies id/occurred-on, `Type` = event `FullName`, `Content` is camelCase JSON (`"payload"`), stores correlation id and trace parent, `ProcessedAtUtc` null, `Attempts` 0; null event throws; `MarkProcessed` sets `ProcessedAtUtc` and clears `NextAttemptAtUtc`; `RecordFailure` increments `Attempts`, stores `LastError`, returns `false` below `maxAttempts`, schedules `NextAttemptAtUtc = now + 2^attempts` seconds (2, 4, 8, 16…), **caps at 300 s**, returns `true` at `maxAttempts` and clears `NextAttemptAtUtc` (dead letter); an error longer than `MaxErrorLength` (2000) is truncated to exactly 2000; `maxAttempts == 1` dead-letters on the first failure; null error throws; `OutboxMessageConsumer` stores its values. |
| `Outbox/OutboxConfigurationsTests.cs` | `OutboxMessageConfiguration`, `OutboxMessageConsumerConfiguration` | from `TestDbContext.Model`: table names; `Type` max 500 + required, `Content` required, `CorrelationId` 64, `TraceParent` 55, `LastError` 2000; composite key `(MessageId, Handler)` and handler max 300; `Id` is `ValueGeneratedNever`. |
| `Outbox/OutboxSettingsTests.cs` | `OutboxSettings` | `Default` = 5 s, batch 20, max attempts 5; record equality. |
| `Outbox/OutboxDispatcherTests.cs` | `OutboxDispatcher` | **Harness** (`Support/OutboxHarness.cs`): `ServiceCollection` with `AddLogging`, scoped `TestDbContext` over one fixed in-memory database name, singleton `ModuleDatabase("Test","test",typeof(TestDbContext))`, singleton `ModuleUnitOfWork("Test", typeof(SampleConsumer).Assembly, typeof(TestDbContext))`, scoped `IUnitOfWorkResolver → UnitOfWorkResolver`, `OutboxEventBus` as `IEventBus`/`IPendingIntegrationEvents`, substitute `IDomainEventDispatcher`, real `CorrelationIdAccessor` over a substitute `IHttpContextAccessor`, `IntegrationEventConsumerRegistry.Scan([assembly])`, consumers registered scoped, `OutboxSettings(10 ms, batch, maxAttempts)`, substitute `IDateTime`. Drive with `await dispatcher.StartAsync(token)`, poll the store (`await Task.Delay(10 ms)`, bounded by a 5 s `CancellationTokenSource`) until the expected state, `StopAsync`, then assert with a fresh `Unrestricted` context. **Cases:** due message + one subscriber → handler called once with the deserialised event, `ProcessedAtUtc` set, one consumer row; **idempotency** (existing consumer row for `(messageId, handlerName)`) → handler not called, message still processed; no subscriber → processed without handler; invalid JSON → `Attempts` 1, `LastError` set, not processed; handler throws → `Attempts` 1, `NextAttemptAtUtc = now + 2 s`, `LastError` has handler name + message, **no consumer row**; failures up to `MaxAttempts` → dead-lettered (`NextAttemptAtUtc` null) and no longer picked up; not-yet-due message skipped; processed message skipped; two consumers, one failing → message not processed, the successful consumer's row **is** recorded; a consumer publishing an onward event → a new `OutboxMessage` row with the dispatching correlation id; domain events raised by a consumer dispatched after commit; the handler sees `ICorrelationIdAccessor.CorrelationId == message.CorrelationId` (or the message id in `"N"` format when null); oldest-first and at most `BatchSize` per cycle; a failing cycle is logged and the loop continues; cancellation stops the loop. InMemory has no real transactions: do **not** assert rollback of data (R4). |

**Verify:** `dotnet test --project tests/Crm.BuildingBlocks.Tests`.

#### Slice 6 — `Crm.BuildingBlocks.Tests`: Presentation (≈ 75 tests)

Folder `Presentation/`. Read first: all of `Crm.BuildingBlocks.Presentation/`.

| File | Covers | Cases |
|---|---|---|
| `ErrorHandling/ProblemTypesTests.cs` | `ProblemTypes` | all twelve constants are the **exact** `urn:crm:problem:*` strings (guard: never rename - the frontend maps them); `ForStatus`: 400/401/403/404/405/409/415/422/429/503 → their types, other `>= 500` → `Internal`, 200/302/418 → `BadRequest`; `TitleFor` asks the localiser for the matching `CrmMessageKeys.ProblemTitle.*` key (substitute echoing the key), unknown type → `ProblemTitle.Internal`; null service throws. |
| `ErrorHandling/ErrorTypeMappingTests.cs` | `ErrorTypeMapping` | `[Theory]` over all seven `ErrorType`s for `ToStatusCode` (400/404/409/403/422/401/500) and `ToProblemType`; an out-of-range value falls to 500/`Internal`. |
| `ErrorHandling/ValidationErrorParserTests.cs` | `ValidationErrorParser.Parse` | `"A: m1 | B: m2"` → two keys; repeated field groups messages in order; entry without colon → field = message = whole entry; `"A: x: y"` → `"x: y"`; whitespace trimmed; empty → empty; consecutive separators ignored. |
| `ErrorHandling/GlobalExceptionHandlerTests.cs` | `GlobalExceptionHandler.TryHandleAsync` | capture the `ProblemDetailsContext` passed to a substitute `IProblemDetailsService` (returns true), `DefaultHttpContext`: `DomainException` → 422 `domain-rule`, `detail == error.Message`, `code` extension; FluentValidation `ValidationException` → 400 `ValidationProblemDetails` grouped by property, type `validation`, detail from `CrmMessageKeys.ProblemDetail.Validation`; `ConcurrencyConflictException` → 409 `conflict`, code `Persistence.ConcurrencyConflict`; `OrganisationScopeViolationException` → 403 `forbidden`, code `Organisation.OutOfScope`; `BadHttpRequestException(…, 415)` → that status and `ProblemTypes.ForStatus`; `HttpRequestException` and a Polly rejection exception (`TimeoutRejectedException`/`BrokenCircuitException` - confirm the public constructor when compiling) → 503 `dependency-unavailable`; any other exception → 500 `internal` with **no exception text** in the problem (detail is the localised "unexpected" text); `OperationCanceledException` with `RequestAborted` cancelled → returns `false`, nothing written; with a live request → 500; `Response.StatusCode` set to the problem status; return value is `TryWriteAsync`'s; log levels (`Information` domain/validation/unreadable, `Warning` concurrency/scope/dependency, `Error` unexpected). |
| `ErrorHandling/ErrorHandlingServiceCollectionExtensionsTests.cs` | `AddCrmErrorHandling` | provider with `AddLogging`, `AddCrmErrorHandling`, substitute `ILocalizationService` and `ICorrelationIdAccessor`; invoke `ProblemDetailsOptions.CustomizeProblemDetails` (from `IOptions<ProblemDetailsOptions>`) on a `ProblemDetailsContext`: no type + status 404 → `urn:crm:problem:not-found` + localised title; framework default `https://tools.ietf.org/...` type replaced; `ValidationProblemDetails` without type → `urn:crm:problem:validation`; an existing `urn:crm:problem:*` type left alone; `Instance` = request path when null; `correlationId` extension only when the accessor returns one; `IExceptionHandler` registered as `GlobalExceptionHandler`. (R8: prefer the delegate; use `TryWriteAsync` to a `MemoryStream` only if it works.) |
| `ApiControllerBaseTests.cs` | `ApiControllerBase` | `public sealed class TestController : ApiControllerBase` exposing the protected members; `ControllerContext` with `DefaultHttpContext` whose `RequestServices` hold a real `DefaultProblemDetailsFactory` (needs `IOptions<ApiBehaviorOptions>` + `IOptions<ProblemDetailsOptions>`), substitute `ILocalizationService`, substitute `ISender`; `ToActionResult(Result<T>)`: success → `OkObjectResult`, failure → `ObjectResult` with `ProblemDetails`; `ToActionResult(Result)`: success → `NoContentResult`; `[Theory]` over the seven `ErrorType`s for status + `type`; `Validation` → `BadRequestObjectResult` with `ValidationProblemDetails` whose `Errors` come from `ValidationErrorParser`, `Extensions["code"] == error.Code`, `detail == error.Description`; other problems carry `Extensions["code"]`; `Sender` resolved once from `RequestServices`; `Problem(null)` throws; class carries `[ApiController]` and `[Route("api/[area]/[controller]")]` (reflection guard for the frontend-facing route template). |
| `Authorization/CrmPoliciesTests.cs` | `CrmPolicies` | constants `"Staff"`, `"Portal"`; provider with `AddLogging()` + `AddCrmAuthorization()`: both policies list both schemes and require an authenticated user; `IAuthorizationService.AuthorizeAsync`: staff-identity principal → Staff ok, Portal **fails**; portal-identity principal → Portal ok, Staff **fails** (403 both ways); anonymous fails both; both identities satisfy both; scheme names compared ordinally (`"staff"` lower-case does not match). |
| `Authorization/StaffAreaAuthorizationConventionTests.cs` | `StaffAreaAuthorizationConvention` | `new ControllerModel(typeof(FixtureController).GetTypeInfo(), [])` with `RouteValues["area"]` `Agent`/`Admin` → every selector of every action gets `AuthorizeAttribute { Policy == "Staff" }` in `EndpointMetadata`; `Portal`/`Public`/no area untouched; several actions/selectors covered; ordinal comparison; null throws. |
| `Authorization/PortalAreaAuthorizationConventionTests.cs` | `PortalAreaAuthorizationConvention` | same shape: `Portal` → `Policy == "Portal"`; Agent/Admin/Public untouched. |
| `CrmAreasTests.cs` | `CrmAreas` | the four constants; `All` is exactly `[Agent, Portal, Admin, Public]` in that order. |
| `Localization/RequestLocalizationExtensionsTests.cs` | `UseCrmRequestLocalization` | `new ApplicationBuilder(services)` + `.UseCrmRequestLocalization()` + terminal middleware capturing `CurrentUICulture`; run the built `RequestDelegate` over `DefaultHttpContext`: `Accept-Language: ar` → `ar`; `en` → `en`; `fr`/absent → default `en`; observe and pin what `ar-SA` does (R9); `Content-Language` response header set. |
| `Scoping/OrganisationScopeMiddlewareTests.cs` | `OrganisationScopeMiddleware`, `UseCrmOrganisationScope` | not rejected → `next` called, nothing written; rejected → `next` **not** called, 403, problem via substitute `IProblemDetailsService` with `type == ProblemTypes.Forbidden`, localised title, detail from `CrmMessageKeys.ProblemDetail.ScopeRejected`, `code == "Organisation.ActiveScopeRejected"`; a `Warning` log naming the header and user; `UseCrmOrganisationScope` registers the middleware. |
| `RateLimiting/RateLimitingServiceCollectionExtensionsTests.cs` | `AddCrmRateLimiting` | each of the four sections (`RateLimiting:Public`, `:SignIn`, `:Portal`, `:PortalSignIn`) missing → `InvalidOperationException` naming it (`[Theory]`); with all four (`PermitLimit`, `Window` `"00:01:00"`) resolve `IOptions<RateLimiterOptions>`: `RejectionStatusCode == 429`; `GlobalLimiter` for a context with endpoint metadata `new AreaAttribute("Public")` (`DefaultHttpContext.SetEndpoint`): the first `PermitLimit` acquisitions from one IP succeed and the next fails, another IP has its own bucket; `Portal` partitions per authenticated `sub` (two users, one IP → independent) and per IP when anonymous; `Agent`/`Admin`/no endpoint never limited; named policies `sign-in` and `portal-sign-in` exist; constants in `CrmRateLimitPolicies`; `OnRejected` writes a 429 problem (`ProblemTypes.RateLimited`) and sets `Retry-After` to the ceiling of the lease's retry-after seconds in invariant culture (also under a UI culture of `ar`); `PublicRateLimitSettings` record equality. (R10) |

**Verify:** `dotnet test --project tests/Crm.BuildingBlocks.Tests`.

#### Slice 7 — wrap-up of the test code

1. Re-run both projects. Forbidden-pattern grep: `rg -n "DateTime\b|UtcNow|TimeProvider|Console\.|#pragma|SuppressMessage|FluentAssertions|Thread\.Sleep" backend/tests -g "*.cs"` must find nothing (`DateTimeOffset.UnixEpoch`/`DateTimeOffset` type names are fine; `UtcNow` may appear only as the `IDateTime.UtcNow` property name on substitutes - review each hit).
2. Null-forgiving check: `rg -n "\w!\s*[.;,)\]]" backend/tests -g "*.cs"` - review hits by eye (boolean negation is fine).
3. `git status --short backend/src` must print nothing.
4. Record the MTP summary counts per project; expected order of magnitude **≈ 400–480 across both projects**. Fewer than ≈ 300 means a slice was skipped.

---

### Task group D — Documentation and policy updates

#### D1 — **File: `CLAUDE.md`**

- **Repository layout block (lines 6–38):** under `backend/`, after `scripts/ef.ps1`, add
  `  tests/                     unit tests (xUnit v3 + NSubstitute): Crm.SharedKernel.Tests, Crm.BuildingBlocks.Tests, Crm.Modules.<Module>.Tests` and, if absent,
  `  global.json                SDK pin + opt-in to Microsoft.Testing.Platform (xunit.v3 4.x does not run on VSTest)`.
- **Replace the section "## Testing policy — automated tests are out of scope" (line 255 up to, not including, `## Workflow (squad-kit)` at line 282)** with
  `## Testing policy — backend unit tests are required, the frontend has none`, containing in order:
  1. **Backend:** every new or changed backend behaviour ships with unit tests in `backend/tests/`; stack xUnit v3 (`xunit.v3` 4.0.2) + NSubstitute 6.2.0 + plain `Assert`, EF Core InMemory only for query/persistence logic. **FluentAssertions is never added (8+ is a paid licence).** Versions live in `backend/Directory.Packages.props`.
  2. **Layout:** `backend/tests/Crm.SharedKernel.Tests`, `Crm.BuildingBlocks.Tests`, `Crm.Modules.<Module>.Tests` (references its own module's Domain, Application, Infrastructure; another module only through its Contracts - CRM0001 rule 1). One test class per production type; folders mirror the production project; PascalCase names without underscores.
  3. **Rules that still bind tests:** analyzers, banned-API lists and `TreatWarningsAsErrors` apply; no suppression, no `!`, no `DateTime`/`DateTimeOffset.UtcNow`/`TimeProvider` (fixed timestamps + a substituted `IDateTime`).
  4. **Run:** from `backend/`: `dotnet test --project tests/<Project>` for one project, `dotnet test --solution Crm.sln` for all (`global.json` opts into Microsoft.Testing.Platform). A running `Crm.Api` can lock `src/Crm.Api/bin`: build the test project alone.
  5. **Not covered / still manual:** the frontend ships without automated tests (no Vitest/Playwright - an owner decision); integration tests against SQL Server, API end-to-end tests and CI coverage gates are not part of the suite. Every story is still verified against the real endpoint (`.http` file) and the real screen in `en` and `ar`.
  6. Keep the "non-negotiable" list, reworded: compiler, `CRM0001` build rules and always-valid aggregates stay; commit request samples stays. Delete "No unit test projects…", "do not add a test framework", and "carry more risk than they otherwise would".
- **Verification section (lines 301–312):** add under `# backend`, after the build line: `dotnet test --solution backend/Crm.sln   # all green, zero skipped (or: cd backend && dotnet test --solution Crm.sln)`.
- **"Do not report a story done until…" paragraph:** add "the backend tests pass".
- Re-read the whole file once for contradictions (for example "The build is the architecture test" bullet and any "no tests" wording in the modular-monolith section).

#### D2 — **File: `.claude/commands/squad-implement.md`**

- Lines 25–27 (the "No automated tests" bullet) → `- **Add or update backend unit tests for changed backend code** (xUnit v3 + NSubstitute, plain Assert; see the testing policy in \`CLAUDE.md\`). \`dotnet test --solution Crm.sln\` from \`backend/\` must pass. The frontend has no automated tests: do not create \`*.spec.ts\`/\`*.test.ts\` files or add Vitest/Playwright.`
- Lines 28–29 → "Never silence an analyzer, and never reach for `!`, `any`, or a ts-ignore to make something compile. The compiler and the unit tests are the automated checks."
- Step 5 (lines 18–20): add "and `dotnet test --solution Crm.sln` (from `backend/`) green" to the backend-done definition.
- The "Enforce invariants in the aggregate…" bullet: end with "Unit tests then assert them."
- Do not touch `squad-test-story.md` or `squad-plan.md`.

#### D3 — **File: `backend/Directory.Build.targets`**

The working tree already has the new header (lines 3–14) and the `Tests` layer in the CRM0001 condition/text (line ~28). **Verify, do not redo.** Remaining edit: change the header's first sentence ("There is no architecture-test project on this repository, so the build is what enforces the module boundaries") to "Module boundaries are enforced by the build, not by an architecture-test library (NetArchTest/ArchUnitNET are deliberately not used)". Leave rules 1–7 untouched; **add no test-specific rule** (a `Crm.Modules.<M>.Tests` project is judged by rule 1 only).

#### D4 — **Files: `backend/README.md`, `README.md`**

- `backend/README.md` line 149: replace "There is **no test project in this repository** — see "No automated tests" below. Verification is:" with "Verification is the build, the unit tests, and the `.http` files:" and extend the code block with `dotnet test --solution Crm.sln   # run from backend/; xUnit v3 on Microsoft.Testing.Platform (opted in by global.json)` and `dotnet test --project tests/Crm.BuildingBlocks.Tests`.
- "Solution layout" block (~lines 59–78): add `tests/` with the six projects.
- Replace "## No automated tests" (lines 380–385) with "## Unit tests": stack, layout, how to run, conventions (fixed timestamps, no FluentAssertions - licence, analyzers apply, naming), the running-`Crm.Api` lock note, and why `global.json` opts into Microsoft.Testing.Platform.
- "Notable pinned versions": add `xunit.v3 4.0.2`, `NSubstitute 6.2.0`, `NSubstitute.Analyzers.CSharp 1.0.17`, `Microsoft.EntityFrameworkCore.InMemory 10.0.11` (lockstep with EF Core), "FluentAssertions is not used (8+ is a paid licence)".
- "Module boundaries": one sentence - a `Crm.Modules.<Module>.Tests` project is a module project for rule 1.
- Root `README.md` lines 233–242 "Quality gates": replace the first sentence with one saying the backend has a unit-test suite and the frontend has none, and add `dotnet test --solution backend/Crm.sln   # or: cd backend && dotnet test --solution Crm.sln` to the code block. This file is not in the intake's acceptance list; it is a consistency fix (it states the opposite of the new policy) - mention it in the report.

#### D5 — Tracker and plan indexes

- **`.squad/UNIT-TEST-STORIES.md`:** row 1 → Status `✅ Done`, Plan `[plan](plans/backend-unit-tests/60-story-unit-tests-foundation-and-building-blocks.md)`, Commit `Implement Story 60 — Unit tests — foundation and building blocks (UT-01)` (the subject; the implementer cannot know its own hash - the next session may add it); "Remaining: 4 stories."; Session log entry (newest first): date, "UT-01 done", test counts per project, the not-covered list, Follow-ups; note that the Override rules section is now redundant (CLAUDE.md and squad-implement.md say the same) but **leave it in place** - UT-02..UT-05 prompts still reference it.
- `.squad/plans/backend-unit-tests/00-overview.md` and `.squad/plans/00-index.md` rows were written by the planning session; the implementer verifies they are in the working tree and commits them with the rest.

---

## Edge Cases & Failure Modes

- **Per-closed-generic statics.** `AuthorizationBehavior<TRequest,TResponse>.Requirement` and `CachingBehavior<…>.HandleCachedMethod` are `static readonly` per closed type. A cacheable request returning a non-`Result<T>` makes `Handle` throw while the type initialiser succeeds - give that case its own request type; never share it with happy-path tests. Enforced in `CachingBehavior.cs` (`HandleCachedMethod`) and `AuthorizationBehavior.cs` (`Requirement`).
- **`RequestAuthorizationGuard.EnsureEveryRequestDeclares` scans every type of an assembly.** Never pass the test assembly (deliberately undeclared fixtures, `NonResultRequest`); pass the Application assembly or a dynamic assembly.
- **`ModuleDbContext.OnModelCreating` applies every `IEntityTypeConfiguration` in `GetType().Assembly`** (`ModuleDbContext.cs`, `ApplyConfigurationsFromAssembly`). A configuration class in the test assembly would apply to every test context: use conventions only.
- **InMemory fidelity gaps** (recorded as risks, never "fixed" in production code): no real transactions (rollback unobservable), no `rowversion`/concurrency tokens (use `ThrowingInterceptor`), no filtered indexes, case-sensitive strings, no relational constraints (max length/required asserted against the model, not the store).
- **Named query filters / `IgnoreQueryFilters(string[])`** (`ModuleDbContext.OrganisationScope.cs`, `SpecificationEvaluator.cs`, `Repository.ThrowIfOutOfScopeAsync`) are EF Core 10 features; if InMemory diverges, stop and record it (R5) rather than weaken assertions.
- **Clock-like code.** `DistributedCacheService` bypass uses `Environment.TickCount64`; `OutboxDispatcher` uses `Task.Delay`. Assert behaviour (cache untouched on the second call; handler called), never durations; bounded polling with a `CancellationToken` is the only waiting allowed and only in `OutboxDispatcherTests`.
- **Parallelism.** xUnit v3 runs test classes in parallel. `CultureInfo.CurrentUICulture` is async-local (restore in `finally`); `ActivityListener`/`MeterListener` are process-global (filter on a unique request/source name, dispose); never touch `Log.Logger`; one `DistributedCacheService` per test.
- **Culture-sensitive formatting.** `Retry-After` is invariant-culture formatted - run that test under UI culture `ar` as well.
- **Arabic text.** This machine breaks Bash heredocs with Arabic; tests compare resx values only to "non-empty and not the key" - no Arabic literal in test source. If one is ever needed, use the Write tool.
- **Analyzer friction** (CA1707, CA1822, CA1861, xUnit1051, xUnit2013, CA1859): change the code, never suppress.
- **Do not weaken production code to make it testable.** `OutboxJson`, `SensitiveData`, `GetDueOutboxMessagesAsync` are `internal`; no `InternalsVisibleTo` exists and none may be added (it would edit `backend/src/`). Test through the public surface; list unreachable cases as Follow-ups.
- **Unexpected production behaviour found by a test** (watch: `TransactionBehavior` rollback for generic exceptions; soft-delete + audit ordering in `AuditingSaveChangesInterceptor`; the write guard ignoring `Deleted` entries): pin the behaviour only when the code or its comments show it is intended; otherwise write no failing test - record it in Follow-ups and the tracker Session log.
- **Full-solution build blocked by a running `Crm.Api`** (lock on `src/Crm.Api/bin`): build each test project alone, say so in the report, do not stop the API.
- **Empty module test projects under MTP** may exit non-zero ("zero tests ran") and fail the solution-wide run - see R2.

---

## Test Plan

This story **is** the test plan; the slice tables above are the tests to add.

1. Slice 1: 12 files in `backend/tests/Crm.SharedKernel.Tests/` (one existing file extended) — ≈ 60 tests.
2. Slice 2: 15 files in `backend/tests/Crm.BuildingBlocks.Tests/Application/…` — ≈ 90 tests.
3. Slice 3: 6 files in `…/Application/Behaviors/` — ≈ 85 tests.
4. Slice 4: 14 files in `…/Infrastructure/{Identity,Correlation,Logging,Time,Caching,Http,Observability}` + `DependencyInjectionTests` — ≈ 85 tests.
5. Slice 5: 14 files in `…/Infrastructure/{Persistence,Outbox}` and `…/Infrastructure/Modules/` — ≈ 100 tests.
6. Slice 6: 12 files in `…/Presentation/…` — ≈ 75 tests.
7. Unit tests only: no integration (SQL Server/Testcontainers), no end-to-end, no smoke against a running API. Reference pattern: `backend/tests/Crm.SharedKernel.Tests/Results/ResultTests.cs`.
8. Nothing is removed. `ResultTests.cs` is extended; its three tests keep their names.

---

## Verification Steps

1. **Prove the risks first (R1–R5), from `D:\CRM\backend`:** `dotnet build tests/Crm.SharedKernel.Tests`; `dotnet test --project tests/Crm.SharedKernel.Tests` (3 passing); `dotnet build tests/Crm.BuildingBlocks.Tests`.
2. **After every slice:** `dotnet test --project tests/Crm.SharedKernel.Tests` (slice 1) or `dotnet test --project tests/Crm.BuildingBlocks.Tests` (slices 2–6) — zero failures, zero skipped, zero build warnings.
3. **Backend builds:** from `D:\CRM`: `dotnet build backend/Crm.sln` — clean, zero warnings, no `CRM0001`, no suppressed analyzer. If a running `Crm.Api` locks `backend/src/Crm.Api/bin`, build `backend/tests/Crm.SharedKernel.Tests`, `…/Crm.BuildingBlocks.Tests` and the four module test projects separately and say in the report that the solution build could not run.
4. **Module test projects build empty:** `dotnet build backend/tests/Crm.Modules.Organisation.Tests` (and Identity, Customers, Tickets) — the `Tests` layer is accepted by CRM0001.
5. **All tests:** from `D:\CRM\backend`: `dotnet test --solution Crm.sln` — zero failures, zero skipped; record per-project totals. Resolve an "empty project" failure per R2.
6. **Policy grep:** `rg -n -i "no automated tests|without automated tests|deliberately has no automated|out of scope" CLAUDE.md README.md backend/README.md .claude/commands/squad-implement.md` — remaining hits only the frontend's "ships without automated tests" statement.
7. **Production untouched:** `git status --short backend/src` prints nothing.
8. **Regression:** nothing to walk manually — no production code changed; say so in the report.

---

## Done Criteria

- [ ] Test infrastructure finished and documented: `backend/tests/Directory.Build.props`, the "Tests" package group, MTP opt-in in `backend/global.json`, the `Tests` layer accepted by `Directory.Build.targets` (`Crm.Modules.<M>.Tests` obeys rule 1).
- [ ] Six test projects in `backend/tests` and in `Crm.sln` (solution folder `tests`); the four module projects hold no tests and still build.
- [ ] `Crm.SharedKernel.Tests` covers `Result`/`Result<T>`/`Error`, `Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `DomainException`/`DomainError`, `AuditableEntity`, `IntegrationEvent`, scoping ids and markers (slice 1).
- [ ] `Crm.BuildingBlocks.Tests` covers all six pipeline behaviours (validation, authorisation, organisation scope, logging, transaction incl. outbox write + cache invalidation + after-commit ordering, caching incl. scope-prefixed keys/`VaryByUser`/failures not cached), `RequestAuthorizationGuard`, `OrganisationScope` (`ResolveWriteTarget`, narrowing, rejection, portal grant), `Specification`, `CrmPermissions`, `ModuleDbContext` scope filter + write guard + unit-of-work members, the auditing/soft-delete interceptor, `Repository`, `OutboxMessage`/`OutboxDispatcher` retry/dead-letter/idempotency, `DistributedCacheService`, log redaction, correlation id, `CurrentUserService`, `GlobalExceptionHandler`/`ProblemTypes`/`ApiControllerBase`, policies and conventions, middleware, rate limiting.
- [ ] No file under `backend/src/` changed; no analyzer suppressed; no `!`; no `DateTime`/`UtcNow` reads/`TimeProvider`; no FluentAssertions.
- [ ] `dotnet build backend/Crm.sln` clean (zero warnings, no `CRM0001`) — or the `Crm.Api` lock documented and every test project built on its own.
- [ ] `dotnet test --solution Crm.sln` from `backend/` passes: zero failures, zero skipped; per-project counts recorded.
- [ ] `CLAUDE.md` updated (Testing policy rewritten, Verification adds the test command, repository layout lists `backend/tests`); `backend/Directory.Build.targets` header and `backend/README.md` updated; root `README.md` quality-gates sentence fixed.
- [ ] `.claude/commands/squad-implement.md`: the "No automated tests" rule replaced by "add/update backend unit tests for changed backend code; `dotnet test --solution Crm.sln` from `backend/` must pass".
- [ ] `.squad/UNIT-TEST-STORIES.md` row UT-01 `✅ Done` with plan linked, "Remaining: 4 stories.", Session log entry (counts, not-covered list, Follow-ups).
- [ ] Plan + `00-overview.md` + `00-index.md` + tracker + tests + docs in **one** commit: `Implement Story 60 — Unit tests — foundation and building blocks (UT-01)`.
- [ ] Overview `00-overview.md` updated with this story.

---

## Named risks — prove each first, in this order, before building on it (planning ran nothing)

- **R1 — The full solution build with the scaffolding is unverified.** The setup session never completed `dotnet build backend/Crm.sln` with the test projects, the `Directory.Build.targets` edit and the package group (a running `Crm.Api` locked the host output). **Prove:** Verification step 1, then step 3. Fix only test-project/config files; a `CRM0001` on a `*.Tests` project means the `Tests` layer condition in `Directory.Build.targets` is wrong - fix it there, never add or weaken a reference rule.
- **R2 — `dotnet test` mechanics under Microsoft.Testing.Platform.** (a) `global.json` `"test": {"runner": "Microsoft.Testing.Platform"}` + xunit.v3 4.0.2 must make both `dotnet test --project tests/X` and `dotnet test --solution Crm.sln` work on SDK 10.0.301; (b) the four **empty** module test projects may fail the solution run with MTP's "zero tests ran" exit code. **Prove:** `dotnet test --solution Crm.sln` right after slice 1. **Fallback (config only, inside this story's scope):** add `<TestingPlatformCommandLineArguments>--minimum-expected-tests 0</TestingPlatformCommandLineArguments>` (or the documented ignore-exit-code switch) to `backend/tests/Directory.Build.props`; if that does not work, stop and report to the owner - do not add placeholder tests to module projects and do not set `IsTestProject` false. Log what was done in the tracker.
- **R3 — Transitive visibility.** The test projects see ASP.NET (`DefaultHttpContext`, `AreaAttribute`, `ControllerModel`), MediatR, FluentValidation, Serilog and Polly only through `ProjectReference` + `FrameworkReference`/`PackageReference` flow. **Prove:** compile one reference of each in slice 2/4 before writing more. **Fallback:** task 1.2 (FrameworkReference / missing PackageReference in `Crm.BuildingBlocks.Tests.csproj` only).
- **R4 — EF InMemory and transactions.** `Database.BeginTransactionAsync` throws on InMemory unless `InMemoryEventId.TransactionIgnoredWarning` is ignored; with it ignored, commit/rollback are no-ops (rollback does not undo writes). **Prove:** the first `ModuleDbContextUnitOfWorkTests` case. Tests never assert data rollback.
- **R5 — InMemory vs EF Core 10 named query filters.** The scope and soft-delete filters are *named* (`HasQueryFilter(name, …)`) with `IgnoreQueryFilters([names])`. **Prove:** the first two `ModuleDbContextScopeFilterTests` cases (scope hides rows; `IgnoreQueryFilters([SoftDeleteFilterName])` keeps scope). If the provider diverges, record it in Follow-ups and cover the filters by inspecting `GetDeclaredQueryFilters()` instead of weakening assertions. Also: the filters read `currentUser.Scope` through the `DbContext` instance, so each context needs its own `ICurrentUserService`.
- **R6 — `Reflection.Emit` dynamic assemblies for the guard/DI failure tests.** `EnsureEveryRequestDeclares` scans whole assemblies. **Prove:** emit one `AssemblyBuilder.DefineDynamicAssembly(..., AssemblyBuilderAccess.Run)` with a request type implementing `IRequest<Result>` and confirm `GetTypes()` works. If emitting is impractical, cover only the passing scenarios and `For(Type)` and list the failing-scenario gap in Follow-ups (a second fixture assembly would need a seventh test project, which the intake forbids).
- **R7 — `AddStandardResilienceHandler` in a unit test.** Binding `Resilience:Outbound` and building the pipeline may need extra configuration. **Prove:** the first `HttpClientBuilderExtensionsTests` case; if it cannot run offline, assert registration only and list the behaviour as not unit-testable.
- **R8 — `IProblemDetailsService` on `DefaultHttpContext`.** `TryWriteAsync` can return `false` when no writer accepts the request. **Prove:** the first `ErrorHandlingServiceCollectionExtensionsTests` case; otherwise test the `CustomizeProblemDetails` delegate directly and rely on `GlobalExceptionHandlerTests` capturing `ProblemDetailsContext`.
- **R9 — `UseCrmRequestLocalization` without a server.** Running the built `RequestDelegate` over `DefaultHttpContext` should work, but the exact behaviour for `ar-SA` and `fr` (supported cultures are only `en`, `ar`) must be **observed, then pinned**, not assumed.
- **R10 — Rate-limiter test seams.** `RateLimiterOptions.GlobalLimiter` is a `PartitionedRateLimiter<HttpContext>` (usable via `AttemptAcquire`); `PolicyMap` and `OnRejectedContext` accessibility varies by ASP.NET version. **Prove** by compiling the test first; fall back to behaviour-level assertions and a Follow-up if a member is inaccessible.
- **R11 — Analyzer noise on test code** (`AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, xunit analyzers). Expect CA1707/CA1861/CA1822/xUnit1051 findings. **Prove:** build after the first file of each slice, not at the end of it.
- **R12 — Observability extension not read during planning.** `Observability/ObservabilityServiceCollectionExtensions.cs` (88 lines) was not opened; its test row is conditional on reading it. A "not unit-testable" decision is listed in the report.
- **R13 — Size.** ≈ 400–480 tests is a large story. If the implementer's context runs out mid-slice, **do not commit**: write exactly which slices are done into the tracker Session log and stop at a slice boundary so a later session can resume at the next one.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 61 (UT-02, Organisation module).**
