# platform-foundation — plan overview

> **MVP re-scope (2026-09-29):** 05 and 06 remain in the MVP with trimmed scope (05 also owns the responsive shell; 06 adds memberships and a Development-only dev login, and defers teams/business hours/calendars). See `.squad/BACKLOG.md`, "MVP re-scope".

Entry point for the **Platform Foundation** feature. This is **feature 1 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** BuildingBlocks and the `Sample` template module (01–03); creates the `Organisation` module (06). Frontend only: 04, 05. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 01 | [`backend-solution-scaffold-with-ddd-and-clean-architecture`](../../stories/platform-foundation/backend-solution-scaffold-with-ddd-and-clean-architecture/intake.md) | Backend solution scaffold with DDD and Clean Architecture | [`01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md`](01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md) | none. This is the first story |
| 02 | [`persistence-baseline-with-auditing-and-soft-delete`](../../stories/platform-foundation/persistence-baseline-with-auditing-and-soft-delete/intake.md) | Persistence baseline with auditing and soft delete | [`02-story-persistence-baseline-with-auditing-and-soft-delete.md`](02-story-persistence-baseline-with-auditing-and-soft-delete.md) | CRM-01 |
| 03 | [`cross-cutting-concerns-error-handling-logging-and-caching`](../../stories/platform-foundation/cross-cutting-concerns-error-handling-logging-and-caching/intake.md) | Cross cutting concerns error handling logging and caching | [`03-story-cross-cutting-concerns-error-handling-logging-and-caching.md`](03-story-cross-cutting-concerns-error-handling-logging-and-caching.md) | CRM-01, CRM-02 |
| 04 | [`frontend-scaffold-with-nuxt-typescript-and-tailwind`](../../stories/platform-foundation/frontend-scaffold-with-nuxt-typescript-and-tailwind/intake.md) | Frontend scaffold with Nuxt TypeScript and Tailwind | [`04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md`](04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md) | CRM-01, for the OpenAPI document |
| 05 | [`bilingual-arabic-and-english-support-with-rtl-layout`](../../stories/platform-foundation/bilingual-arabic-and-english-support-with-rtl-layout/intake.md) | Bilingual Arabic and English support with RTL layout | _not planned yet_ | CRM-03, CRM-04 |
| 06 | [`multi-department-and-multi-branch-organisation-scoping`](../../stories/platform-foundation/multi-department-and-multi-branch-organisation-scoping/intake.md) | Multi department and multi branch organisation scoping | _not planned yet_ | CRM-02, CRM-04 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._

### Story 01 — decisions carried forward

- **MediatR pinned to 12.4.1 (Apache-2.0).** 13.x and later are RPL-1.5 or a paid Lucky Penny Software licence;
  RPL-1.5 would require releasing this CRM's source. Do not bump without a licence decision.
- **`dotnet ef` pinned to 10.0.11 via a local tool manifest.** The globally installed 10.0.3 is older than the
  EF Core packages story 02 needs, and EF errors on that skew rather than warning.
- **`pnpm` is not installed on the dev machine.** It blocks Story 04, not this story.

### Story 02 — decisions carried forward

- **EF Core pinned to 10.0.11**, matching the `dotnet-ef` local tool already pinned in
  `.config/dotnet-tools.json`. Tool and packages move together; EF errors, not warns, on skew.
- **The soft-delete query filter is registered with EF Core 10's _named_ `HasQueryFilter` overload**
  (`CrmDbContext.SoftDeleteFilterName`). **Story 06 must add its tenant/department/branch filter as a second
  named filter.** The unnamed overload silently *replaces* an existing filter — using it would resurrect
  soft-deleted rows product-wide with no error.
- **Domain events dispatch from `TransactionBehavior` after `CommitAsync`, never from a `SaveChangesInterceptor`.**
  Verified by probe: with an explicit transaction open, `SavedChangesAsync` fires *before* the commit.
- **Soft delete converts `Deleted` → `Unchanged` and then rescues cascaded owned entries**, not
  `Deleted` → `Modified`. The naive form nulls owned value-object columns — it throws on a non-nullable column
  and silently blanks data on a nullable one.
