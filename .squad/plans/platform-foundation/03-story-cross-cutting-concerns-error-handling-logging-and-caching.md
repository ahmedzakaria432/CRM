# Story 03 — Cross cutting concerns error handling logging and caching

## Prerequisites

- **Story 01 and Story 02 are implemented and committed**, and the backend has been restructured into a
  **modular monolith** (commit `0b84cee`, "Restructure backend into a modular monolith"). Every path in this plan is
  the post-restructure layout: `backend/src/BuildingBlocks/*` and `backend/src/Modules/Sample/*`. Stories 01 and 02
  plans are historical; read [`00-overview.md`](00-overview.md), "Architecture change — modular monolith", before
  starting.
- **Module:** **BuildingBlocks** (cross-cutting). The **`Sample`** template module is where every mechanism is
  demonstrated and verified: it publishes the first integration event, subscribes to it, and carries the first
  cacheable query. No new module is created.
- This story **replaces three placeholders** Story 02 left on purpose:
  - `backend/src/Crm.Api/Program.cs` line 73 — `app.UseExceptionHandler(); // Story 03 replaces this…`
  - `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ResultActionResultExtensions.cs` lines 7–10 —
    "Story 03 replaces this with the global exception handler and a static class of stable RFC 9457 `type` URIs".
  - `CLAUDE.md` "In-process bus and outbox are delivered by Story 03" — this story delivers them.
- **Verified on this machine during planning (2026-09-28)** — re-check before starting:
  - .NET SDK **10.0.401** (`global.json` pins `10.0.301` with `rollForward: latestFeature`).
  - Docker **29.6.2**; `crm-sqlserver` running on **14330**. Ports **16379, 14317, 18888** are free.
  - Latest **stable** packages on `api.nuget.org` (flat-container index), chosen below:
    `Serilog.AspNetCore` **10.0.0**, `Serilog.Sinks.OpenTelemetry` **4.2.0**,
    `OpenTelemetry.Extensions.Hosting` **1.19.1**, `OpenTelemetry.Exporter.OpenTelemetryProtocol` **1.19.1**,
    `OpenTelemetry.Instrumentation.AspNetCore` / `.Http` / `.Runtime` **1.19.0**,
    `OpenTelemetry.Instrumentation.EntityFrameworkCore` **1.19.1-beta.1** (**only ever shipped as beta** — the one
    prerelease pin in the solution), `Microsoft.Extensions.Http.Resilience` **10.10.0**,
    `Microsoft.Extensions.Caching.StackExchangeRedis` **10.0.11** (matches the EF Core pin), `Polly.Core` **8.6.6**.
  - `Microsoft.Extensions.Http.Resilience` 10.10.0 depends on `Microsoft.Extensions.*` **≥ 10.0.12**, while
    `Directory.Packages.props` pins `Microsoft.Extensions.Logging.Abstractions` **10.0.11**. See Edge Cases for the
    one sanctioned fix if restore reports NU1605.
- **Probe run during planning** — a throwaway `net10.0` web app in the session scratchpad, with the packages
  above restored together under `TreatWarningsAsErrors`. Results that shaped the design are marked **Probe:**
  inline:
  1. `ControllerBase.Problem(...)` goes through `DefaultProblemDetailsFactory`, which **does** apply
     `ProblemDetailsOptions.CustomizeProblemDetails` (the `correlationId` extension appeared on the body).
  2. `UseStatusCodePages()` after `AddProblemDetails()` turns a bare 404 into `application/problem+json`, also
     customised.
  3. A `GlobalLimiter` partitioned on `ctx.GetEndpoint()?.Metadata.GetMetadata<AreaAttribute>()` limits **only**
     the `Public` Area: 3rd call → **429** with `Retry-After: 30`; Admin unaffected.
  4. A Serilog `IDestructuringPolicy` redacts nested properties of an object logged with a **`[LoggerMessage]`**
     template `{@Payload}` — `Password`, `ApiKey`, nested `ClientSecret` all rendered `"***"`.
  5. **.NET 10 does not log an exception an `IExceptionHandler` handled** — the probe's handled throw produced no
     log line at all. The handler must log the exception itself or the 500 is invisible.
- **Coordination:**
  - **Story 06** adds tenant/department/branch scoping. This story adds `TenantId`/`DepartmentId`/`BranchId` to
    `ICurrentUserService` (read from claims, `null` until then) and **prefixes every cache key with that scope**.
    Story 06 fills the values; it must not change the cache key shape.
  - **Story 07** issues the JWT. This story fixes the claim names in `CrmClaimTypes` (Task 5). Story 07 issues
    tokens with exactly those names, and re-verifies the "log carries user/tenant/department/branch" criterion with a
    real token.
  - **Story 22** (channel outbound outbox) and every story that reacts to another module's change **depend on
    Task 11**. It is the only cross-module write path.

---

## Story Goal

Give every later story an observability and resilience baseline it inherits without writing any of it, and deliver
the in-process integration-event bus the modular monolith needs for cross-module writes.

When this story is done:

1. **Every error is an RFC 9457 problem** with a stable `type` URI from one static class (`ProblemTypes`), a
   `correlationId`, and a `traceId`. Validation → **400**, not found → **404**, domain-rule violation → **422**,
   concurrency clash → **409**, rate limited → **429**, unhandled → **500** with **no** exception text, stack trace,
   or connection string, in any environment.
2. **Every log line is JSON**, carries the request's `CorrelationId`, and carries `UserId`, `TenantId`,
   `DepartmentId`, `BranchId` when the caller is authenticated. Secrets are redacted by name.
3. **`X-Correlation-Id`** is accepted, generated when absent, returned on every response (including the 500), and
   forwarded on every outbound HTTP call and every integration event.
4. **OpenTelemetry traces and metrics** cover HTTP in, HTTP out, EF Core, and every MediatR request, exported over
   OTLP to a local Aspire dashboard.
5. **`ICacheService`** over Redis. A query implementing **`ICacheableQuery`** is cached by `CachingBehavior`, and a
   command implementing **`ICacheInvalidatingCommand`** invalidates its tags **after commit**.
6. **Rate limiting** on the `Public` Area, 429 + `Retry-After`.
7. **Outbound HTTP resilience** (retry, circuit breaker, timeout) through `AddCrmResilience()` on any
   `IHttpClientBuilder`.
8. **Integration events:** `IIntegrationEvent` (shared kernel), `IEventBus` (building blocks), an `outbox_messages`
   table in **each module's own schema**, written in the command's transaction, and a background dispatcher that
   delivers to every `IIntegrationEventHandler<T>` in the subscriber's own transaction, with retry and a per-handler
   idempotency record (`outbox_message_consumers`).
9. `backend/requests/diagnostics.http` exercises every failure mode above in one pass.

**Not in scope:** alerting/notifications (Story 36), the queryable audit log (Story 10), authentication (Story 07
— this story only reads claims if present), any test project, any frontend change.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith" (module catalogue, "Modules talk only through Contracts", lines ~70–82) and
   "Application, API, and persistence" (pipeline order **validation → authorisation → logging → transaction →
   caching**). Task 14 edits this file.
2. `backend/src/Crm.Api/Program.cs` lines 1–91 — the whole composition root. Task 13 rewrites it; note line 11
   (`AddProblemDetails`), line 70 (`MigrateAndSeedModulesAsync` before `Run`, so hosted services start after
   migrations), line 73 (the placeholder exception handler).
3. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/DependencyInjection.cs` lines 16–35 — MediatR
   registered **once**; `AddOpenBehavior` order is execution order. Task 8 appends `CachingBehavior` last; Task 11
   scans integration-event handlers here.
4. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs` lines 20–70 — the
   commit point. **Harvest before commit, dispatch after** (lines 51–59). Task 8 adds post-commit cache
   invalidation; Task 11 adds the outbox write before commit.
5. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/LoggingBehavior.cs` lines 7–46 —
   `[LoggerMessage]` source-generated logging, the style every new log call copies. Task 7 adds the Activity and
   metric here.
6. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/ValidationBehavior.cs` lines 38–50 —
   joins failures as `"Field: message | Field: message"`, and **throws `ValidationException`** when `TResponse` is
   not a `Result` (line 43). Task 2 maps both shapes.
7. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ResultActionResultExtensions.cs` lines 1–61 and
   `ApiControllerBase.cs` lines 14–27 — the current Result → HTTP mapping. Task 2 replaces the extension with
   `ProblemDetailsFactory`-based mapping in `ApiControllerBase`, reusing the field-error parser (lines 31–50).
8. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/IUnitOfWork.cs` lines 18–32 and
   `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.cs` lines 20–115 — the
   per-module unit of work. Task 11 adds `AddToOutbox` and maps the two outbox tables in `OnModelCreating`
   (lines 29–41).
9. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Modules/UnitOfWorkResolver.cs` lines 6–18 —
   resolves a module's unit of work **from a type's assembly**. The outbox dispatcher reuses it for subscriber
   handlers (a handler lives in its module's Application assembly).
10. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Modules/ModuleServiceCollectionExtensions.cs`
    lines 21–49 — `AddModuleDbContext` registers `ModuleDatabase` (line 46) and `ModuleUnitOfWork` (line 45). The
    dispatcher enumerates `ModuleDatabase` to poll every module's outbox.
11. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/DependencyInjection.cs` lines 17–35 and
    `Identity/CurrentUserService.cs` lines 1–11 — shared registrations; `CurrentUserService` returns `null` today.
12. `backend/src/BuildingBlocks/Crm.SharedKernel/DomainException.cs` lines 6–14 and `Results/ErrorType.cs` —
    `DomainException` carries a `DomainError(Code, Message)`; `ErrorType` has **no** `Unauthorized` member and this
    story does not add one (401 comes from the auth middleware in Story 07, mapped by status code).
13. `backend/src/Modules/Sample/` — the proving ground:
    - `Crm.Modules.Sample.Domain/SampleAggregate.cs` lines 39–60 (`Create`, `Rename`) — Task 3 adds the first
      invariant that only the domain can catch.
    - `Crm.Modules.Sample.Application/Commands/RenameSample/RenameSampleCommandHandler.cs` lines 13–35 — Task 11
      publishes `SampleRenamedIntegrationEvent` here; Task 8 marks the command cache-invalidating.
    - `Crm.Modules.Sample.Application/Queries/GetSampleById/GetSampleByIdQuery.cs` line 9 — Task 8 makes it
      `ICacheableQuery`.
    - `Crm.Modules.Sample.Contracts/ISampleModuleApi.cs` — Contracts reference **only** `Crm.SharedKernel`; the
      integration event record goes next to it.
    - `Crm.Modules.Sample.Infrastructure/Persistence/Migrations/20260927232755_Initial.cs` — the only migration; Task
      11 adds `AddOutbox` after it.
14. `backend/Directory.Build.props` lines 28–39 and `backend/build/BannedSymbols.*.txt` — central banned-API
    wiring. Task 6 adds `BannedSymbols.Logging.txt` the same way.
15. `backend/Directory.Build.targets` lines 29–61 — the `CRM0001` rules. **Every new reference in this plan is
    legal:** building blocks reference only building blocks and packages; `Crm.BuildingBlocks.Presentation` gains a
    reference to `Crm.BuildingBlocks.Application` (no rule covers building block → building block).
16. `backend/.editorconfig` lines 1–5 — severity overrides live here. Task 6 adds CA2254/CA1848.
17. `backend/docker-compose.yml` lines 1–23 — Task 12 adds `redis` and `aspire-dashboard`, remapped like SQL
    Server.
18. `backend/requests/persistence.http` and `backend/requests/health.http` line 1 — `@host = http://localhost:5080`
    is **wrong**: `dotnet run` uses `Properties/launchSettings.json` line 8, **`http://localhost:5187`**. The README
    (line 35) repeats the mistake. Task 12 fixes all three.
