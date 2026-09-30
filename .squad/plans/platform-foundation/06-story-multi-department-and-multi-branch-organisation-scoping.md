# Story 06 — Multi department and multi branch organisation scoping

## Prerequisites

- **Stories 01–05 are implemented and committed** (latest `f898922`). Read [`00-overview.md`](00-overview.md),
  "Story 02 — decisions carried forward" (the scope filter is a **second named** filter; never the unnamed
  `HasQueryFilter`), "Story 03 — decisions carried forward" (`CrmClaimTypes`, cache-key shape
  `t=…|d=…|b=…:<key>` stays), and "Story 05 — decisions carried forward" (cached results are culture-neutral;
  `.resx` + key constants; `Accept-Language` sent from `baseFetch`).
- **Modules:** creates the **`Organisation`** module (five projects, schema `organisation`). Changes the
  **BuildingBlocks** (`Crm.SharedKernel`, `Crm.BuildingBlocks.Application`, `Crm.BuildingBlocks.Infrastructure`,
  `Crm.BuildingBlocks.Presentation`) and the host. Touches the **`Sample`** template module only to make it the
  first organisation-scoped aggregate (the cross-department matrix runs against it; Story 11 copies it), via
  `Crm.Modules.Organisation.Contracts` (dev-seed ids) — a Contracts reference, rule 1 allows it.
- **MVP scope (2026-09-29).** Multi-department + multi-branch, memberships, Development-only dev login, admin
  branch/department screens, agent scope switcher. **Deferred:** teams, business hours, holiday calendars, branch
  time zones, department reparenting, audit-log entries (rejections are logged only), membership admin *screens*
  (endpoints only), organisation (tenant) admin endpoints (seed only), caching of Organisation queries.
- **Checked during planning (2026-09-29, from files, no runs):** .NET SDK pin `10.0.301` (`backend/global.json`,
  rollForward latestFeature), EF Core **10.0.11**, MediatR **12.4.1** (do not bump), FluentValidation **12.1.1**,
  Swashbuckle **10.2.3** (`backend/Directory.Packages.props`). **No new NuGet or npm package:** cookie/header
  authentication and authorisation ship in `Microsoft.AspNetCore.App` (already a `FrameworkReference` of
  `Crm.BuildingBlocks.Infrastructure` and `Crm.BuildingBlocks.Presentation`). Frontend pins unchanged
  (`frontend/package.json`: Nuxt 4.5.2, `@nuxtjs/i18n` 10.6.0, Pinia 4.0.3, pnpm 12.6.0).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — `Contains` over a strongly-typed-id list translates.** The scope filter is
    `ScopeDepartmentIds.Contains(e.DepartmentId)` where both sides are `DepartmentId` (value-converted to `Guid`
    by convention, Task B4). EF Core 10 must translate it to `IN (@p1, @p2 …)` or `OPENJSON`. **Prove:** after B4,
    run the API, call `GET /api/Admin/Samples` as `agentA` (organisation.http request 20) and read the
    `Microsoft.EntityFrameworkCore.Database.Command` log line (Information in `appsettings.Development.json`).
    **Fallback (no other):** change the three `IOrganisationScoped`/`ITenantScoped` members to plain `Guid`
    (`Guid TenantId`, …) on the entity side and make the filter lists `IReadOnlyList<Guid>`; keep the typed ids
    in `OrganisationScope` and convert with `.Value`. Record which path shipped in the story report.
  - **R2 — the filter is parameterised per context instance.** The lambdas read `this.ScopeTenantId` etc. inside
    a generic method called by reflection from `OnModelCreating`; EF caches the model once per context type and
    must re-bind `this` per query. **Prove:** the SQL shows parameters (`@__ScopeDepartmentIds_…`/`@p…`), never
    literal Guids, and requests 20 (agentA) then 21 (agentB) in the same process return different rows.
  - **R3 — EF 10 generic named-filter overload.** `EntityTypeBuilder<T>.HasQueryFilter(string, Expression<…>)`.
    Story 02 proved only the non-generic `HasQueryFilter(string, LambdaExpression)`. If the generic overload is
    missing, call `modelBuilder.Entity(typeof(T)).HasQueryFilter(ScopeFilterName, (LambdaExpression)filter)`.
    **Prove:** `dotnet build` + the SQL of request 20 carries **both** `[IsDeleted] = CAST(0 AS bit)` **and** the
    scope predicates.
  - **R4 — scope is not poisoned during authentication.** The dev auth handler queries `organisation.Memberships`
    while `HttpContext.User` is still anonymous. `CurrentUserService` recomputes the scope whenever
    `HttpContext.User` is a different instance (Task B3). **Prove:** request 3 (`GET /api/Agent/MyScope` as
    `supervisor`) returns two departments on the **first** call after startup.
  - **R5 — MediatR inside `AuthenticateAsync`.** The handler resolves `IDevUserDirectory` (scoped) from request
    services and sends a query through the full pipeline. **Prove:** request 3 works; the log shows the
    `GetMembershipsOfUserQuery` handled line before `MyScope`'s.
  - **R6 — `[Authorize]` is enforced with the implicit routing of minimal hosting.** `UseAuthentication()` +
    `UseAuthorization()` are added without an explicit `UseRouting()`. **Prove:** request 8 (Admin endpoint,
    no `X-Dev-User`) → 401 problem; request 9 (as `agentA`) → 403 problem, both with `urn:crm:problem:*` types.
  - **R7 — Sample migration on a database that already holds the Story 02 seed row.** The migration back-fills
    scope columns with the dev-seed ids (Task B11). **Prove:** after startup,
    `SELECT COUNT(*) FROM sample.Samples WHERE TenantId = '00000000-0000-0000-0000-000000000000'` returns 0.
- **Coordination:**
  - **Story 07** deletes `DevUserAuthenticationHandler`, `IDevUserDirectory`, `DevUserDirectory`, `/_dev/login`,
    and the `X-Dev-User` plumbing. It issues JWTs with exactly the claims the dev handler builds (Task B3 table),
    reads memberships through `IOrganisationModuleApi.GetMembershipsAsync`, and seeds users with the ids in
    `OrganisationDevSeed`.
  - **Story 11** deletes `Sample` and copies its scoped shape (`IOrganisationScoped`, `ResolveWriteTarget`, the
    list query) for `Customer`.
  - **Story 58** adds tenant configuration/branding to the `Organisation` aggregate created here.

---

## Story Goal

Data isolation by tenant, branch, and department is enforced **once, at the data layer**, so no handler can leak
across a boundary by omission.

When this story is done:

1. `Crm.SharedKernel` holds `TenantId`, `BranchId`, `DepartmentId`, and the markers `ITenantScoped` and
   `IOrganisationScoped`. `ModuleDbContext` applies a second **named** query filter, `ScopeFilterName`
   (`"OrganisationScope"`), to every such entity in every module with no opt-in, and refuses to save an
   out-of-scope row.
2. `ICurrentUserService.Scope` is the caller's resolved `OrganisationScope`: tenant, memberships
   (department → branch → role), and the active scope from the `X-Crm-Scope` header (`all` or a department id).
   The header only narrows; a department the caller does not belong to is rejected with **403**.
3. Reading, listing, or updating an entity of another department returns **403 `urn:crm:problem:forbidden`**
   (code `Organisation.OutOfScope`), never an accidental 404; lists simply do not contain it.
4. The `Organisation` module owns `Organisation` (the tenant), `Branch`, `Department`, and `Membership` aggregates,
   bilingual names (`LocalizedText`), codes, active flags, Admin endpoints, and
   `IOrganisationModuleApi.GetMembershipsAsync(userId)` for Story 07.
5. A Development-only `X-Dev-User` authentication handler signs in the four seeded users from their memberships.
6. Admin screens list, create, rename, and deactivate branches and departments in `en` and `ar`; the agent
   topbar has a scope switcher ("All my departments" or one department) persisted in the `crm-scope` cookie.

**Not in scope:** real sign-in (Story 07), a permission catalogue (Story 07/09 — only an `Administrator` policy
here), membership admin screens, audit-log writes, teams/business hours/calendars, and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith" (module catalogue: `Organisation` owns stories 06, 58), "Application, API, and
   persistence" bullets **Localization keys** (~line 179), **Scoping** (~line 195), **Caching**; "Testing policy".
2. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.cs` — primary
   constructor line 22 (gains `ICurrentUserService`), `SoftDeleteFilterName` line 24, `OnModelCreating` lines
   31–49 (soft-delete filter applied at line 45), `SaveChangesAsync` lines 54–64 (write guard goes before
   `base.SaveChangesAsync`).
3. `…/Crm.BuildingBlocks.Infrastructure/Persistence/ModelBuilderExtensions.cs` lines 11–41 — the named soft-delete
   filter and the warning against the unnamed overload. The scope filter follows the same rules.
4. `…/Crm.BuildingBlocks.Application/Abstractions/ICurrentUserService.cs` lines 1–16 and `CrmClaimTypes.cs`
   lines 7–13; `…/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs` lines 8–25 (claims read only
   when authenticated).
5. `…/Crm.BuildingBlocks.Application/Behaviors/CachingBehavior.cs` lines 83–95 — `BuildKey` reads
   `TenantId`/`DepartmentId`/`BranchId` strings. **Code unchanged**; Task B3 changes what those strings hold.
6. `…/Crm.BuildingBlocks.Application/DependencyInjection.cs` lines 26–33 (behaviour order) and
   `…/Behaviors/TransactionBehavior.cs` lines 91–98 (the `ConcurrencyConflictException` catch — the scope
   exception gets the same treatment) and `…/Behaviors/ValidationBehavior.cs` lines 53–69 (reflection that builds
   a failed `TResponse` — copy it).
7. `…/Crm.BuildingBlocks.Infrastructure/Persistence/Repositories/Repository.cs` lines 21–31 and
   `SpecificationEvaluator.cs` lines 13–16; `…/Crm.BuildingBlocks.Application/Specifications/Specification.cs`
   lines 28 and 46 (`IncludeSoftDeleted` is the pattern for the new scope-lifting flag).
8. `…/Crm.BuildingBlocks.Infrastructure/Logging/CrmLogEnricher.cs` lines 25–35 (reads `department_id`/`branch_id`
   claims, which this story stops issuing).
9. `…/Crm.BuildingBlocks.Presentation/ErrorHandling/GlobalExceptionHandler.cs` lines 32–40 (exception switch) and
   `ApiControllerBase.cs` lines 36–68; `ErrorTypeMapping.cs` (`ErrorType.Forbidden` → 403 `forbidden`).
10. `…/Crm.BuildingBlocks.Application/Localization/CrmMessageKeys.cs` and `Resources/CrmMessages{,.ar}.resx`.
11. `backend/src/Crm.Api/Program.cs` lines 26–43 (service registration, Development block) and 84–113 (the
    **ORDER MATTERS** middleware block); `Modules/ModuleCatalog.cs` lines 12–16; `Crm.Api.csproj` lines 7–12.
12. The whole `backend/src/Modules/Sample/` module — the template for every Organisation file: `SampleAggregate.cs`
    (factory with optional well-known id, lines 39–59), `SampleName.cs` (EF constructor-parameter rule),
    `SampleAggregateConfiguration.cs`, `SampleDbContext.cs`, `SampleDbContextFactory.cs`, `SampleSeeder.cs`,
    `SampleModule.cs`, `SampleModuleApi.cs`, `Controllers/Admin/SamplesController.cs`, all five `.csproj`.
13. `backend/Directory.Build.targets` rules 1–7 — Organisation.Contracts may be referenced by Sample; the host
    references only Organisation.Infrastructure and .Presentation.
14. `backend/requests/diagnostics.http`, `persistence.http`, `localization.http` — every `/api/Admin/Samples`
    request needs `X-Dev-User` after this story (Task B13).
15. `frontend/app/plugins/api.ts` lines 7–13 (`baseFetch`), `frontend/app/layouts/agent.vue` lines 37–41 (topbar)
    and 88 (`agentNav`), `frontend/app/pages/login.vue`, `frontend/app/composables/useApi.ts`,
    `frontend/app/components/ui/{Select,Table,Modal,Input,Badge}.vue` props, `frontend/i18n/locales/en/errors.json`
    (`errors.code` is empty), `frontend/modules/dev-only-pages.ts`, `frontend/nuxt.config.ts` lines 28–31
    (`routeRules`).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **Two markers.** `ITenantScoped` (tenant only: `Branch`, `Department`, `Membership`) and `IOrganisationScoped :
  ITenantScoped` (tenant + branch + department: operational data — `SampleAggregate` now, customers/tickets later).
  `Organisation` itself is unscoped (its id *is* the tenant).
- **Effective scope.** `all` (or no header) = every department the caller has a membership in; a department id =
  that one department. Branches in scope = the branches of the departments in scope. The filter checks all three
  columns (tenant, branch, department) — the branch predicate is redundant with the department one by design
  (defence in depth, and the AC reads the three predicates in SQL).
- **No administrator bypass.** Administrators see what their memberships give them (the seed puts the admin in
  every department). The `Administrator` role only unlocks Admin endpoints.
- **Who is unrestricted.** Code running with **no `HttpContext`** (seeders, the outbox dispatcher and its
  consumers, design-time tooling) gets `OrganisationScope.Unrestricted`. A request **with** an `HttpContext` and
  no authenticated user gets `OrganisationScope.None` and sees nothing scoped. There is no third state.
- **The one permitted scope lift** is `Specification.IgnoreOrganisationScope()`, used by exactly one
  specification: `MembershipsOfUserSpecification` (sign-in membership lookup — the caller is not signed in yet).
  It lifts **only** `ScopeFilterName`; the soft-delete filter stays, so a removed membership never grants access.
- **Deactivation is not deletion.** A deactivated branch or department keeps its rows and its memberships (existing
  records do not break, members keep read access); it disappears from pickers (scope switcher, the department
  form's branch select, `activeOnly` lists). No delete endpoint for branches/departments.
- **Claims (Story 07 issues exactly these):** `sub`, `tenant_id`, one `membership` per membership with value
  `"<departmentId>|<branchId>|<role>"` (Guids in `D` format, role name), and one `role` per distinct role.
  `department_id`/`branch_id` claims are **removed** from `CrmClaimTypes`; nothing issues them.
- **Cache-key shape unchanged** (`t=…|d=…|b=…`). `d` and `b` now hold the **sorted, comma-joined effective**
  department/branch ids, so two users with different membership sets never share a cached entry.
- **`LocalizedText` lives in `Crm.Modules.Organisation.Domain`**, not the shared kernel. A later module that needs
  bilingual names defines its own (or promotes this one in a story that says so).
- **Codes:** `^[A-Z0-9-]{2,20}$`, unique per tenant among branches and among departments. Names: `ar` and `en`
  both required, trimmed, ≤ 200.

---

## Backend Tasks

All paths are under `backend/src/` unless they start with `backend/`. Use the **Write tool** for every new file
(Arabic text and `$` break Bash heredocs on this machine). Every new C# file uses file-scoped namespaces and
`sealed` classes like the Sample module. No `!` null-forgiving operator in new code.

### B1 — Scope ids and markers (`Crm.SharedKernel`)

**Create file: `BuildingBlocks/Crm.SharedKernel/Scoping/ScopeIds.cs`**

```csharp
namespace Crm.SharedKernel.Scoping;

/// <summary>The organisation (tenant). Also the id of the Organisation aggregate.</summary>
public sealed record TenantId(Guid Value) : StronglyTypedId(Value)
{
    public static readonly TenantId None = new(Guid.Empty);
}

public sealed record BranchId(Guid Value) : StronglyTypedId(Value)
{
    public static BranchId New() => new(Guid.NewGuid());
}

public sealed record DepartmentId(Guid Value) : StronglyTypedId(Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());
}
```

**Create file: `BuildingBlocks/Crm.SharedKernel/Scoping/ITenantScoped.cs`**

```csharp
namespace Crm.SharedKernel.Scoping;

/// <summary>Rows of one tenant. ModuleDbContext's named "OrganisationScope" filter restricts reads to the
/// caller's tenant and refuses to save a row of another tenant. Nothing to opt into.</summary>
public interface ITenantScoped
{
    TenantId TenantId { get; }
}

/// <summary>Operational rows of one department (and therefore one branch) of one tenant. Reads are restricted to
/// the caller's effective departments and branches; saves outside them are refused (403).</summary>
public interface IOrganisationScoped : ITenantScoped
{
    BranchId BranchId { get; }

    DepartmentId DepartmentId { get; }
}
```

### B2 — `OrganisationScope`, roles, claims (`Crm.BuildingBlocks.Application`)

**Create file: `BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/OrganisationScope.cs`**

```csharp
using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Application.Abstractions;

public sealed record ScopeMembership(DepartmentId DepartmentId, BranchId BranchId, string Role);

public sealed record ScopeWriteTarget(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId);

/// <summary>The caller's data scope for one request. Built only by CurrentUserService; read by the
/// ModuleDbContext scope filter, the write guard, CachingBehavior (via ICurrentUserService strings), and the
/// Organisation module's "my scope" query.</summary>
public sealed class OrganisationScope
{
    public const string HeaderName = "X-Crm-Scope";
    public const string AllDepartments = "all";

    /// <summary>No HttpContext: seeders, outbox consumers, design-time tooling. Filters are a no-op.</summary>
    public static readonly OrganisationScope Unrestricted = new(true, TenantId.None, [], null, false);

    /// <summary>An HttpContext with no authenticated user. Sees no scoped row.</summary>
    public static readonly OrganisationScope None = new(false, TenantId.None, [], null, false);

    public OrganisationScope(bool isUnrestricted, TenantId tenant, IReadOnlyList<ScopeMembership> memberships,
        DepartmentId? activeDepartment, bool activeScopeRejected) { /* assign; compute Departments/Branches */ }

    public bool IsUnrestricted { get; }
    public TenantId Tenant { get; }
    public IReadOnlyList<ScopeMembership> Memberships { get; }
    /// <summary>Null = "all my departments".</summary>
    public DepartmentId? ActiveDepartment { get; }
    /// <summary>The X-Crm-Scope header named a department the caller is not a member of (or was malformed).
    /// OrganisationScopeMiddleware answers 403; Departments/Branches are then empty.</summary>
    public bool ActiveScopeRejected { get; }
    /// <summary>Effective departments: ActiveDepartment alone, or every membership's department. Sorted by Value.</summary>
    public IReadOnlyList<DepartmentId> Departments { get; }
    /// <summary>Branches of <see cref="Departments"/>, distinct, sorted by Value.</summary>
    public IReadOnlyList<BranchId> Branches { get; }

    public bool Allows(ITenantScoped row);   // Unrestricted, or tenant matches and (not IOrganisationScoped, or branch+department in the effective sets)

    /// <summary>Where a new scoped row goes. <paramref name="requestedDepartment"/> wins when given and in scope;
    /// otherwise ActiveDepartment; otherwise the single effective department. Null when none resolves - the
    /// handler answers Forbidden (requested, out of scope) or DepartmentRequired (ambiguous).</summary>
    public ScopeWriteTarget? ResolveWriteTarget(Guid? requestedDepartment);

    public IReadOnlyList<string> Roles { get; } // distinct Memberships.Role, ordinal-sorted
}
```

Keep this class short: `Departments`/`Branches` computed once in the constructor with `OrderBy(d => d.Value)`;
`Departments` is empty when `activeScopeRejected`. `Allows` is the only place the three-column rule is written in
C#; the SQL rule in B4 must stay equivalent (the two are next to each other in review).

**Create file: `…/Abstractions/CrmRoles.cs`** — `public static class CrmRoles { public const string Administrator
= "Administrator"; public const string Supervisor = "Supervisor"; public const string Agent = "Agent"; }` (the
string form of Organisation's `SystemRole`, used by claims and the policy).

**File: `…/Abstractions/CrmClaimTypes.cs`** — replace lines 11–12 (`DepartmentId`, `BranchId`) with:

```csharp
    /// <summary>One per membership: "&lt;departmentId&gt;|&lt;branchId&gt;|&lt;role&gt;" (Guid "D" format). Story 06.</summary>
    public const string Membership = "membership";
    public const string Role = "role";
```

Update the class summary: Story 07 issues `sub`, `tenant_id`, `membership`*, `role`*.

**File: `…/Abstractions/ICurrentUserService.cs`** — replace lines 10–15:

```csharp
    /// <summary>The caller's tenant id, or null when unauthenticated.</summary>
    string? TenantId { get; }

    /// <summary>Effective department ids, sorted and comma-joined (log + cache-key segment); null when none.</summary>
    string? DepartmentId { get; }

    /// <summary>Effective branch ids, sorted and comma-joined; null when none.</summary>
    string? BranchId { get; }

    /// <summary>The resolved data scope (Story 06). Never null.</summary>
    OrganisationScope Scope { get; }
