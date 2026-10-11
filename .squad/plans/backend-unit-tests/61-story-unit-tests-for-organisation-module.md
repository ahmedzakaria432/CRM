# Story 61 — Unit tests — Organisation module

## Prerequisites

- **Tracker id UT-02.** Row 2 of [`.squad/UNIT-TEST-STORIES.md`](../../UNIT-TEST-STORIES.md). Depends on UT-01 (Story 60, committed as
  `Implement Story 60 — Unit tests — foundation and building blocks (UT-01)`). Runs alone: UT-03..UT-05 are separate sessions.
- **Module touched: `Organisation`** (stories 06 and 58). Only `backend/tests/Crm.Modules.Organisation.Tests/**` is written. **Nothing under
  `backend/src/` changes.** A bug or an untestable seam found while testing is recorded in the tracker's Follow-ups, never fixed here.
- **The test project exists and builds empty:** `backend/tests/Crm.Modules.Organisation.Tests/Crm.Modules.Organisation.Tests.csproj` references
  the module's Domain, Application and Infrastructure (and, transitively, Contracts, SharedKernel and the BuildingBlocks Application /
  Infrastructure projects). `tests/Directory.Build.props` supplies `xunit.v3` 4.0.2, `NSubstitute` 6.2.0, `NSubstitute.Analyzers.CSharp`,
  global usings `Xunit` + `NSubstitute`, `OutputType Exe`.
- **UT-01's rules apply unchanged** (CLAUDE.md "Testing policy" is already rewritten): plain `Assert`, no FluentAssertions, no `!`, no
  suppressions, no `DateTime`/`DateTimeOffset.UtcNow`/`TimeProvider`, no `System.Console`, PascalCase test names without underscores
  (CA1707), one test class per production type, folders mirror the production project, `TreatWarningsAsErrors` on.
- **Plans mined for test cases** (code wins where they differ; every difference goes to Follow-ups):
  `platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md` (product rules, codes `^[A-Z0-9-]{2,20}$`,
  names ar+en ≤ 200, deactivation is not deletion, removed membership soft-deleted, sign-in lookup lifts only the scope filter) and
  `integrations-platform/58-story-custom-branding-theming-and-tenant-configuration.md` (D1–D15, S1–S5, edge-case list, reference ratios
  `#777777` → 4.48 fail / `#767676` → 4.54 pass, swap-style revert, cross-tenant logo = 404).
- **No integration events, no domain events, no consumers exist in this module** (read: no `IEventBus`, `IIntegrationEventConsumer`,
  `INotificationHandler` or `IDomainEvent` anywhere under `backend/src/Modules/Organisation`; plan 58 D14 says Audit's `BrandingChanged` is
  deferred). The intake's "events / consumers" criteria are satisfied by tests that assert **no domain event is raised** by each aggregate
  method (`DomainEvents` empty) and a record in Follow-ups. Do not invent events.

## Story Goal

Every aggregate, value object, command handler, validator, query handler, specification, DTO mapping, theming algorithm, the
`IOrganisationModuleApi` implementation and the unit-testable Infrastructure adapters (`SvgSanitizer`, `BrandingConverter`,
`OrganisationSeeder`, `OrganisationModule` registrations) of the Organisation module has unit tests, so `dotnet test --project
tests/Crm.Modules.Organisation.Tests` (from `backend/`) catches regressions. Expected size: **≈ 520–620 tests in 5 slices**, zero skipped.

Out of scope (intake): EF configurations and migrations, controllers/HTTP, SQL Server, other modules' tests, building blocks (UT-01), frontend.

## Context — Read These Files First

All paths are under `backend/src/Modules/Organisation/` unless stated.

- Domain (14 files, no setters, private constructors): `Crm.Modules.Organisation.Domain/{Organisation,Branch,Department,Membership,BrandLogo,
  BrandPalette,BrandColour,Branding,LocalizedText,OrganisationCode,ContrastCheck}.cs`, enums `ArabicFont`, `LogoContentType`, `SystemRole`, ids
  `BrandLogoId`, `MembershipId` (`BranchId`, `DepartmentId`, `TenantId` live in `Crm.SharedKernel.Scoping`).
- Application: `Commands/<UseCase>/{Command,CommandHandler,CommandValidator}.cs` (12 use cases), `Queries/<UseCase>/…` (15 queries),
  `Specifications/*.cs` (11), `Theming/{BrandTokenGenerator,Oklch,BrandingRefusals,ISvgSanitizer}.cs`, `Validation/OrganisationRules.cs`,
  `OrganisationModuleApi.cs`, `OrganisationErrors.cs`, `OrganisationCacheKeys.cs`, `Dtos.cs`, `Localization/OrganisationMessageKeys.cs` and
  `Localization/Resources/OrganisationMessages{,.ar}.resx`.
- Infrastructure: `Theming/SvgSanitizer.cs`, `Persistence/Converters/BrandingConverter.cs`, `Persistence/OrganisationSeeder.cs`,
  `Persistence/OrganisationDbContext.cs` (public, `ModuleDbContext` subclass), `OrganisationModule.cs`. Not tested: `Persistence/Configurations/*`,
  `Migrations/*`, `OrganisationDbContextFactory`.
- Contracts: `IOrganisationModuleApi.cs` (records `UserMemberships`, `UserMembership`, `DepartmentReference`, `DepartmentMember`),
  `DevSeed/OrganisationDevSeed.cs` (well-known ids used by the seeder tests).
- UT-01 helpers to **copy the pattern of** (do not reference the test project; see R1): `backend/tests/Crm.BuildingBlocks.Tests/Support/{Scopes,
  CurrentUsers,Clock,ScopeFixtures,NullArguments}.cs` and `Support/Persistence/{TestDbContext,Seed}.cs` (InMemory context, ignore
  `InMemoryEventId.TransactionIgnoredWarning`, `AuditingSaveChangesInterceptor(user, clock)`).
- Production behaviours that shape the tests: `Repository<TAggregate,TId,TContext>` (`Crm.BuildingBlocks.Infrastructure/Persistence/Repositories/
  Repository.cs`: a scoped miss that exists elsewhere throws `OrganisationScopeViolationException`, `SetOriginalRowVersion` sets the EF original
  value), `SpecificationEvaluator`, `ModuleDbContext` (soft-delete + scope filters, write guard), `AuditingSaveChangesInterceptor` (turns
  `Remove` into a soft delete), `ResourceLocalizationService` + `LocalizationResource` (for real localisation of the `.resx` pair).