19. [`02-story-persistence-baseline-with-auditing-and-soft-delete.md`](02-story-persistence-baseline-with-auditing-and-soft-delete.md)
    — the precedent for tone, for the probe-first approach, and for the "matrix replaces the suite" test plan.

---

## Product rules (from story)

| Concern | Current behaviour | New behaviour |
|---|---|---|
| Unhandled exception | Default `UseExceptionHandler`, generic problem, not logged with context | 500, `type = urn:crm:problem:internal`, fixed detail text, `correlationId`, logged at Error with the exception |
| `DomainException` from an aggregate | **500** | **422**, `type = urn:crm:problem:domain-rule`, `code` = `DomainError.Code` |
| Validation failure | 400, no `type` URI | 400, `type = urn:crm:problem:validation`, `errors` by field |
| Not found / conflict | 404 / 409, `title` = error code | Same status, stable `type`, `title` stable English, `code` extension |
| Log format | MEL console, plain text | Serilog, compact JSON on console, OTLP when configured |
| Cross-module write | Not possible | Integration event → module outbox → dispatcher → subscriber's transaction |

---

## Backend Tasks

### 1 — Packages and project wiring

**File: `backend/Directory.Packages.props`** — add inside the existing `<ItemGroup>`, keeping the MediatR comment
and every existing pin **unchanged**:

```xml
<!-- Story 03 — observability, caching, resilience. -->
<PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
<PackageVersion Include="Serilog.Sinks.OpenTelemetry" Version="4.2.0" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.19.1" />
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.19.1" />
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.19.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.19.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.19.0" />
<!-- EF Core instrumentation has only ever shipped as a beta. The one prerelease pin in the solution. -->
<PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.19.1-beta.1" />
<PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="10.10.0" />
<PackageVersion Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.0.11" />
<PackageVersion Include="Polly.Core" Version="8.6.6" />
```

**File: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Crm.BuildingBlocks.Infrastructure.csproj`**
— add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (correlation middleware, `IHttpContextAccessor`,
`UseSerilogRequestLogging`) and package references: `Serilog.AspNetCore`, `Serilog.Sinks.OpenTelemetry`, the five
`OpenTelemetry.*` packages, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Caching.StackExchangeRedis`.

**File: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/Crm.BuildingBlocks.Presentation.csproj`** — add
`<ProjectReference Include="..\Crm.BuildingBlocks.Application\Crm.BuildingBlocks.Application.csproj" />` (for
`ConcurrencyConflictException`, `ICorrelationIdAccessor`, FluentValidation's `ValidationException`) and
`<PackageReference Include="Polly.Core" />` (for `Polly.ExecutionRejectedException`, the base of
`BrokenCircuitException` and `TimeoutRejectedException`). Keep the existing `SharedKernel` reference — module
Presentation projects set `DisableTransitiveProjectReferences`, so they already reference the building blocks
directly.

**No module project changes** in this task. `Crm.Api` gets everything transitively through
`Crm.BuildingBlocks.Infrastructure` and `.Presentation`.

Run `dotnet build backend/Crm.sln` now. It must be clean before any code is written.

### 2 — Problem types and global error handling

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs`** — the
**one** list of `type` URIs. URNs, not invented `https://` URLs: they are stable identifiers the frontend maps to
localised messages (Story 05), and a URN does not pretend to be dereferenceable.

```csharp
namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

/// <summary>
/// The stable RFC 9457 <c>type</c> URI of every error class the API returns. The frontend maps these to localised
/// messages - never rename one; add a new one instead. Listed in backend/README.md, "Error responses".
/// </summary>
public static class ProblemTypes
{
    public const string Validation = "urn:crm:problem:validation";                   // 400 - a validator rejected the input
    public const string BadRequest = "urn:crm:problem:bad-request";                  // 400 - malformed request (unparseable body)
    public const string Unauthorized = "urn:crm:problem:unauthorized";               // 401
    public const string Forbidden = "urn:crm:problem:forbidden";                     // 403
    public const string NotFound = "urn:crm:problem:not-found";                      // 404
    public const string MethodNotAllowed = "urn:crm:problem:method-not-allowed";     // 405
    public const string Conflict = "urn:crm:problem:conflict";                       // 409 - includes the concurrency clash
    public const string UnsupportedMediaType = "urn:crm:problem:unsupported-media-type"; // 415
    public const string DomainRule = "urn:crm:problem:domain-rule";                  // 422 - an aggregate invariant refused the change
    public const string RateLimited = "urn:crm:problem:rate-limited";               // 429
    public const string Internal = "urn:crm:problem:internal";                       // 500
    public const string DependencyUnavailable = "urn:crm:problem:dependency-unavailable"; // 503 - an outbound call failed or its circuit is open

    public static string ForStatus(int statusCode) => statusCode switch { /* the eleven above; default Internal for >= 500, BadRequest for other 4xx */ };

    public static string TitleFor(string type) => type switch { /* one fixed English title per constant, e.g. "Validation failed" */ };
}
```

Also define `ErrorTypeMapping.ToStatusCode(ErrorType)` and `ErrorTypeMapping.ToProblemType(ErrorType)` in the
same folder. It is **the one** status mapping: Validation 400, NotFound 404, Conflict 409, Forbidden 403, Domain 422,
Failure 500. It moves the `ToStatusCode` switch out of `ResultActionResultExtensions.cs` lines 53–60.

**Create file: `…/Crm.BuildingBlocks.Presentation/ErrorHandling/ValidationErrorParser.cs`** — move
`BuildValidationProblemDetails`'s splitting logic (`ResultActionResultExtensions.cs` lines 33–43) here as
`public static IDictionary<string, string[]> Parse(string description)`.

**Create file: `…/Crm.BuildingBlocks.Presentation/ErrorHandling/GlobalExceptionHandler.cs`** — `sealed partial
class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) :
IExceptionHandler`. Map in this order:

| Exception | Status | `type` | `detail` | Log |
|---|---|---|---|---|
| `OperationCanceledException` while `httpContext.RequestAborted.IsCancellationRequested` | — | — | — | **return `false`** (the framework treats it as a client abort) |
| `DomainException` | 422 | `DomainRule` | `ex.Error.Message`; extension `code` = `ex.Error.Code` | Information, `{ErrorCode}` |
| `FluentValidation.ValidationException` | 400 | `Validation` | `"One or more validation errors occurred."`; `errors` grouped by `PropertyName` (write a `ValidationProblemDetails`) | Information |
| `ConcurrencyConflictException` | 409 | `Conflict` | `"The record was modified by someone else. Reload and try again."`; `code` = `Persistence.ConcurrencyConflict` | Warning |
| `BadHttpRequestException` | `ex.StatusCode` | `ForStatus(ex.StatusCode)` | `"The request could not be read."` | Information |
| `Polly.ExecutionRejectedException`, `HttpRequestException` | 503 | `DependencyUnavailable` | `"A downstream service is unavailable. Try again shortly."` | Warning **with the exception** |
| anything else | 500 | `Internal` | **fixed** `"An unexpected error occurred."` — **never** `ex.Message` | **Error with the exception** |

Write through `problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext, ProblemDetails, Exception = ex })`
after setting `httpContext.Response.StatusCode`, and return its result. **Probe 5:** .NET 10 suppresses the
middleware's own exception log once a handler returns `true`, so **every branch logs** through a `[LoggerMessage]`
method. Put the exception in the 500 and 503 branches only.

**Create file: `…/Crm.BuildingBlocks.Presentation/ErrorHandling/ErrorHandlingServiceCollectionExtensions.cs`** —
`public static IServiceCollection AddCrmErrorHandling(this IServiceCollection services)`:

```csharp
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    ProblemDetails problem = context.ProblemDetails;
    int status = problem.Status ?? context.HttpContext.Response.StatusCode;

    // Framework-produced problems (status-code pages, [ApiController] model-state 400s) carry the RFC 9110 link
    // or nothing. Replace it with ours; never overwrite a type our own code already chose.
    if (problem.Type is null || problem.Type.StartsWith("https://tools.ietf.org/", StringComparison.Ordinal))
    {
        problem.Type = problem is ValidationProblemDetails ? ProblemTypes.Validation : ProblemTypes.ForStatus(status);
        problem.Title = ProblemTypes.TitleFor(problem.Type);
    }

    problem.Instance ??= context.HttpContext.Request.Path;
    string? correlationId = context.HttpContext.RequestServices.GetRequiredService<ICorrelationIdAccessor>().CorrelationId;
    if (correlationId is not null) { problem.Extensions["correlationId"] = correlationId; }
    // traceId is added by the framework (probe 1/2 showed it) - do not add it again.
});
return services;
```

**File: `…/Crm.BuildingBlocks.Presentation/ApiControllerBase.cs`** — replace `result.Error.ToActionResult()` (lines
23, 26) with a protected `ActionResult Problem(Error error)` that builds the body through
**`ProblemDetailsFactory`** (`HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()`), so
`CustomizeProblemDetails` applies (**Probe 1**):
- `ErrorType.Validation` → `CreateValidationProblemDetails(HttpContext, modelState, 400, title, ProblemTypes.Validation, error.Description)`,
  with `modelState` built from `ValidationErrorParser.Parse(error.Description)`, returned as `BadRequestObjectResult`.
- everything else → `CreateProblemDetails(HttpContext, status, ProblemTypes.TitleFor(type), type, error.Description)`,
  then `problem.Extensions["code"] = error.Code`, returned as `ObjectResult { StatusCode = status }`.