```

**Create file: `…/Abstractions/IDevUserDirectory.cs`** (Development-only seam; Story 07 deletes it):

```csharp
namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>DEVELOPMENT ONLY (Story 06). Lets the X-Dev-User handler in BuildingBlocks build a principal from
/// the Organisation module's memberships without referencing the module. Deleted by Story 07.</summary>
public interface IDevUserDirectory
{
    Task<DevUser?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record DevUser(Guid UserId, Guid TenantId, IReadOnlyList<DevUserMembership> Memberships);

public sealed record DevUserMembership(Guid DepartmentId, Guid BranchId, string Role);
```

**Create file: `…/Exceptions/OrganisationScopeViolationException.cs`**

```csharp
namespace Crm.BuildingBlocks.Application.Exceptions;

/// <summary>A read or write touched a row outside the caller's organisation scope. Thrown by the repository
/// (read of an existing out-of-scope row) and by ModuleDbContext's write guard; turned into 403 by
/// OrganisationScopeBehavior (and GlobalExceptionHandler as a backstop).</summary>
public sealed class OrganisationScopeViolationException(string entityType, string operation)
    : Exception($"{operation} of a {entityType} outside the caller's organisation scope.")
{
    public string EntityType { get; } = entityType;
    public string Operation { get; } = operation; // "Read" or "Write"
}
```

**File: `…/Specifications/Specification.cs`** — after line 28 add
`public bool IgnoreOrganisationScopeFilter { get; private set; }`; after line 46 add:

```csharp
    /// <summary>THE ONE PERMITTED SCOPE LIFT (Story 06): used only by the sign-in membership lookup
    /// (Organisation's MembershipsOfUserSpecification). Lifts only ModuleDbContext.ScopeFilterName - the
    /// soft-delete filter stays. Adding a second caller is a security review, not a refactor.</summary>
    protected void IgnoreOrganisationScope() => IgnoreOrganisationScopeFilter = true;
```

Update the class summary (lines 5–11): two named escape hatches, each lifting exactly one filter.

### B3 — Scope resolution, dev login, log enricher (`Crm.BuildingBlocks.Infrastructure`)

**File: `BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs`** — rewrite. Keep it the
single place that turns claims + header into a scope:

- `UserId`/`UserIdOrSystem` unchanged.
- `Scope`: `HttpContext` null → `OrganisationScope.Unrestricted`. User not authenticated → `OrganisationScope.None`.
  Otherwise parse: `tenant_id` (Guid; missing/invalid → `None`), every `membership` claim split on `'|'` into
  3 parts (Guid, Guid, role; a malformed claim is skipped, never thrown). Header `X-Crm-Scope`: absent, empty, or
  `all` (ordinal-ignore-case) → active null; a Guid that is one of the membership departments → that department;
  anything else → `activeScopeRejected: true`.
- **Cache per principal, not per request (R4):** store `(ClaimsPrincipal user, OrganisationScope scope)`; recompute
  when `!ReferenceEquals(httpContext.User, cachedUser)`. The auth handler runs a query while the user is still
  anonymous; a request-lifetime cache would pin `None` for the rest of the request.
- `TenantId` = `Scope.Tenant == TenantId.None ? null : Scope.Tenant.Value.ToString()`; `DepartmentId` /
  `BranchId` = `string.Join(',', Scope.Departments/Branches.Select(x => x.Value.ToString()))`, null when empty.
  Change the file comment line 7 to "Story 06: scope from membership claims + X-Crm-Scope; Story 07 issues the JWT".

**Create file: `…/Identity/SystemCurrentUser.cs`** — `public sealed class SystemCurrentUser : ICurrentUserService`
with `public static readonly SystemCurrentUser Instance = new();`, `UserId => null`, `UserIdOrSystem => "system"`,
null strings, `Scope => OrganisationScope.Unrestricted`. Used only by design-time DbContext factories.

**Create file: `…/Identity/DevUserAuthenticationHandler.cs`** — DEVELOPMENT ONLY, deleted by Story 07.

```csharp
public static class DevUserAuthentication
{
    public const string Scheme = "DevUser";
    public const string HeaderName = "X-Dev-User";
}

public sealed partial class DevUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // not Development -> NoResult, always (the header is ignored outside Development)
        // header absent -> NoResult (anonymous); not a Guid, or unknown user -> Fail + LogRejected
        // IDevUserDirectory resolved from Context.RequestServices (scoped)
        // claims: sub, tenant_id, membership per membership, role per distinct role
        // new ClaimsIdentity(claims, Scheme, CrmClaimTypes.UserId, CrmClaimTypes.Role)
    }
}
```

Log a rejection with a `[LoggerMessage]` warning: `"Dev login rejected for X-Dev-User {DevUser}: {Reason}"`
(the raw header value is not sensitive). Use the handler's `Logger` property through a static partial method
taking `ILogger` (CA1848). Add extension in the same file:

```csharp
public static AuthenticationBuilder AddCrmDevUser(this AuthenticationBuilder builder) =>
    builder.AddScheme<AuthenticationSchemeOptions, DevUserAuthenticationHandler>(DevUserAuthentication.Scheme, null);
```

**File: `…/Logging/CrmLogEnricher.cs`** lines 25–35 — replace the claim reads for tenant/department/branch with
`httpContext.RequestServices.GetService<ICurrentUserService>()` (per event; the enricher is a singleton, the
service scoped): add `UserId` from the claim as today, `TenantId`, `DepartmentId`, `BranchId` from the service's
strings when non-null. Keep the "authenticated only" guard. Update the summary comment.

### B4 — The scope filter and the write guard: one file (`ModuleDbContext`)

**File: `…/Persistence/ModuleDbContext.cs`** — line 22 becomes
`public abstract partial class ModuleDbContext(DbContextOptions options, ICurrentUserService currentUser) : DbContext(options), IUnitOfWork`.
In `OnModelCreating`, after line 45 add `ApplyOrganisationScopeFilters(modelBuilder);`. In `SaveChangesAsync`, first
statement inside `try`: `EnsureWritesInScope();`. Add `using Crm.SharedKernel.Scoping;`.

**Create file: `…/Persistence/ModuleDbContext.OrganisationScope.cs`** — the most security-relevant code in the
system. Keep everything below in this one file and nowhere else.

```csharp
public abstract partial class ModuleDbContext
{
    /// <summary>Second NAMED filter next to SoftDeleteFilterName (Story 02 rule: never the unnamed overload - it
    /// would replace the soft-delete filter). Lifted only by Specification.IgnoreOrganisationScope().</summary>
    public const string ScopeFilterName = "OrganisationScope";

    // Read by EF at query time from the CURRENT context instance (R2). Never cache these in fields.
    private OrganisationScope Scope => currentUser.Scope;
    private bool ScopeIsUnrestricted => Scope.IsUnrestricted;
    private TenantId ScopeTenantId => Scope.Tenant;
    private IReadOnlyList<BranchId> ScopeBranchIds => Scope.Branches;
    private IReadOnlyList<DepartmentId> ScopeDepartmentIds => Scope.Departments;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        // Every module maps the scope ids to uniqueidentifier the same way (R1 relies on convention converters).
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<BranchId>().HaveConversion<BranchIdConverter>();
        configurationBuilder.Properties<DepartmentId>().HaveConversion<DepartmentIdConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    private void ApplyOrganisationScopeFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList())
        {
            string? method = typeof(IOrganisationScoped).IsAssignableFrom(entityType.ClrType) ? nameof(FilterOrganisationScoped)
                : typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType) ? nameof(FilterTenantScoped)
                : null;
            if (method is null) continue;
            typeof(ModuleDbContext).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void FilterOrganisationScoped<T>(ModelBuilder modelBuilder) where T : class, IOrganisationScoped =>
        modelBuilder.Entity<T>().HasQueryFilter(ScopeFilterName, e =>
            ScopeIsUnrestricted
            || (e.TenantId.Equals(ScopeTenantId)
                && ScopeBranchIds.Contains(e.BranchId)
                && ScopeDepartmentIds.Contains(e.DepartmentId)));

    private void FilterTenantScoped<T>(ModelBuilder modelBuilder) where T : class, ITenantScoped =>
        modelBuilder.Entity<T>().HasQueryFilter(ScopeFilterName, e => ScopeIsUnrestricted || e.TenantId.Equals(ScopeTenantId));

    /// <summary>Write guard: no Added/Modified scoped row may leave the caller's scope - covers a create that names
    /// a foreign department and a change that moves a row. Rows are never loaded out of scope, so this is the
    /// backstop, not the main path.</summary>
    private void EnsureWritesInScope()
    {
        if (Scope.IsUnrestricted) return;
        foreach (EntityEntry<ITenantScoped> entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified && !Scope.Allows(entry.Entity))
            {
                throw new OrganisationScopeViolationException(entry.Entity.GetType().Name, "Write");
            }
        }
    }

    private sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(v => v.Value, v => new TenantId(v));
    private sealed class BranchIdConverter() : ValueConverter<BranchId, Guid>(v => v.Value, v => new BranchId(v));
    private sealed class DepartmentIdConverter() : ValueConverter<DepartmentId, Guid>(v => v.Value, v => new DepartmentId(v));
}
```

If `HaveConversion<T>()` rejects a private nested converter type (it needs a public parameterless constructor),
make the three converters `internal sealed` top-level classes in the same file. `.Equals` mirrors
`Repository.GetByIdAsync` line 22 (proven by Story 02); do not switch to `==`. R1/R2/R3 are proven against this file.

**File: `…/Persistence/ModelBuilderExtensions.cs`** lines 11–18 — update the comment: "Story 06's scope filter is
`ModuleDbContext.ScopeFilterName`, applied in `ModuleDbContext.OrganisationScope.cs`".

**File: `…/Persistence/Repositories/Repository.cs`** — reads of an existing out-of-scope row become 403:

```csharp
    public async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        await Context.Set<TAggregate>().FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken)
        ?? await ThrowIfOutOfScopeAsync(Context.Set<TAggregate>().Where(e => e.Id.Equals(id)), cancellationToken);

    public async Task<TAggregate?> FirstOrDefaultAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        await ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken)
        ?? await ThrowIfOutOfScopeAsync(ApplySpecification(specification), cancellationToken);

    // Only on a miss, only for scoped aggregates: re-ask with ONLY the scope filter lifted. A hit means the row
    // exists in another tenant/branch/department -> 403, never an accidental 404 (Story 06).
    private static async Task<TAggregate?> ThrowIfOutOfScopeAsync(IQueryable<TAggregate> query, CancellationToken cancellationToken)
    {
        if (typeof(ITenantScoped).IsAssignableFrom(typeof(TAggregate))
            && await query.IgnoreQueryFilters([ModuleDbContext.ScopeFilterName]).AnyAsync(cancellationToken))
        {
            throw new OrganisationScopeViolationException(typeof(TAggregate).Name, "Read");
        }
        return null;
    }
