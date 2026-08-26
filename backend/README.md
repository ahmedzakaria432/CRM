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

# 4. Run the API
dotnet run --project backend/src/Crm.Api
```

The API listens on the URL printed at startup (Kestrel's default `http://localhost:5080` in Development
unless overridden). Swagger / OpenAPI JSON is available at `/openapi/v1.json` in Development.

## Verify

There is **no test project in this repository** — see "No automated tests" below. Verification is:

```bash
dotnet build backend/Crm.sln   # must be clean, zero warnings
```

...plus exercising the `.http` files under `backend/requests/` against a running API (VS Code's REST Client
extension, JetBrains' HTTP Client, or `curl` all work). Start with `backend/requests/health.http`.

## Add a new vertical slice

Every use case follows the shape of the **canonical sample slice** at
`src/Crm.Application/Sample/Queries/GetSample/` (deleted once Story 11 lands the first real feature):

```
src/Crm.Application/<Context>/{Commands,Queries}/<UseCase>/
    <UseCase>{Command,Query}.cs            request record : IRequest<Result<T>>
    <UseCase>{Command,Query}Handler.cs     handler, depends only on abstractions
    <UseCase>{Command,Query}Validator.cs   FluentValidation
```

Expose it from `Crm.Api` by adding a class that implements `Crm.Api.Endpoints.IEndpointGroup` under
`src/Crm.Api/Endpoints/`. It is picked up automatically by assembly scan in
`EndpointGroupExtensions.MapEndpointGroups()` — no manual registration needed. See `SampleEndpoints.cs`.

Handlers return `Result` / `Result<T>` (`src/Crm.Domain/Common/Results/`), never throw for expected business
failures. The API translates a failed `Result` to an RFC 9457 problem-details response via
`Crm.Api.Common.ResultEndpointExtensions.ToProblem()` — a deliberately minimal mapper that Story 03 replaces
with the global exception handler and a static class of stable `type` URIs. Do not build that mapping
framework out further in the meantime.

## The layer rule

Dependency direction is strictly `Api -> Infrastructure -> Application -> Domain`. `Crm.Domain` has:

- **No project references at all.** Adding one is a design mistake, not a style nit.
- **No package reference to EF Core, MediatR, or ASP.NET.** Enforced two ways:
  - Structurally: there is nothing to reference, so `using Microsoft.EntityFrameworkCore;` fails with
    `CS0246` in the shipped state.
  - If the package *is* ever added, `Microsoft.CodeAnalysis.BannedApiAnalyzers` (wired via
    `BannedSymbols.txt` in each project + `.editorconfig`) fails the build with `RS0030` on first use of a
    banned namespace or member. `DateTime.Now` / `DateTime.Today` are banned solution-wide the same way —
    use `DateTimeOffset.UtcNow`.

## No automated tests

This project ships without automated tests — a deliberate decision recorded in `CLAUDE.md` ("Testing
policy"). Do not create a `tests/` folder, a `*.Tests.csproj`, or add xUnit/NUnit/FluentAssertions/
Testcontainers to any project file. The compiler (`TreatWarningsAsErrors`, nullable, the banned-API
analyzer) and the `.http` request files under `backend/requests/` are what take a test suite's place.

## Notable pinned versions

- **MediatR 12.4.1**, not the latest. 13.x+ is licensed under RPL-1.5 or a paid Lucky Penny Software
  licence; RPL-1.5 would require releasing this CRM's source. 12.4.1 is the last `Apache-2.0` release.
  Do not bump without a licence decision.
- **`dotnet-ef` 10.0.11** via the local tool manifest at `.config/dotnet-tools.json` (repository root).
  Run `dotnet tool restore` before `dotnet ef ...` commands — the globally installed version on this
  machine (10.0.3) predates the EF Core packages referenced here and will error on the mismatch.