**Delete file: `…/Crm.BuildingBlocks.Presentation/ResultActionResultExtensions.cs`.** `grep -r "ToActionResult(" backend/src`
afterwards must show only the two `ApiControllerBase` methods and the controllers calling them.

### 3 — A domain rule only the domain can catch (Sample)

The 422 path needs an invariant a validator does not duplicate. **File:
`backend/src/Modules/Sample/Crm.Modules.Sample.Domain/SampleAggregate.cs`** — in `Create` (lines 39–49), before
adding lines, reject duplicates:

```csharp
// The first invariant only the aggregate can enforce - CreateSampleCommandValidator checks each line is not
// empty, not that lines are distinct. Surfaces as 422 urn:crm:problem:domain-rule (Story 03).
List<string> lineList = lines.ToList();
if (lineList.Count != lineList.Distinct(StringComparer.OrdinalIgnoreCase).Count())
{
    throw new DomainException(new DomainError("Sample.DuplicateLine", "A sample cannot contain the same line twice."));
}
```

Iterate `lineList` in the existing loop. **Do not** add a matching validator rule; the point is that the domain
refuses what the validator allowed.

### 4 — Correlation id

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/ICorrelationIdAccessor.cs`:**

```csharp
public interface ICorrelationIdAccessor
{
    /// <summary>The current request's or dispatched message's correlation id; null outside both.</summary>
    string? CorrelationId { get; }

    /// <summary>Sets the id for the current async flow (outbox dispatcher, background work). Dispose to restore.</summary>
    IDisposable BeginScope(string correlationId);
}
```

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Correlation/CorrelationIdAccessor.cs`** — singleton. Reads an
`AsyncLocal<string?>` override first (set by `BeginScope`), then falls back to
`IHttpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey]`. **Why both:** an `AsyncLocal` set inside
a middleware is **not** visible to middleware outside it once the call unwinds, so `UseExceptionHandler` would lose
it. `HttpContext.Items` is visible everywhere in the request. `IHttpClientFactory` handlers run in their own DI
scope, so a *scoped* accessor would be invisible to the outbound handler. A singleton over the
`IHttpContextAccessor` avoids both problems.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Correlation/CorrelationIdMiddleware.cs`** —
`public const string HeaderName = "X-Correlation-Id"`, `ItemKey = "Crm.CorrelationId"`:
1. Read the request header. **Accept it only if** it is 1–64 characters of `[A-Za-z0-9._:-]`. Otherwise (absent,
   too long, or anything else, which is a log/header-injection vector) generate one:
   `Activity.Current?.TraceId.ToHexString() ?? Guid.NewGuid().ToString("N")`. A generated id then equals the trace id,
   so logs and traces join on one value.
2. `context.Items[ItemKey] = id`; `Activity.Current?.SetTag("crm.correlation_id", id)`.
3. `context.Response.OnStarting(() => { context.Response.Headers[HeaderName] = id; return Task.CompletedTask; })`
   — **OnStarting, not a direct header write**: `ExceptionHandlerMiddleware` clears response headers before writing
   the 500, and a header set up front is lost on exactly the response that needs it most.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Correlation/CorrelationIdDelegatingHandler.cs`** — adds
`X-Correlation-Id` from the accessor to every outbound request if not already present. Registered transient in Task
10.

### 5 — Current user from claims

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/CrmClaimTypes.cs`:**

```csharp
/// <summary>The claim names Story 07's JWT must carry. Logging, auditing, scoping, and cache keys read these.</summary>
public static class CrmClaimTypes
{
    public const string UserId = "sub";
    public const string TenantId = "tenant_id";
    public const string DepartmentId = "department_id";
    public const string BranchId = "branch_id";
}
```

**File: `…/Crm.BuildingBlocks.Application/Abstractions/ICurrentUserService.cs`** — add `string? TenantId`,
`string? DepartmentId`, `string? BranchId` (nullable strings like `UserId`; Story 06 decides whether they become
strongly typed ids).

**File: `…/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs`** — inject `IHttpContextAccessor`. Each
property reads `HttpContext?.User` **only when `Identity.IsAuthenticated`**, via `FindFirst(CrmClaimTypes.X)?.Value`.
Replace the `// Story 07 replaces this with the JWT principal.` comment with: `// Story 07 adds the JWT handler;
this reads its claims (CrmClaimTypes).`

### 6 — Structured logging with Serilog

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Logging/CrmLogEnricher.cs`** — `sealed class CrmLogEnricher(ICorrelationIdAccessor correlation, IHttpContextAccessor httpContext) : ILogEventEnricher`.
Adds `CorrelationId` when present. When `HttpContext.User.Identity.IsAuthenticated`, also adds `UserId`, `TenantId`,
`DepartmentId`, `BranchId` from `CrmClaimTypes` (only the ones present). Reads claims directly, not
`ICurrentUserService`: the enricher is a singleton and `ICurrentUserService` is scoped.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Logging/SensitiveData.cs`** — `internal static bool IsSensitive(string name)`:
lower-case the name, strip `_`, `-`, and `.`, then return true if it contains any of `password`, `passwd`, `secret`,
`token`, `apikey`, `authorization`, `connectionstring`, `credential`.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Logging/SensitiveDataDestructuringPolicy.cs`** — the policy
proven in **Probe 4**. For any non-`System.*`, non-primitive, non-`string` object logged with `@`:
`StructureValue` over readable, non-indexer public instance properties. A property whose name `IsSensitive` becomes
`new ScalarValue("***")`; any other property goes through `propertyValueFactory.CreatePropertyValue(value, destructureObjects: true)`,
which recurses into nested objects.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Logging/SensitivePropertyRedactionEnricher.cs`** —
`ILogEventEnricher` that replaces every **top-level** property whose name `IsSensitive` with `"***"`
(`logEvent.AddOrUpdateProperty`). This covers a scalar template such as `{ApiKey}`, which destructuring never sees.
**Register it last**, so it also covers properties other enrichers added.

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs`** —
`public static IServiceCollection AddCrmObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)`:

```csharp
services.AddHttpContextAccessor();
services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
services.AddSingleton<ILogEventEnricher, CrmLogEnricher>();

string? otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];   // null => no OTLP export at all

services.AddSerilog((sp, logger) =>
{
    logger.ReadFrom.Configuration(configuration)                        // MinimumLevel + overrides only
        .ReadFrom.Services(sp)                                           // picks up CrmLogEnricher
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", environment.ApplicationName)
        .Destructure.With(new SensitiveDataDestructuringPolicy())
        .Enrich.With(new SensitivePropertyRedactionEnricher())           // after every other enricher
        .WriteTo.Console(new RenderedCompactJsonFormatter());            // JSON in EVERY environment - not configurable

    if (otlpEndpoint is not null)
    {
        logger.WriteTo.OpenTelemetry(o => { o.Endpoint = otlpEndpoint; o.Protocol = OtlpProtocol.Grpc;
                                            o.ResourceAttributes["service.name"] = environment.ApplicationName; });
    }
});
// + OpenTelemetry (Task 7)
return services;
```

**Why ReadFrom.Services for the enricher but `new` for the policy and redaction enricher:**
`ReadFrom.Services` resolves `ILogEventEnricher`, not `IDestructuringPolicy`. Adding the redaction enricher from DI
would put it before the builder's own enrichers.

Also add `public static IApplicationBuilder UseCrmRequestLogging(this IApplicationBuilder app)`, which wraps
`UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) => diag.Set("RequestHost", http.Request.Host.Value))`.
Keep Serilog's default message template, which does not log the query string.

**File: `backend/src/Crm.Api/appsettings.json`** — replace the `"Logging"` section with:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System.Net.Http.HttpClient": "Warning"
    }
  }
}
```

**File: `backend/src/Crm.Api/appsettings.Development.json`** — replace `"Logging"` with a `"Serilog"` section with
the same overrides plus `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` (the SQL log the caching
criterion reads) and `"Microsoft.EntityFrameworkCore.Database.Transaction": "Debug"` (kept from Story 02). **Remove
both `"Logging"` sections entirely.** Serilog replaces `ILoggerFactory`, so MEL `Logging:LogLevel` would be a second,
silently ignored source of truth.

**Create file: `backend/build/BannedSymbols.Logging.txt`:**

```
T:System.Console;Write through ILogger<T> with a [LoggerMessage] template. Console output bypasses Serilog, correlation, and redaction.
T:System.Diagnostics.Debug;Write through ILogger<T> with a [LoggerMessage] template.
```

**File: `backend/Directory.Build.props`** — add
`<AdditionalFiles Include="$(MSBuildThisFileDirectory)build/BannedSymbols.Logging.txt" />` next to the Clock line
(line 35). It applies to every project.

**File: `backend/.editorconfig`** — under `[*.cs]`:

```
dotnet_diagnostic.CA2254.severity = error   # log message template must be a constant - no interpolated log strings
dotnet_diagnostic.CA1848.severity = error   # use [LoggerMessage] source-generated logging
```

`Serilog.Log` (static) is used **only** in `Program.cs` for the bootstrap logger and the fatal crash line (Task 13).

### 7 — OpenTelemetry traces and metrics

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Diagnostics/CrmDiagnostics.cs`:**

```csharp
public static class CrmDiagnostics
{
    public const string SourceName = "Crm";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    /// <summary>Every MediatR request, tagged crm.request.name / crm.request.kind (command|query) / crm.outcome (success|failure|exception).</summary>
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("crm.request.duration", unit: "ms", description: "MediatR request duration");
}
```

**File: `…/Behaviors/LoggingBehavior.cs`** — the "logging" stage becomes the observability stage. Its pipeline
position does not change. Around `next()`:
- `using Activity? activity = CrmDiagnostics.ActivitySource.StartActivity(requestName)`, with tags
  `crm.request.name` and `crm.request.kind` (`request is ICommandMarker ? "command" : "query"`).
- On a failed `Result`: `activity?.SetStatus(ActivityStatusCode.Error, error.Code)`, tag `crm.error.code`, outcome
  `failure`. Log `Handled {RequestName} with {ErrorCode} in {ElapsedMilliseconds} ms` at Warning (new
  `[LoggerMessage]`).
- On an exception: `activity?.AddException(ex)`, status Error, outcome `exception`. Keep the existing
  `LogFailed`.
- Always `CrmDiagnostics.RequestDuration.Record(elapsed, tags)`.
- New Debug-level `[LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName} {@Request}")]`, logged
  only when `logger.IsEnabled(LogLevel.Debug)`. The payload goes through the redaction policy.

**File: `ObservabilityServiceCollectionExtensions.AddCrmObservability`** (Task 6) — append:

```csharp
OpenTelemetryBuilder otel = services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(environment.ApplicationName))
    .WithTracing(t => t
        .AddSource(CrmDiagnostics.SourceName)
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(m => m
        .AddMeter(CrmDiagnostics.SourceName)
        .AddMeter("Polly")                          // resilience events: retries, circuit state
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

if (otlpEndpoint is not null)
{
    otel.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(otlpEndpoint));
}
```

`appsettings.Development.json`: `"OpenTelemetry": { "OtlpEndpoint": "http://localhost:14317" }` (the Aspire
dashboard, Task 12). **No** endpoint in `appsettings.json`. Production sets it through configuration or
`OpenTelemetry__OtlpEndpoint`.

### 8 — Distributed caching

**Create files in `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Caching/`:**

```csharp
public interface ICacheService
{
    /// <summary>Returns the cached value, or runs <paramref name="factory"/> and caches its result. A null from the
    /// factory is returned and NOT cached (a failed Result must never be cached). Tag versions are read BEFORE the
    /// factory runs, so an invalidation that lands while the factory is reading stale data makes the entry
    /// unreachable instead of caching it as fresh.</summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CacheEntrySettings settings,
        CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Makes every entry carrying any of these tags unreachable, in every scope, on every instance.</summary>
    Task InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
}

public sealed record CacheEntrySettings(TimeSpan Expiration, IReadOnlyCollection<string> Tags);

/// <summary>A query whose successful result CachingBehavior caches. Keys and tags follow
/// "{module}:{aggregate-plural}:{id}" (e.g. "sample:samples:{id}"); the behaviour prefixes the caller's scope.</summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    IReadOnlyCollection<string> CacheTags { get; }
    TimeSpan? CacheDuration => null;     // null => Caching:DefaultExpiration
    bool VaryByUser => false;            // true when the result depends on who asks, not only on their scope
}

/// <summary>A command whose successful commit invalidates these tags. Invalidated by TransactionBehavior AFTER
/// commit - never inside the transaction.</summary>
public interface ICacheInvalidatingCommand
{
    IReadOnlyCollection<string> CacheTagsToInvalidate { get; }
}

public sealed record CachingSettings(TimeSpan DefaultExpiration);   // bound from "Caching", default 00:05:00
```

**Create file: `…/Crm.BuildingBlocks.Application/Behaviors/CachingBehavior.cs`** — `CachingBehavior<TRequest,
TResponse>(ICacheService cache, ICurrentUserService currentUser, CachingSettings settings)`:
- `request is not ICacheableQuery cacheable` → `return await next()`.
- If `TResponse` is not `Result<T>`, throw `InvalidOperationException` ("ICacheableQuery must return Result<T>").
  This is loud on purpose, like `ValidationBehavior` line 43.
- Effective key: `$"{scope}:{cacheable.CacheKey}"`, where `scope` =
  `$"t={currentUser.TenantId ?? "-"}|d={currentUser.DepartmentId ?? "-"}|b={currentUser.BranchId ?? "-"}"`, plus
  `$"|u={currentUser.UserIdOrSystem}"` when `VaryByUser`. **A key without the scope would serve one tenant's cached
  data to another once Story 06 lands.**
- Call `cache.GetOrCreateAsync<CachedValue<TValue>>(...)` through a private generic method invoked by reflection,
  with the `MethodInfo` cached in a `static readonly` field of the closed behaviour type (the same reflection
  technique as `ValidationBehavior` lines 53–69). The factory runs `next()`: on success it returns
  `new CachedValue<TValue>(result.Value)`; on failure it stores the failed response in a local and returns `null`.
  A `null` from the cache call means "return the stored failure".
- `internal sealed record CachedValue<TValue>(TValue Value)` — wrapping lets value-type results satisfy
  `where T : class`.

**File: `…/Crm.BuildingBlocks.Application/DependencyInjection.cs`** — `cfg.AddOpenBehavior(typeof(CachingBehavior<,>))`
**after** `TransactionBehavior` (the documented order). Bind `CachingSettings` from `configuration.GetSection("Caching")`.
This means `AddBuildingBlocksApplication` gains an `IConfiguration configuration` parameter; update the one call in
`Program.cs`.

**File: `…/Behaviors/TransactionBehavior.cs`** — inject `ICacheService cache`. After `CommitTransactionAsync`
(line 58) and **before** `dispatcher.DispatchAsync`:

```csharp
if (request is ICacheInvalidatingCommand invalidating)
{
    // After commit, never before: invalidating inside the transaction lets a concurrent read re-cache the old
    // committed row before this one commits.
    await cache.InvalidateTagsAsync(invalidating.CacheTagsToInvalidate, cancellationToken);
}
```

**Create file: `…/Crm.BuildingBlocks.Infrastructure/Caching/DistributedCacheService.cs`** — `sealed partial class
DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger) : ICacheService`:
- Tag key `crm:tag:{tag}` holds an opaque version token (`Guid.NewGuid().ToString("N")`), stored with a 30-day
  absolute expiry. A missing tag key means "never invalidated" and is created on first read.
- Entry value: UTF-8 JSON `{"v":{"<tag>":"<token>",…},"p":<payload>}`, `System.Text.Json` with `JsonSerializerDefaults.Web`.
- `GetOrCreateAsync`: read the entry. If every stored tag token equals the current one, deserialize and return
  (Debug log `Cache hit {CacheKey}`). Otherwise, **read current tag tokens first**, run the factory, and store the
  result with **those** tokens and `settings.Expiration` (Debug log `Cache miss {CacheKey}`).
- `InvalidateTagsAsync`: write a fresh token to each tag key. There is no read-modify-write, so there is no race.
- **Every `IDistributedCache` call is wrapped:** any exception except `OperationCanceledException` is logged at
  Warning (`Cache unavailable during {Operation} for {CacheKey}`). Get failure → run the factory. Set failure →
  ignore. Invalidate failure → log at **Error** (the entry stays stale until its TTL). **A cache outage must never
  fail a request.**

**Registration** — in `AddBuildingBlocksInfrastructure`:

```csharp
string? redis = configuration.GetConnectionString("Redis");
if (redis is not null)
{
    services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "crm:"; });
}
else
{
    // Single-instance fallback. Invalidation is local to this process - never run more than one instance like this.
    services.AddDistributedMemoryCache();
}
services.AddSingleton<ICacheService, DistributedCacheService>();
```

Log the fallback once at startup: a startup warning from `Program.cs` through the bootstrap logger, or an
`IHostedService` that logs `Redis is not configured; using the in-process cache`.

`appsettings.Development.json` → `"ConnectionStrings": { …, "Redis": "localhost:16379,abortConnect=false,connectTimeout=2000" }`.
`abortConnect=false` keeps startup working while Redis is down; `connectTimeout=2000` bounds the slowdown per call.

**Sample module:**
- **Create file: `backend/src/Modules/Sample/Crm.Modules.Sample.Application/SampleCacheKeys.cs`** —
  `public static string Sample(Guid id) => $"sample:samples:{id}";` (used as both key and tag).
- **File: `…/Queries/GetSampleById/GetSampleByIdQuery.cs`** →
  `public sealed record GetSampleByIdQuery(Guid Id) : IRequest<Result<SampleAggregateDto>>, ICacheableQuery`, with
  `public string CacheKey => SampleCacheKeys.Sample(Id);` and `public IReadOnlyCollection<string> CacheTags => [SampleCacheKeys.Sample(Id)];`.
- **File: `…/Commands/RenameSample/RenameSampleCommand.cs`** and **`…/Commands/DeleteSample/DeleteSampleCommand.cs`**
  → add `, ICacheInvalidatingCommand` and `public IReadOnlyCollection<string> CacheTagsToInvalidate => [SampleCacheKeys.Sample(Id)];`.

### 9 — Rate limiting on public endpoints

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/RateLimiting/RateLimitingServiceCollectionExtensions.cs`:**

```csharp
public sealed record PublicRateLimitSettings(int PermitLimit, TimeSpan Window);   // "RateLimiting:Public"

public static IServiceCollection AddCrmRateLimiting(this IServiceCollection services, IConfiguration configuration)
{
    PublicRateLimitSettings settings = configuration.GetSection("RateLimiting:Public").Get<PublicRateLimitSettings>()
        ?? throw new InvalidOperationException("RateLimiting:Public is not configured.");

    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Probe 3: endpoint metadata carries the controller's [Area], so one global limiter covers every Public
        // controller in every module with no attribute to forget.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            ctx.GetEndpoint()?.Metadata.GetMetadata<AreaAttribute>()?.RouteValue == CrmAreas.Public
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"public:{ctx.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.PermitLimit, Window = settings.Window, QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("unlimited"));
        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }
            await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = { Status = 429, Type = ProblemTypes.RateLimited, Title = ProblemTypes.TitleFor(ProblemTypes.RateLimited),
                                   Detail = "Too many requests. Retry after the time in the Retry-After header." },
            });
        };
    });
    return services;
}
```

Config: `appsettings.json` → `"RateLimiting": { "Public": { "PermitLimit": 60, "Window": "00:01:00" } }`;
`appsettings.Development.json` → `PermitLimit: 5, Window: "00:00:30"`, so the 429 is reachable by hand.

Record in `AREAS.md` under "Public Area": *Rate limited per client IP by the global limiter
(`AddCrmRateLimiting`). Stories 27 and 47 add named policies (per email, per account) with `[EnableRateLimiting]`
for anonymous endpoints outside this Area.*

### 10 — Resilient outbound HTTP

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Http/HttpClientBuilderExtensions.cs`:**

```csharp
/// <summary>Every outbound HTTP client (ERP, WhatsApp, email providers, webhooks) goes through this - never a
/// hand-rolled retry loop. Correlation id first (outermost), then the standard resilience pipeline: total timeout
/// -> retry -> circuit breaker -> per-attempt timeout, all bound from "Resilience:Outbound".</summary>
public static IHttpClientBuilder AddCrmResilience(this IHttpClientBuilder builder, IConfiguration configuration)
{
    builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
    builder.AddStandardResilienceHandler().Configure(configuration.GetSection("Resilience:Outbound"));
    return builder;
}
```

Register `services.AddTransient<CorrelationIdDelegatingHandler>()` in `AddBuildingBlocksInfrastructure`.

`appsettings.json`:

```json
"Resilience": {
  "Outbound": {
    "TotalRequestTimeout": { "Timeout": "00:00:30" },
    "Retry": { "MaxRetryAttempts": 3, "BackoffType": "Exponential", "UseJitter": true, "Delay": "00:00:01" },
    "CircuitBreaker": { "FailureRatio": 0.5, "MinimumThroughput": 5, "SamplingDuration": "00:00:30", "BreakDuration": "00:00:15" },
    "AttemptTimeout": { "Timeout": "00:00:10" }
  }
}
```