## Test conventions (apply to every slice)

- Namespaces mirror folders: `Crm.Modules.Organisation.Tests.Domain`, `…Tests.Application.Commands.CreateBranch`, … Class name = production type
  + `Tests` (`BrandLogoTests`, `CreateBranchCommandHandlerTests`, `CreateBranchCommandValidatorTests`).
- Domain errors are asserted as `DomainException.Error.Code` (`Assert.Throws<DomainException>(…).Error.Code`). Result failures are asserted
  as `result.IsFailure`, `result.Error.Code` **and** `result.Error.Type` (`ErrorType.NotFound|Conflict|Forbidden|Domain`).
- Handlers get **substituted** `IRepository<,>`, `IOrganisationUnitOfWork`, `ICurrentUserService`, `ILocalizationService`, `ISvgSanitizer`. A
  substituted repository returning `Task<T?>` needs `Returns(Task.FromResult<T?>(x))` (nullable-annotated `Returns(x)` trips NSubstitute
  analyzers); `SaveChangesAsync` is asserted with `await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>())` and
  `DidNotReceive()` on every failure path. Time-related values come from `Support/Clock.Fixed()` (Organisation has no clock use; included for
  the seeder/auditing interceptor).
- Cancellation: pass `TestContext.Current.CancellationToken` (xUnit1051 is an error).
- Validators are tested with `validator.Validate(command)` / `TestValidate` is **not** available (no FluentValidation.TestHelper reference):
  assert `result.IsValid` and `result.Errors.Select(e => e.PropertyName)`. Validators that take `ILocalizationService` get a hand-written
  `FakeLocalization` (key → `"<key>"`) in `Support/`.
- Queries that carry logic beyond a projection (`GetMyScope`, `ListDepartments`, `GetMembershipsOfUser`, the specifications) run against EF
  Core InMemory through the **real** `Repository<,,OrganisationDbContext>`; plain projections use substituted repositories.
- Every `foreach (…)` over attribute/element cases uses `[Theory]` + `[InlineData]`/`[MemberData]` rather than loops in a `[Fact]`.
- No test touches `backend/src/`. If a seam is missing, name it in Follow-ups.

## Implementation tasks

### Task group 1 — Project and support files

#### 1.1 — Confirm the scaffolding builds (run first; risks R1, R2)

Run `dotnet build tests/Crm.Modules.Organisation.Tests` from `backend/` (build the project alone; a running `Crm.Api` may lock
`src/Crm.Api/bin`). Confirm the module's `Application` assembly exposes what is needed: the project has **no `InternalsVisibleTo`** (grep found
none), so `Oklch` and `BrandingRefusals` (both `internal`) are tested only through public callers (R3).

#### 1.2 — **File: `backend/tests/Crm.Modules.Organisation.Tests/Crm.Modules.Organisation.Tests.csproj`**

- Delete the `<PropertyGroup>` holding `<TestingPlatformCommandLineArguments>--ignore-exit-code 8</TestingPlatformCommandLineArguments>` and its
  comment, **in the same change as the first test file** (tracker, UT-01 R2 resolution).
- Add `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />` (version comes from `Directory.Packages.props`, as in
  `Crm.BuildingBlocks.Tests.csproj`). Keep the three `ProjectReference`s; add **no** other project reference (no other module's non-Contracts
  project, no `Crm.BuildingBlocks.Tests`).

#### 1.3 — **Create files** under `backend/tests/Crm.Modules.Organisation.Tests/Support/` (namespace `Crm.Modules.Organisation.Tests.Support`)