```

`ListAsync`/`CountAsync` are unchanged: a list is simply filtered.

**File: `…/Persistence/Repositories/SpecificationEvaluator.cs`** — after lines 13–16 add:

```csharp
        if (specification.IgnoreOrganisationScopeFilter)
        {
            query = query.IgnoreQueryFilters([ModuleDbContext.ScopeFilterName]);
        }
```

**File: `…/Modules/ModuleDesignTimeDbContextFactory.cs`** — no signature change; each module factory passes
`SystemCurrentUser.Instance` (B11, B10).

**File: `…/DependencyInjection.cs`** — no change (line 35 already registers `CurrentUserService` scoped; EF's
`AddDbContext` resolves the new constructor parameter from DI).

### B5 — Pipeline: `OrganisationScopeBehavior` + transaction rollback

**Create file: `BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/OrganisationScopeBehavior.cs`**

- `public sealed partial class OrganisationScopeBehavior<TRequest, TResponse>(ICurrentUserService currentUser,
  ILogger<OrganisationScopeBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>`.
- `try { return await next(); } catch (OrganisationScopeViolationException ex) when (typeof(Result).IsAssignableFrom(typeof(TResponse)))`
  → `LogRejected(typeof(TRequest).Name, ex.Operation, ex.EntityType, currentUser.UserIdOrSystem, currentUser.DepartmentId ?? "-")`
  (Warning, `"Organisation scope rejected {Operation} of {EntityType} in {RequestName} for user {UserId} (departments {Departments})"`)
  then return a failed `TResponse` for
  `Error.Forbidden("Organisation.OutOfScope", "The record belongs to a department outside your scope.")` — build it
  with the same reflection as `ValidationBehavior.CreateFailureResponse` (lines 53–69; copy, do not share).
- A non-`Result` `TResponse` lets the exception propagate to `GlobalExceptionHandler` (B8).

**File: `…/DependencyInjection.cs`** lines 26–32 — insert after `AuthorizationBehavior`:
`cfg.AddOpenBehavior(typeof(OrganisationScopeBehavior<,>));` and update the order comment to
"validation -> authorisation -> organisation scope -> logging -> transaction -> caching".

**File: `…/Behaviors/TransactionBehavior.cs`** — after the `catch (ConcurrencyConflictException ex)` block
(lines 91–98) add:

```csharp
        catch (OrganisationScopeViolationException)
        {
            // Explicit rollback, then let OrganisationScopeBehavior (outer) turn it into 403.
            pendingIntegrationEvents.DequeueAll();
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
```

### B6 — Authentication, authorisation, and the active-scope middleware (`Crm.BuildingBlocks.Presentation`)

**Create file: `BuildingBlocks/Crm.BuildingBlocks.Presentation/Authorization/CrmPolicies.cs`**

```csharp
public static class CrmPolicies
{
    /// <summary>Authenticated, with the Administrator role in at least one department. Story 07/09 refine it.</summary>
    public const string Administrator = "Administrator";

    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Administrator, policy => policy.RequireAuthenticatedUser().RequireRole(CrmRoles.Administrator))
            .Services;
}
```

**Create file: `…/Scoping/OrganisationScopeMiddleware.cs`** — runs after `UseAuthentication()`:

- `InvokeAsync(HttpContext context, ICurrentUserService currentUser, IProblemDetailsService problems,
  ILocalizationService localization, ILogger<OrganisationScopeMiddleware> logger)`.
- When `currentUser.Scope.ActiveScopeRejected`: log Warning
  `"Rejected X-Crm-Scope {Header} for user {UserId}"`, set 403, write a problem
  (`Type = ProblemTypes.Forbidden`, `Title = ProblemTypes.TitleFor(ProblemTypes.Forbidden, localization)`,
  `Detail = localization.Get(CrmMessageKeys.ProblemDetail.ScopeRejected)`, `Extensions["code"] =
  "Organisation.ActiveScopeRejected"`), and return without calling `next`. Otherwise `await next(context)`.
- Extension `public static IApplicationBuilder UseCrmOrganisationScope(this IApplicationBuilder app) =>
  app.UseMiddleware<OrganisationScopeMiddleware>();`.

**File: `…/ErrorHandling/GlobalExceptionHandler.cs`** — add a switch arm before `_` (line 39):
`OrganisationScopeViolationException scope => OutOfScope(scope),` returning
`Create(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden, localization.Get(CrmMessageKeys.ProblemDetail.OutOfScope), "Organisation.OutOfScope")`
with a `[LoggerMessage]` Warning like the other branches.

**File: `BuildingBlocks/Crm.BuildingBlocks.Application/Localization/CrmMessageKeys.cs`** — add to `ProblemDetail`:

```csharp
        public const string ScopeRejected = "Problem.Detail.ScopeRejected";
        public const string OutOfScope = "Problem.Detail.OutOfScope";
```

**Files: `…/Localization/Resources/CrmMessages.resx` and `CrmMessages.ar.resx`** — two `<data>` rows each:

| Key | English | Arabic |
|---|---|---|
| `Problem.Detail.ScopeRejected` | The selected department is not one of your departments. | القسم المحدد ليس من الأقسام التي تنتمي إليها. |
| `Problem.Detail.OutOfScope` | The record belongs to a department outside your scope. | ينتمي هذا السجل إلى قسم خارج نطاق صلاحياتك. |

### B7 — Host wiring

**File: `backend/src/Crm.Api/Program.cs`**

- After line 28 (`AddCrmRateLimiting`): 

```csharp
    // Story 06: X-Dev-User sign-in, honoured in Development only (the handler returns NoResult elsewhere, so
    // every [Authorize] endpoint answers a 401 challenge, never a 500). Story 07 replaces it with JWT bearer.
    builder.Services.AddAuthentication(DevUserAuthentication.Scheme).AddCrmDevUser();
    builder.Services.AddCrmAuthorization();
```

  The scheme is registered in every environment on purpose: with no default scheme, an `[Authorize]` challenge
  throws "No authenticationScheme was specified" and becomes a 500. The handler (B3) injects `IHostEnvironment` and
  returns `AuthenticateResult.NoResult()` first thing when `!environment.IsDevelopment()`.
- Middleware block (lines 84–113): after `app.UseStatusCodePages();` and the Development block, before
  `app.UseRateLimiter();`:

```csharp
    // 6. authentication, then the active-scope check (403 on a forged X-Crm-Scope), then authorisation.
    app.UseAuthentication();
    app.UseCrmOrganisationScope();
    app.UseAuthorization();
```

  and renumber the ORDER MATTERS comment (rate limiter becomes 7). Add usings
  `Crm.BuildingBlocks.Infrastructure.Identity`, `Crm.BuildingBlocks.Presentation.Authorization`,
  `Crm.BuildingBlocks.Presentation.Scoping`, `Microsoft.AspNetCore.Authentication`.
- **File: `Modules/ModuleCatalog.cs`** line 13 — add the Organisation entry **first** (its seed is the one others
  point at): `new(new Crm.Modules.Organisation.Infrastructure.OrganisationModule(), Crm.Modules.Organisation.Presentation.AssemblyReference.Assembly),`
- **File: `Crm.Api.csproj`** — add ProjectReferences to `..\Modules\Organisation\Crm.Modules.Organisation.Infrastructure\…csproj`
  and `…Presentation\…csproj`.

### B8 — Organisation module: projects

Scaffold by copying the five Sample projects' `.csproj` files (never the code):

| Project | References |
|---|---|
| `Modules/Organisation/Crm.Modules.Organisation.Domain` | `Crm.SharedKernel` |
| `…Contracts` | `Crm.SharedKernel` |
| `…Application` | `Crm.BuildingBlocks.Application`, `…Domain`, `…Contracts` |
| `…Infrastructure` | `Crm.BuildingBlocks.Infrastructure`, `…Application`; `Microsoft.EntityFrameworkCore.Design` exactly as Sample |
| `…Presentation` | `DisableTransitiveProjectReferences` + `Crm.SharedKernel`, `Crm.BuildingBlocks.Application`, `Crm.BuildingBlocks.Presentation`, `…Application` |

Add all five to the solution: `dotnet sln backend/Crm.sln add` with each `.csproj` path (they land under
`src/Modules/Organisation` like Sample). Each Application/Presentation project gets an `AssemblyReference.cs` copied
from Sample.

### B9 — Organisation module: Domain (`Crm.Modules.Organisation.Domain`)

Every aggregate: `sealed`, `AuditableEntity<TId>`, `IHasRowVersion` (`public byte[] RowVersion { get; private set; } = [];`),
private parameterless constructor for EF (as `SampleAggregate` lines 24–27), factory `Create(…, TId? id = null)` so
the seeder passes well-known ids. Invariants throw `DomainException(new DomainError(code, message))` (English).

- **`OrganisationCode.cs`** — value object `Value`; `Create(string value)`: trim, upper-invariant, must match
  `^[A-Z0-9-]{2,20}$` else `Organisation.CodeInvalid`. Constructor parameter named `value` (EF rule, `SampleName`).
- **`LocalizedText.cs`** — value object `Arabic`, `English`; `Create(string arabic, string english)`: both trimmed,
  non-empty (`Organisation.NameRequired`), ≤ 200 (`Organisation.NameTooLong`). Private constructor parameters
  named `arabic`, `english`.
- **`SystemRole.cs`** — `public enum SystemRole { Administrator, Supervisor, Agent }` (names equal `CrmRoles`).
- **`Organisation.cs`** — `Organisation : AuditableEntity<TenantId>, IHasRowVersion` with `Code`, `Name`, `IsActive`.
  `Create(OrganisationCode, LocalizedText, TenantId? id = null)`. No other method in this story.
- **`Branch.cs`** — `Branch : AuditableEntity<BranchId>, ITenantScoped, IHasRowVersion`: `TenantId`, `Code`, `Name`,
  `IsActive`. `Create(TenantId tenantId, OrganisationCode, LocalizedText, BranchId? id = null)` (tenant not
  `TenantId.None`, else `Organisation.TenantRequired`); `Rename(LocalizedText)`; `Deactivate()` (already inactive →
  `Organisation.BranchAlreadyInactive`).
- **`Department.cs`** — `Department : AuditableEntity<DepartmentId>, ITenantScoped, IHasRowVersion`: `TenantId`,
  `BranchId`, `Code`, `Name`, `IsActive`. `Create(Branch branch, OrganisationCode, LocalizedText, DepartmentId? id = null)`:
  `branch.IsActive` else `Organisation.BranchInactive`; tenant copied from the branch. `Rename`, `Deactivate`
  (`Organisation.DepartmentAlreadyInactive`). Reading `Branch` in the factory is a read, not a second aggregate
  mutated.
- **`MembershipId.cs`** — `sealed record MembershipId(Guid Value) : StronglyTypedId(Value)` with `New()`.
- **`Membership.cs`** — `Membership : AuditableEntity<MembershipId>, ITenantScoped, ISoftDeletable, IHasRowVersion`:
  `TenantId`, `UserId` (`Guid`, users arrive in Story 07), `DepartmentId`, `BranchId` (copied from the department at
  creation; departments never move — reparenting is out of scope), `Role` (`SystemRole`), soft-delete trio as
  `SampleAggregate` lines 16–20. `Create(Department department, Guid userId, SystemRole role, MembershipId? id = null)`:
  `userId != Guid.Empty` (`Organisation.UserRequired`), `department.IsActive` (`Organisation.DepartmentInactive`).
  `ChangeRole(SystemRole role)`. Removal is `repository.Remove` (soft delete).

No domain events in this story (no consumer exists).

### B10 — Organisation module: Application (`Crm.Modules.Organisation.Application`)

- **`IOrganisationUnitOfWork.cs`** — `public interface IOrganisationUnitOfWork : IUnitOfWork;`
- **`Dtos.cs`** — primitives only (no SharedKernel id types in DTOs — keeps Swagger/`gen:api` shapes plain):

```csharp
public sealed record LocalizedTextDto(string Ar, string En);
public sealed record BranchDto(Guid Id, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion);
public sealed record DepartmentDto(Guid Id, Guid BranchId, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion);
public sealed record MembershipDto(Guid Id, Guid UserId, Guid DepartmentId, Guid BranchId, string Role, byte[] RowVersion);
public sealed record MyDepartmentDto(Guid DepartmentId, string DepartmentCode, LocalizedTextDto DepartmentName,
    Guid BranchId, LocalizedTextDto BranchName, string Role, bool IsActive);
public sealed record MyScopeDto(Guid UserId, Guid TenantId, Guid? ActiveDepartmentId,
    IReadOnlyList<string> Roles, IReadOnlyList<MyDepartmentDto> Departments);
```

  Each has a static `FromDomain` where it maps an aggregate (as `SampleAggregateDto`).
- **`Specifications/`** — `BranchesSpecification(bool activeOnly)` (ordered by `Code`, `AsReadOnly`),
  `BranchByCodeSpecification(OrganisationCode)`, `DepartmentsSpecification(Guid? branchId, bool activeOnly)`,
  `DepartmentByCodeSpecification(OrganisationCode)`, `DepartmentsByIdsSpecification(IReadOnlyList<DepartmentId>)`,
  `BranchesByIdsSpecification(IReadOnlyList<BranchId>)`, `MembershipsSpecification(Guid? userId, Guid? departmentId)`,
  `ActiveMembershipSpecification(Guid userId, DepartmentId)`, and **`MembershipsOfUserSpecification(Guid userId)`
  — the only caller of `IgnoreOrganisationScope()`** (comment says so). No spec filters by tenant by hand: the
  scope filter does it (that is the point of the story).
- **Commands** (one folder each under `Commands/`, record + handler + validator; handlers inject
  `IRepository<…>`, `IOrganisationUnitOfWork`, `ICurrentUserService` where the tenant is needed):

| Use case | Record | Handler rules |
|---|---|---|
| `CreateBranch` | `(string Code, string NameAr, string NameEn) : ICommand<BranchDto>` | tenant = `currentUser.Scope.Tenant` (`None` → `Error.Forbidden("Organisation.TenantRequired", …)`); code taken (`CountAsync(BranchByCode) > 0`) → `Error.Conflict("Organisation.BranchCodeTaken", …)` |
| `RenameBranch` | `(Guid Id, string NameAr, string NameEn, byte[] RowVersion) : ICommand<BranchDto>` | not found → `Error.NotFound("Organisation.BranchNotFound", …)`; `SetOriginalRowVersion` then `Rename` |
| `DeactivateBranch` | `(Guid Id, byte[] RowVersion) : ICommand<BranchDto>` | as rename, `Deactivate()` |
| `CreateDepartment` | `(Guid BranchId, string Code, string NameAr, string NameEn) : ICommand<DepartmentDto>` | branch not found → `Organisation.BranchNotFound`; code taken → `Organisation.DepartmentCodeTaken` (409) |
| `RenameDepartment` / `DeactivateDepartment` | as the branch pair | `Organisation.DepartmentNotFound` |
| `AddMembership` | `(Guid UserId, Guid DepartmentId, string Role) : ICommand<MembershipDto>` | department not found → 404; existing active `(user, department)` → `Error.Conflict("Organisation.MembershipExists", …)` |
| `ChangeMembershipRole` | `(Guid Id, string Role, byte[] RowVersion) : ICommand<MembershipDto>` | `Organisation.MembershipNotFound` |
| `RemoveMembership` | `(Guid Id) : ICommand` | `repository.Remove` (soft delete) |

  Validators: `Code` `NotEmpty().Matches("^[A-Za-z0-9-]{2,20}$")`; `NameAr`/`NameEn` `NotEmpty().MaximumLength(200)`;
  ids `NotEmpty()`; `RowVersion` `NotEmpty()`; `Role` `Must(r => Enum.TryParse<SystemRole>(r, false, out _))`
  with message "Role must be Administrator, Supervisor, or Agent." Domain guards stay as the second line.
- **Queries** (`Queries/`, `IRequest<Result<…>>`, not cached in this story):

| Use case | Returns | Notes |
|---|---|---|
| `ListBranches(bool ActiveOnly)` | `IReadOnlyList<BranchDto>` | |
| `ListDepartments(Guid? BranchId, bool ActiveOnly)` | `IReadOnlyList<DepartmentDto>` | `ActiveOnly` also drops departments whose branch is inactive (load active branch ids first) |
| `ListMemberships(Guid? UserId, Guid? DepartmentId)` | `IReadOnlyList<MembershipDto>` | |
| `GetMyScope()` | `MyScopeDto` | from `currentUser.Scope` (no memberships → `Error.Forbidden("Organisation.NoMemberships", …)`); names via `DepartmentsByIds` + `BranchesByIds`; `IsActive = department.IsActive && branch.IsActive` |
| `GetMembershipsOfUser(Guid UserId)` | `UserMemberships` (Contracts) | uses `MembershipsOfUserSpecification`; the only scope-lifted read |

- **`OrganisationModuleApi.cs`** — implements the Contracts API by sending `GetMembershipsOfUserQuery`
  (as `SampleModuleApi`).
- **`DevUserDirectory.cs`** — DEVELOPMENT ONLY: `IDevUserDirectory` over `IOrganisationModuleApi`; returns null for
  no memberships; maps to `DevUser` (`Role` = `SystemRole` name). Deleted by Story 07.

### B11 — Organisation module: Contracts, Infrastructure, Presentation

**Contracts (`Crm.Modules.Organisation.Contracts`)**

- **`IOrganisationModuleApi.cs`** — `Task<Result<UserMemberships>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);`
  plus `public sealed record UserMemberships(Guid UserId, Guid TenantId, IReadOnlyList<UserMembership> Memberships);`
  and `public sealed record UserMembership(Guid MembershipId, Guid DepartmentId, Guid BranchId, string Role);`.
  `TenantId` is `Guid.Empty` with an empty list when the user has none (success, not failure).
- **`DevSeed/OrganisationDevSeed.cs`** — the well-known Development ids, primitives, shared with Story 07's user seed
  and the Sample seed:

| Constant | Guid | Meaning |
|---|---|---|
| `TenantId` | `06000000-0000-0000-0000-000000000001` | Organisation `DEMO`, "Demo Organisation" / "المنشأة التجريبية" |
| `RiyadhBranchId` | `06000000-0000-0000-0001-000000000001` | `RYD`, "Riyadh" / "الرياض" |
| `JeddahBranchId` | `06000000-0000-0000-0001-000000000002` | `JED`, "Jeddah" / "جدة" |
| `RiyadhCareDepartmentId` | `06000000-0000-0000-0002-000000000001` | Riyadh, `RYD-CARE`, "Customer Care – Riyadh" / "خدمة العملاء - الرياض" |
| `RiyadhBillingDepartmentId` | `06000000-0000-0000-0002-000000000002` | Riyadh, `RYD-BILL`, "Billing – Riyadh" / "الفوترة - الرياض" |
| `JeddahCareDepartmentId` | `06000000-0000-0000-0002-000000000003` | Jeddah, `JED-CARE`, "Customer Care – Jeddah" / "خدمة العملاء - جدة" |
| `AdminUserId` | `06000000-0000-0000-0003-000000000001` | Administrator in all three departments |
| `SupervisorUserId` | `06000000-0000-0000-0003-000000000002` | Supervisor in `RYD-CARE`, Agent in `RYD-BILL` (two departments, two roles) |
| `AgentAUserId` | `06000000-0000-0000-0003-000000000003` | Agent in `RYD-CARE` |
| `AgentBUserId` | `06000000-0000-0000-0003-000000000004` | Agent in `JED-CARE` (other department **and** other branch) |

  Membership ids `06000000-0000-0000-0004-00000000000N`, N = 1…7 in table order (admin 1–3, supervisor 4–5,
  agentA 6, agentB 7).

**Infrastructure (`Crm.Modules.Organisation.Infrastructure`)**

- **`Persistence/OrganisationDbContext.cs`** — `SchemaName = "organisation"`;
  `OrganisationDbContext(DbContextOptions<OrganisationDbContext> options, ICurrentUserService currentUser) : ModuleDbContext(options, currentUser), IOrganisationUnitOfWork`;
  DbSets `Organisations`, `Branches`, `Departments`, `Memberships`.
- **`Persistence/OrganisationDbContextFactory.cs`** — as Sample's; `Create(options) => new(options, SystemCurrentUser.Instance)`.
- **`Persistence/Configurations/`** — one `IEntityTypeConfiguration` per aggregate, copying
  `SampleAggregateConfiguration`: typed-id key conversion + `ValueGeneratedNever()`; `OwnsOne(Code)` → column `Code`
  (max 20); `OwnsOne(Name)` → columns `NameAr`, `NameEn` (max 200, required); `RowVersion` `IsRowVersion()`;
  `Role` `HasConversion<string>().HasMaxLength(20)`. **Do not** configure a conversion for `TenantId`/`BranchId`/
  `DepartmentId` — the convention in B4 does it. Indexes: `Branch` unique `(TenantId, Code)`; `Department` unique
  `(TenantId, Code)` and `(TenantId, BranchId)`; `Membership` unique `(UserId, DepartmentId)` with
  `.HasFilter("[IsDeleted] = 0")`, plus `(TenantId, DepartmentId)` and `IsDeleted`. **`Code` is mapped with
  `HasConversion(c => c.Value, v => OrganisationCode.Create(v)).HasColumnName("Code").HasMaxLength(20)`, not
  `OwnsOne`**: a composite unique index cannot span an owner column and an owned column, and
  `HasIndex(x => new { x.TenantId, x.Code }).IsUnique()` needs both on the owner. `Name` stays `OwnsOne`. No FKs
  between the aggregates (ids only).
- **Migration:** `pwsh backend/scripts/ef.ps1 -Module Organisation migrations add Initial` (outbox tables come from
  `ModuleDbContext`). Read the generated file: schema `organisation`, no `dbo` object, filtered unique index present.
- **`Persistence/OrganisationSeeder.cs`** — `IModuleSeeder`, idempotent per row (check each well-known id with
  `AnyAsync`; seeders run with no HttpContext → unrestricted, so no filter lifting is needed; add
  `IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName])` on the membership check so a removed seed membership
  is not re-inserted over its soft-deleted row — the filtered unique index would allow it, but the id would clash).
  Creates the rows of the dev-seed table through the domain factories with the well-known ids.
- **`OrganisationModule.cs`** — as `SampleModule`: `AddModuleDbContext<OrganisationDbContext, IOrganisationUnitOfWork>`,
  `AddRepository<…>` for all four aggregates, `IModuleSeeder`, `IOrganisationModuleApi`, and
  `services.AddScoped<IDevUserDirectory, DevUserDirectory>();` (harmless outside Development: nothing calls it).

**Presentation (`Crm.Modules.Organisation.Presentation`)** — `ApiControllerBase`, `[Area(…)]`, the Sample
controller style (`ProducesResponseType` per status):

| Controller (Area) | Auth | Actions |
|---|---|---|
| `Controllers/Admin/BranchesController` | `[Authorize(Policy = CrmPolicies.Administrator)]` | `GET ?activeOnly=false`, `POST`, `PUT {id}/name` (`{nameAr,nameEn,rowVersion}`), `POST {id}/deactivate` (`{rowVersion}`) |
| `Controllers/Admin/DepartmentsController` | same | `GET ?branchId&activeOnly`, `POST` (`{branchId,code,nameAr,nameEn}`), `PUT {id}/name`, `POST {id}/deactivate` |
| `Controllers/Admin/MembershipsController` | same | `GET ?userId&departmentId`, `POST` (`{userId,departmentId,role}`), `PUT {id}/role` (`{role,rowVersion}`), `DELETE {id}` (204) |
| `Controllers/Agent/MyScopeController` | `[Authorize]` | `GET` → `MyScopeDto` |

Request records next to each controller (as `CreateSampleRequest`). Controller names are unique per Area (no
`Samples`/`Diagnostics` clash).

### B12 — Sample becomes the first organisation-scoped aggregate

- **`Crm.Modules.Sample.Domain/SampleAggregate.cs`** — implement `IOrganisationScoped`; add
  `public TenantId TenantId { get; private set; } = TenantId.None;` and `BranchId`, `DepartmentId` likewise
  (initialise to `new BranchId(Guid.Empty)` / `new DepartmentId(Guid.Empty)` for the EF constructor). `Create` gains
  `TenantId tenantId, BranchId branchId, DepartmentId departmentId` **before** `SampleId? id = null`; guard
  `tenantId != TenantId.None` (`Sample.ScopeRequired`).
- **`…Application/Commands/CreateSample/CreateSampleCommand.cs`** — add `Guid? DepartmentId` (last parameter).
  **Handler** — first: `ScopeWriteTarget? target = currentUser.Scope.ResolveWriteTarget(request.DepartmentId);`
  null with a requested department → `Error.Forbidden("Organisation.OutOfScope", …)`; null otherwise →
  `Error.Validation("Organisation.DepartmentRequired", "DepartmentId: Choose the department this record belongs to.")`
  (the `Field: message` shape `ValidationErrorParser` reads). Then `Create(…, target.TenantId, target.BranchId, target.DepartmentId)`.
- **`…Application/SampleAggregateDto.cs`** — add `Guid TenantId, Guid BranchId, Guid DepartmentId` (primitives).
- **New use case `Queries/ListSamples/`** — `ListSamplesQuery() : IRequest<Result<IReadOnlyList<SampleAggregateDto>>>`,
  handler over `SamplesSpecification` (`AsReadOnly()`, `ApplyOrderByDescending(s => s.CreatedAtUtc!)` is **not**
  allowed — use `ApplyOrderByDescending(s => s.CreatedAtUtc ?? DateTimeOffset.MinValue)`, then `ApplyPaging(0, 100)`). **The spec has no scope
  criteria — it is the "naive new query" of the acceptance criterion.**
- **`…Presentation/Controllers/Admin/SamplesController.cs`** — `CreateSampleRequest` (line 50) gains
  `Guid? DepartmentId = null`; pass it through (line 26); add `[HttpGet] List`.
- **`…Infrastructure/Persistence/Configurations/SampleAggregateConfiguration.cs`** — add
  `builder.HasIndex(x => new { x.TenantId, x.DepartmentId });` (no conversion lines — convention).
- **`…Infrastructure/Persistence/SampleDbContext.cs` / `SampleDbContextFactory.cs`** — constructor gains
  `ICurrentUserService currentUser`; factory passes `SystemCurrentUser.Instance`.
- **`…Infrastructure/Persistence/SampleSeeder.cs`** lines 25–28 — pass `OrganisationDevSeed.TenantId`,
  `RiyadhBranchId`, `RiyadhCareDepartmentId` (wrapped in the typed ids). **`…Infrastructure.csproj`** — add a
  ProjectReference to `..\..\Organisation\Crm.Modules.Organisation.Contracts\Crm.Modules.Organisation.Contracts.csproj`.
- **Migration:** `pwsh backend/scripts/ef.ps1 -Module Sample migrations add AddOrganisationScope`. Edit its `Up` to
  back-fill existing rows **after** the `AddColumn` calls (R7) — Sample is demo-only and deleted in Story 11:

```csharp
            // Story 06: rows created before scoping belong to the dev seed's Riyadh Customer Care department.
            migrationBuilder.Sql(
                "UPDATE [sample].[Samples] SET TenantId = '06000000-0000-0000-0000-000000000001', " +
                "BranchId = '06000000-0000-0000-0001-000000000001', DepartmentId = '06000000-0000-0000-0002-000000000001' " +
                "WHERE TenantId = '00000000-0000-0000-0000-000000000000';");