`appsettings.Development.json` overrides only `Retry.Delay: "00:00:00.200"`, so the retries are visible without
waiting. **The "configured failure count" is `MinimumThroughput`:** Polly v8's breaker is ratio-based. Each call
makes **4 attempts** (1 + 3 retries) and the breaker sits *inside* the retry, so every attempt counts. With every
attempt failing, the **2nd** call crosses 5 failures and the **3rd** call is rejected fast with
`BrokenCircuitException` → 503 `dependency-unavailable`.

### 11 — Integration events and the transactional outbox

#### 11a — Contracts

**Create file: `backend/src/BuildingBlocks/Crm.SharedKernel/IIntegrationEvent.cs`:**

```csharp
/// <summary>"This happened" between modules. Records live in the PUBLISHING module's Contracts project, carry
/// primitives only, and are append-only: add properties, never rename or remove one - an undelivered outbox row
/// still holds the old shape.</summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredOnUtc { get; }
}

public abstract record IntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IIntegrationEvent;
```

**Create files in `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Messaging/`:**

- `IEventBus.cs` — `void Publish(IIntegrationEvent integrationEvent);`. XML doc: *"Call from a command handler
  only. The event is written to the module's outbox by TransactionBehavior in the command's own transaction, and
  is discarded if the command fails or rolls back."*
- `IIntegrationEventHandler.cs`:

  ```csharp
  public interface IIntegrationEventHandler
  {
      Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
  }

  /// <summary>Lives in the SUBSCRIBING module's Application project. Runs in that module's own transaction; must
  /// be safe to run again after a crash (the idempotency record is written in the same transaction, so a success
  /// is never repeated, but a failure is retried). Must not send a MediatR command.</summary>
  public interface IIntegrationEventHandler<in TEvent> : IIntegrationEventHandler where TEvent : IIntegrationEvent
  {
      Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);

      Task IIntegrationEventHandler.HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
          HandleAsync((TEvent)integrationEvent, cancellationToken);
  }
  ```

- `IPendingIntegrationEvents.cs` — `IReadOnlyList<IIntegrationEvent> DequeueAll(); bool HasPending { get; }`.
- `OutboxEventBus.cs` — scoped `sealed class OutboxEventBus : IEventBus, IPendingIntegrationEvents` over a
  `List<IIntegrationEvent>`.
- `IntegrationEventHandlerRegistry.cs` — singleton built at registration time. It maps the event type's `FullName`
  to `(Type EventType, IReadOnlyList<Type> HandlerTypes)`, via `bool TryGet(string eventTypeName, out IntegrationEventSubscription subscription)`.

**File: `…/Crm.BuildingBlocks.Application/DependencyInjection.cs`** — after `AddValidatorsFromAssemblies`, scan
`moduleApplicationAssemblies` for non-abstract classes implementing a closed `IIntegrationEventHandler<T>`. Register
each **by its concrete type** (`services.AddScoped(handlerType)`) and build the registry from them. Register
`OutboxEventBus` scoped, with `IEventBus` and `IPendingIntegrationEvents` forwarding to the same instance
(`sp => sp.GetRequiredService<OutboxEventBus>()`).

#### 11b — Writing to the outbox in the command's transaction

**File: `…/Abstractions/IUnitOfWork.cs`** — add:

```csharp
/// <summary>Stages integration events as outbox rows in THIS module's schema. TransactionBehavior calls it before
/// commit, so the rows commit or roll back with the change that raised them.</summary>
void AddToOutbox(IReadOnlyCollection<IIntegrationEvent> integrationEvents, string? correlationId);
```

**File: `…/Behaviors/TransactionBehavior.cs`** — inject `IPendingIntegrationEvents pendingEvents` and
`ICorrelationIdAccessor correlation`. The command path becomes:

```csharp
TResponse response = await next();

if (response is Result { IsFailure: true })
{
    pendingEvents.DequeueAll();                                 // discard - a failed command publishes nothing
    await unitOfWork.RollbackTransactionAsync(cancellationToken);
    return response;
}

IReadOnlyList<IIntegrationEvent> integrationEvents = pendingEvents.DequeueAll();
if (integrationEvents.Count > 0)
{
    unitOfWork.AddToOutbox(integrationEvents, correlation.CorrelationId);
    await unitOfWork.SaveChangesAsync(cancellationToken);      // same transaction as the handler's own save
}

IReadOnlyCollection<IDomainEvent> domainEvents = unitOfWork.GetAndClearDomainEvents();
await unitOfWork.CommitTransactionAsync(cancellationToken);    // past this line the write AND its outbox rows are durable
// cache invalidation (Task 8)
await dispatcher.DispatchAsync(domainEvents, cancellationToken);

if (pendingEvents.HasPending)
{
    throw new InvalidOperationException(
        "An integration event was published after commit (from a domain-event handler?). Publish integration events from the command handler.");
}
return response;
```

Also: in the concurrency `catch`, call `pendingEvents.DequeueAll()` before rolling back. On the **query** path
(line 28), after `await next()`, throw the same `InvalidOperationException` if `pendingEvents.HasPending`. A query
publishing an event is a wiring mistake, and silently dropping the event would be worse.

#### 11c — Outbox tables in every module's schema

**Create file: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Outbox/OutboxMessage.cs`** — `sealed
class` with private setters: `Guid Id` (= the event's `Id`), `string Type` (event `FullName`, max 500), `string
Content` (JSON), `DateTimeOffset OccurredOnUtc`, `string? CorrelationId` (64), `string? TraceParent` (55,
`Activity.Current?.Id` at publish, so dispatch spans link back to the request's trace), `DateTimeOffset?
ProcessedAtUtc`, `int Attempts`, `DateTimeOffset? NextAttemptAtUtc`, `string? LastError` (max 2000). Methods:
`static OutboxMessage From(IIntegrationEvent e, string? correlationId, string? traceParent)`,
`MarkProcessed(DateTimeOffset now)`, `RecordFailure(string error, DateTimeOffset now, int maxAttempts)`. Backoff =
`now + min(2^Attempts, 300) seconds`; `NextAttemptAtUtc = null` once `Attempts >= maxAttempts` (dead-lettered).

**Create file: `…/Outbox/OutboxMessageConsumer.cs`** — `Guid MessageId`, `string Handler` (handler `FullName`, max
300), `DateTimeOffset ProcessedAtUtc`.

**Create files: `…/Outbox/OutboxMessageConfiguration.cs`, `…/Outbox/OutboxMessageConsumerConfiguration.cs`** —
`IEntityTypeConfiguration<T>`:
- `ToTable("outbox_messages")`, PK `Id` `ValueGeneratedNever`, and index `(ProcessedAtUtc, NextAttemptAtUtc)` with
  `.HasFilter("[ProcessedAtUtc] IS NULL")`.
- `ToTable("outbox_message_consumers")`, composite PK `(MessageId, Handler)`. **The PK is the idempotency
  guarantee.** Two dispatchers racing on the same message cannot both commit a consumer row.

No schema is set in the configurations; `HasDefaultSchema(Schema)` puts both tables in the owning module's schema.

**File: `…/Persistence/ModuleDbContext.cs`** — in `OnModelCreating`, after `ApplyConfigurationsFromAssembly`
(line 36):

```csharp
// Every module gets its own outbox, in its own schema (CLAUDE.md, modular monolith). Applied here, not by assembly
// scan - the configurations live in this building-block assembly, not the module's.
modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
modelBuilder.ApplyConfiguration(new OutboxMessageConsumerConfiguration());
```

Implement `AddToOutbox`: `Set<OutboxMessage>().AddRange(events.Select(e => OutboxMessage.From(e, correlationId, Activity.Current?.Id)))`,
where `Content = JsonSerializer.Serialize(e, e.GetType(), OutboxJson.Options)`. Put
`internal static class OutboxJson { public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web); }`
in `Outbox/`.
Add `internal` helpers for the dispatcher: `Task<bool> HasConsumedAsync(Guid messageId, string handler, CancellationToken)`
and `void RecordConsumer(Guid messageId, string handler, DateTimeOffset now)`.

#### 11d — The dispatcher

**Create file: `…/Outbox/OutboxSettings.cs`** — `sealed record OutboxSettings(TimeSpan PollingInterval, int BatchSize, int MaxAttempts)`
bound from `"Outbox"`. `appsettings.json`: `{ "PollingInterval": "00:00:05", "BatchSize": 20, "MaxAttempts": 5 }`.
Development: `PollingInterval: "00:00:02"`.

**Create file: `…/Outbox/OutboxDispatcher.cs`** — `sealed partial class OutboxDispatcher(IServiceScopeFactory scopes,
IntegrationEventHandlerRegistry registry, OutboxSettings settings, IDateTime dateTime, ICorrelationIdAccessor correlation,
ILogger<OutboxDispatcher> logger) : BackgroundService`:

```
loop until stoppingToken:
  try: foreach ModuleDatabase db (resolved from a fresh scope): await DispatchModuleAsync(db)
  catch (Exception ex) when not stopping: log Error "Outbox dispatch cycle failed"   // a BackgroundService that throws stops the host
  await Task.Delay(settings.PollingInterval, stoppingToken)

DispatchModuleAsync(db):
  scope; var publisher = (ModuleDbContext)sp.GetRequiredService(db.DbContextType)
  batch = publisher.Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxAttempts && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.OccurredOnUtc).Take(BatchSize).ToListAsync()
  foreach message:
    using correlation.BeginScope(message.CorrelationId ?? message.Id.ToString("N"))
    using logger.BeginScope(OutboxMessageId = message.Id, IntegrationEvent = message.Type)
    if !registry.TryGet(message.Type, out sub): message.MarkProcessed(now); continue      // no subscriber - nothing to deliver
    evt = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, sub.EventType, OutboxJson.Options)
    failures = []
    foreach handlerType in sub.HandlerTypes:
      try await DeliverAsync(message, evt, handlerType)
      catch (Exception ex): failures.Add(ex); log Warning with exception "{Handler} failed for {OutboxMessageId}, attempt {Attempt}"
    if failures empty: message.MarkProcessed(now)
    else: message.RecordFailure(summary, now, MaxAttempts); if dead-lettered: log Error "Outbox message {OutboxMessageId} dead-lettered after {Attempts} attempts"
  await publisher.SaveChangesAsync()