| File | Content |
|------|---------|
| `Ids.cs` | fixed `TenantId` A and B, `BranchId` / `DepartmentId` / `Guid` constants and a `RowVersion` (`byte[]` of 8 bytes) used by every test |
| `Clock.cs` | `Fixed()` → substituted `IDateTime` at `2026-10-11T09:00Z` (same shape as UT-01's) |
| `Scopes.cs`, `CurrentUsers.cs` | real `OrganisationScope`s (`Agent`, `AgentIn`, `Narrowed`, `None`, `Unrestricted`) and `ICurrentUserService` substitutes whose `UserId` is a **parseable Guid string** (`GetMyScopeQueryHandler` calls `Guid.Parse(currentUser.UserId!)`, R7) |
| `OrganisationFixtures.cs` | builders: `Organisations.New(code?, name?)`, `Branches.New(tenant?, code?, active?)`, `Departments.New(branch, code?, active?)`, `Memberships.New(department, userId?, role?)`, `Logos.Png(tenant?)`, `Logos.Svg(tenant?)`, `Brandings.Valid(light?, dark?)` (primary `#0E6655`, accent `#B7791F`, `Tajawal`), `Colours.Primary/Accent`, `PngBytes(size)` (8-byte signature + padding), `SvgBytes(string)` |
| `FakeLocalization.cs` | `ILocalizationService` returning the real `OrganisationMessages` strings when built `FakeLocalization.English()` (a `ResourceManager("Crm.Modules.Organisation.Application.Localization.Resources.OrganisationMessages", assembly)` through `ResourceLocalizationService`) and `FakeLocalization.Keys()` returning the key itself |
| `OrganisationTestContext.cs` | `Create(string database, ICurrentUserService user)` → `OrganisationDbContext` over InMemory (`ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))`, `AuditingSaveChangesInterceptor(user, Clock.Fixed())`), `NewDatabaseName()`, `Store(db)` (unrestricted, for seeding/readback), `Repo<TAggregate,TId>(context)` helper returning `new Repository<TAggregate,TId,OrganisationDbContext>(context)` |
| `NullArguments.cs` | copy of UT-01's helper for `ArgumentNullException` guard tests (a literal `null` does not compile warning-free and `!` is banned) |

Add no Support type that production code needs; every file is test-only.

### Test classes to write, grouped in slices

Stop at a slice boundary if context runs short: build, run the project, log the finished slices in the tracker's Session log, do not commit.

#### Slice 1 — Domain (≈ 190 tests) — `Tests/Domain/`

| Class | Cases (code in parentheses) |
|-------|-----------------------------|
| `OrganisationCodeTests` | trims and upper-cases (`" ryd-01 "` → `RYD-01`); accepts 2 and 20 chars, digits, hyphen; rejects empty, 1 char, 21 chars, space inside, underscore, Arabic letters, `null` → `ArgumentNullException` (`Organisation.CodeInvalid`); value equality and hash by `Value`; `==` operator |
| `LocalizedTextTests` | trims both languages; rejects blank/whitespace Arabic, blank English (`Organisation.NameRequired`); 200 accepted, 201 rejected for each language (`Organisation.NameTooLong`); null guards; equality by both strings; Arabic text round-trips |
| `BrandColourTests` | `Create` trims, upper-cases, parses R/G/B; rejects `blue`, `#12345`, `#1234567`, `#GGGGGG`, `123456`, empty (`Organisation.BrandColourInvalid`); `White` constant is `#FFFFFF` (255,255,255); luminance of black 0, white 1; `ContrastWith` black/white = 21, symmetric, colour with itself = 1; **R8 pair** `#777777` 4.48 (< 4.5) and `#767676` 4.54 (≥ 4.5) vs white (asserted with `Math.Round(x, 2)` and the unrounded comparison); `FromLinearRgb` clamps below 0 and above 1, round-trips `ToLinearRgb` for a table of colours, boundary `0.0031308` branch, rounding `AwayFromZero`; equality by `Hex` (lower vs upper case input equal); `ContrastWith(null)` guard |
| `ContrastCheckTests` | `Passes` is `>=` (equal passes, just under fails); `Names` constants are `PrimaryText` / `AccentOnSurface`; record equality |
| `BrandPaletteTests` | `Evaluate` returns two checks in order (PrimaryText required 4.5, AccentOnSurface required 3.0) with real ratios; `Create` accepts `#0E6655`/`#B7791F` (plan D11 seed), accepts `#000000`/`#767676`; rejects primary `#FFD400` and `#777777` (`Organisation.BrandingContrastTooLow`, message names `PrimaryText` and the `4.48:1`/`4.5:1` figures with invariant culture even under a `ar-SA` current culture); rejects accent `#F5F5F5` (names `AccentOnSurface`); primary failure reported before accent failure when both fail; null guards; equality by primary+accent; constants `TextContrast`, `NonTextContrast` |
| `BrandingTests` | `Create` happy path with/without logos; unsupported enum value `(ArabicFont)99` → `Organisation.ArabicFontUnsupported`; dark without light → `Organisation.DarkLogoWithoutLight`; light without dark allowed; null guards; equality (same values equal, any single component different not equal, including logo ids) |
| `BrandLogoTests` | `Sniff`: PNG signature → `Png`; PNG signature alone (8 bytes) accepted (pin); empty → `Organisation.LogoEmpty`; `MaxBytes` (524 288) accepted, +1 → `Organisation.LogoTooLarge`; SVG with and without BOM, with leading whitespace, upper-case `<SVG`; text not starting with `<` → `Organisation.LogoTypeNotAllowed`; XML without `<svg` rejected; invalid UTF-8 bytes (e.g. `0xFF 0xFE 0x00`) rejected; JPEG (`FF D8 FF`) and GIF (`GIF89a`) rejected; PNG with a wrong signature byte rejected; null guard. `Create`: valid PNG/SVG sets `ContentType`, `MediaType` (`image/png`, `image/svg+xml`), copies the content (mutating the source array afterwards does not change the entity), uses the supplied id or generates one, `TenantId.None` → `Organisation.TenantRequired`, null tenant guard; `BelongsTo` true/false; every rejected content from `Sniff` also rejected from `Create` |
| `BranchTests` | `Create` sets tenant, code, name, `IsActive = true`, generates id or uses the supplied; `TenantId.None` → `Organisation.TenantRequired`; `Rename` replaces the name; `Deactivate` flips `IsActive`; second `Deactivate` → `Organisation.BranchAlreadyInactive`; implements `ITenantScoped`; no domain event raised |
| `DepartmentTests` | `Create` copies `TenantId` and `BranchId` from the branch, active by default, supplied id used; inactive branch → `Organisation.BranchInactive`; null branch guard; `Rename`; `Deactivate` / twice → `Organisation.DepartmentAlreadyInactive`; deactivating the department leaves the branch untouched; no domain event |
| `MembershipTests` | `Create` copies tenant and branch from the department, role set, id supplied/generated; `Guid.Empty` user → `Organisation.UserRequired`; inactive department → `Organisation.DepartmentInactive`; guard order (empty user is reported before inactive department); `ChangeRole` for all three `SystemRole` values; `ISoftDeletable` defaults (`IsDeleted` false, `DeletedAtUtc` null); null department guard; no domain event |
| `OrganisationTests` | `Create` generates a `TenantId` or uses the supplied one, active, `Branding`/`PreviousBranding` null, `CanRevertBranding` false. `ChangeBranding`: first save → `Previous` null, `CanRevertBranding` true; second different save moves current to previous; **saving an equal branding is a no-op that keeps the real previous version and leaves `CanRevertBranding` as it was** (plan D2); null guard. `RevertBranding`: after first save restores `null` defaults and `CanRevertBranding` stays true; swaps (revert twice re-applies); with no history → `Organisation.NoPreviousBranding`; a revert after a no-op save still restores the right version; no domain event |
| `StronglyTypedIdTests` (module ids) | `BrandLogoId.New()` / `MembershipId.New()` yield non-empty distinct guids; value equality; **do not assert `ToString()` text** (UT-01 follow-up 1: the record's synthesized `ToString` wins) |
| `EnumContractTests` | `ArabicFont` has exactly `IbmPlexSansArabic=0, NotoKufiArabic=1, Tajawal=2` (names are API/storage contract); `SystemRole` names `Administrator, Supervisor, Agent`; `LogoContentType` `Png=0, Svg=1` |

#### Slice 2 — Application: commands and their validators (≈ 150 tests) — `Tests/Application/Commands/<UseCase>/`

One handler test class and one validator test class per use case (24 classes). `CurrentUsers.AgentIn(...)` supplies the tenant.

| Use case | Handler cases | Validator cases |
|----------|---------------|-----------------|
| `AddMembership` | success returns `MembershipDto` (ids, role string, branch copied from the department), `Add` + `SaveChanges` called once; department missing → `NotFound` `Organisation.DepartmentNotFound`, no save; duplicate active membership (`CountAsync` > 0) → `Conflict` `Organisation.MembershipExists`, no `Add`; inactive department → `DomainException` `Organisation.DepartmentInactive` propagates (pins that this is thrown, not a Result); every `SystemRole` value maps; role string not an enum name → `ArgumentException` from `Enum.Parse` (validator is the guard, pinned); the specification passed to `CountAsync` is an `ActiveMembershipSpecification` for that user/department | empty `UserId`, empty `DepartmentId`, role `"Administrator"/"Supervisor"/"Agent"` valid, `"agent"` (case) invalid, `""`, `"1"`, `"Owner"` invalid; message text of the role rule |
| `ChangeMembershipRole` | success changes role, `SetOriginalRowVersion(membership, rowVersion)` called with the request's bytes **before** `SaveChanges`, returns dto; not found → `NotFound` `Organisation.MembershipNotFound`; bad role string propagates `ArgumentException` | empty `Id`, bad role, empty/null `RowVersion` (`NotEmpty` on `byte[]`), all valid |
| `RemoveMembership` | success calls `Remove` then `SaveChanges`, returns `Result.Success`; not found → `NotFound`; no `SetOriginalRowVersion` | empty `Id` |
| `CreateBranch` | success (tenant from scope, code upper-cased, dto); `TenantId.None` scope → `Forbidden` `Organisation.TenantRequired`, no repository call; duplicate code (`CountAsync` > 0) → `Conflict` `Organisation.BranchCodeTaken` whose description contains the normalised code; invalid code reaching the handler → `DomainException` `Organisation.CodeInvalid`; blank names → `DomainException` `Organisation.NameRequired` | code empty / 1 char / 21 chars / `"a b"` / `"a_b"` invalid, `"ab"`, `"A-1"`, 20 chars valid; names empty / 201 chars invalid, 200 valid (Arabic and English each) |
| `RenameBranch` | success (rowversion set, name replaced, dto); not found → `NotFound` `Organisation.BranchNotFound`; blank name → `DomainException` | empty `Id`, names empty / 201, empty `RowVersion` |
| `DeactivateBranch` | success sets `IsActive` false; already inactive → `DomainException` `Organisation.BranchAlreadyInactive`, no save; not found → `NotFound`; rowversion applied | empty `Id`, empty `RowVersion` |
| `CreateDepartment` | success (tenant and branch copied from the branch); branch missing → `NotFound` `Organisation.BranchNotFound`; code taken → `Conflict` `Organisation.DepartmentCodeTaken`; inactive branch → `DomainException` `Organisation.BranchInactive`; check order (branch lookup before the code check) | empty `BranchId`, code rules, name rules |
| `RenameDepartment` | as `RenameBranch` with `Organisation.DepartmentNotFound` | as `RenameBranch` |
| `DeactivateDepartment` | as `DeactivateBranch` with `Organisation.DepartmentAlreadyInactive` / `DepartmentNotFound` | as `DeactivateBranch` |
| `ChangeBranding` | success sets `Branding` and returns `BrandingSettingsDto` (`IsCustom` true, `CanRevert` true, `Previous` null on first save); light-only and light+dark logos resolved via `logos.GetByIdAsync`; organisation missing → `NotFound` `Organisation.OrganisationNotFound`; contrast too low → `Domain` `Organisation.BrandingContrastTooLow` with the **localised** detail (English: `Text on the primary colour has a contrast of 1.63:1, but WCAG AA needs at least 4.5:1. …`, Arabic with a resource-backed `ar` service under `CultureInfo.CurrentUICulture` — see R6), accent failure names the accent check, `SaveChanges` not called; logo missing → `NotFound` `Organisation.BrandLogoNotFound`; logo of another tenant → the **same** `NotFound` (D4), checked for light and for dark; dark logo without light → `DomainException` `Organisation.DarkLogoWithoutLight` (the handler resolves it, the aggregate refuses); unsupported font string → `ArgumentException`; invalid hex reaching the handler → `DomainException` `Organisation.BrandColourInvalid`; saving the same branding twice keeps the first version as `Previous`; `SetOriginalRowVersion` called on the organisation with the request bytes; command carries `ICacheInvalidatingCommand` tag `organisation:brandings` and `[RequirePermission(CrmPermissions.AdminBranding)]` | ar/en display name empty / 201, `PrimaryColour` and `AccentColour` empty / `blue` / `#12345` / `#GGGGGG` (localised message equals the `.resx` text), `ArabicFont` empty / `Arial` / `1` / `tajawal` invalid and the three names valid, empty `RowVersion`, logo ids optional |
| `RevertBranding` | success swaps and returns dto; organisation missing → `NotFound`; `CanRevertBranding` false → `Domain` `Organisation.NoPreviousBranding` (Result, not exception), no save; second revert re-applies; rowversion applied; cache tag and permission attributes | empty `RowVersion` |
| `UploadBrandLogo` | PNG saved without calling the sanitizer, dto `(id, "image/png", size)`; SVG goes through `ISvgSanitizer.Sanitize` and the **sanitised** bytes are stored (assert on the added `BrandLogo.Content`) and dto size is the sanitised length; sanitizer returns `null` → `Domain` `Organisation.LogoTypeNotAllowed`, no `Add`, no save; empty content → `DomainException` `Organisation.LogoEmpty`; over 512 KB → `Organisation.LogoTooLarge`; unknown type → `Organisation.LogoTypeNotAllowed` (exception) and sanitizer never called; logo stamped with the caller's tenant; `TenantId.None` scope → `DomainException` `Organisation.TenantRequired` | `Content` null invalid, non-null valid (empty array is the domain's concern, pinned) |

Also in this slice, small files: `OrganisationErrorsTests` (code, description and `ErrorType` of each of the five factories; `BrandLogoNotFound` description contains the id), `OrganisationCacheKeysTests` (`Brandings`, `CurrentBranding`, `PublicBranding(" ryd ")` → `organisation:brandings:code:RYD`, null guard), `OrganisationRulesTests` (`HexColour`, `SupportedArabicFont`, `OrganisationCode` rule builders: valid/invalid values, message comes from the localization key, null `rule`/`localization` guards — via a tiny `InlineValidator<T>`), `OrganisationMessageKeysTests` (every key has a non-empty `en` and `ar` value in the resource pair: iterate the constants with reflection over the nested static classes; `ar` differs from `en`).

#### Slice 3 — Application: queries, specifications, DTOs, Contracts API (≈ 140 tests) — `Tests/Application/`

| Class | Cases |
|-------|-------|
| `GetBrandingSettingsQueryHandlerTests` | custom branding → `IsCustom` true with palette hex values; no branding → defaults (`#145EC1`, `#0F766E`, `IbmPlexSansArabic`, display name = organisation name, `IsCustom` false); organisation missing → `NotFound`; reads by `currentUser.Scope.Tenant`; row version copied |
| `GetCurrentBrandingQueryHandlerTests` | branded → tokens non-null with `--crm-primary-600` equal to the primary hex; unbranded → `Tokens` null, name and `IbmPlexSansArabic`; missing → `NotFound`; query `CacheKey` is `organisation:brandings:current`, `CacheTags` is the one tag, `[RequireAuthenticatedUser]` |
| `GetPublicBrandingQueryHandlerTests` (+ validator) | active organisation by code; unknown code → `NotFound`; **inactive organisation → `NotFound`**; code is normalised (`ryd` finds `RYD`); invalid code reaching the handler → `DomainException` `Organisation.CodeInvalid`; query cache key per code and tag; validator: empty, `a`, `a b`, 21 chars invalid, `demo` valid, message is the localised `OrganisationCodeInvalid`; `[AllowAnonymousRequest]` |
| `GetBrandLogoQueryHandlerTests` (+ validator) | found → bytes + media type; missing → `NotFound` `Organisation.BrandLogoNotFound`; validator empty id; `[AllowAnonymousRequest]` and **not** `ICacheableQuery` (bytes stay out of Redis, D12) |
| `PreviewBrandingQueryHandlerTests` (+ validator) | passing palette → both checks `Passes` true, ratios rounded to 2 decimals, `Required` 4.5 / 3.0, tokens present; **failing palette still returns success** with `Passes` false (`#FFD400`); `#777777` → 4.48 false and `#767676` → 4.54 true; invalid hex reaching the handler → `DomainException`; validator hex rules and localised messages |
| `GetMyScopeQueryHandlerTests` (InMemory, real repositories) | no memberships → `Forbidden` `Organisation.NoMemberships`; single membership → one `MyDepartmentDto` with localized names of department and branch, role from the **scope membership**, `IsActive` true; two departments in two branches; one inactive department or inactive branch → `IsActive` false; a membership whose department/branch row is missing is skipped (defensive branch); duplicate department ids in the scope are de-duplicated for the lookup; `ActiveDepartment` (narrowed scope) copied into `ActiveDepartmentId`, `null` otherwise; `Roles` copied; user id parsed from `UserId` (non-Guid `UserId` → `FormatException`, pinned, R7) |
| `ListBranchesQueryHandlerTests` (InMemory) | all vs `ActiveOnly`; ordered by code; other tenant's branches invisible (scope filter); empty list is success |
| `ListDepartmentsQueryHandlerTests` (InMemory) | no filter returns all of the tenant ordered by code; `BranchId` filter; `ActiveOnly` drops inactive departments **and departments of inactive branches** (the extra branch query); `ActiveOnly` false keeps departments of inactive branches; other tenant invisible; branch id filter + active combined |
| `ListMembershipsQueryHandlerTests` (InMemory) | no filter; by user; by department; by both; soft-deleted membership excluded; other tenant excluded |
| `GetMembershipsOfUserQueryHandlerTests` (InMemory) | no memberships → success with `Guid.Empty` tenant and empty list; several memberships → tenant of the first, list of `(membership, department, branch, role-name)`; **runs with `OrganisationScope.None`** (the scope-lifting spec, plan 06) and still sees the user's rows; a soft-deleted membership is not returned; another user's rows are not returned; `[AllowAnonymousRequest]` |
| `IsDepartmentMemberQueryHandlerTests` (+ validator) | true / false; removed membership → false; validator empty ids; `[RequireAuthenticatedUser]` |
| `ListDepartmentMembersQueryHandlerTests` (+ validator) | empty ids → empty success **without** touching the repository; distinct ids; multiple departments; role strings; validator: `null` list invalid, 100 ids valid, 101 invalid |
| `GetDepartmentReferenceQueryHandlerTests` (+ validator) | found maps all seven fields; missing → `NotFound` `Organisation.DepartmentNotFound`; validator empty id |
| `ListDepartmentReferencesQueryHandlerTests` (InMemory) | `activeOnly` true/false; ordering; mapping |
| `OrganisationModuleApiTests` | each of the five methods sends **exactly** the matching query record through a substituted `ISender` (`Arg.Is<GetMembershipsOfUserQuery>(q => q.UserId == id)` etc.) and returns its result unchanged (success and failure passthrough); `GetDepartmentMembersAsync` copies the collection to a list; cancellation token forwarded; `ListDepartmentsAsync(activeOnly)` flag forwarded |
| `Specifications/*Tests` (11 classes or one theory file per spec, InMemory against seeded `OrganisationDbContext`) | `ActiveMembershipSpecification` (user+department, removed row excluded), `BranchByCodeSpecification`, `DepartmentByCodeSpecification`, `OrganisationByCodeSpecification` (code equality after normalisation), `BranchesSpecification` (ordering by code, `activeOnly`), `DepartmentsSpecification` (null branch, branch, activeOnly, combination; ordering), `BranchesByIdsSpecification`, `DepartmentsByIdsSpecification` (empty list matches nothing), `MembershipsOfDepartmentsSpecification` (null guard), `MembershipsSpecification` (all four filter combinations), `MembershipsOfUserSpecification` (`IgnoreOrganisationScopeFilter` true, `IgnoreSoftDeleteFilter` false, `AsNoTracking` true). Every spec asserts `AsNoTracking` true |
| `DtosTests` | `LocalizedTextDto`, `BranchDto`, `DepartmentDto`, `MembershipDto` map every field (role as string); `BrandingDto.FromDomain` (hex upper-case, font name, logo guids, null logos) and `Defaults` (constants); `BrandingSettingsDto.FromDomain` for never branded / branded once / branded twice (`Previous` present) / reverted to defaults (`Previous` set, `Current` defaults? — assert what the code does: after revert `Branding` is `null`, so `IsCustom` false and `CanRevert` true); `PublicBrandingDto.FromDomain` branded vs not; null guards |

#### Slice 4 — Theming algorithms (≈ 70 tests) — `Tests/Application/Theming/`

| Class | Cases |
|-------|-------|
| `BrandTokenGeneratorTests` | `BrandDefaults` constants; `Generate` returns 22 keys per theme (primary and accent × 11 steps `50,100,…,900,950`), key pattern `--crm-(primary\|accent)-<step>`; **light step 600 equals the input hex exactly** for a table of colours (`#0E6655`, `#B7791F`, `#145EC1`, `#000000`, `#FFFFFF`, `#767676`); every value matches `^#[0-9A-F]{6}$`; scale is monotonic in lightness (relative luminance of 50 > 100 > … > 950) for non-degenerate colours; deterministic (same input twice equal); **dark window**: for primaries `#0B1F4B`, `#FFD400`, `#000000`, `#FFFFFF`, `#0E6655` the dark `--crm-primary-600` has `RelativeLuminance` in `[0.115, 0.180]` (`BrandTokenGenerator.DarkMinLuminance`, `DarkPrimaryMaxLuminance`) with a small epsilon, and white-on-it contrast ≥ 4.5; dark accent luminance ≥ 0.115; a colour already inside the window is returned unchanged; very dark and very light inputs never throw (D7 "dark never fails"); grey input (zero chroma) works; null guards |
| `OklchViaGeneratorTests` | `Oklch` is `internal`, so test it through behaviour (R3): out-of-gamut ladder steps are clamped (every produced hex valid); white and black anchors produce valid scales; colour → OKLCH → colour round trip through `step 600` exactness. If the implementer prefers direct tests, adding `InternalsVisibleTo` is a **production change and is not allowed here** — record it as a Follow-up instead |
| `BrandingRefusalsViaHandlerTests` | `BrandingRefusals` is `internal` too: covered through `ChangeBrandingCommandHandlerTests` (English text with real ratio/required digits, Arabic text under `ar` culture, invariant-culture digits `1.63`/`4.5`, `PrimaryText` vs `AccentOnSurface` check names, passing palette → no refusal) |

#### Slice 5 — Infrastructure adapters (≈ 90 tests) — `Tests/Infrastructure/`

| Class | Cases |
|-------|-------|
| `SvgSanitizerTests` | returns UTF-8 **without BOM**; valid simple SVG round-trips its allowed elements and attributes (`viewBox`, `width`, `fill`, `d`); root not `svg` or wrong namespace (`<svg>` without xmlns) → `null`; not XML (`"hello"`) → `null`; truncated XML → `null`; **DTD / XXE payload** (`<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]>`) → `null`; `<script>` removed with its content; `foreignObject`, `image`, `a`, `style`, `animate`, `set` elements removed; unknown namespace element removed; nested disallowed element inside an allowed `g` removed while siblings stay; `onload`, `onclick`, `ONLOAD` attributes removed; `style` attribute removed; `href` / `xlink:href` kept only when starting with `#` (also with leading whitespace), removed for `http://…`, `javascript:…`, `data:…`; attribute values containing `javascript:` or `data:` removed (case-insensitive); `fill="url(#g)"` kept, `fill="url(http://x)"` and `url( 'x')` removed; attributes in a foreign namespace removed, `xml:space` kept; namespace declarations kept; comments and processing instructions dropped; every allowed element name survives (theory over the 21 names); text content in `text`/`tspan` preserved; output re-parses as valid XML; idempotent (sanitising the output equals the output); document over 2 000 000 characters → `XmlException` path returns `null`; null guard |
| `BrandingConverterTests` | `ConvertToProvider`/`ConvertFromProvider` (obtain the delegates from the public `ValueConverter` properties): round trip with and without logos; JSON contains the expected property names (`NameAr`, `NameEn`, `Primary`, `Accent`, `ArabicFont`, `LightLogo`, `DarkLogo`) and font **name**; Arabic text survives; `Serialize(null)` → `ArgumentNullException`; stored JSON `"null"` → `InvalidOperationException`; tampered JSON whose palette fails contrast → `DomainException` `Organisation.BrandingContrastTooLow` (plan 58 "tampered stored JSON"); unknown font name → `ArgumentException`; malformed JSON → `JsonException` |
| `OrganisationSeederTests` (InMemory) | first run creates: 1 organisation (`DEMO`, id `OrganisationDevSeed.TenantId`), 2 branches (`RYD`, `JED`), 3 departments (`RYD-CARE`, `RYD-BILL`, `JED-CARE`), **11 memberships** with the well-known ids and roles (Supervisor in care / Agent in billing, etc.), 2 logos with the well-known ids and `Svg` type, demo branding (`Tajawal`, `#0E6655`, `#B7791F`, both logo ids, `CanRevertBranding` true); **second run adds nothing** (counts equal, ids unchanged); a soft-deleted seed membership is not resurrected nor duplicated (set `IsDeleted` through the store, rerun, assert no new row and still deleted); an administrator's revert to defaults (`Branding` null, `CanRevertBranding` true) is not overwritten; an organisation with custom branding is not overwritten; seed uses `Unrestricted` scope (context built with `CurrentUsers.Unrestricted()`) |
| `OrganisationModuleTests` | `Name` is `Organisation`; `ApplicationAssembly` is the Application assembly; `AddServices` on a `ServiceCollection` with an in-memory `IConfiguration` (a `ConnectionStrings:Crm` value; use the helper pattern from UT-01's `AddModuleDbContext` tests) registers `IOrganisationModuleApi → OrganisationModuleApi` (scoped), `IModuleSeeder → OrganisationSeeder` (scoped), `ISvgSanitizer → SvgSanitizer` (singleton), the five repositories (`IRepository<Domain.Organisation,TenantId>`, `Branch`, `Department`, `Membership`, `BrandLogo`), `IOrganisationUnitOfWork`, and a `LocalizationResource` whose manager resolves `Organisation.Validation.ColourInvalid` in `en` and `ar`; descriptors only are inspected — **do not build a provider that opens SQL Server** (R5) |
| `OrganisationDbContextTests` (InMemory, a few) | `SchemaName` is `organisation`; the five `DbSet`s exist; writes outside the caller's tenant are refused by the guard (a `Branch` of tenant B saved by a tenant A caller → `OrganisationScopeViolationException`), reads of tenant B rows return nothing; `Membership` removal becomes a soft delete; `Organisation` and `BrandLogo` are **not** tenant-scoped (readable with `OrganisationScope.None`, plan 58 D4/S1) |
| `RepositoryBehaviourWithOrganisationAggregatesTests` (InMemory, 4–6 tests) | out-of-scope id of a `Branch` → `OrganisationScopeViolationException` (403), unknown id → `null` (404); soft-deleted membership → `null`; `SetOriginalRowVersion` sets the original value on the tracked entry — only the module-specific aggregate mapping, the generic behaviour is UT-01's |

#### Slice 6 — Wrap-up

- Remove any placeholder/empty test file; confirm no file under `backend/src/` changed (`git status --short backend/src` prints nothing).
- Run the verification commands below; paste the final counts into the tracker.

### Task group D — Tracker and plan indexes (no production docs change)

- **D1 — `.squad/UNIT-TEST-STORIES.md`:** row 2 → `✅ Done` with plan link and commit message `Implement Story 61 — Unit tests — Organisation module (UT-02)`;
  change "Remaining: 4 stories" to 3; add a Session log entry (counts, what is not testable, follow-ups).
- **D2 — `.squad/plans/backend-unit-tests/00-overview.md`:** add row `| 61 | [61-story-unit-tests-for-organisation-module.md](61-story-unit-tests-for-organisation-module.md) | Unit tests — Organisation module | UT-02 | 60 |`.
- **D3 — `.squad/plans/00-index.md`:** `backend-unit-tests` already exists as a feature; nothing to add.
- **D4 — `backend/README.md`:** only if it lists per-module test counts (UT-01 wrote it); otherwise untouched.

## Edge Cases & Failure Modes

- **Validator vs handler layering.** Handlers do not re-validate: invalid role strings, fonts, codes, colours and blank names reaching a handler surface as
  `ArgumentException` (`Enum.Parse`) or `DomainException`. Tests pin this (the pipeline's `ValidationBehavior` runs first in production).
- **Result vs exception.** Expected failures are `Result`s (`NotFound`, `Conflict`, `Forbidden`, `Domain` for contrast / no-previous / SVG refused);
  aggregate invariants (`BranchInactive`, `DepartmentInactive`, `DepartmentAlreadyInactive`, `LogoTooLarge`, …) are `DomainException`s that
  `GlobalExceptionHandler` turns into 422. Each test asserts the right one of the two.
- **Concurrency.** Handlers call `SetOriginalRowVersion` with the client's bytes; the conflict itself (`DbUpdateConcurrencyException` →
  `ConcurrencyConflictException`) is UT-01's and is not reproducible on InMemory. Tests assert the call and its order only.
- **Scope.** `Branch`, `Department`, `Membership` are `ITenantScoped` (tenant filter, not branch/department); `Organisation` and `BrandLogo` are not scoped.
  `MembershipsOfUserSpecification` is the one scope lift. InMemory evaluates the real named filters, so these are real tests, not mock checks.
- **Culture.** `BrandingRefusals` formats with `CurrentCulture` (the format string) but ratios with `InvariantCulture`; tests set
  `CultureInfo.CurrentCulture`/`CurrentUICulture` to `ar` and restore in `IDisposable` (UT-01's `UiCulture` helper pattern) and mark such classes
  `[Collection]`-serialised so culture changes cannot leak into parallel tests.
- **Floating point.** Contrast and luminance assertions use `Assert.Equal(expected, actual, precision: 2)` (or 3 for luminance); the R8 pair is asserted
  on the unrounded `>=` comparison, never on the rounded display value.
- **Large inputs.** 512 KB and 2 000 000-character documents are built with `new byte[n]`/`new string('a', n)` once per test, not in `MemberData`.
- **Parallelism.** Each InMemory test uses its own `Guid` database name; no static mutable state in `Support/`.

## Test Plan

| # | Check | How | Expected |
|---|-------|-----|----------|
| 1 | Project builds clean | `dotnet build tests/Crm.Modules.Organisation.Tests` from `backend/` | 0 warnings, 0 errors, no `CRM0001`, no analyzer suppression |
| 2 | Project passes | `dotnet test --project tests/Crm.Modules.Organisation.Tests` | all green, **0 failed, 0 skipped**, ≈ 520–620 tests; MTP does not need `--ignore-exit-code 8` any more |
| 3 | Whole suite | `dotnet test --solution Crm.sln` from `backend/` | UT-01's 826 + this story's tests pass; Identity/Customers/Tickets projects still build empty |
| 4 | No production change | `git status --short backend/src` and `git diff --stat -- backend/src` | empty |
| 5 | Banned APIs / `!` | `grep -rnE "DateTime\.|DateTimeOffset\.(UtcNow\|Now)|TimeProvider|Console\.|NoWarn|pragma warning|\!\.|\!\)" backend/tests/Crm.Modules.Organisation.Tests --include=*.cs` | no hit (other than `!=`/`!x` negations) |
| 6 | Solution build | `dotnet build backend/Crm.sln` when no `Crm.Api` holds `src/Crm.Api/bin` | clean; if locked, say so in the report and rely on 1–3 |

## Verification Steps

1. `cd backend && dotnet build tests/Crm.Modules.Organisation.Tests` (run first and after every slice).
2. `dotnet test --project tests/Crm.Modules.Organisation.Tests` after every slice; fix, never skip.
3. `dotnet test --solution Crm.sln` at the end.
4. `git status --short` shows only: the plan file, `00-overview.md`, `.squad/UNIT-TEST-STORIES.md`, the test csproj, and files under
   `backend/tests/Crm.Modules.Organisation.Tests/`.
5. Commit (implementation session): `Implement Story 61 — Unit tests — Organisation module (UT-02)`, ending with the
   `Co-Authored-By` line given by the session's attribution reminder; one commit with plan, overview, tracker and tests.

## Done Criteria

- [ ] Domain: every type in `Crm.Modules.Organisation.Domain` has constructor/factory, method, guard and "no domain event" tests (Slice 1).
- [ ] All 12 command handlers: success and every failure Result / `DomainException` path, repository/unit-of-work interactions, rowversion, cache-tag and permission attributes (Slice 2).
- [ ] All 12 command validators plus `OrganisationRules` and the resource-key tests (Slice 2); query validators (Slice 3).
- [ ] All 15 query handlers, 11 specifications, DTO mappings and `OrganisationModuleApi` (Slice 3); `BrandTokenGenerator` through its public surface (Slice 4).
- [ ] Integration events / consumers / domain-event handlers: none exist; recorded in the tracker, aggregates asserted to raise no events.
- [ ] Infrastructure: `SvgSanitizer`, `BrandingConverter`, `OrganisationSeeder`, `OrganisationModule`, `OrganisationDbContext` tests (Slice 5); EF configurations and migrations not tested (intake).
- [ ] The csproj no longer carries `--ignore-exit-code 8`; `Microsoft.EntityFrameworkCore.InMemory` referenced.
- [ ] Build clean, project tests green and skipped = 0, whole-suite green; `backend/src/` untouched; tracker row 2 ✅ and Follow-ups logged.

## Decisions a later story must not silently reverse

- `Crm.Modules.<Module>.Tests` projects **do not reference `Crm.BuildingBlocks.Tests`**; each module's `Support/` copies the few helpers it needs (one rule-compliant reference set, no shared test assembly).
- `internal` production types (`Oklch`, `BrandingRefusals`) are tested through public callers; adding `InternalsVisibleTo` to production is a separate, explicit change.
- FluentAssertions stays out (8+ is paid). Tool/package versions read from `Directory.Packages.props` during planning: `xunit.v3` 4.0.2, `NSubstitute` 6.2.0, `NSubstitute.Analyzers.CSharp` 1.0.17, `Microsoft.EntityFrameworkCore.InMemory` 10.0.11 (as recorded by plan 60).

## Named risks — prove each first, in this order, before building on it (planning ran nothing)

- **R1 — Reusing helpers across test projects.** The module test project cannot reference `Crm.BuildingBlocks.Tests` safely (CRM0001's "Tests" layer rule and a
  second `Exe` test project). Verification: build with only the three existing references + InMemory package; copy the helpers into `Support/` (task 1.3). If a helper needs a
  type that is `internal` in BuildingBlocks (e.g. `AuditingSaveChangesInterceptor`), check it is `public` first (`grep -n "class AuditingSaveChangesInterceptor"`); if it is not, the InMemory
  context omits the interceptor and soft-delete tests use `Remove` assertions on the substituted repository only — record in Follow-ups.
- **R2 — `--ignore-exit-code 8` removal and an empty-run exit.** Verify `dotnet test --project tests/Crm.Modules.Organisation.Tests` exits 0 with tests present after the property is removed.
- **R3 — `internal` types without `InternalsVisibleTo`.** `Oklch` and `BrandingRefusals` cannot be named from the test assembly. Verify by compiling; test through `BrandTokenGenerator` and `ChangeBrandingCommandHandler`.
  If coverage of a branch (e.g. `Oklch.ClampChroma` bisection) is unreachable through the public API, say so in the tracker rather than editing production.
- **R4 — Real `Repository` + InMemory semantics.** InMemory does not generate `RowVersion`, enforce unique indexes, or support transactions; named query filters (`.IgnoreQueryFilters([name])`, EF 10) must work. Verify with a first
  smoke test (`ListBranchesQueryHandlerTests` with two tenants) before writing the other InMemory classes. Value-converted `OrganisationCode` equality in `Where(b => b.Code.Equals(code))` must translate on InMemory; if it does not, test the specification's `Criteria` by compiling it and
  applying it to an in-memory `List<T>` with `AsQueryable()` instead.
- **R5 — `OrganisationModule.AddServices` needs a connection string / provider.** `AddModuleDbContext` may call `UseSqlServer` lazily. Inspect descriptors without resolving the context; if `AddServices` throws without configuration, supply `ConnectionStrings:Crm` with a dummy SQL Server string (nothing connects).
- **R6 — Culture-dependent refusal text.** `string.Format(CultureInfo.CurrentCulture, localization.Get(key) …)` with a real `.resx` pair requires `CurrentUICulture = ar` for the Arabic template and satellite-assembly resolution of `OrganisationMessages.ar.resx` from the test output. Verify the satellite `ar/Crm.Modules.Organisation.Application.resources.dll` is copied to the
  test bin; if not, assert the Arabic path with `FakeLocalization.Keys()` and keep the `.resx` check in `OrganisationMessageKeysTests` through `ResourceManager.GetString(key, new CultureInfo("ar"))`.
- **R7 — `GetMyScopeQueryHandler` parses `UserId` with `Guid.Parse(currentUser.UserId!)`.** UT-01's `CurrentUsers.DefaultUserId` is `"user-1"` and would throw `FormatException`; the Organisation `CurrentUsers` helper must pass a Guid string. Pinned as a Follow-up (a non-staff or non-Guid subject crashes with 500 rather than a Result).
- **R8 — Floating-point reference values.** The `4.48` / `4.54` contrasts and the dark-window luminance bounds depend on exact formulas; compute expected values with the same WCAG constants once in `BrandColourTests` and reuse. If `BrandTokenGenerator.ForDark` lands outside `[0.115, 0.180]` by < 1e-3 for an edge colour (24-step bisection), allow an epsilon of 1e-3 and record it.
- **R9 — Test-count/time growth.** ≈ 600 tests with InMemory contexts; keep one context per test and no `Thread.Sleep`; if the run exceeds ~60 s, split InMemory classes into a shared `IClassFixture` seed (read-only data only).
- **R10 — Candidate production observations to log (no code change):** (a) `AddMembership`/`ChangeBranding` rely on `Enum.Parse` after validation — a request skipping the validator 500s;
  (b) `ChangeBrandingCommandHandler` runs `SetOriginalRowVersion` before palette/logo validation (harmless); (c) `Department.Create` accepts a branch of any tenant (the handler only gets branches through the scoped repository);
  (d) `BrandLogo.Sniff` accepts a bare 8-byte PNG signature and any bytes after it; (e) `ListDepartmentsQueryHandler` applies the active-branch filter in memory (two queries); (f) `GetMembershipsOfUserQueryHandler` assumes all memberships share one tenant (takes the first).
  Confirm each against the code while writing the test, pin it with a test whose name states the current behaviour, and list it under Follow-ups.