```

### B13 — Request samples

**Create file: `backend/requests/organisation.http`** (`@host = http://localhost:5187`, header comment like
`localization.http`; variables `@admin`, `@supervisor`, `@agentA`, `@agentB`, `@ryCare`, `@ryBill`, `@jeCare` with
the dev-seed Guids). Each request has a `###` line stating the expected status and body. Minimum set:

1. `GET /api/Agent/MyScope` as `admin` → 200, three departments, roles `["Administrator"]`.
2. Same, `Accept-Language: ar` → names unchanged (both languages are always returned).
3. As `supervisor` → two departments with roles Supervisor/Agent (R4, R5).
4. As `supervisor` + `X-Crm-Scope: {{ryBill}}` → `activeDepartmentId` = ryBill.
5. As `agentA` + `X-Crm-Scope: {{jeCare}}` → **403** `urn:crm:problem:forbidden`, code `Organisation.ActiveScopeRejected` (forged header AC).
6. As `agentA` + `X-Crm-Scope: not-a-guid` → 403 (same code).
7. No `X-Dev-User`, `GET /api/Agent/MyScope` → 401 problem.
8. No `X-Dev-User`, `GET /api/Admin/Branches` → 401 (R6).
9. As `agentA`, `GET /api/Admin/Branches` → 403 (not an administrator) (R6).
10. As `admin`: `GET /api/Admin/Branches` → 2 rows; `POST` `{ "code": "dmm", "nameAr": "الدمام", "nameEn": "Dammam" }` → 200, code `DMM`; repeat → 409 `Organisation.BranchCodeTaken`; `PUT …/{id}/name` → 200; `POST …/{id}/deactivate` → 200 `isActive: false`; again → 422 `Organisation.BranchAlreadyInactive`; `GET ?activeOnly=true` → Dammam absent, `GET` → present.
11. As `admin`: `POST /api/Admin/Departments` on the deactivated branch → 422 `Organisation.BranchInactive`; on Riyadh → 200; rename; deactivate; `GET ?activeOnly=true` hides it.
12. Validation: `POST /api/Admin/Branches` `{ "code": "x", "nameAr": "", "nameEn": "" }` + `Accept-Language: ar` → 400 with Arabic messages for `Code`, `NameAr`, `NameEn`.
13. Memberships as `admin`: `GET ?userId={{supervisor}}` → 2; `POST` duplicate `{supervisor, ryCare, Agent}` → 409 `Organisation.MembershipExists`; `PUT …/{id}/role` → 200.
14. **Cross-department matrix setup** — as `agentA`, `POST /api/Admin/Samples` `{ "name": "A only", "lines": [] }` → 200, `departmentId` = ryCare (capture `@sampleA`, `@rowA`).
15. As `agentB`, `POST` same with `"departmentId": "{{ryCare}}"` → **403** `Organisation.OutOfScope` (write into a foreign department).
16. As `agentB`, `GET /api/Admin/Samples/{{sampleA}}` → **403** `Organisation.OutOfScope` (read).
17. As `agentB`, `PUT /api/Admin/Samples/{{sampleA}}` `{ name, rowVersion }` → **403** (update).
18. As `agentB`, `DELETE /api/Admin/Samples/{{sampleA}}` → **403**.
19. As `agentA`, `GET /api/Admin/Samples/00000000-0000-0000-0000-00000000dead` → **404** (a real miss stays 404).
20. As `agentA`, `GET /api/Admin/Samples` → contains "A only", nothing from Jeddah. **Read the EF log: `WHERE` has `[s].[IsDeleted] = CAST(0 AS bit)`, `[s].[TenantId] = @…`, `[s].[BranchId] IN (…)`, `[s].[DepartmentId] IN (…)`** (naive-query AC; R1–R3).
21. As `agentB`, `GET /api/Admin/Samples` → does not contain "A only" (R2).
22. As `supervisor`, create one sample with `X-Crm-Scope: {{ryBill}}` (lands in ryBill), then `GET /api/Admin/Samples`: with no header → both "A only" and the billing one (union AC); with `X-Crm-Scope: {{ryCare}}` → only ryCare rows; with `{{ryBill}}` → only the billing row.
23. As `supervisor` with no header, `POST /api/Admin/Samples` without `departmentId` → 400 `Organisation.DepartmentRequired` (ambiguous target).
24. **Soft-deleted rows stay hidden when scope is lifted:** as `admin`, `DELETE /api/Admin/Memberships/{{supervisorBillingMembershipId}}` → 204; then request 3 again → only ryCare; the EF log of the sign-in lookup shows `[m].[IsDeleted] = CAST(0 AS bit)` and **no** `TenantId` predicate. Then `POST /api/Admin/Memberships` `{supervisor, ryBill, Agent}` to restore (the filtered unique index allows it).