DeliverAsync(message, evt, handlerType):          // one handler, one scope, one subscriber transaction
  await using scope
  using Activity? activity = CrmDiagnostics.ActivitySource.StartActivity($"outbox {evt.GetType().Name} -> {handlerType.Name}",
                                   ActivityKind.Consumer, message.TraceParent)   // parent = the publishing request's trace
  var unitOfWork = sp.GetRequiredService<IUnitOfWorkResolver>().ForRequest(handlerType)   // assembly-based: the subscriber's module
  var subscriber = (ModuleDbContext)unitOfWork
  string handlerName = handlerType.FullName
  if await subscriber.HasConsumedAsync(message.Id, handlerName): log Information "Skipping {Handler}: already handled {OutboxMessageId}"; return
  await unitOfWork.BeginTransactionAsync()
  try
    await ((IIntegrationEventHandler)sp.GetRequiredService(handlerType)).HandleAsync(evt)
    pending = sp.GetRequiredService<IPendingIntegrationEvents>().DequeueAll()   // a subscriber may publish onward
    if pending.Count > 0: unitOfWork.AddToOutbox(pending, correlation.CorrelationId)
    subscriber.RecordConsumer(message.Id, handlerName, now)
    await unitOfWork.SaveChangesAsync()
    domainEvents = unitOfWork.GetAndClearDomainEvents()
    await unitOfWork.CommitTransactionAsync()
    await sp.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(domainEvents)
    log Information "Delivered {IntegrationEvent} {OutboxMessageId} to {Handler}"
  catch: await unitOfWork.RollbackTransactionAsync(); throw
```

Every log call above is a `[LoggerMessage]` method. `TraceParent` null → `StartActivity(name, ActivityKind.Consumer)`
with no parent (use the overload taking `string? parentId`; a null parent id starts a new root). A deserialize
failure (a `JsonException` or null result) counts as a failure of the message: record it and retry. Never throw
out of the loop.

Register in `AddBuildingBlocksInfrastructure`: bind `OutboxSettings`, then `services.AddHostedService<OutboxDispatcher>()`.

#### 11e — Sample: publish, subscribe, migrate

- **Create file: `backend/src/Modules/Sample/Crm.Modules.Sample.Contracts/IntegrationEvents/SampleRenamedIntegrationEvent.cs`:**
  `public sealed record SampleRenamedIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc, Guid SampleId, string Name) : IntegrationEvent(Id, OccurredOnUtc);`
- **File: `…/Commands/RenameSample/RenameSampleCommandHandler.cs`** — inject `IEventBus eventBus`. After
  `sample.Rename(...)` (line 30), read `DateTimeOffset now = dateTime.UtcNow` once and reuse it for both the rename
  and `eventBus.Publish(new SampleRenamedIntegrationEvent(Guid.NewGuid(), now, sample.Id.Value, sample.Name.Value))`.
- **Create file: `backend/src/Modules/Sample/Crm.Modules.Sample.Application/IntegrationEvents/SampleRenamedAcknowledgementHandler.cs`**
  — `CANONICAL SLICE` header comment. `IIntegrationEventHandler<SampleRenamedIntegrationEvent>`; logs
  `Sample {SampleId} renamed to {SampleName} - acknowledged` through `[LoggerMessage]`. Comment: *"In a real module
  the subscriber lives in ANOTHER module's Application project, referencing the publisher's Contracts. Sample is
  the only module, so it subscribes to itself."*
- **Create file: `…/IntegrationEvents/FlakySampleRenamedHandler.cs`** — `// DIAGNOSTICS ONLY - deleted with the
  Sample module in Story 11.` When `integrationEvent.Name` starts with `"flaky"` (ordinal, ignore case), it throws
  `InvalidOperationException("Simulated subscriber failure")` on the first **two** deliveries of that event id (a
  `private static readonly ConcurrentDictionary<Guid, int>` counter). The third delivery succeeds. When the name
  starts with `"poison"`, it always throws, which exercises dead-lettering.
- **Migration:** from `D:\CRM\backend`, run `./scripts/ef.ps1 -Module Sample migrations add AddOutbox`. **Read the
  generated file.** It must create exactly `sample.outbox_messages` and `sample.outbox_message_consumers`, with the
  filtered index and the composite PK, and must not touch `Samples`/`SampleLines`. Do not hand-edit it.

### 12 — Local infrastructure, diagnostics endpoints, request samples

**File: `backend/docker-compose.yml`** — add, with a comment matching the SQL Server remap:

```yaml
  redis:
    image: redis:7.4-alpine
    container_name: crm-redis
    ports:
      - "16379:6379"      # remapped like SQL Server - 6379 is commonly taken on shared dev machines
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:latest
    container_name: crm-telemetry
    environment:
      DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"   # local dev only
    ports:
      - "18888:18888"     # dashboard UI - traces, metrics, structured logs
      - "14317:18889"     # OTLP/gRPC ingest (OpenTelemetry:OtlpEndpoint)
```

**Create file: `backend/src/Crm.Api/Diagnostics/DevelopmentOnlyAttribute.cs`** — `sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter`.
Returns `NotFoundResult` unless `IHostEnvironment.IsDevelopment()`. These endpoints deliberately throw, echo
payloads into logs, and call the host itself, so they must not exist outside Development.

**Create file: `backend/src/Crm.Api/Diagnostics/Admin/DiagnosticsController.cs`** — namespace
`Crm.Api.Diagnostics.Admin`, `[Area(CrmAreas.Admin)] [DevelopmentOnly] sealed partial class DiagnosticsController(ILogger<DiagnosticsController> logger, IHttpClientFactory httpClients) : ApiControllerBase`:

| Action | Route | Behaviour |
|---|---|---|
| `Throw` | `GET throw` | `throw new InvalidOperationException("Simulated failure. Server=diagnostics-db;Password=do-not-leak")` — the 500 body must contain neither string |
| `Credentials` | `POST credentials` | Body `DiagnosticsCredentials(string Username, string Password, string ApiKey, string Token, DiagnosticsNested Nested)` with `DiagnosticsNested(string ClientSecret, string Note)`. Logs `Received credentials payload {@Payload}` **and** `Scalar api key {ApiKey}`, returns 204 |
| `Forbidden` | `GET forbidden` | `Problem(Error.Forbidden("Diagnostics.Forbidden", "Simulated forbidden result."))` → 403 |
| `Unauthorized` | `GET unauthorized` | `Unauthorized()` → 401, turned into a problem by `[ApiController]` client-error mapping + `CustomizeProblemDetails` |
| `Unstable` | `GET unstable?fail=true` | `fail` → `StatusCode(503)`; otherwise `Ok()`. The endpoint that "fails on demand" |
| `Outbound` | `GET outbound?fail=true` | `httpClients.CreateClient(DiagnosticsHttpClient.Name).GetAsync($"api/Admin/Diagnostics/unstable?fail={fail}")`, then `EnsureSuccessStatusCode()`, then 200 `{ "status": "downstream ok" }` |

Every action carries `[ProducesResponseType…]`.

**Create file: `backend/src/Crm.Api/Diagnostics/Public/DiagnosticsController.cs`** — namespace
`Crm.Api.Diagnostics.Public`, `[Area(CrmAreas.Public)] [DevelopmentOnly]`, `GET ping` → 200 `"pong"`. It exists
only so the Public-Area rate limit has a target before Story 55 adds real Public endpoints. The same class name in
two Areas is legal: routes are `api/[area]/[controller]`.

**Create file: `backend/src/Crm.Api/Diagnostics/DiagnosticsHttpClient.cs`** — `public const string Name = "Diagnostics";`
and `AddDiagnostics(this IServiceCollection, IConfiguration)`:
`services.AddHttpClient(Name, c => c.BaseAddress = new Uri(configuration["Diagnostics:SelfBaseUrl"] ?? "http://localhost:5187/")).AddCrmResilience(configuration);`
`appsettings.Development.json`: `"Diagnostics": { "SelfBaseUrl": "http://localhost:5187/" }`.

**Create file: `backend/requests/diagnostics.http`** — `@host = http://localhost:5187`, one request per failure
mode. Each `###` title states the expected status and `type`:

1. Unhandled exception → 500 `internal`; body has `correlationId`, **no** "Password", "Server=", or stack frames.
2. Correlation id round-trip → send `X-Correlation-Id: diag-0001` to `GET /api/Agent/Sample?name=Ahmed`; the
   response header echoes it.
3. Invalid correlation id → send `X-Correlation-Id: bad id with spaces`; the response carries a **generated**
   32-hex id instead.
4. Validation → `POST /api/Admin/Samples` `{ "name": "", "lines": [] }` → 400 `validation`, `errors.Name`.
5. Malformed body → `POST /api/Admin/Samples` with body `{ "name": ` → 400 `validation` (model-state), not 500.
6. Not found → `GET /api/Admin/Samples/00000000-0000-0000-0000-00000000dead` → 404 `not-found`, `code: Sample.NotFound`.
7. Unknown route → `GET /api/Admin/Nope` → 404 `not-found` (status-code pages).
8. Domain rule → `POST /api/Admin/Samples` `{ "name": "Dup", "lines": ["a", "A"] }` → 422 `domain-rule`,
   `code: Sample.DuplicateLine`.
9. Concurrency → `PUT /api/Admin/Samples/00000000-0000-0000-0000-000000000001` with `rowVersion: "AAAAAAAAB9E="`
   → 409 `conflict`.
10. Forbidden → `GET /api/Admin/Diagnostics/forbidden` → 403 `forbidden`.
11. Unauthorised → `GET /api/Admin/Diagnostics/unauthorized` → 401 `unauthorized`.
12. Rate limit → `GET /api/Public/Diagnostics/ping`, run **6×** within 30 s → the 6th is 429 `rate-limited` with
    `Retry-After`.
13. Redaction → `POST /api/Admin/Diagnostics/credentials` with password/apiKey/token/clientSecret → 204; the log
    shows `"***"` for all four and plain `Username`/`Note`.
14. Cache miss then hit → `GET /api/Admin/Samples/{{sampleId}}` twice → one `SELECT` in the EF log.
15. Cache invalidated → `PUT` rename with the current rowVersion, then `GET` again → a new `SELECT`.
16. Outbox delivered → the rename in 15 → within ~2 s the log shows `Delivered SampleRenamedIntegrationEvent … to
    …SampleRenamedAcknowledgementHandler` **and** to `…FlakySampleRenamedHandler`.
17. Subscriber retried → rename to `"flaky one"` → two Warning `failed … attempt` lines, then `Delivered` for the
    flaky handler. The acknowledgement handler is delivered **once**, and on the retries logs `Skipping … already
    handled`.
18. Dead letter → rename to `"poison one"` → five attempts, then `dead-lettered`.
19. Rolled-back command publishes nothing → stale-rowVersion rename (request 9 shape) → no new `outbox_messages`
    row (SQL in the comment below the request).