- **API surface changed by owner decision: MVC controllers in per-persona Areas (`Agent`, `Portal`, `Admin`,
  `Public`) with Swashbuckle Swagger, one document per Area.** This replaces story 01's minimal-API
  `IEndpointGroup` convention and `AddOpenApi`/`MapOpenApi`. Recorded in `CLAUDE.md` by Story 02 Task 15.
  **Story 04 generates its typed client from the four per-Area Swagger documents — `/openapi/v1.json` is gone.**
- **Swashbuckle pinned to 10.2.3**, which is OpenAPI.NET v2 — `OpenApiInfo` lives in `Microsoft.OpenApi`, not
  `Microsoft.OpenApi.Models`.
- **Only one OpenAPI generator ships.** `Microsoft.AspNetCore.OpenApi` stays pinned in
  `Directory.Packages.props` but is no longer referenced by `Crm.Api`.

### Architecture change — modular monolith (after Story 02, owner decision)

The backend was restructured from four layer projects into a **modular monolith** after Stories 01 and 02 were
implemented. Stories 01 and 02 are not re-planned; their plans stay as the historical record. Every plan from
Story 03 onward is written against this layout (full rules in `CLAUDE.md`, runbook in `backend/README.md`):

- **Projects.** `Crm.Domain/Common` became `BuildingBlocks/Crm.SharedKernel`; `Crm.Application/Common` became
  `Crm.BuildingBlocks.Application`; the shared parts of `Crm.Infrastructure` became `Crm.BuildingBlocks.Infrastructure`;
  `ApiControllerBase`/`CrmAreas`/the Result mapper moved to `Crm.BuildingBlocks.Presentation`. `Crm.Api` is only the
  host. The Sample slice became the five-project template module `backend/src/Modules/Sample/`.
- **One DbContext and schema per module.** `CrmDbContext` is gone. Each module has a `ModuleDbContext` subclass in its
  own schema (`sample`) with its own `__EFMigrationsHistory`. The old `dbo` `Initial` migration was deleted and the
  Sample module re-migrated (`20260927232755_Initial`). No production data existed. Drop the orphaned `dbo` tables
  or run `docker compose down -v` on existing dev databases.
- **The soft-delete filter name is now `ModuleDbContext.SoftDeleteFilterName`.** Story 06 adds the scope filter as a
  second *named* filter in `ModuleDbContext`, which applies it to every module. The Story 02 rule against the unnamed
  overload still stands.
- **Per-module unit of work.** Handlers inject `I<Module>UnitOfWork`. `TransactionBehavior` resolves the command's
  module from its assembly through `IUnitOfWorkResolver`. Repositories are registered per aggregate with
  `AddRepository<TAggregate, TId, TContext>()`, not as an open generic.
- **MediatR is registered once by the host** over every module's Application assembly (`ModuleCatalog`).
- **Boundaries are build-enforced.** `backend/Directory.Build.targets` fails with `CRM0001` on a forbidden
  `ProjectReference`. Presentation projects set `DisableTransitiveProjectReferences`, so Domain types are not visible
  to controllers. Verified by probe: each rule fires, and a `using` of the Domain namespace in a controller gives `CS0234`.
- **Banned-API lists are central.** `backend/build/BannedSymbols.{Clock,TimeProvider,Pure}.txt`, wired once in
  `Directory.Build.props`. No per-project `BannedSymbols.txt` any more.
- **Time goes through `IDateTime`** (owner decision). `System.DateTime` is banned as a type, `DateTimeOffset.UtcNow`/`Now`
  are banned, and `System.TimeProvider` is banned everywhere except `Crm.BuildingBlocks.Infrastructure`, whose
  `SystemDateTime` is the only implementation. `TimeProvider` is no longer registered in DI.
- **`ef.ps1` takes `-Module <Name>`.** Migrations land in that module's `Persistence/Migrations`.
- **Story 03 now also owns the integration-event bus and outbox**, the only cross-module write path. Its intake was
  extended accordingly.
- **Every intake's stack section was updated**, and each intake now names its backend module.

### Story 03 — decisions carried forward

- **Error `type` URIs are URNs in one class**, `Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs`
  (`urn:crm:problem:validation`, `…:domain-rule`, …). **Story 05 maps them to `en`/`ar` messages. Never rename one.**