**Files: `backend/requests/persistence.http`, `diagnostics.http`, `localization.http`** — add
`X-Dev-User: 06000000-0000-0000-0003-000000000003` (agentA) to every `/api/Admin/Samples` request, with one comment
line at the top of each file ("Story 06: Sample rows are department-scoped; requests run as agentA").
Expected results are otherwise unchanged.

### B14 — Documentation

- **`CLAUDE.md`**: "Scoping" bullet (~line 195) — replace with: markers `ITenantScoped`/`IOrganisationScoped`
  (`Crm.SharedKernel.Scoping`); `ModuleDbContext.ScopeFilterName`, applied automatically; write guard in
  `ModuleDbContext.OrganisationScope.cs`; out-of-scope read/update → 403 `Organisation.OutOfScope`; the one lift is
  `Specification.IgnoreOrganisationScope()` (sign-in lookup only); `X-Crm-Scope` narrows only; creating a scoped row
  resolves its department with `OrganisationScope.ResolveWriteTarget`. Pipeline bullet: add "organisation scope"
  after authorisation. Shared kernel bullet: "org-scope ids once Story 06 adds them" → "org-scope ids
  (`TenantId`, `BranchId`, `DepartmentId`) and the scope markers".
- **`backend/README.md`**: a short "Organisation scope and dev login" section — the four dev users, `X-Dev-User`
  (Development only), `X-Crm-Scope`, `organisation.http`.