20. Redelivery is not re-applied → comment block with
    `UPDATE sample.outbox_messages SET ProcessedAtUtc = NULL, Attempts = 0 WHERE Id = '<id from 16>';`, then watch
    the log: both handlers log `Skipping … already handled`, and `SELECT COUNT(*) FROM sample.outbox_message_consumers WHERE MessageId = '<id>'`
    stays **2**.
21. Retries then open circuit → `GET /api/Admin/Diagnostics/outbound?fail=true` ×3 → calls 1–2: 503
    `dependency-unavailable` after 4 attempts each (4 `GET …/unstable` client spans per call in the dashboard, Polly
    `OnRetry` log lines); call 3: 503 immediately, `BrokenCircuitException` in the log, **no** downstream span.
22. Healthy downstream → `GET /api/Admin/Diagnostics/outbound?fail=false` after `BreakDuration` (15 s) → 200.

Use `# @name` + `{{…response.body…}}` request variables for `sampleId`/`rowVersion`. Create a fresh sample at the
top of the file, so the file runs top to bottom.

**Files: `backend/requests/persistence.http`, `backend/requests/health.http`** — line 1 → `@host = http://localhost:5187`.
**File: `backend/README.md`** line 35 → "`http://localhost:5187`, from `Properties/launchSettings.json`".

### 13 — Composition root

**File: `backend/src/Crm.Api/Program.cs`** — the final shape (keep the Swagger block, health checks, and module
loops exactly as they are):

```csharp
using Serilog; using Serilog.Formatting.Compact; // + existing usings, + Crm.Api.Diagnostics, + the new extension namespaces

// Bootstrap logger: startup failures (bad config, a module that throws in AddServices) are JSON too.
Log.Logger = new LoggerConfiguration().WriteTo.Console(new RenderedCompactJsonFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddCrmObservability(builder.Configuration, builder.Environment);   // Serilog + OpenTelemetry + correlation
    builder.Services.AddCrmErrorHandling();                                              // replaces AddProblemDetails()
    builder.Services.AddCrmRateLimiting(builder.Configuration);

    builder.Services.AddBuildingBlocksApplication(builder.Configuration, [.. ModuleCatalog.All.Select(m => m.Module.ApplicationAssembly)]);
    builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration, builder.Environment);
    // … module loop, AddControllers + application parts, Swagger - unchanged …

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddDiagnostics(builder.Configuration);
    }

    var app = builder.Build();

    // ORDER MATTERS:
    // 1. correlation first - every later middleware, log line, and problem body can read it;
    // 2. request logging outside the exception handler - it logs the final (handled) status once;
    // 3. exception handler, then status-code pages - bare 401/404/405 become problems too;
    // 4. rate limiter after routing (implicit), so the endpoint's [Area] is known (probe 3).
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseCrmRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment()) { /* UseSwagger, UseSwaggerUI, MigrateAndSeedModulesAsync - unchanged */ }

    app.UseRateLimiter();
    // health checks - unchanged
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)   // HostAbortedException = `dotnet ef` stopping the host on purpose
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

Delete the `// Story 03 replaces this…` comment. `CorrelationIdMiddleware` is registered by type, so no DI
registration is needed. If CA1031 (catch general exception) fires on the top-level catch, the `when` filter does
not satisfy it; suppress **that one line** with `#pragma warning disable CA1031` and a justification comment. It is
the process's last-chance handler, which is the rule's documented exception.

### 14 — Record the conventions (CLAUDE.md, README)

**File: `CLAUDE.md`:**
- "Application, API, and persistence" → **Results** bullet: append *"Error `type` URIs are the constants in
  `Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs` — never inline a type string, never rename one. A
  `DomainException` from an aggregate becomes 422 `domain-rule`; a validator failure 400 `validation`."*
- New bullet **Logging:** *"Serilog, compact JSON, every line carries `CorrelationId` (and user/tenant/department/branch
  when authenticated). Log only through `ILogger<T>` with a `[LoggerMessage]` method — CA1848/CA2254 are errors and
  `System.Console` is banned. Anything logged with `{@X}` is redacted by property name (password, secret, token,
  apikey, …); never log a request body any other way."*
- New bullet **Caching:** *"Mark a query `ICacheableQuery` (key and tags `{module}:{aggregate-plural}:{id}`, built
  by a `<Module>CacheKeys` class) and every command that changes its data `ICacheInvalidatingCommand` with the same
  tag. `CachingBehavior` prefixes the caller's scope; invalidation runs after commit. Never cache a query whose
  result depends on the user without `VaryByUser`."*
- New bullet **Outbound HTTP:** *"Every `HttpClient` is registered with `.AddCrmResilience(configuration)`. No
  hand-rolled retry loops."*
- "Modules talk only through Contracts" → **Integration event** sub-bullet: replace *"The in-process bus and outbox
  are delivered by Story 03."* with *"Publish with `IEventBus.Publish` from the command handler (never from a
  domain-event handler — that runs after commit and throws). Subscribe with an `IIntegrationEventHandler<T>` in the
  subscriber's Application project; it is discovered automatically and runs in the subscriber's own transaction,
  at least once, with a per-handler idempotency record. Event records are append-only."*

**File: `backend/README.md`** — add sections **"Error responses"** (the `ProblemTypes` table: status, `type`,
when), **"Observability"** (`docker compose up -d` now starts Redis on 16379 and the Aspire dashboard at
`http://localhost:18888`; where to find a request's trace by `X-Correlation-Id`), **"Caching"**, **"Integration
events and the outbox"** (tables, retry/backoff, dead letters are rows with `ProcessedAtUtc IS NULL AND Attempts >= MaxAttempts`,
and how to replay one: reset `Attempts`/`NextAttemptAtUtc`), and add the new pins to "Notable pinned versions",
including the EF Core instrumentation beta.

---

## Frontend Tasks

No frontend changes required. **Story 05** maps `ProblemTypes` URIs to `en`/`ar` messages. The list it needs is
the README "Error responses" table from Task 14.

---

## Edge Cases & Failure Modes

- **NU1605 (package downgrade) on restore.** `Microsoft.Extensions.Http.Resilience` 10.10.0 requires
  `Microsoft.Extensions.*` ≥ 10.0.12, while `Microsoft.Extensions.Logging.Abstractions` is pinned at 10.0.11
  (`Directory.Packages.props` line 12). If restore fails with NU1605, bump **only** that pin to **10.0.12** and
  record why in its comment. **Do not** touch the EF Core 10.0.11 pins (they move in lockstep with `dotnet-ef`), and
  **do not** add `NoWarn`.
- **Exception handler loses the correlation id.** An `AsyncLocal` set in `CorrelationIdMiddleware` does not flow
  back out to outer middleware. `CorrelationIdAccessor` falls back to `HttpContext.Items` (Task 4). Verify with
  `diagnostics.http` #1: the 500 body **and** the exception log line both carry the id.
- **Header lost on the 500.** `ExceptionHandlerMiddleware` clears headers before re-executing, so the header is
  written in `OnStarting` (Task 4). Verify the header on request #1.
- **Hostile `X-Correlation-Id`.** CR/LF, >64 chars, or spaces are rejected and replaced, never echoed
  (`CorrelationIdMiddleware`, Task 4). Request #3.
- **Handled exceptions are not logged by .NET 10** (**Probe 5**). Every `GlobalExceptionHandler` branch logs
  explicitly. If a 500 shows up in the response but not in the log, this is why.
- **Exception text in logs.** The 500 **response** never contains `ex.Message`. The **log** does, because that is
  the point of the log. The redaction policy covers structured properties, not free text inside exception messages.
  A connection string inside a `SqlException` message reaches the log. Acceptable for this story; recorded in the
  README "Observability" section.
- **EF Core sensitive-data logging** (`PersistenceSettings.EnableSensitiveDataLogging`, Development only) prints SQL
  parameter values, which property-name redaction cannot see. It stays Development-only. Story 07 must not log
  password hashes through it: the Identity module sets its credential columns' parameters as non-sensitive or keeps
  the flag off.
- **`ValidationException` thrown instead of returned** (`ValidationBehavior` line 43, a request whose response is
  not a `Result`) → 400 `validation` through `GlobalExceptionHandler`, same body shape as the Result path.
- **Validation messages containing `" | "` or `":"`** break `ValidationErrorParser` (inherited from Story 02). A
  field message becomes misattributed; the status and `type` stay correct. Not fixed here.
- **Redis down.** `DistributedCacheService` logs a Warning and runs the query (Task 8). `connectTimeout=2000` bounds
  the delay; with Redis stopped, each cacheable query is up to ~2 s slower, and the log says why. **Invalidation
  failure** is logged at Error: the entry stays stale up to its TTL (default 5 min).
- **No Redis configured** (no `ConnectionStrings:Redis`) → in-memory cache plus a startup warning. **Correct only on
  one instance.** A second instance would serve entries the first invalidated.
- **Cache poisoned by a race** (a query reads the old row, a command commits and invalidates, the query then caches
  the old row). This is prevented by reading tag versions **before** the factory runs (Task 8,
  `DistributedCacheService.GetOrCreateAsync`). Invalidation after commit (Task 8, `TransactionBehavior`) closes the
  other half.
- **Cross-tenant cache leak** once Story 06 fills scope claims. Prevented by the scope prefix in `CachingBehavior`.
  Invalidation is by **tag**, which is scope-free, so an admin's rename invalidates every branch's cached copy.
- **Cached failures.** A failed `Result` is never cached (the factory returns `null`). A 404 for an id that is
  created later is therefore not stale.
- **Rate limiter behind a reverse proxy.** `RemoteIpAddress` is the proxy's address, so every client shares one
  partition. Deployment must add `UseForwardedHeaders` with known proxies before this goes live. Record it in the
  README; not in scope.
- **Circuit breaker validation.** `HttpStandardResilienceOptions` validates at first client creation:
  `SamplingDuration` ≥ 2 × `AttemptTimeout` (30 s vs 10 s ✓), and `TotalRequestTimeout` > `AttemptTimeout` ✓. A bad
  config fails the **first outbound call** with `OptionsValidationException`, not startup. Request #21 is the check.
- **The circuit is per named client**, not per host. The Diagnostics client's open circuit does not affect a future
  ERP client.
- **Integration event published from a domain-event handler or a query** → `InvalidOperationException`
  (`TransactionBehavior`, Task 11b). The alternative is losing the event silently.