- **Claim names are fixed in `CrmClaimTypes`** (`sub`, `tenant_id`, `department_id`, `branch_id`). **Story 07 issues
  tokens with exactly these**, and re-verifies the "log carries user/tenant/department/branch" criterion, which cannot
  run before authentication exists.
- **Cache keys are prefixed with the caller's tenant/department/branch scope; invalidation is by scope-free tag.**
  Story 06 fills the scope values and must not change the key shape.
- **Integration events are published from the command handler through `IEventBus`**, written to the module's own
  `outbox_messages` in the command's transaction, and delivered at least once, with a per-handler idempotency row in
  `outbox_message_consumers`. Publishing from a domain-event handler or a query throws. Event records are append-only.
- **`OpenTelemetry.Instrumentation.EntityFrameworkCore` is pinned to a beta** (1.19.1-beta.1). It has never shipped
  stable.
- **All `.http` files use `http://localhost:5187`** (from `launchSettings.json`). The earlier `5080` was wrong.

### Story 04 — decisions carried forward

- **pnpm 12.6.0, enabled without admin rights** with `corepack enable pnpm --install-directory "$APPDATA/npm"`.
  Plain `corepack enable` fails with EPERM on `C:\Program Files\nodejs`. Pinned by `packageManager` in
  `frontend/package.json`. pnpm 12 reads its settings from `frontend/pnpm-workspace.yaml` (`allowBuilds`,
  `overrides`), not from `package.json`.
- **Every frontend dependency is pinned exactly (no `^`).** Nuxt 4.5.2, Vue 3.5.43, Tailwind 4.3.3 via
  `@tailwindcss/vite` (**not** `@nuxtjs/tailwindcss`, which is v3-era), `@nuxt/eslint` 1.17.0 on ESLint 10.11.0,
  Pinia 4.0.3, openapi-typescript 7.13.0, openapi-fetch 0.17.0.
- **TypeScript stays on 5.9.3.** 7.x is `latest` but openapi-typescript peers `^5.x`, and vue-tsc sits on the TS 5 API.
- **`flat-cache` is overridden to 6.1.23.** 6.1.24 depends on an unpublished `cacheable@^2.5.1`. Remove the override
  only when a clean install without it succeeds.
- **The npm registry on this machine is a slow mirror** (`najizportalnpm.azurewebsites.net`, user `~/.npmrc`).
  Never commit a project `.npmrc` that overrides it.
- **Design tokens are `--crm-*` CSS variables mapped with `@theme inline`**, and `dark:` keys off `data-theme`.
  Plain `@theme` breaks nested theming. **Story 58 rebrands by overriding `--crm-*` only.**
- **API types come only from `pnpm gen:api`.** It fetches the four Development Swagger documents into
  `frontend/openapi/*.json` and generates `frontend/app/api/generated/*.ts`. Both are committed, and
  `--offline` regenerates from the snapshots. **A backend story that changes a controller or DTO reruns it in the
  same commit.**
- **The browser calls `/api` on the Nuxt origin.** `nitro.devProxy` forwards to 5187 in development, so the backend
  has **no CORS policy**. Production needs a same-origin reverse proxy (a deployment decision, not yet made).
- **`createCrmFetch` contract (Story 07 must keep it):** one single-flight refresh per 401 burst, one retry, then
  `AuthSession.clear()` and redirect to `/login?redirect=…`. Story 07 implements `AuthSession`
  (`accessToken`, `refresh`, `clear`, `isRefreshRequest`) in a client-only plugin and replaces `pages/login.vue`.
- **Validation problems are never toasted.** `toProblem` camel-cases each segment of the backend's PascalCase field
  keys (`Name` → `name`, `Lines[0]` → `lines[0]`). `useFormErrors().apply` binds them to fields.
- **Physical-direction utilities are lint errors from Story 04 on** (templates and script strings). The kit uses
  `ui-hover:`/`ui-focus:` so `/_dev/ui` can force states. **Story 05 must not relax either rule.**
- **Shell copy lives in `app/copy/en.ts`, and the UI kit has no copy.** Story 05 moves `shellCopy` into locale files.
- **`/_dev/**` is stripped from production by `frontend/modules/dev-only-pages.ts`** (`pages:extend`). `ignore`
  under `$production` was proven not to work.