- **`backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md`**: Admin Area — Organisation's controllers
  carry `CrmPolicies.Administrator`; replace "unnamed IgnoreQueryFilters() escape hatch … is permitted" (line 27) with
  "no Area lifts the scope filter; the only lift is the sign-in membership lookup".

---

## Frontend Tasks

All paths are under `frontend/` unless stated. Use the **Write tool** for files with Arabic text. Logical utilities
only; every string a key.

### F1 — Regenerate the API types

With the API running in Development: `pnpm --dir frontend gen:api`. Commit `openapi/{agent,admin}.json` and
`app/api/generated/{agent,admin}.ts`. Never hand-write a DTO type; import `components['schemas']['MyScopeDto']` etc.

### F2 — Headers on every call

**File: `app/plugins/api.ts`** — in the plugin body, before `createCrmFetch` (composables are only safe here, not
inside `baseFetch`):

```ts
  const scopeCookie = useCookie<string | null>('crm-scope', { sameSite: 'lax', maxAge: 60 * 60 * 24 * 365 })
  // DEVELOPMENT ONLY (Story 06): the dev login's user. Story 07 deletes this and /_dev/login.
  const devUserCookie = useCookie<string | null>('crm-dev-user', { sameSite: 'lax' })
```

and inside `baseFetch` after the `Accept-Language` line:

```ts
      if (scopeCookie.value) headers.set('X-Crm-Scope', scopeCookie.value)
      if (import.meta.dev && devUserCookie.value) headers.set('X-Dev-User', devUserCookie.value)
```

The cookie names are also exported as constants from `app/utils/scope.ts` (F3) and used here.

### F3 — Scope store, composable, switcher

- **Create file: `app/utils/scope.ts`** — `export const scopeCookieName = 'crm-scope'`,
  `export const allDepartments = 'all'`, `export const devUserCookieName = 'crm-dev-user'`, and
  `localizedName(text: { ar: string; en: string }, locale: string): string` (`ar` → `text.ar`, else `text.en`).
- **Create file: `app/stores/scope.ts`** (Pinia — the scope is shared across every route) — state `myScope:
  MyScopeDto | null`, `status: 'idle' | 'loading' | 'ready' | 'error'`; getters `isAdministrator`
  (`roles.includes('Administrator')`), `activeDepartments` (only `isActive`); action `setActive(value: string)`
  writes the `crm-scope` cookie (`useCookie`) and calls `refreshNuxtData()` so every list refetches.
- **Create file: `app/composables/useMyScope.ts`** — loads `GET /api/Agent/MyScope` through `useApi().call(…,
  { toastOnError: false })` into the store. **Self-healing:** on a 403 whose `code` is
  `Organisation.ActiveScopeRejected` (a stale cookie after a membership was removed) set the cookie to `all` and
  retry once.