- **Command fails after publishing** (failed `Result`, concurrency conflict, or exception) → pending events are
  discarded, the transaction rolls back, and no outbox row exists (request #19).
- **Crash between a subscriber's commit and the publisher's `MarkProcessed`** → the message is redelivered and the
  consumer row makes the handler skip (request #20). **At-least-once delivery, effectively-once application**, per
  handler.
- **Two dispatchers** (two API instances) pick the same batch. Both run the handler, and the second
  `RecordConsumer` insert fails on the composite PK, which rolls back that handler's work. The handler's side
  effects must therefore be inside its transaction. A handler that calls an external API directly can double-fire;
  such work must go through its own outbox (Story 22). A row-lock (`UPDLOCK, READPAST`) claim is deferred until
  multi-instance deployment; note it in the README.
- **Event type renamed or moved** → `registry.TryGet` fails for old rows, which are then marked processed as "no
  subscriber". This is why event records are **append-only** (Task 11a XML doc, CLAUDE.md).
- **Poison message** → dead-lettered after `MaxAttempts` (5), with backoff 2, 4, 8, 16 s. `LastError` is kept. It is
  never deleted automatically (request #18).
- **SQL Server down while the dispatcher polls** → the cycle logs Error and retries next interval. The host does not
  stop (the `BackgroundService` catch, Task 11d).
- **Subscriber sends a MediatR command** → `TransactionBehavior` calls `BeginTransactionAsync` on a context that
  already has a transaction, and EF throws. The handler must work with its module's repositories directly (XML doc
  on `IIntegrationEventHandler<T>`).
- **Aspire dashboard image tag `latest`.** If the pull fails or the UI changes, pin the tag that works and record it
  in `docker-compose.yml`. The OTLP ingest port inside the container is **18889**.
- **Diagnostics endpoints outside Development** → 404 (`DevelopmentOnlyAttribute`), and `AddDiagnostics` is not
  called, so no self-calling HTTP client exists.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not create a test project, a
`tests/` folder, a `*.Tests.csproj`, or add any test framework.** This matrix replaces the suite. Run every row
against a live API with `docker compose up -d` (SQL Server, Redis, dashboard), and record each result in the story
report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Clean build | `dotnet build backend/Crm.sln` | **0 warnings, 0 errors**, no `CRM0001` |
| 2 | Migration | `./scripts/ef.ps1 -Module Sample database update` (or just run the API in Development) | `sample.outbox_messages`, `sample.outbox_message_consumers` exist; `sample.__EFMigrationsHistory` has 2 rows |
| 3 | 500 shape | `diagnostics.http` #1 | 500, `type` `urn:crm:problem:internal`, `correlationId`, `traceId`; **no** `Password`, `Server=`, `at Crm.` |
| 4 | 500 in Production | Run with `ASPNETCORE_ENVIRONMENT=Production` and a temporary throw in `SampleController.Get` (revert after) | Same body shape, no stack trace. Diagnostics routes 404 |
| 5 | Status/type matrix | #4–#11 | 400 validation, 400 validation (malformed), 404 not-found ×2, 422 domain-rule, 409 conflict, 403 forbidden, 401 unauthorized; **six distinct** `type` values |
| 6 | Correlation | #2, #3, then grep the console for that id | Header echoed / regenerated; **every** log line of that request (request log, MediatR, EF command) carries `CorrelationId` |
| 7 | JSON logs | Console output | Every line parses as JSON (`@t`, `@m`, `@l`) |
| 8 | User/tenant enrichment | Not reachable before Story 07 (no authentication yet) | Enricher code reads `CrmClaimTypes`; **Story 07 re-runs this row with a real token**. Record as deferred |
| 9 | Redaction | #13 | `Password`, `ApiKey`, `Token`, `ClientSecret` → `"***"` in both `@m` and the property; `Username`, `Note` plain; scalar `{ApiKey}` → `"***"` |
| 10 | Cache hit | #14 with EF command logging | **One** `SELECT … FROM [sample].[Samples]` for two GETs |
| 11 | Cache invalidation | #15 | A fresh `SELECT` after the rename, and the GET returns the new name |
| 12 | Redis down | `docker stop crm-redis`, repeat #14 | Both GETs 200, two `SELECT`s, Warning `Cache unavailable`; `docker start crm-redis` |
| 13 | Rate limit | #12 | 6th call 429 `rate-limited` + `Retry-After`; `GET /api/Admin/Samples/{id}` ×10 never 429 |
| 14 | Retries in trace | #21 call 1, dashboard → Traces | One server span with **4** `GET` client spans to `/unstable`; Polly `OnRetry` logs ×3 |
| 15 | Circuit opens | #21 calls 2–3 | Call 3 returns 503 instantly with **no** client span; log shows `BrokenCircuitException` |
| 16 | EF + MediatR spans | Dashboard trace of #14 | Server span → `GetSampleByIdQuery` span → EF `SELECT` span (first call only) |
| 17 | Outbox same transaction | #15 with the `Database.Transaction` Debug log | `INSERT INTO [sample].[outbox_messages]` between `BEGIN` and `COMMIT` of the rename |
| 18 | Not delivered on rollback | #19 | No new outbox row, no `Delivered` line |
| 19 | Delivered once per subscriber | #16 | Exactly one `Delivered` per handler; 2 rows in `outbox_message_consumers`; `ProcessedAtUtc` set |
| 20 | Subscriber retried | #17 | 2 failures then success; `Attempts` = 2 on the row before success |
| 21 | Redelivery not re-applied | #20 | Both handlers `Skipping … already handled`; consumer count stays 2 |
| 22 | Dead letter | #18 | `Attempts` = 5, `ProcessedAtUtc` NULL, `NextAttemptAtUtc` NULL, `LastError` set, Error log line |
| 23 | Dispatch span linked | Dashboard, trace of #15 | The `outbox SampleRenamedIntegrationEvent -> …` spans appear in the **same trace** as the PUT |
| 24 | Console ban | Add `Console.WriteLine("x");` to `SampleController`, build | **RS0030**; revert |
| 25 | Template ban | Add `logger.LogInformation($"x {1}");` anywhere with a logger, build | **CA2254/CA1848** error; revert |
| 26 | Regression | `persistence.http`, `health.http` on port 5187 | Unchanged results; the 409/400 bodies now carry `type` + `correlationId` |

---

## Migration / Rollback

- **`AddOutbox`** is additive: two new tables in `sample`, no change to existing ones. Rollback:
  `./scripts/ef.ps1 -Module Sample database update Initial`, then `./scripts/ef.ps1 -Module Sample migrations remove`.
- **Every future module** gets both outbox tables in its **Initial** migration automatically, because
  `ModuleDbContext` maps them. A module scaffolded from Sample must not delete them from its first migration.
- **Half-applied state:** a failed `database update` leaves the tables without a history row. On a dev box,
  `docker compose down -v && docker compose up -d` and rerun. There is no production data.
- **Undelivered outbox rows when rolling the code back** stay in the table and are harmless. Delete them if the
  migration is rolled back too.
- **Logging config migration:** removing the `"Logging"` sections is safe only together with `AddSerilog`. Commit
  them in the same change.

---

## Verification Steps

1. **Infrastructure starts:** `cd backend && docker compose up -d` → `crm-sqlserver`, `crm-redis` healthy;
   `crm-telemetry` running and `http://localhost:18888` loads.
2. **Backend builds:** `dotnet build backend/Crm.sln` from `D:\CRM` → **0 warnings, 0 errors**.
3. **Migration generated and read:** Task 11e; the file contains only the two outbox tables.
4. **Backend runs:** `dotnet run --project backend/src/Crm.Api` → JSON log lines, `Now listening on: http://localhost:5187`,
   migrations applied, no unhandled exception, the dispatcher polling quietly.
5. **Failure modes:** run `backend/requests/diagnostics.http` top to bottom → matrix rows 3, 5, 6, 9–11, 13–23.
6. **Degradation:** matrix rows 4 and 12.
7. **Bans bite:** matrix rows 24–25, then **revert** and repeat step 2.
8. **Regression:** matrix row 26, then `grep -rn "Console\.\|ToActionResult(this" backend/src` → no matches.

---

## Done Criteria

- [ ] An unhandled exception returns **500** with a problem body carrying `correlationId`, and **never** a stack
      trace or connection string, in Development **and** Production.
- [ ] Validation → **400**, missing resource → **404**, domain rule → **422**, concurrency → **409**, each with a
      distinct stable `type` from `ProblemTypes`; 401/403/429/503 are typed too.
- [ ] `ResultActionResultExtensions.cs` is deleted; `ApiControllerBase` builds problems through `ProblemDetailsFactory`.
- [ ] The same correlation id appears in the `X-Correlation-Id` response header and in **every** log line of that
      request, including the 500 and the EF command lines; it is forwarded on outbound calls and on outbox rows.
- [ ] Log output is JSON in every environment and includes `UserId`/`TenantId`/`DepartmentId`/`BranchId` from
      `CrmClaimTypes` when authenticated (**runtime check deferred to Story 07**, recorded in the report).
- [ ] Traces cover HTTP in/out, EF Core, and MediatR, and are visible in the Aspire dashboard; metrics include
      `crm.request.duration` and Polly.
- [ ] `GetSampleByIdQuery` (an `ICacheableQuery`) hits the database **once** across two identical calls, confirmed
      from the EF Core log, and `RenameSampleCommand`/`DeleteSampleCommand` invalidate it **after commit**.
- [ ] A Redis outage degrades to uncached reads with a Warning, never an error response.
- [ ] Exceeding the Public limit returns **429** with `Retry-After`; no other Area is limited.
- [ ] The Diagnostics client pointed at `unstable?fail=true` shows the configured retries in the trace, then an
      open circuit after `MinimumThroughput` failures.
- [ ] `System.Console` is banned (RS0030), CA2254/CA1848 are errors, and `grep -rn "Console\." backend/src` is empty.
- [ ] The destructuring policy and redaction enricher redact password, token, apikey, and secret values, confirmed
      by posting the credential payload and reading the log.
- [ ] A rename writes `sample.outbox_messages` **inside** the command's transaction; a rolled-back rename writes
      nothing; each subscriber gets the event once after commit; a throwing subscriber is retried; a redelivered event
      is skipped by the idempotency record; a poison event dead-letters after 5 attempts — confirmed in the EF log and
      by SQL.
- [ ] `AddOutbox` migration committed; `ef.ps1 -Module Sample database update` applies it from a cold database.
- [ ] Every failure mode has a request in `backend/requests/diagnostics.http`, and every request was run.
- [ ] `persistence.http`, `health.http`, and the README point at **5187**.
- [ ] `CLAUDE.md` and `backend/README.md` record the conventions from Task 14.
- [ ] **No test project, no `tests/` folder, and no test framework package exists anywhere in the repository.**
- [ ] `00-overview.md` updated with this story.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04.**