- **Create file: `app/components/shell/ScopeSwitcher.vue`** — `UiSelect` (label `t('organisation.scope.label')`,
  visually hidden via the kit's label prop as the kit allows) with options `all` → `t('organisation.scope.all')` and
  one per active department → `t('organisation.scope.option', { department, branch })` (localised names). Hidden
  (`v-if`) while loading or when the user has one department; a `UiSpinner` while loading; on error nothing but a
  retry button is shown in the topbar (`t('organisation.load.retry')`).
- **File: `app/layouts/agent.vue`** — call `useMyScope()` in `<script setup>`; put `<ShellScopeSwitcher />` first in
  the topbar group (line 37); `agentNav` (line 88) adds `{ to: '/admin/branches', label: t('organisation.nav.branches') }`
  and `{ to: '/admin/departments', label: t('organisation.nav.departments') }` when `scope.isAdministrator`.

### F4 — Admin screens

**File: `nuxt.config.ts`** `routeRules` — add `'/admin/**': { ssr: false }` (an SPA, like `/agent/**`).

**Create files: `app/composables/useBranches.ts`, `app/composables/useDepartments.ts`** — one composable per use
case group, over `useApi().clients.admin`: `list(activeOnly)`, `create(body)`, `rename(id, body)`,
`deactivate(id, rowVersion)`, each returning `ApiResult`. Lists use `useAsyncData` keys `admin-branches` /
`admin-departments` so the scope switcher's `refreshNuxtData()` reaches them.

**Create files: `app/pages/admin/branches.vue`, `app/pages/admin/departments.vue`** (`definePageMeta({ layout:
'agent' })`):

- Header: title, "Show inactive" checkbox (default off → `activeOnly=true`), "New branch"/"New department" button.
- `UiTable` columns: code (`<bdi dir="ltr">`), name (`localizedName(row.name, locale)`), branch (departments only,
  localised through the branches list), status `UiBadge` (success "Active" / neutral "Inactive"), actions (Rename,
  Deactivate — hidden for inactive rows).
- States: loading (`UiTable :loading` / `UiSkeleton`), empty (`UiEmptyState`), error (`UiErrorState` + retry).
- Create/rename in a `UiModal`: `UiInput` code (create only, `dir="ltr"`), name (English, `dir="ltr"`), name
  (Arabic, `dir="rtl" lang="ar"`); departments add a `UiSelect` of **active** branches. 400 → `useFormErrors().apply`
  (fields `code`, `nameAr`, `nameEn`, `branchId`), never a toast; 409/422 toast through `useApi` with
  `errors.code.*` text.
- Deactivate asks a confirmation `UiModal` (`t('organisation.confirm.deactivate', { name })`), then refreshes.
- A non-administrator who opens the URL gets the 403 problem → `UiErrorState` with `errors.problem.forbidden`.

### F5 — Dev login (Development only, `/_dev/**`)

**Create file: `app/pages/_dev/login.vue`** — four buttons (Admin, Supervisor, Agent A — Riyadh care, Agent B —
Jeddah care) with the dev-seed Guids hard-coded in the page (dev-only English, exempt from the bare-string rule),
the current `crm-dev-user` value, and "Sign out" (clears it). Choosing a user sets `crm-dev-user`, resets
`crm-scope` to `all`, and navigates to `/agent`. `modules/dev-only-pages.ts` already strips `/_dev/**` from
production builds — no change there.

**File: `app/pages/login.vue`** — add `<NuxtLink v-if="isDev" to="/_dev/login">{{ t('organisation.devLogin') }}</NuxtLink>`
with `const isDev = import.meta.dev`.

### F6 — Copy

**Create files: `i18n/locales/en/organisation.json` and `i18n/locales/ar/organisation.json`** (top-level key
`organisation`):

| Key | en | ar |
|---|---|---|
| `nav.branches` / `nav.departments` | Branches / Departments | الفروع / الأقسام |
| `scope.label` | Department scope | نطاق القسم |
| `scope.all` | All my departments | جميع أقسامي |
| `scope.option` | {department} · {branch} | {department} · {branch} |
| `branches.title` / `branches.create` / `branches.empty` | Branches / New branch / No branches yet | الفروع / فرع جديد / لا توجد فروع بعد |
| `departments.title` / `departments.create` / `departments.empty` | Departments / New department / No departments yet | الأقسام / قسم جديد / لا توجد أقسام بعد |
| `fields.code` / `nameEn` / `nameAr` / `branch` / `status` / `actions` | Code / Name (English) / Name (Arabic) / Branch / Status / Actions | الرمز / الاسم (بالإنجليزية) / الاسم (بالعربية) / الفرع / الحالة / الإجراءات |
| `status.active` / `status.inactive` | Active / Inactive | نشط / غير نشط |
| `actions.rename` / `deactivate` / `save` / `cancel` / `close` / `showInactive` | Rename / Deactivate / Save / Cancel / Close / Show inactive | إعادة التسمية / إلغاء التفعيل / حفظ / إلغاء / إغلاق / إظهار غير النشط |
| `confirm.deactivate` | Deactivate {name}? It disappears from pickers; existing records keep it. | إلغاء تفعيل {name}؟ سيختفي من القوائم وتبقى السجلات الحالية مرتبطة به. |
| `load.error` / `load.retry` | Could not load the data / Try again | تعذّر تحميل البيانات / حاول مرة أخرى |
| `devLogin` | Development sign-in | تسجيل دخول التطوير |

**Files: `i18n/locales/{en,ar}/errors.json`** — fill `errors.code` (nested by the dot in the code):
`Organisation.OutOfScope` ("This record belongs to a department outside your scope." / "ينتمي هذا السجل إلى قسم
خارج نطاق صلاحياتك."), `ActiveScopeRejected` ("The selected department is not one of yours." / "القسم المحدد
ليس من أقسامك."), `DepartmentRequired` ("Choose a department first." / "اختر القسم أولاً."), `BranchCodeTaken` /
`DepartmentCodeTaken` ("That code is already in use." / "هذا الرمز مستخدم بالفعل."), `BranchInactive` ("The branch is
inactive." / "الفرع غير نشط."), `BranchAlreadyInactive` / `DepartmentAlreadyInactive` ("It is already inactive." /
"غير نشط بالفعل."), `MembershipExists` ("The user already belongs to this department." / "المستخدم عضو في هذا القسم
بالفعل."). Confirm `problemMessage` resolves `errors.code.Organisation.OutOfScope` (vue-i18n nests on `.`).

---

## Edge Cases & Failure Modes

- **Unnamed `HasQueryFilter` used anywhere** → replaces the soft-delete filter and resurrects deleted rows.
  Enforced by B4 using only the named overload; grep `HasQueryFilter(` in `backend/src` before commit — every hit
  passes a filter name.
- **No `HttpContext` ≠ anonymous.** Seeders/outbox consumers get `Unrestricted`; an anonymous HTTP request gets
  `None`. A background job that should be scoped does not exist yet; when one is added it must carry a scope
  explicitly. Enforced in `CurrentUserService` (B3).
- **Scope poisoned during authentication** (R4) → recompute per principal instance (B3).
- **Forged, stale, or malformed `X-Crm-Scope`** → 403 from `OrganisationScopeMiddleware` before any endpoint,
  logged. The frontend self-heals a stale cookie (F3). The header can never widen: parsing only accepts a member
  department.
- **Out-of-scope id vs real miss** → the repository re-asks with only the scope filter lifted (B4): exists → 403,
  otherwise 404. A soft-deleted row in another department still returns 404 (soft-delete filter stays).
- **Existence oracle.** 403 on an out-of-scope id confirms that the id exists. Accepted by the intake ("never an
  accidental 404"); ids are random Guids.
- **Create with a foreign `departmentId`** → `ResolveWriteTarget` returns null → 403. A handler that forgets it and
  stamps a foreign department hits the SaveChanges write guard → 403 anyway (B4 backstop).
- **Ambiguous create** ("all" active, several departments, no `departmentId`) → 400 `Organisation.DepartmentRequired`.
- **Handler forgets the repository and queries by hand** → impossible in Application (no EF reference); every
  path goes through `Repository`/`SpecificationEvaluator`, both filtered.
- **Cache leak across users** → the key's `d`/`b` segments hold the full effective sets (B3); `VaryByUser` stays the
  per-query opt-in.
- **Deactivated department/branch** → rows and memberships intact; hidden from `activeOnly` lists, the scope
  switcher, and the department form's branch picker; creating a department in an inactive branch or a membership in
  an inactive department → 422.
- **Removed membership** → soft-deleted; the sign-in lookup keeps the soft-delete filter, so access disappears on
  the next request (dev login re-reads memberships per request). Re-adding creates a new row (filtered unique index).
- **Duplicate code race** → the pre-check returns 409; two concurrent creates can still reach the unique index and
  surface as 500. Accepted for the MVP (admin-only, rare).
- **Tenant with no memberships / unknown dev user** → dev handler `Fail` → 401 on `[Authorize]` endpoints; anonymous
  endpoints see nothing scoped.
- **`X-Dev-User` outside Development** → the handler returns `NoResult` before reading the header; every
  `[Authorize]` endpoint is a 401 challenge (not a 500). Verified by reading the handler, not by running Production.
- **Migrations on an existing dev database** → Sample back-fill (R7). `docker compose down -v` is the reset.
- **Arabic names with Latin codes** → codes render in `<bdi dir="ltr">`; name inputs carry explicit `dir`.
- **Very many departments** → the SQL `IN` list and the cache key grow linearly; fine for an organisation's
  department count, noted for later.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the build's `CRM0001` rules,
`backend/requests/organisation.http`, and the browser walkthrough replace the suite. Run every row; record results
(and which R1 path shipped) in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001` |
| 2 | Boundaries | read the five Organisation `.csproj` files and `Sample.Infrastructure.csproj` | only rule-compliant references (Sample → Organisation.Contracts only) |
| 3 | Migrations | read `Organisation/…/Migrations/*_Initial.cs` and `Sample/…/*_AddOrganisationScope.cs` | schema `organisation` only; filtered unique index; back-fill SQL after `AddColumn` |
| 4 | Risks R1–R7 | `organisation.http` 3, 8, 9, 20, 21 + the R7 SQL | as stated in Prerequisites |
| 5 | Forged scope (AC) | requests 5, 6 | 403, code `Organisation.ActiveScopeRejected`, a Warning log line |
| 6 | Cross-department matrix (AC) | requests 14–19 | 403 on foreign create/read/update/delete; 404 on a real miss |
| 7 | Union vs one department (AC) | request 22 | union with no header; one department's rows when active |
| 8 | Naive query scoped (AC) | request 20 + EF log | three scope predicates **and** the soft-delete predicate |
| 9 | Lift keeps soft delete (AC) | request 24 + EF log | removed membership gone; `IsDeleted` predicate present, no tenant predicate |
| 10 | Admin CRUD (AC) | requests 10–13 | create/rename/deactivate; inactive hidden with `activeOnly` |
| 11 | Old request files | `persistence.http`, `diagnostics.http`, `localization.http` with `X-Dev-User` | same results as before this story |
| 12 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0; lint prints `check-locales: 3 domain files, en/ar key sets match` |
| 13 | Dev login | `/_dev/login` → Admin | `/agent` loads; switcher lists three departments; admin nav shows Branches/Departments |
| 14 | Scope switcher | as Supervisor, switch to Billing; reload | Network shows `X-Crm-Scope` on API calls; cookie `crm-scope` persists across reload |
| 15 | Stale scope heals | as Supervisor pick Billing, remove that membership (request 24), reload `/agent` | switcher falls back to "All my departments", no error screen |
| 16 | Admin screens (AC) | `/admin/branches`, `/admin/departments` as Admin | create, rename, deactivate; deactivated row hidden until "Show inactive"; the department form's branch picker omits inactive branches |
| 17 | States | throttle to offline; empty tenant view by filtering; a 400 | error state with retry; empty state; field errors under inputs, no toast |
| 18 | Non-admin | `/admin/branches` as Agent A | forbidden error state; no admin nav items |
| 19 | Locales + widths (AC) | rows 13–18 in `en` and `ar` at 375 / 768 / 1280 | names in the active locale, RTL mirrored, no horizontal scroll (`document.documentElement.scrollWidth === innerWidth`) |
| 20 | Production strip | `pnpm --dir frontend build`; `grep -rl "crm-dev-user" frontend/.output/public` | the `/_dev/login` page is absent; the header code is dead in production (`import.meta.dev`) |

---

## Migration / Rollback

- Two migrations: `Organisation/Initial` (new schema) and `Sample/AddOrganisationScope` (three non-null
  `uniqueidentifier` columns + index + back-fill). Both apply automatically in Development at startup.
- **Rollback:** `pwsh backend/scripts/ef.ps1 -Module Sample database update 20260928011548_AddOutbox`, then
  `ef.ps1 -Module Organisation database update 0`, then revert the commit. Or `docker compose -f
  backend/docker-compose.yml down -v` (no production data exists).
- **Half-applied state:** if the Organisation migration applied but Sample's failed, the API fails at startup in
  `MigrateAndSeedModulesAsync` (loud, not silent). Fix the migration and restart; never hand-edit
  `__EFMigrationsHistory`.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/Crm.sln` → Test Plan rows 1–3.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` → prove R1–R7 first, then run
   `backend/requests/organisation.http` top to bottom → rows 4–10; re-run the three older `.http` files → row 11.
3. **Frontend types:** with the API up, `pnpm --dir frontend gen:api` → only `agent`/`admin` snapshots and generated
   files change.
4. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` →
   rows 12, 20.
5. **Frontend runs:** `pnpm --dir frontend dev` with the API up → rows 13–19 in Edge, `en` and `ar`, 375/768/1280.
6. **Regression:** `/_dev/api` Fake transport scenarios A–E still PASS (the `baseFetch` headers must not change the
   401 contract); `grep -rn "HasQueryFilter(" backend/src` shows only named calls; `grep -rn "IgnoreOrganisationScope()" backend/src`
   shows the definition and exactly one caller.

---

## Done Criteria

- [ ] An agent in department A cannot read, list, or update an entity in department B; the attempt returns 403
      (requests 14–18, 21).
- [ ] A user in two departments sees the union with "all my departments" active and one department's data when
      that department is active (request 22, row 14).
- [ ] Scope filtering is automatic: the naive `ListSamples` query's EF SQL shows the tenant, branch, and department
      predicates (request 20).
- [ ] The scope filter is the second **named** filter (`ModuleDbContext.ScopeFilterName`); soft-deleted rows stay
      hidden when scope is lifted in the one permitted path, the sign-in membership lookup (request 24).
- [ ] Admin can create, rename, and deactivate branches and departments; deactivated ones disappear from pickers
      without breaking existing records (requests 10–11, row 16).
- [ ] A forged `X-Crm-Scope` naming a department the caller does not belong to returns 403 (requests 5–6).
- [ ] Branch and department names render in the active locale; screens walked in `en` and `ar` at 375/768/1280
      (row 19).
- [ ] Every endpoint and the cross-department matrix are recorded in `backend/requests/organisation.http`.
- [ ] Memberships: one user in several departments with a different role in each (supervisor seed); Admin endpoints
      add, change, and remove memberships; `IOrganisationModuleApi.GetMembershipsAsync(userId)` exists in Contracts.
- [ ] Development-only `X-Dev-User` login works for the four seeded users and is not registered outside Development.
- [ ] Every rejection (forged header, out-of-scope read/write, dev-login failure) writes a Warning log line.
- [ ] `dotnet build backend/Crm.sln` is clean with 0 warnings and no `CRM0001`; both migrations are included;
      `pnpm typecheck`/`lint`/`build` are clean; `gen:api` output committed.
- [ ] Server-side resource keys are constants in `CrmMessageKeys` (no string-literal keys); `en`/`ar` resx rows added.
- [ ] `CLAUDE.md`, `backend/README.md`, and `AREAS.md` updated (B14).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**
