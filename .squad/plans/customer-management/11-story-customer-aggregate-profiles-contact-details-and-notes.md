# Story 11 — Customer aggregate profiles contact details and notes

## Prerequisites

- **Stories 01–07 are implemented and committed** (latest `b909f80`). Read the "Product rules" of
  [`../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md`](../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md)
  (the scope filter, the write guard, `ResolveWriteTarget`, 403 never 404) and of
  [`../identity-access/07-story-authentication-users-roles-and-permissions.md`](../identity-access/07-story-authentication-users-roles-and-permissions.md)
  (every request declares exactly one authorisation attribute; `customers.read`/`customers.write` already exist in
  `CrmPermissions`; failed `Result`s roll back). Story 06's coordination bullet says: "Story 11 deletes `Sample` and
  copies its scoped shape (`IOrganisationScoped`, `ResolveWriteTarget`, the list query) for `Customer`."
- **Modules:** creates the **`Customers`** module (five projects, schema `customers`) and **deletes the `Sample`**
  module (five projects, `ModuleCatalog` line, host references, solution entries, `sample` schema by a documented
  drop). Touches the **BuildingBlocks** only for (a) one small `Specification` addition (`ApplyThenBy`, B3) and
  (b) comments that name `Sample`. Touches **`Organisation`** only in two comments. Cross-module work:
  `Customers.Infrastructure` reads `OrganisationDevSeed` ids (Contracts, rule 1 allowed — the Sample precedent).
  **No other cross-module reference.** Customers publishes two integration events (no subscribers yet).
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md`).** Absorbs the **notes** half of story 12. **Deferred:**
  attachments, customer merge, fuzzy duplicate detection, addresses, tax numbers, tiers, the interaction timeline
  (story 13), the customer UI (story 14), department transfer of a customer, a customer delete endpoint.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`,
  rollForward latestFeature); EF Core **10.0.11**, `dotnet-ef` **10.0.11** (`.config/dotnet-tools.json`, local tool —
  `dotnet tool restore` first); MediatR **12.4.1** (licence pin — do not bump); FluentValidation **12.1.1**;
  Swashbuckle **10.2.3** (`backend/Directory.Packages.props`). **No new NuGet package. No new npm package.**
  Frontend pins unchanged (`frontend/package.json`: Nuxt 4.5.2, openapi-typescript 7.13.0, openapi-fetch 0.17.0,
  pnpm 12.6.0). This machine has **no Python** and **no `pnpm` on PATH** until
  `corepack enable pnpm --install-directory "$APPDATA/npm"` (CLAUDE.md); Arabic text and `$` break Bash heredocs —
  use the **Write tool** for every file with Arabic in it (`.resx`, seeder, `.http`).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — optional value objects nested in an owned collection.** `Contact.Email` and `Contact.Phone` are `OwnsOne`
    inside `OwnsMany(Contacts)`, both optional, sharing the `CustomerContacts` table (B7). The model must build, the
    migration must give nullable `Email`/`Phone` columns on `customers.CustomerContacts`, a phone-only contact must
    round-trip with `email: null`, and `ct.Email != null && ct.Email.Value.Contains(term)` must translate.
    **Prove:** read the generated `Initial` migration (Test Plan row 3); then `customers.http` requests 6 (create with
    a phone-only contact → `contacts[1].email` is `null`) and 3c (search by email fragment → the EF log shows an
    `EXISTS (SELECT 1 FROM [customers].[CustomerContacts] … [Email] LIKE …)` subquery). EF may log
    `OptionalDependentWithoutIdentifyingPropertyWarning` at startup — a log line, not a failure; accept it.
    **Fallback (no other):** map `Email`/`Phone` with `HasConversion` to nullable `nvarchar` columns (like
    `BranchConfiguration`'s `Code`), change the search and duplicate predicates to **exact** normalised matches
    (`ct.Email == email` with a constructed `EmailAddress` parameter), and record that email/phone search became
    exact-match.
  - **R2 — the customer-number sequence read.** `SqlCustomerNumberGenerator` (B7) runs
    `SELECT NEXT VALUE FOR [customers].[CustomerNumbers] AS [Value]` through
    `Database.SqlQuery<long>(…).ToListAsync()` — **un-composed**, because SQL Server rejects `NEXT VALUE FOR` inside a
    subquery and any LINQ operator after `SqlQuery` (`First`, `Single`) wraps it in one. It must run on the command's
    open transaction. **Prove:** `customers.http` request 6 → `customer.number` is `C-000004` on a fresh database
    (three seeded customers take 1–3); the EF log shows the `NEXT VALUE FOR` statement between `BEGIN TRANSACTION`
    and the `INSERT INTO [customers].[Customers]`. **Fallback (no other):** in the same class,
    `DbCommand command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT NEXT VALUE
    FOR [customers].[CustomerNumbers]"; command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();`
    then `Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture)` (open the connection
    first if `State != Open`); record which path shipped.
  - **R3 — a child-only change is concurrency-checked.** Contacts live in their own table, so adding a contact alone
    would not UPDATE the `Customers` row and its `rowversion` would be neither checked nor bumped (and
    `AuditingSaveChangesInterceptor.Audit`, lines 97–104, only looks at owned **references**, not collections). Every
    profile/status/contact method on `Customer` therefore calls `MarkChanged()` (`Revision++`, B5). **Prove:**
    `customers.http` request 19 → EF log shows `UPDATE [customers].[Customers] SET [ModifiedAtUtc] = …, [ModifiedBy]
    = …, [Revision] = … WHERE [Id] = @… AND [RowVersion] = @…` and `INSERT INTO [customers].[CustomerContacts]` in one
    transaction; request 26 (add contact with a stale `rowVersion`) → **409** `Persistence.ConcurrencyConflict`.
    **Fallback:** none — if the root UPDATE is missing, `MarkChanged()` is not being called; fix the method.
  - **R4 — `ApplyThenBy` in the building blocks.** B3 changes `SpecificationEvaluator`'s ordering block. **Prove:**
    `dotnet build`; `users.http` request 1 (Admin users list, `UsersSpecification` — `OrderBy` only) still returns the
    users ordered by display name; `customers.http` request 4 (sort `nameEn`, page size 2) shows
    `ORDER BY [c].[DisplayNameEn], [c].[Number]` in the EF log.
  - **R5 — Swagger schema ids are unique in the Agent document.** Swashbuckle names schemas by type name, and the
    Agent document already holds Organisation's `LocalizedTextDto` and Identity's `MeDto`, `SignInResponse`,
    `RefreshRequest`, `ChangePasswordRequest`. Every Customers DTO/request name in this plan is new (`CustomerNameDto`,
    not `LocalizedTextDto`). **Prove:** `GET /swagger/agent/swagger.json` → 200 (a clash is a 500 "Conflicting
    schemaIds"). **Fallback:** rename the clashing Customers record with a `Customer` prefix; record it.
  - **R6 — `Sample` leaves no residue.** Deleting tracked files leaves untracked `bin/`/`obj/` folders, stale
    `Crm.Modules.Sample.*.dll` copies in `backend/src/Crm.Api/bin/`, and the solution folder `Sample` in `Crm.sln`.
    **Prove:** Test Plan row 13 (delete every `bin`/`obj` under `backend/src`, rebuild, then
    `grep -r Sample backend/src` prints nothing — including "Binary file … matches").
  - **R7 — the documented `sample` schema drop.** **Prove:** run the SQL in B13's README section against the dev
    database, restart the API: startup migrates `organisation`, `identity`, `customers` only; `SELECT name FROM
    sys.schemas WHERE name = 'sample'` returns no row.
  - **R8 — Arabic search.** Parameters are `nvarchar` and the default collation matches Arabic characters exactly.
    **Prove:** `customers.http` request 2 (search `أحمد`, percent-encoded) returns the seeded "Ahmed Al-Qahtani".
  - **R9 — paging with two owned collections.** `Customer` owns `Contacts` and `Notes`; EF loads both with every
    customer and applies `Skip`/`Take` to the root in a subquery. **Prove:** `customers.http` request 4
    (`pageSize=2`) returns **2 distinct** customers and a `total` equal to the unpaged count. A
    `MultipleCollectionIncludeWarning` log line is acceptable.
  - **R10 — the cached `CustomerDto` round-trips.** `GetCustomerByIdQuery` is cached (nested lists, `byte[]`).
    **Prove:** `customers.http` request 16 twice → one `SELECT … FROM [customers].[Customers]` in the EF log across both
    calls and identical bodies (including `rowVersion`); request 17 then 16 → a fresh SELECT and the new values.
  - **R11 — `pnpm` and `gen:api`.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, then
    `pnpm --version` prints 12.6.0 before F1.
- **Coordination:**
  - **Story 14** (Customer 360 UI) consumes exactly the Agent endpoints in B9 and maps every `Customers.*` error
    code in the Product rules to `errors.code.Customers.*` i18n keys (domain messages are English server text, like
    every other module's). Duplicate warning: `POST /api/Agent/Customers` returns **200** with `created: false` and
    `duplicateCandidates`; "create anyway" re-sends with `confirmDuplicate: true`. Filters/sort in `SearchCustomersQuery`
    are the ones its intake lists. `CustomerPicker` uses `GET /api/Agent/Customers/lookup`.
  - **Story 15** (Tickets) reads `ICustomersModuleApi.GetCustomerSummaryAsync` (403 out of scope, 404 unknown — both
    failed `Result`s) and checks the contact id against `CustomerSummary.Contacts`. It may seed tickets against
    `CustomersDevSeed` ids.
  - **Story 47** (Portal) calls `FindContactByEmailAsync` during **anonymous** registration. That request declares
    `[RequirePermission(CrmPermissions.CustomersRead)]` and runs under the caller's scope, so an anonymous caller gets
    401 and a portal token gets 403. Story 47 must add its own, security-reviewed path (a second lift is a security
    review — `Specification.cs` lines 50–53) — **do not** loosen `FindContactsByEmailQuery` here. The seeded
    Ahmed contact has portal access enabled for that story.
  - **Story 13** (deferred) subscribes to `CustomerCreatedIntegrationEvent` / `CustomerUpdatedIntegrationEvent`.

---

## Story Goal

The Customer bounded context exists and is right before tickets are built on it.

When this story is done:

1. A new `Customers` module (`Crm.Modules.Customers.*`, schema `customers`) holds the `Customer` aggregate: a
   generated `CustomerNumber` (`C-000123`), a bilingual display name, type (individual / organisation), legal name
   (organisations only, required), status (active / inactive), preferred language (`en`/`ar`), and owning branch
   and department (`IOrganisationScoped`, so Story 06's filter and write guard apply).
2. `Contact` children (name, job title, `EmailAddress`, E.164 `PhoneNumber`, primary flag, portal-access flag) and
   `CustomerNote` children (body, pinned flag, author id, timestamps) are reachable only through `Customer`: no
   `DbSet<Contact>`, no `DbSet<CustomerNote>`, no repository for either.
3. The aggregate guards: exactly one primary contact whenever any contact exists (adding or setting a primary demotes
   the previous one in the same operation); no duplicate email or phone within one customer; an organisation
   requires a legal name; malformed email / non-E.164 phone rejected when the value object is built — all 422
   `urn:crm:problem:domain-rule`.
4. Agent endpoints (`/api/Agent/Customers`, permission `customers.read` / `customers.write`): create (with a
   duplicate warning on an exact contact email/phone match in the caller's scope, confirmable), update, deactivate,
   reactivate, add / update / remove contact, set primary contact, add / pin / unpin / remove note, get by id
   (cached), paged search (Arabic/English name, number, email, phone; filters; sort), and a lookup projection for
   the ticket customer picker.
5. `Customers.Contracts` exposes `ICustomersModuleApi` (`GetCustomerSummaryAsync`, `FindContactByEmailAsync`), the
   `CustomersDevSeed` ids, and two integration events.
6. `Sample` is gone: `grep -r Sample backend/src` prints nothing, the build is clean, the `sample` schema has a
   documented drop, and every `.http` file that used Sample now uses Customers.
7. `backend/requests/customers.http` exercises every use case and every rejected invariant.

**Not in scope:** everything deferred in Prerequisites, any customer screen (story 14), and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith" (catalogue: `Customers` owns 11–13; "The `Sample` module is the canonical
   template and is deleted in Story 11" at lines 71–72), "Domain-driven design", "Application, API, and
   persistence" (**Results**, **Localization keys**, **Caching**, **Scoping**, **Time**), "Testing policy".
2. **The whole `backend/src/Modules/Sample/` module — the template, read it before deleting it.** Especially:
   `Crm.Modules.Sample.Domain/SampleAggregate.cs` lines 10–89 (scoped aggregate shape, EF constructor `null!`,
   `Create` with optional id and `TenantId.None` guard), `SampleLine.cs` (child entity), `SampleName.cs` (value object
   constructor parameter named after the mapped property); `Application/Commands/CreateSample/CreateSampleCommandHandler.cs`
   lines 17–35 (`ResolveWriteTarget` and its two failures — copy verbatim), `Commands/RenameSample/*` (rowversion,
   `IEventBus.Publish` after `SaveChangesAsync`), `Queries/GetSampleById/*` (`ICacheableQuery`), `SampleCacheKeys.cs`,
   `SampleModuleApi.cs`, `ISampleUnitOfWork.cs`, `AssemblyReference.cs`; `Infrastructure/Persistence/Configurations/SampleAggregateConfiguration.cs`
   lines 12–47, `SampleDbContext.cs`, `SampleDbContextFactory.cs`, `SampleSeeder.cs` lines 14–39 (idempotency with
   both named filters lifted), `SampleModule.cs`; `Presentation/Controllers/Admin/SamplesController.cs`; all five
   `.csproj` (Infrastructure lines 14–16: the Organisation.Contracts reference; Presentation line 7:
   `DisableTransitiveProjectReferences`).
3. `backend/src/Modules/Identity/` — `Crm.Modules.Identity.Domain/EmailAddress.cs` lines 1–62 (copy for Customers,
   change the error code), `PreferredLocale.cs` (copy as `PreferredLanguage`), `RefreshToken.cs` (child `Entity<Guid>`
   with `internal` constructor); `Infrastructure/Persistence/Configurations/UserConfiguration.cs` lines 20–25 (`OwnsOne`
   so `.Value.Contains` translates) and `RefreshTokenFamilyConfiguration.cs` lines 23–35 (`OwnsMany` + **`ValueGeneratedNever`**
   on the child key — without it EF UPDATEs a new child instead of INSERTing it); `Application/Queries/ListUsers/*` and
   `Specifications/UsersSpecification.cs` (paged search + count with the same specification);
   `Application/Validation/IdentityRules.cs`, `Localization/IdentityMessageKeys.cs`,
   `Localization/Resources/IdentityMessages{,.ar}.resx`, `IdentityModule.cs` lines 35–37 (`AddLocalizationResource`);
   `Presentation/Controllers/Admin/UsersController.cs` (paged list action, `[ProducesResponseType]` style).
4. `backend/src/Modules/Organisation/` — `Crm.Modules.Organisation.Domain/Branch.cs`, `LocalizedText.cs` (shape to copy,
   different rules), `Application/Dtos.cs` lines 5–9 (`LocalizedTextDto` — the name you must **not** reuse, R5),
   `Specifications/DepartmentsSpecification.cs` lines 12–20 (one combined `Where`; `.Equals` on scope ids),
   `Contracts/DevSeed/OrganisationDevSeed.cs` (the ids the seed uses; comment lines 3–5 name Sample),
   `Infrastructure/Persistence/OrganisationSeeder.cs`, `Configurations/MembershipConfiguration.cs` line 16 (comment
   names `SampleId`).
5. BuildingBlocks: `Crm.SharedKernel/AuditableEntity.cs`, `Entity.cs`, `ValueObject.cs`, `StronglyTypedId.cs`,
   `DomainException.cs`, `IIntegrationEvent.cs` lines 8–15, `Scoping/ITenantScoped.cs`, `Scoping/ScopeIds.cs`;
   `Crm.BuildingBlocks.Application/Specifications/Specification.cs` lines 14–53 (no `ThenBy` today; `Where` replaces);
   `Abstractions/IRepository.cs`, `Abstractions/OrganisationScope.cs` lines 116–148 (`ResolveWriteTarget`),
   `Caching/ICacheableQuery.cs`, `Messaging/IEventBus.cs`, `Messaging/ICommandMarker.cs`, `Paging/PagedResult.cs`,
   `Behaviors/TransactionBehavior.cs` lines 49–89 (failed `Result` rolls back; events staged in the outbox; cache
   invalidated after commit), `Behaviors/OrganisationScopeBehavior.cs` lines 24–35 (repository 403 → failed Result);
   `Crm.BuildingBlocks.Infrastructure/Persistence/Repositories/SpecificationEvaluator.cs` lines 30–37 (ordering block
   B3 changes), `Repositories/Repository.cs` lines 23–58 (`GetByIdAsync` 403-on-miss), `Persistence/ModuleDbContext.cs`
   lines 32–51 (`OnModelCreating` — the override in B7 calls `base` first), `Persistence/Interceptors/AuditingSaveChangesInterceptor.cs`
   lines 39–106, `Modules/ModuleServiceCollectionExtensions.cs`; `Crm.BuildingBlocks.Presentation/ApiControllerBase.cs`
   lines 26–68, `ErrorHandling/ErrorTypeMapping.cs`, `AREAS.md` line 25.
6. Host: `backend/src/Crm.Api/Modules/ModuleCatalog.cs` lines 12–22 (Sample line 20–21), `Crm.Api.csproj` lines 14–15
   (Sample references), `Program.cs` lines 61–95 (Swagger: schema ids by type name — R5).
7. `backend/Crm.sln` lines 22–32 (the `Sample` solution folder and its five projects) and its `NestedProjects`
   section; `backend/Directory.Build.targets` rules 1–7; `backend/build/BannedSymbols.*.txt`.
8. `backend/requests/diagnostics.http` (lines 6–8, 22–30, 35–43, 45–78, 95–134 use Sample), `organisation.http` lines
   216–295 (requests 14–23), `persistence.http` (whole file), `localization.http` lines 7, 86–124, `health.http`
   lines 9–16; `auth.http` lines 1–20 for the login pattern.
9. `backend/README.md` lines 42–45, 57–63, 68–74, 106–116, 140–143, 165–171, 207, 217–226, 277–280 (every Sample
   mention).
10. Frontend: `frontend/app/pages/_dev/api.vue` lines 22–29, 83–102, 133–158, 290–308 (the only frontend code using
    Sample endpoints); `frontend/scripts/gen-api.ts` lines 1–37 (online mode needs the API on :5187);
    `frontend/app/api/problem.ts` lines 28–29, 103–110 (field keys become camelCase).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **Customer number.** `C-` + the next value of the SQL Server sequence `customers.CustomerNumbers`, zero-padded to
  six digits (`C-000123`; `C-1000000` past a million). Globally unique (unique index on `Number`), never reused,
  gaps allowed (a create that fails after drawing a number leaves a gap; sequences are not transactional). Not
  editable.
- **Display name.** `LocalizedText` with Arabic and English sides; **at least one is required**; a missing side is
  filled with the other, so both columns are always non-null and both languages always render. ≤ 200 each.
- **Type and legal name.** `Individual` or `Organisation`. An organisation **requires** a legal name (≤ 300); an
  individual **must not** carry one. Both are aggregate rules (422), deliberately **not** validator rules, so the
  acceptance criterion "rejected by the aggregate with 422 `domain-rule`" holds.
- **Status.** `Active` (on create) or `Inactive`. Deactivating an inactive customer or reactivating an active one is
  422. An inactive customer stays readable and editable; the lookup (picker) returns active customers only.
- **Preferred language.** `en` or `ar` (validator, localized message; the value object also guards).
- **Contacts.** Name (1–200), optional job title (≤ 100), optional `EmailAddress`, optional `PhoneNumber`, **at least
  one of email or phone** (422 `Customers.ContactChannelRequired`). Portal access requires an email (422
  `Customers.PortalAccessRequiresEmail`). The **first** contact becomes primary automatically; adding with
  `isPrimary: true` or calling set-primary demotes every other contact in the same operation. The primary contact
  cannot be removed while other contacts exist (422 `Customers.PrimaryContactRemoval` — set another primary first);
  the last contact can be removed. No duplicate email and no duplicate phone within one customer (normalised
  comparison, 422). Contacts render primary first, then by name.
- **Email** — trimmed, lower-case invariant, ≤ 254, `local@domain.tld` shape (same rule as Identity; Customers owns
  its own `EmailAddress`). **Phone** — spaces, `-`, `(`, `)`, `.` stripped, then E.164 `^\+[1-9]\d{1,14}$`;
  `0501234567` is rejected, `+966 50 123 4567` is stored `+966501234567`. Both are rejected **at value-object
  construction** (422 `Customers.EmailInvalid` / `Customers.PhoneInvalid`); validators check only length.
- **Notes.** Body 1–4000, internal only, author = the signed-in user id, `CreatedAtUtc` from `IDateTime`. Pin/unpin
  is idempotent. Notes render **pinned first**, then newest first. Removing a note deletes its row (no soft delete on
  children). Note commands take **no** `rowVersion` and do not bump `Revision`: notes carry no cross-note invariant,
  so concurrent note adds by two agents never 409.
- **Concurrency.** Every profile, status, and contact command takes the customer's last-seen `rowVersion`
  (base64) and calls `SetOriginalRowVersion`; each of those aggregate methods increments `Customer.Revision` so the
  root row is always part of the UPDATE (R3). Every command returns the full `CustomerDto` with the new
  `rowVersion`.
- **Duplicate warning.** On create only: if `confirmDuplicate` is false and any submitted contact email or phone
  **exactly** matches (normalised) a contact of an existing customer **in the caller's scope** (Story 06 filter —
  another department's customer is invisible, so it never warns), the command creates nothing and returns **200**
  `{ created: false, customer: null, duplicateCandidates: [...] }` (max 10, with the matched emails/phones). With
  `confirmDuplicate: true` the check is skipped. Malformed values are rejected (422) **before** the duplicate check.
- **Search.** One term matched with `LIKE %term%` against `DisplayNameAr`, `DisplayNameEn`, `Number`, `LegalName`,
  any contact `Email`, and any contact `Phone` (the phone side with separators stripped); plus filters `status`,
  `type`, `departmentId`, `branchId`; sort `createdAt` (default, descending), `number`, `nameEn`, `nameAr`, always
  tie-broken by `Number`; `page` ≥ 1, `pageSize` 1–100 (default 20). Filters outside the caller's scope return an
  empty page, not 403 (the scope filter already removed those rows). No Arabic normalisation (alef/hamza variants,
  diacritics) — deferred with fuzzy matching.
- **Caching.** Only `GetCustomerByIdQuery` is cached (`customers:customers:{id}`, culture-neutral — both name
  languages); every customer command invalidates that tag after commit.
- **Scope.** `Customer` is `IOrganisationScoped`: every read and write is filtered/guarded by tenant, branch, and
  department; an existing out-of-scope id is 403 `Organisation.OutOfScope`, an unknown id 404
  `Customers.CustomerNotFound`. Create resolves its department with `ResolveWriteTarget` (optional `departmentId`).
  The owning department never changes in this story.
- **Integration events** (Contracts, append-only, primitives, no subscribers yet): `CustomerCreatedIntegrationEvent`
  from create; `CustomerUpdatedIntegrationEvent` from update, deactivate, reactivate. Contact and note changes publish
  nothing in the MVP.
- **Soft delete.** `Customer` is `ISoftDeletable` (global filter, `IsDeleted` index) for later merge/delete stories;
  this story exposes no delete endpoint.
- **Error codes** (the frontend's `errors.code.*` contract — never rename one): `Customers.CustomerNotFound`,
  `Customers.ContactNotFound`, `Customers.NoteNotFound` (404); `Customers.ScopeRequired`, `Customers.NumberInvalid`,
  `Customers.TypeUnsupported`, `Customers.NameRequired`, `Customers.NameTooLong`, `Customers.LegalNameRequired`,
  `Customers.LegalNameNotAllowed`, `Customers.LegalNameTooLong`, `Customers.LanguageUnsupported`,
  `Customers.AlreadyActive`, `Customers.AlreadyInactive`, `Customers.EmailInvalid`, `Customers.PhoneInvalid`,
  `Customers.ContactNameRequired`, `Customers.ContactNameTooLong`, `Customers.JobTitleTooLong`,
  `Customers.ContactChannelRequired`, `Customers.PortalAccessRequiresEmail`, `Customers.DuplicateContactEmail`,
  `Customers.DuplicateContactPhone`, `Customers.PrimaryContactRemoval`, `Customers.NoteBodyRequired`,
  `Customers.NoteBodyTooLong`, `Customers.NoteAuthorRequired` (422); plus the existing `Organisation.OutOfScope`
  (403), `Organisation.DepartmentRequired` (400), `Persistence.ConcurrencyConflict` (409).
- **Reference module.** From this story on, `Customers` is the module to copy (README "Add a module").

---

## Backend Tasks

All paths are under `backend/src/` unless they start with `backend/` or `frontend/`. File-scoped namespaces,
`sealed` classes, primary constructors for handlers, `IDateTime` for every clock read, `ArgumentNullException.ThrowIfNull`
on public methods' reference parameters. EF-only private constructors may assign `null!` to value-object properties
exactly as `SampleAggregate.cs` line 35 does; **no other `!`** in new code. Customers namespaces:
`Crm.Modules.Customers.{Domain,Application,Contracts,Infrastructure,Presentation}`.

### B1 — Projects and solution

Create `Modules/Customers/` with five projects, each a copy of the matching Sample `.csproj` with `Sample` →
`Customers` in every path:

- `Crm.Modules.Customers.Domain/Crm.Modules.Customers.Domain.csproj` — references `Crm.SharedKernel` only.
- `Crm.Modules.Customers.Contracts/Crm.Modules.Customers.Contracts.csproj` — `Crm.SharedKernel` only.
- `Crm.Modules.Customers.Application/Crm.Modules.Customers.Application.csproj` — `Crm.BuildingBlocks.Application`,
  own Domain, own Contracts.
- `Crm.Modules.Customers.Infrastructure/Crm.Modules.Customers.Infrastructure.csproj` — the
  `Microsoft.EntityFrameworkCore.Design` package block (comment: `ef.ps1 -Module Customers`),
  `Crm.BuildingBlocks.Infrastructure`, own Application, and
  `..\..\Organisation\Crm.Modules.Organisation.Contracts\Crm.Modules.Organisation.Contracts.csproj` (comment: "The dev
  seed places customers in the Organisation dev-seed departments. A Contracts reference is rule 1 allowed.").
- `Crm.Modules.Customers.Presentation/Crm.Modules.Customers.Presentation.csproj` — `DisableTransitiveProjectReferences`
  with the same comment, `Crm.SharedKernel`, `Crm.BuildingBlocks.Application`, `Crm.BuildingBlocks.Presentation`,
  own Application.

Then, from the repo root:

```bash
dotnet sln backend/Crm.sln add backend/src/Modules/Customers/Crm.Modules.Customers.Domain/Crm.Modules.Customers.Domain.csproj \
  backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/Crm.Modules.Customers.Contracts.csproj \
  backend/src/Modules/Customers/Crm.Modules.Customers.Application/Crm.Modules.Customers.Application.csproj \
  backend/src/Modules/Customers/Crm.Modules.Customers.Infrastructure/Crm.Modules.Customers.Infrastructure.csproj \
  backend/src/Modules/Customers/Crm.Modules.Customers.Presentation/Crm.Modules.Customers.Presentation.csproj
```

Confirm the projects landed under a `Customers` solution folder inside `Modules` (like `Identity`). Sample's removal
is B11.

### B2 — Contracts (`Crm.Modules.Customers.Contracts`)

**Create file: `ICustomersModuleApi.cs`**

```csharp
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Contracts;

/// <summary>What another module may ask the Customers module, synchronously. Implemented by sending the module's
/// own queries, so validation, authorisation (customers.read) and organisation scoping apply: an existing customer
/// outside the caller's scope is a failed Result (403 Organisation.OutOfScope), an unknown id a failed Result
/// (404 Customers.CustomerNotFound). Primitives and contract DTOs only.</summary>
public interface ICustomersModuleApi
{
    /// <summary>Story 15: does this customer exist in my scope, which department owns it, which contacts does it have.</summary>
    Task<Result<CustomerSummary>> GetCustomerSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Story 47: every in-scope contact whose email equals <paramref name="email"/> (normalised). A
    /// malformed email is an empty list, not a failure.</summary>
    Task<Result<IReadOnlyList<CustomerContactMatch>>> FindContactByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public sealed record CustomerSummary(
    Guid Id,
    string Number,
    string DisplayNameAr,
    string DisplayNameEn,
    string Type,
    bool IsActive,
    string PreferredLanguage,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId,
    IReadOnlyList<CustomerContactSummary> Contacts);

public sealed record CustomerContactSummary(Guid Id, string Name, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess);

public sealed record CustomerContactMatch(
    Guid CustomerId,
    string CustomerNumber,
    bool IsCustomerActive,
    Guid ContactId,
    string ContactName,
    string Email,
    bool HasPortalAccess,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId);
```

**Create file: `IntegrationEvents/CustomerCreatedIntegrationEvent.cs`** and **`IntegrationEvents/CustomerUpdatedIntegrationEvent.cs`**
(shape of `SampleRenamedIntegrationEvent.cs` lines 4–10):

```csharp
/// <summary>Published by CreateCustomerCommandHandler. Primitives only; append-only (never rename or remove a member).</summary>
public sealed record CustomerCreatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid CustomerId, string Number, string DisplayNameAr, string DisplayNameEn,
    string Type, Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by UpdateCustomer, DeactivateCustomer and ReactivateCustomer. Primitives only; append-only.</summary>
public sealed record CustomerUpdatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid CustomerId, string DisplayNameAr, string DisplayNameEn, string Type,
    string Status) : IntegrationEvent(Id, OccurredOnUtc);
```

**Create file: `DevSeed/CustomersDevSeed.cs`** — primitives, Development only (shape of `OrganisationDevSeed.cs`):

```csharp
namespace Crm.Modules.Customers.Contracts.DevSeed;

/// <summary>Well-known Development customer and contact ids, as primitives - for later seeds (Story 15 tickets,
/// Story 47 portal accounts) and the .http files. Never used outside Development.</summary>
public static class CustomersDevSeed
{
    /// <summary>Individual, Riyadh Customer Care. Contact has portal access (Story 47).</summary>
    public static readonly Guid AhmedCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000001");
    public static readonly Guid AhmedContactId = Guid.Parse("11000000-0000-0000-0001-000000000001");
    public const string AhmedEmail = "ahmed@example.com";
    public const string AhmedPhone = "+966501234567";

    /// <summary>Organisation, Riyadh Customer Care, two contacts and a pinned note.</summary>
    public static readonly Guid NajdLogisticsCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000002");
    public static readonly Guid NajdSaraContactId = Guid.Parse("11000000-0000-0000-0001-000000000002");
    public static readonly Guid NajdOmarContactId = Guid.Parse("11000000-0000-0000-0001-000000000003");
    public static readonly Guid NajdPinnedNoteId = Guid.Parse("11000000-0000-0000-0002-000000000001");

    /// <summary>Organisation, Jeddah Customer Care (agent B's department - invisible to agent A).</summary>
    public static readonly Guid RedSeaTradingCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000003");
    public static readonly Guid RedSeaFahadContactId = Guid.Parse("11000000-0000-0000-0001-000000000004");
}
```

### B3 — Building blocks: `ApplyThenBy` (deterministic paging)

**File: `BuildingBlocks/Crm.BuildingBlocks.Application/Specifications/Specification.cs`** — after line 20 add
`public Expression<Func<T, object>>? ThenBy { get; private set; }`; after line 38 add
`protected void ApplyThenBy(Expression<Func<T, object>> thenBy) => ThenBy = thenBy;` with the XML summary "Secondary
ascending key after OrderBy/OrderByDescending - paging on a non-unique key is non-deterministic on SQL Server without
one.".

**File: `BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Persistence/Repositories/SpecificationEvaluator.cs`** — replace
lines 30–37 with:

```csharp
        if (specification.OrderBy is not null)
        {
            IOrderedQueryable<T> ordered = query.OrderBy(specification.OrderBy);
            query = specification.ThenBy is null ? ordered : ordered.ThenBy(specification.ThenBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            IOrderedQueryable<T> ordered = query.OrderByDescending(specification.OrderByDescending);
            query = specification.ThenBy is null ? ordered : ordered.ThenBy(specification.ThenBy);
        }
```

Existing specifications set no `ThenBy`, so their SQL is unchanged (R4).

### B4 — Domain: value objects and enums (`Crm.Modules.Customers.Domain`)

**Create file: `CustomerId.cs`** — `public sealed record CustomerId(Guid Value) : StronglyTypedId(Value) { public static CustomerId New() => new(Guid.NewGuid()); }`
(shape of `SampleId.cs`).

**Create file: `CustomerType.cs`** — `public enum CustomerType { Individual, Organisation }`.
**Create file: `CustomerStatus.cs`** — `public enum CustomerStatus { Active, Inactive }`.

**Create file: `CustomerDomainErrors.cs`** — one `internal static class CustomerDomainErrors` holding every 422
`DomainError` listed in Product rules as `public static readonly DomainError X = new("Customers.X", "<English
message>");`, e.g. `LegalNameRequired = new("Customers.LegalNameRequired", "An organisation customer requires a legal
name.")`, `PrimaryContactRemoval = new("Customers.PrimaryContactRemoval", "Make another contact primary before removing
this one.")`, `ContactNotFound = new("Customers.ContactNotFound", "The contact does not belong to this customer.")`,
`NoteNotFound = new("Customers.NoteNotFound", "The note does not belong to this customer.")`. Every `throw new
DomainException(...)` in the domain passes one of these — no inline code strings elsewhere in the Domain project.

**Create file: `CustomerNumber.cs`**

```csharp
using System.Globalization;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>"C-" + a sequence value, zero-padded to six digits. The private constructor parameter is named after
/// the mapped property (EF owned-type binding). Built only from the customers.CustomerNumbers sequence.</summary>
public sealed class CustomerNumber : ValueObject
{
    public const int MaxLength = 20;
    public const string Prefix = "C-";

    public string Value { get; }

    private CustomerNumber(string value)
    {
        Value = value;
    }

    public static CustomerNumber FromSequence(long sequenceValue)
    {
        if (sequenceValue <= 0)
        {
            throw new DomainException(CustomerDomainErrors.NumberInvalid);
        }

        return new CustomerNumber(string.Create(CultureInfo.InvariantCulture, $"{Prefix}{sequenceValue:D6}"));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Create file: `LocalizedText.cs`** — copy `Organisation.Domain/LocalizedText.cs` (private constructor
`(string arabic, string english)`, properties `Arabic`/`English`, `MaxLength = 200`) with this `Create`:

```csharp
    /// <summary>At least one side is required; a missing side takes the other's text, so both languages always render.</summary>
    public static LocalizedText Create(string? arabic, string? english)
    {
        string ar = arabic?.Trim() ?? string.Empty;
        string en = english?.Trim() ?? string.Empty;

        if (ar.Length == 0 && en.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.NameRequired);
        }

        if (ar.Length > MaxLength || en.Length > MaxLength)
        {
            throw new DomainException(CustomerDomainErrors.NameTooLong);
        }

        return new LocalizedText(ar.Length == 0 ? en : ar, en.Length == 0 ? ar : en);
    }
```

**Create file: `EmailAddress.cs`** — copy `Identity.Domain/EmailAddress.cs` lines 1–103 verbatim, namespace
`Crm.Modules.Customers.Domain`, summary "A customer contact email…", and `Create` throws
`new DomainException(CustomerDomainErrors.EmailInvalid)`.

**Create file: `PhoneNumber.cs`**

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>E.164: '+', a non-zero first digit, at most 15 digits. Spaces, '-', '(', ')' and '.' are stripped first,
/// so "+966 50 123 4567" is stored "+966501234567"; "0501234567" (no country code) is rejected.</summary>
public sealed partial class PhoneNumber : ValueObject
{
    public const int MaxLength = 16;

    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryCreate(value, out PhoneNumber? phone))
        {
            throw new DomainException(CustomerDomainErrors.PhoneInvalid);
        }

        return phone;
    }

    public static bool TryCreate(string? value, [NotNullWhen(true)] out PhoneNumber? phone)
    {
        phone = null;

        if (value is null)
        {
            return false;
        }

        string normalised = Separators().Replace(value.Trim(), string.Empty);

        if (!E164().IsMatch(normalised))
        {
            return false;
        }

        phone = new PhoneNumber(normalised);
        return true;
    }

    /// <summary>The search-side normalisation: separators stripped, no shape check. Empty when nothing is left.</summary>
    public static string StripSeparators(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Separators().Replace(value.Trim(), string.Empty);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164();
}
```

**Create file: `PreferredLanguage.cs`** — copy `Identity.Domain/PreferredLocale.cs` as `PreferredLanguage`
(`English`/`Arabic` statics, `Create` → `CustomerDomainErrors.LanguageUnsupported`).

### B5 — Domain: `Customer`, `Contact`, `CustomerNote`

**Create file: `Contact.cs`**

```csharp
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>Child entity - no DbSet, no repository. Reachable only through Customer, which owns every rule that
/// spans contacts (one primary, no duplicate email/phone). The rules of a single contact live here.</summary>
public sealed class Contact : Entity<Guid>
{
    public const int NameMaxLength = 200;
    public const int JobTitleMaxLength = 100;

    public string Name { get; private set; }

    public string? JobTitle { get; private set; }

    public EmailAddress? Email { get; private set; }

    public PhoneNumber? Phone { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool HasPortalAccess { get; private set; }

    private Contact()
    {
        Name = string.Empty;
    }

    internal Contact(Guid id, string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
        : base(id)
    {
        Name = string.Empty;
        Change(name, jobTitle, email, phone, hasPortalAccess);
    }

    internal void Change(string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
    {
        ArgumentNullException.ThrowIfNull(name);

        string trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.ContactNameRequired);
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException(CustomerDomainErrors.ContactNameTooLong);
        }

        string? trimmedTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        if (trimmedTitle is { Length: > JobTitleMaxLength })
        {
            throw new DomainException(CustomerDomainErrors.JobTitleTooLong);
        }

        if (email is null && phone is null)
        {
            throw new DomainException(CustomerDomainErrors.ContactChannelRequired);
        }

        if (hasPortalAccess && email is null)
        {
            throw new DomainException(CustomerDomainErrors.PortalAccessRequiresEmail);
        }

        Name = trimmedName;
        JobTitle = trimmedTitle;
        Email = email;
        Phone = phone;
        HasPortalAccess = hasPortalAccess;
    }

    internal void MarkPrimary() => IsPrimary = true;

    internal void ClearPrimary() => IsPrimary = false;
}
```

**Create file: `CustomerNote.cs`** — `public sealed class CustomerNote : Entity<Guid>` with `BodyMaxLength = 4000`,
properties `string Body`, `bool IsPinned`, `Guid AuthorId`, `DateTimeOffset CreatedAtUtc`, `DateTimeOffset? PinnedAtUtc`
(all `private set`), EF constructor `private CustomerNote() { Body = string.Empty; }`, and:

```csharp
    internal CustomerNote(Guid id, string body, Guid authorId, DateTimeOffset createdAtUtc) : base(id)
    {
        ArgumentNullException.ThrowIfNull(body);

        string trimmed = body.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.NoteBodyRequired);
        }

        if (trimmed.Length > BodyMaxLength)
        {
            throw new DomainException(CustomerDomainErrors.NoteBodyTooLong);
        }

        if (authorId == Guid.Empty)
        {
            throw new DomainException(CustomerDomainErrors.NoteAuthorRequired);
        }

        Body = trimmed;
        AuthorId = authorId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Idempotent: pinning a pinned note keeps its original PinnedAtUtc.</summary>
    internal void Pin(DateTimeOffset pinnedAtUtc)
    {
        if (IsPinned)
        {
            return;
        }

        IsPinned = true;
        PinnedAtUtc = pinnedAtUtc;
    }

    internal void Unpin()
    {
        IsPinned = false;
        PinnedAtUtc = null;
    }
```

`CustomerNote` is **not** `IAuditable` — its own `CreatedAtUtc` is domain data, and the interceptor never touches it.

**Create file: `Customer.cs`**

```csharp
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Domain;

/// <summary>The aggregate root of the Customers context. Contacts and notes are reachable only through it; every
/// rule that spans contacts (exactly one primary, no duplicate email or phone) is enforced here.</summary>
public sealed class Customer : AuditableEntity<CustomerId>, ISoftDeletable, IHasRowVersion, IOrganisationScoped
{
    public const int LegalNameMaxLength = 300;

    private readonly List<Contact> _contacts = [];
    private readonly List<CustomerNote> _notes = [];

    public CustomerNumber Number { get; private set; }

    public LocalizedText DisplayName { get; private set; }

    public CustomerType Type { get; private set; }

    public string? LegalName { get; private set; }

    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;

    public PreferredLanguage PreferredLanguage { get; private set; }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    /// <summary>Incremented by every profile, status and contact change (<see cref="MarkChanged"/>). Contacts live in
    /// their own table, so without this a contact-only change would not UPDATE the Customers row - its rowversion
    /// would be neither checked nor bumped, and two concurrent "make primary" calls could both win. Do not remove.</summary>
    public int Revision { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    public IReadOnlyCollection<CustomerNote> Notes => _notes.AsReadOnly();

    public bool IsActive => Status == CustomerStatus.Active;

    private Customer()
    {
        Number = null!;
        DisplayName = null!;
        PreferredLanguage = null!;
    }

    private Customer(
        CustomerId id, CustomerNumber number, LocalizedText displayName, CustomerType type, string? legalName,
        PreferredLanguage preferredLanguage, TenantId tenantId, BranchId branchId, DepartmentId departmentId) : base(id)
    {
        Number = number;
        DisplayName = displayName;
        Type = type;
        LegalName = legalName;
        PreferredLanguage = preferredLanguage;
        TenantId = tenantId;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary><paramref name="id"/> defaults to a fresh id; the seeder passes a well-known one.</summary>
    public static Customer Create(
        CustomerNumber number, LocalizedText displayName, CustomerType type, string? legalName,
        PreferredLanguage preferredLanguage, TenantId tenantId, BranchId branchId, DepartmentId departmentId,
        CustomerId? id = null)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(preferredLanguage);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(departmentId);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(CustomerDomainErrors.ScopeRequired);
        }

        return new Customer(
            id ?? CustomerId.New(), number, displayName, EnsureDefined(type), NormaliseLegalName(type, legalName),
            preferredLanguage, tenantId, branchId, departmentId);
    }

    public void UpdateProfile(LocalizedText displayName, CustomerType type, string? legalName, PreferredLanguage preferredLanguage)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(preferredLanguage);

        string? normalisedLegalName = NormaliseLegalName(EnsureDefined(type), legalName);

        DisplayName = displayName;
        Type = type;
        LegalName = normalisedLegalName;
        PreferredLanguage = preferredLanguage;
        MarkChanged();
    }

    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
        {
            throw new DomainException(CustomerDomainErrors.AlreadyInactive);
        }

        Status = CustomerStatus.Inactive;
        MarkChanged();
    }

    public void Reactivate()
    {
        if (Status == CustomerStatus.Active)
        {
            throw new DomainException(CustomerDomainErrors.AlreadyActive);
        }

        Status = CustomerStatus.Active;
        MarkChanged();
    }

    /// <summary>The first contact is always primary; <paramref name="isPrimary"/> demotes the current primary in the
    /// same operation.</summary>
    public Contact AddContact(
        string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool isPrimary, bool hasPortalAccess,
        Guid? id = null)
    {
        EnsureChannelsUnique(email, phone, exceptContactId: null);

        var contact = new Contact(id ?? Guid.NewGuid(), name, jobTitle, email, phone, hasPortalAccess);
        bool makePrimary = isPrimary || _contacts.Count == 0;

        _contacts.Add(contact);

        if (makePrimary)
        {
            PromoteToPrimary(contact);
        }

        MarkChanged();
        return contact;
    }

    public void UpdateContact(Guid contactId, string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
    {
        Contact contact = FindContact(contactId);
        EnsureChannelsUnique(email, phone, exceptContactId: contactId);

        contact.Change(name, jobTitle, email, phone, hasPortalAccess);
        MarkChanged();
    }

    public void SetPrimaryContact(Guid contactId)
    {
        PromoteToPrimary(FindContact(contactId));
        MarkChanged();
    }

    public void RemoveContact(Guid contactId)
    {
        Contact contact = FindContact(contactId);

        if (contact.IsPrimary && _contacts.Count > 1)
        {
            throw new DomainException(CustomerDomainErrors.PrimaryContactRemoval);
        }

        _contacts.Remove(contact);
        MarkChanged();
    }

    public bool HasContact(Guid contactId) => _contacts.Exists(c => c.Id == contactId);

    public CustomerNote AddNote(string body, Guid authorId, DateTimeOffset createdAtUtc, Guid? id = null)
    {
        var note = new CustomerNote(id ?? Guid.NewGuid(), body, authorId, createdAtUtc);
        _notes.Add(note);
        return note;
    }

    public void PinNote(Guid noteId, DateTimeOffset pinnedAtUtc) => FindNote(noteId).Pin(pinnedAtUtc);

    public void UnpinNote(Guid noteId) => FindNote(noteId).Unpin();

    public void RemoveNote(Guid noteId) => _notes.Remove(FindNote(noteId));

    public bool HasNote(Guid noteId) => _notes.Exists(n => n.Id == noteId);

    private void PromoteToPrimary(Contact contact)
    {
        foreach (Contact other in _contacts)
        {
            other.ClearPrimary();
        }

        contact.MarkPrimary();
    }

    private void EnsureChannelsUnique(EmailAddress? email, PhoneNumber? phone, Guid? exceptContactId)
    {
        if (email is not null && _contacts.Exists(c => c.Id != exceptContactId && email.Equals(c.Email)))
        {
            throw new DomainException(CustomerDomainErrors.DuplicateContactEmail);
        }

        if (phone is not null && _contacts.Exists(c => c.Id != exceptContactId && phone.Equals(c.Phone)))
        {
            throw new DomainException(CustomerDomainErrors.DuplicateContactPhone);
        }
    }

    private Contact FindContact(Guid contactId) =>
        _contacts.Find(c => c.Id == contactId) ?? throw new DomainException(CustomerDomainErrors.ContactNotFound);

    private CustomerNote FindNote(Guid noteId) =>
        _notes.Find(n => n.Id == noteId) ?? throw new DomainException(CustomerDomainErrors.NoteNotFound);

    private void MarkChanged() => Revision++;

    private static CustomerType EnsureDefined(CustomerType type) =>
        Enum.IsDefined(type) ? type : throw new DomainException(CustomerDomainErrors.TypeUnsupported);

    private static string? NormaliseLegalName(CustomerType type, string? legalName)
    {
        string? trimmed = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();

        if (type == CustomerType.Organisation && trimmed is null)
        {
            throw new DomainException(CustomerDomainErrors.LegalNameRequired);
        }

        if (type == CustomerType.Individual && trimmed is not null)
        {
            throw new DomainException(CustomerDomainErrors.LegalNameNotAllowed);
        }

        if (trimmed is { Length: > LegalNameMaxLength })
        {
            throw new DomainException(CustomerDomainErrors.LegalNameTooLong);
        }

        return trimmed;
    }
}
```

`_contacts.Exists`/`Find` are `List<T>` methods (no LINQ allocation); `email.Equals(c.Email)` is
`ValueObject.Equals(ValueObject?)` (null-safe). No domain events are raised — nothing in the module handles one
yet (CLAUDE.md: do not add a pattern without a use case).

### B6 — Application (`Crm.Modules.Customers.Application`)

**Create files** (shapes of the Sample equivalents):

- `AssemblyReference.cs` — copy of Sample's.
- `ICustomersUnitOfWork.cs` — `public interface ICustomersUnitOfWork : IUnitOfWork;` with Sample's summary.
- `Abstractions/ICustomerNumberGenerator.cs` — `public interface ICustomerNumberGenerator { Task<CustomerNumber> NextAsync(CancellationToken cancellationToken = default); }`
  (summary: "Draws the next value of the customers.CustomerNumbers sequence. Not transactional: a rolled-back create
  leaves a gap.").
- `CustomersCacheKeys.cs` — `public static string Customer(Guid id) => $"customers:customers:{id}";`.
- `CustomersErrors.cs` — `internal static class`, shape of `IdentityErrors.cs`:
  `CustomerNotFound(Guid id)` → `Error.NotFound("Customers.CustomerNotFound", $"Customer '{id}' was not found.")`;
  `ContactNotFound(Guid id)` → `Error.NotFound("Customers.ContactNotFound", …)`; `NoteNotFound(Guid id)` →
  `Error.NotFound("Customers.NoteNotFound", …)`; `DepartmentOutOfScope()` →
  `Error.Forbidden("Organisation.OutOfScope", "The requested department is outside your scope.")`;
  `DepartmentRequired()` → `Error.Validation("Organisation.DepartmentRequired", "DepartmentId: Choose the department this record belongs to.")`
  (both texts verbatim from `CreateSampleCommandHandler.cs` lines 24–25).
- `ContactChannels.cs` — `internal static class ContactChannels` with
  `public static (EmailAddress? Email, PhoneNumber? Phone) Parse(string? email, string? phone)`: blank → `null`,
  otherwise `EmailAddress.Create(email)` / `PhoneNumber.Create(phone)` (throws 422 — the AC's "rejected at
  value-object construction").
- `Localization/CustomersMessageKeys.cs` — shape of `IdentityMessageKeys.cs`:

  ```csharp
  public static class CustomersMessageKeys
  {
      public static class Validation
      {
          public const string NameRequired = "Customers.Validation.NameRequired";
          public const string LanguageUnsupported = "Customers.Validation.LanguageUnsupported";
          public const string SortUnsupported = "Customers.Validation.SortUnsupported";
      }
  }
  ```

- `Localization/Resources/CustomersMessages.resx` and `CustomersMessages.ar.resx` (Write tool; header copied from
  `IdentityMessages.ar.resx`):

  | Key | en | ar |
  |---|---|---|
  | `Customers.Validation.NameRequired` | Enter the customer's name in Arabic or English. | أدخل اسم العميل بالعربية أو الإنجليزية. |
  | `Customers.Validation.LanguageUnsupported` | The preferred language must be en or ar. | يجب أن تكون اللغة المفضلة en أو ar. |
  | `Customers.Validation.SortUnsupported` | Sort must be one of createdAt, number, nameEn, nameAr. | يجب أن يكون الترتيب أحد القيم createdAt أو number أو nameEn أو nameAr. |

- `Validation/CustomersRules.cs` — shape of `IdentityRules.cs`: `SupportedLanguage<T>(this IRuleBuilder<T, string>,
  ILocalizationService)` (`Must(l => l is "en" or "ar")` + `LanguageUnsupported` message) and
  `SupportedSort<T>(this IRuleBuilder<T, string?>, ILocalizationService)` (`Must(s => s is null || CustomerSortParser.TryParse(s, out _))`
  + `SortUnsupported`).

**Create file: `Dtos.cs`** — primitives only (Organisation `Dtos.cs` line 5 comment), the one place that touches
domain types for mapping:

```csharp
public sealed record CustomerNameDto(string Ar, string En)
{
    public static CustomerNameDto FromDomain(LocalizedText name) => new(name.Arabic, name.English);
}

public sealed record ContactDto(Guid Id, string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess)
{
    public static ContactDto FromDomain(Contact contact) => new(
        contact.Id, contact.Name, contact.JobTitle, contact.Email?.Value, contact.Phone?.Value, contact.IsPrimary, contact.HasPortalAccess);
}

public sealed record CustomerNoteDto(Guid Id, string Body, bool IsPinned, Guid AuthorId, DateTimeOffset CreatedAtUtc, DateTimeOffset? PinnedAtUtc)
{
    public static CustomerNoteDto FromDomain(CustomerNote note) => new(
        note.Id, note.Body, note.IsPinned, note.AuthorId, note.CreatedAtUtc, note.PinnedAtUtc);
}

/// <summary>Contacts: primary first, then by name. Notes: pinned first, then newest first (AC).</summary>
public sealed record CustomerDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Type, string? LegalName, string Status,
    string PreferredLanguage, Guid TenantId, Guid BranchId, Guid DepartmentId,
    IReadOnlyList<ContactDto> Contacts, IReadOnlyList<CustomerNoteDto> Notes,
    byte[] RowVersion, string? CreatedBy, DateTimeOffset? CreatedAtUtc, string? ModifiedBy, DateTimeOffset? ModifiedAtUtc)
{
    public static CustomerDto FromDomain(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerDto(
            customer.Id.Value, customer.Number.Value, CustomerNameDto.FromDomain(customer.DisplayName),
            customer.Type.ToString(), customer.LegalName, customer.Status.ToString(), customer.PreferredLanguage.Value,
            customer.TenantId.Value, customer.BranchId.Value, customer.DepartmentId.Value,
            [.. customer.Contacts.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name, StringComparer.Ordinal).Select(ContactDto.FromDomain)],
            [.. customer.Notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.CreatedAtUtc).Select(CustomerNoteDto.FromDomain)],
            customer.RowVersion, customer.CreatedBy, customer.CreatedAtUtc, customer.ModifiedBy, customer.ModifiedAtUtc);
    }
}

public sealed record CustomerListItemDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Type, string Status, Guid BranchId, Guid DepartmentId,
    string? PrimaryContactName, string? PrimaryEmail, string? PrimaryPhone, DateTimeOffset? CreatedAtUtc);   // + FromDomain via the IsPrimary contact

public sealed record CustomerLookupDto(Guid Id, string Number, CustomerNameDto DisplayName, string? PrimaryEmail, string? PrimaryPhone);   // + FromDomain

public sealed record DuplicateCandidateDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Status,
    IReadOnlyList<string> MatchedEmails, IReadOnlyList<string> MatchedPhones);

/// <summary>Created = false: nothing was written; the candidates are in-scope customers with a matching contact
/// email or phone. Re-send with ConfirmDuplicate = true to create anyway.</summary>
public sealed record CreateCustomerOutcome(CustomerDto? Customer, IReadOnlyList<DuplicateCandidateDto> DuplicateCandidates)
{
    public bool Created => Customer is not null;

    public static CreateCustomerOutcome Success(CustomerDto customer) => new(customer, []);

    public static CreateCustomerOutcome Duplicates(IReadOnlyList<DuplicateCandidateDto> candidates) => new(null, candidates);
}
```

**Create folder `Specifications/`:**

- `CustomerByIdSpecification.cs` — `Where(c => c.Id == id); AsReadOnly();` (Sample precedent).
- `CustomerSort.cs` — `public enum CustomerSort { CreatedAt, Number, NameEn, NameAr }` and
  `public static class CustomerSortParser { public static bool TryParse(string value, out CustomerSort sort) }`
  mapping `createdAt`/`number`/`nameEn`/`nameAr` (ordinal ignore-case).
- `CustomerSearchSpecification.cs` (`using System.Linq.Expressions;`):

  ```csharp
  /// <summary>The paged search. No scope criteria on purpose - Story 06's filter adds tenant/branch/department.
  /// Paging applies only when both page arguments are given (the count passes neither).</summary>
  public sealed class CustomerSearchSpecification : Specification<Customer>
  {
      public CustomerSearchSpecification(
          string? search, CustomerStatus? status, CustomerType? type, Guid? departmentId, Guid? branchId,
          CustomerSort sort, bool descending, int? page, int? pageSize)
      {
          string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
          string? phoneTerm = term is null ? null : PhoneNumber.StripSeparators(term);
          if (phoneTerm is { Length: 0 })
          {
              phoneTerm = null;
          }

          bool anyStatus = status is null;
          CustomerStatus statusValue = status ?? CustomerStatus.Active;
          bool anyType = type is null;
          CustomerType typeValue = type ?? CustomerType.Individual;
          DepartmentId? department = departmentId is { } d ? new DepartmentId(d) : null;
          BranchId? branch = branchId is { } b ? new BranchId(b) : null;

          // One combined Where - Specification.Where replaces, it does not AND (DepartmentsSpecification line 12).
          Where(c =>
              (term == null
                  || c.DisplayName.Arabic.Contains(term)
                  || c.DisplayName.English.Contains(term)
                  || c.Number.Value.Contains(term)
                  || (c.LegalName != null && c.LegalName.Contains(term))
                  || c.Contacts.Any(ct =>
                      (ct.Email != null && ct.Email.Value.Contains(term))
                      || (phoneTerm != null && ct.Phone != null && ct.Phone.Value.Contains(phoneTerm))))
              && (anyStatus || c.Status == statusValue)
              && (anyType || c.Type == typeValue)
              && (department == null || c.DepartmentId.Equals(department))
              && (branch == null || c.BranchId.Equals(branch)));

          Expression<Func<Customer, object>> key = sort switch
          {
              CustomerSort.Number => c => c.Number.Value,
              CustomerSort.NameEn => c => c.DisplayName.English,
              CustomerSort.NameAr => c => c.DisplayName.Arabic,
              _ => c => c.CreatedAtUtc ?? DateTimeOffset.MinValue,
          };

          if (descending)
          {
              ApplyOrderByDescending(key);
          }
          else
          {
              ApplyOrderBy(key);
          }

          ApplyThenBy(c => c.Number.Value);

          if (page is { } p && pageSize is { } size)
          {
              ApplyPaging((p - 1) * size, size);
          }

          AsReadOnly();
      }
  }
  ```

- `CustomerLookupSpecification.cs` — `(string? search, int take)`: active only, the same `term` predicate on names,
  number, email, phone; `ApplyOrderBy(c => c.DisplayName.English)`, `ApplyThenBy(c => c.Number.Value)`,
  `ApplyPaging(0, take)`, `AsReadOnly()`.
- `CustomersWithContactChannelsSpecification.cs` — `(IReadOnlyList<string> emails, IReadOnlyList<string> phones)`:
  `Where(c => c.Contacts.Any(ct => (ct.Email != null && emails.Contains(ct.Email.Value)) || (ct.Phone != null && phones.Contains(ct.Phone.Value))))`,
  `ApplyOrderBy(c => c.Number.Value)`, `ApplyPaging(0, 10)`, `AsReadOnly()`.
- `CustomersWithContactEmailSpecification.cs` — `(string email)`: `Where(c => c.Contacts.Any(ct => ct.Email != null && ct.Email.Value == email))`,
  `ApplyOrderBy(c => c.Number.Value)`, `ApplyPaging(0, 20)`, `AsReadOnly()`.

**Commands** — one folder each under `Commands/`, each with `…Command.cs`, `…CommandHandler.cs`, `…CommandValidator.cs`.
Every command is `[RequirePermission(CrmPermissions.CustomersWrite)]`; every command except `CreateCustomer` also
implements `ICacheInvalidatingCommand` with `CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)]`
(`Id` for the three profile/status commands). Handlers inject `IRepository<Customer, CustomerId>`,
`ICustomersUnitOfWork` and what the table says; every handler that loads a customer starts with

```csharp
Customer? customer = await repository.GetByIdAsync(new CustomerId(request.CustomerId), cancellationToken);
if (customer is null)
{
    return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.CustomerId));
}
```

(`GetByIdAsync` throws for an existing out-of-scope row → `OrganisationScopeBehavior` → 403), then — for the rows
marked "RV" — `repository.SetOriginalRowVersion(customer, request.RowVersion);`, then the domain call, then
`await unitOfWork.SaveChangesAsync(cancellationToken);`, then (where listed) `eventBus.Publish(...)`, then
`return Result.Success(CustomerDto.FromDomain(customer));`. For contact/note commands, check existence first and
return 404: `if (!customer.HasContact(request.ContactId)) return Result.Failure<CustomerDto>(CustomersErrors.ContactNotFound(request.ContactId));`
(same with `HasNote` / `NoteNotFound`).

| Folder / record | Fields | RV | Domain call | Extra |
|---|---|---|---|---|
| `CreateCustomer/CreateCustomerCommand` : `ICommand<CreateCustomerOutcome>` | `string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage, IReadOnlyList<NewContact> Contacts, bool ConfirmDuplicate, Guid? DepartmentId` | — | see below | publishes `CustomerCreatedIntegrationEvent` |
| `UpdateCustomer/UpdateCustomerCommand` : `ICommand<CustomerDto>` | `Guid Id, string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage, byte[] RowVersion` | ✓ | `UpdateProfile(LocalizedText.Create(ar, en), Enum.Parse<CustomerType>(Type, true), LegalName, PreferredLanguage.Create(…))` | publishes `CustomerUpdatedIntegrationEvent` |
| `DeactivateCustomer/DeactivateCustomerCommand` | `Guid Id, byte[] RowVersion` | ✓ | `Deactivate()` | publishes `CustomerUpdatedIntegrationEvent` |
| `ReactivateCustomer/ReactivateCustomerCommand` | `Guid Id, byte[] RowVersion` | ✓ | `Reactivate()` | publishes `CustomerUpdatedIntegrationEvent` |
| `AddContact/AddContactCommand` | `Guid CustomerId, string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess, byte[] RowVersion` | ✓ | `ContactChannels.Parse` then `AddContact(…)` | — |
| `UpdateContact/UpdateContactCommand` | `Guid CustomerId, Guid ContactId, string Name, string? JobTitle, string? Email, string? Phone, bool HasPortalAccess, byte[] RowVersion` | ✓ | 404 check, `ContactChannels.Parse`, `UpdateContact(…)` | — |
| `SetPrimaryContact/SetPrimaryContactCommand` | `Guid CustomerId, Guid ContactId, byte[] RowVersion` | ✓ | 404 check, `SetPrimaryContact(id)` | — |
| `RemoveContact/RemoveContactCommand` | `Guid CustomerId, Guid ContactId, byte[] RowVersion` | ✓ | 404 check, `RemoveContact(id)` | — |
| `AddNote/AddNoteCommand` | `Guid CustomerId, string Body` | — | `AddNote(Body, authorId, dateTime.UtcNow)` | inject `ICurrentUserService`, `IDateTime`; `Guid authorId = Guid.TryParse(currentUser.UserId, out Guid parsed) ? parsed : Guid.Empty;` (Empty → 422 `NoteAuthorRequired`) |
| `PinNote/PinNoteCommand` | `Guid CustomerId, Guid NoteId, bool IsPinned` | — | 404 check, `IsPinned ? PinNote(id, dateTime.UtcNow) : UnpinNote(id)` | inject `IDateTime` |
| `RemoveNote/RemoveNoteCommand` | `Guid CustomerId, Guid NoteId` | — | 404 check, `RemoveNote(id)` | — |

`NewContact` is `public sealed record NewContact(string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess);`
in `CreateCustomer/CreateCustomerCommand.cs`. The two events: `new CustomerUpdatedIntegrationEvent(Guid.NewGuid(),
dateTime.UtcNow, customer.Id.Value, customer.DisplayName.Arabic, customer.DisplayName.English, customer.Type.ToString(),
customer.Status.ToString())` — published **after** `SaveChangesAsync` (RenameSample precedent, lines 39–41).

**`CreateCustomerCommandHandler`** (inject `IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork,
ICurrentUserService currentUser, ICustomerNumberGenerator numbers, IDateTime dateTime, IEventBus eventBus`):

```csharp
    public async Task<Result<CreateCustomerOutcome>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        ScopeWriteTarget? target = currentUser.Scope.ResolveWriteTarget(request.DepartmentId);
        if (target is null)
        {
            return Result.Failure<CreateCustomerOutcome>(request.DepartmentId is not null
                ? CustomersErrors.DepartmentOutOfScope()
                : CustomersErrors.DepartmentRequired());
        }

        // Every value object first: a malformed email or phone is 422 before any duplicate lookup (AC).
        LocalizedText displayName = LocalizedText.Create(request.DisplayNameAr, request.DisplayNameEn);
        CustomerType type = Enum.Parse<CustomerType>(request.Type, ignoreCase: true);
        PreferredLanguage language = PreferredLanguage.Create(request.PreferredLanguage);
        List<(NewContact Input, EmailAddress? Email, PhoneNumber? Phone)> contacts =
            [.. request.Contacts.Select(c => { (EmailAddress? e, PhoneNumber? p) = ContactChannels.Parse(c.Email, c.Phone); return (c, e, p); })];

        if (!request.ConfirmDuplicate)
        {
            List<string> emails = [.. contacts.Select(c => c.Email).OfType<EmailAddress>().Select(e => e.Value).Distinct(StringComparer.Ordinal)];
            List<string> phones = [.. contacts.Select(c => c.Phone).OfType<PhoneNumber>().Select(p => p.Value).Distinct(StringComparer.Ordinal)];

            if (emails.Count > 0 || phones.Count > 0)
            {
                IReadOnlyList<Customer> matches = await repository.ListAsync(
                    new CustomersWithContactChannelsSpecification(emails, phones), cancellationToken);

                if (matches.Count > 0)
                {
                    return Result.Success(CreateCustomerOutcome.Duplicates(
                        [.. matches.Select(m => DuplicateCandidates.From(m, emails, phones))]));
                }
            }
        }

        CustomerNumber number = await numbers.NextAsync(cancellationToken);
        Customer customer = Customer.Create(
            number, displayName, type, request.LegalName, language, target.TenantId, target.BranchId, target.DepartmentId);

        foreach ((NewContact input, EmailAddress? email, PhoneNumber? phone) in contacts)
        {
            customer.AddContact(input.Name, input.JobTitle, email, phone, input.IsPrimary, input.HasPortalAccess);
        }

        repository.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new CustomerCreatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, customer.Id.Value, customer.Number.Value, customer.DisplayName.Arabic,
            customer.DisplayName.English, customer.Type.ToString(), customer.TenantId.Value, customer.BranchId.Value,
            customer.DepartmentId.Value));

        return Result.Success(CreateCustomerOutcome.Success(CustomerDto.FromDomain(customer)));
    }
```

No `!` in new code — the `OfType<…>()` projections replace a null-forgiving `.Value`.
`DuplicateCandidates.From(Customer, IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones)` is a
small `internal static` helper in the same folder that returns a `DuplicateCandidateDto` with the matched values
(`m.Contacts.Select(c => c.Email?.Value).OfType<string>().Where(emails.Contains)`, same for phones). A duplicate
within the request itself (two submitted contacts, same email) is caught by `AddContact` → 422
`Customers.DuplicateContactEmail`.

**Validators** (built-in FluentValidation messages follow `Accept-Language`; custom ones use `CustomersMessageKeys`):

- `CreateCustomerCommandValidator(ILocalizationService localization)`: `RuleFor(c => c.DisplayNameEn).NotEmpty().WithMessage(_ => localization.Get(CustomersMessageKeys.Validation.NameRequired)).When(c => string.IsNullOrWhiteSpace(c.DisplayNameAr));`
  `RuleFor(c => c.DisplayNameAr).MaximumLength(200); RuleFor(c => c.DisplayNameEn).MaximumLength(200);`
  `RuleFor(c => c.Type).NotEmpty().IsEnumName(typeof(CustomerType), caseSensitive: false);`
  `RuleFor(c => c.LegalName).MaximumLength(300);` (**no** "required for organisations" rule — AC 1),
  `RuleFor(c => c.PreferredLanguage).SupportedLanguage(localization);`
  `RuleFor(c => c.Contacts).NotNull().Must(c => c.Count <= 20);`
  `RuleForEach(c => c.Contacts).ChildRules(contact => { contact.RuleFor(x => x.Name).NotEmpty().MaximumLength(200); contact.RuleFor(x => x.JobTitle).MaximumLength(100); contact.RuleFor(x => x.Email).MaximumLength(254); contact.RuleFor(x => x.Phone).MaximumLength(32); });`
  (**no** email/phone format rule — AC 3).
- `UpdateCustomerCommandValidator` — `Id` NotEmpty, the same name/type/legal-name/language rules, `RowVersion` NotEmpty.
- `DeactivateCustomerCommandValidator`, `ReactivateCustomerCommandValidator`, `SetPrimaryContactCommandValidator`,
  `RemoveContactCommandValidator` — ids and `RowVersion` NotEmpty.
- `AddContactCommandValidator`, `UpdateContactCommandValidator` — ids, the contact rules above, `RowVersion` NotEmpty.
- `AddNoteCommandValidator` — `CustomerId` NotEmpty, `Body` NotEmpty, MaximumLength(4000).
- `PinNoteCommandValidator`, `RemoveNoteCommandValidator` — ids NotEmpty.

**Queries** — `[RequirePermission(CrmPermissions.CustomersRead)]`, each `IRequest<Result<T>>`:

| Folder / record | Returns | Handler |
|---|---|---|
| `GetCustomerById/GetCustomerByIdQuery(Guid Id)`, `ICacheableQuery` (`CacheKey`/`CacheTags` = `CustomersCacheKeys.Customer(Id)`) | `CustomerDto` | `repository.FirstOrDefaultAsync(new CustomerByIdSpecification(new CustomerId(Id)))` → null → `CustomerNotFound`; else `CustomerDto.FromDomain` (GetSampleById shape) |
| `SearchCustomers/SearchCustomersQuery(string? Search, string? Status, string? Type, Guid? DepartmentId, Guid? BranchId, string? Sort, bool Descending = true, int Page = 1, int PageSize = 20)` | `PagedResult<CustomerListItemDto>` | parse `Status`/`Type` with `Enum.Parse<…>(…, true)` when not null, `Sort` with `CustomerSortParser` (null → `CreatedAt`); `ListAsync(spec with page)` + `CountAsync(spec without page)` (ListUsers shape) |
| `LookupCustomers/LookupCustomersQuery(string? Search, int Take = 10)` | `IReadOnlyList<CustomerLookupDto>` | `ListAsync(new CustomerLookupSpecification(Search, Take))` |
| `FindContactsByEmail/FindContactsByEmailQuery(string Email)` | `IReadOnlyList<CustomerContactMatch>` (Contracts type — the `GetMembershipsOfUserQuery` precedent) | `EmailAddress.TryCreate` false → `Result.Success<IReadOnlyList<CustomerContactMatch>>([])`; else `ListAsync(new CustomersWithContactEmailSpecification(email.Value))` and one match per contact whose `Email?.Value == email.Value` |

Validators: `SearchCustomersQueryValidator(ILocalizationService)` — `Page >= 1`, `PageSize` 1–100, `Search` ≤ 200,
`Status` `IsEnumName(typeof(CustomerStatus), false)` when not null, `Type` likewise, `Sort` `SupportedSort`;
`LookupCustomersQueryValidator` — `Take` 1–25, `Search` ≤ 200; `GetCustomerByIdQueryValidator` — `Id` NotEmpty;
`FindContactsByEmailQueryValidator` — `Email` NotEmpty, ≤ 254.

**Create file: `CustomersModuleApi.cs`** (shape of `SampleModuleApi.cs`):

```csharp
public sealed class CustomersModuleApi(ISender sender) : ICustomersModuleApi
{
    public async Task<Result<CustomerSummary>> GetCustomerSummaryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Result<CustomerDto> result = await sender.Send(new GetCustomerByIdQuery(customerId), cancellationToken);

        return result.IsSuccess
            ? Result.Success(ToSummary(result.Value))
            : Result.Failure<CustomerSummary>(result.Error);
    }

    public Task<Result<IReadOnlyList<CustomerContactMatch>>> FindContactByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        sender.Send(new FindContactsByEmailQuery(email), cancellationToken);

    private static CustomerSummary ToSummary(CustomerDto c) => new(
        c.Id, c.Number, c.DisplayName.Ar, c.DisplayName.En, c.Type, c.Status == nameof(CustomerStatus.Active),
        c.PreferredLanguage, c.TenantId, c.BranchId, c.DepartmentId,
        [.. c.Contacts.Select(x => new CustomerContactSummary(x.Id, x.Name, x.Email, x.Phone, x.IsPrimary, x.HasPortalAccess))]);
}
```

### B7 — Infrastructure (`Crm.Modules.Customers.Infrastructure`)

**Create file: `Persistence/CustomersDbContext.cs`**

```csharp
public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), ICustomersUnitOfWork
{
    public const string SchemaName = "customers";

    /// <summary>customers.CustomerNumbers - SqlCustomerNumberGenerator's SQL names it literally.</summary>
    public const string CustomerNumberSequence = "CustomerNumbers";

    protected override string Schema => SchemaName;

    // Aggregate roots only. Contact and CustomerNote have no DbSet - reachable only through Customer (AC).
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The one model element that is not an entity type, so no IEntityTypeConfiguration can declare it.
        modelBuilder.HasSequence<long>(CustomerNumberSequence, SchemaName).StartsAt(1).IncrementsBy(1);
    }
}
```

**Create file: `Persistence/CustomersDbContextFactory.cs`** — copy `SampleDbContextFactory.cs` (comment:
`ef.ps1 -Module Customers`).

**Create file: `Persistence/Configurations/CustomerConfiguration.cs`**

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        // CustomerId is not a scope type, so it needs its own conversion; TenantId/BranchId/DepartmentId are
        // converted by the ModuleDbContext convention - never configure them here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new CustomerId(value))
            .ValueGeneratedNever();

        // OwnsOne (not HasConversion) so c.Number.Value.Contains(term) translates. Unique globally - the sequence is global.
        builder.OwnsOne(x => x.Number, number =>
        {
            number.Property(p => p.Value).HasColumnName("Number").HasMaxLength(CustomerNumber.MaxLength).IsRequired();
            number.HasIndex(p => p.Value).IsUnique();
        });

        builder.OwnsOne(x => x.DisplayName, name =>
        {
            name.Property(p => p.Arabic).HasColumnName("DisplayNameAr").HasMaxLength(LocalizedText.MaxLength).IsRequired();
            name.Property(p => p.English).HasColumnName("DisplayNameEn").HasMaxLength(LocalizedText.MaxLength).IsRequired();
        });

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LegalName).HasMaxLength(Customer.LegalNameMaxLength);

        builder.Property(x => x.PreferredLanguage)
            .HasConversion(l => l.Value, v => PreferredLanguage.Create(v))
            .HasColumnName("PreferredLanguage")
            .HasMaxLength(2)
            .IsRequired();

        builder.Ignore(x => x.IsActive);

        builder.OwnsMany(x => x.Contacts, contacts =>
        {
            contacts.ToTable("CustomerContacts");
            contacts.WithOwner().HasForeignKey("CustomerId");
            contacts.HasKey(c => c.Id);
            // Client-generated key: without this EF treats a contact added to a tracked customer as an existing row
            // (UPDATE, 0 rows) - RefreshTokenFamilyConfiguration line 28.
            contacts.Property(c => c.Id).ValueGeneratedNever();
            contacts.Property(c => c.Name).HasMaxLength(Contact.NameMaxLength).IsRequired();
            contacts.Property(c => c.JobTitle).HasMaxLength(Contact.JobTitleMaxLength);

            // Optional owned value objects in the same table (R1): all-null columns = no email / no phone.
            contacts.OwnsOne(c => c.Email, email =>
            {
                email.Property(p => p.Value).HasColumnName("Email").HasMaxLength(EmailAddress.MaxLength);
                email.HasIndex(p => p.Value);
            });
            contacts.OwnsOne(c => c.Phone, phone =>
            {
                phone.Property(p => p.Value).HasColumnName("Phone").HasMaxLength(PhoneNumber.MaxLength);
                phone.HasIndex(p => p.Value);
            });
        });
        builder.Navigation(x => x.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Notes, notes =>
        {
            notes.ToTable("CustomerNotes");
            notes.WithOwner().HasForeignKey("CustomerId");
            notes.HasKey(n => n.Id);
            notes.Property(n => n.Id).ValueGeneratedNever();
            notes.Property(n => n.Body).HasMaxLength(CustomerNote.BodyMaxLength).IsRequired();
        });
        builder.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // WHERE IsDeleted = 0 and the scope filter's (TenantId, BranchId, DepartmentId) predicate on every read.
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
    }
}
```

Uniqueness of a contact email/phone **within one customer** is enforced by the aggregate only: a composite unique
index cannot span the owner FK and a nested owned column (`BranchConfiguration.cs` lines 12–14's reasoning).

**Create file: `Persistence/SqlCustomerNumberGenerator.cs`** — `public sealed class` (not `internal`: an internal
class only the container instantiates trips CA1812):

```csharp
/// <summary>Un-composed on purpose: SQL Server rejects NEXT VALUE FOR inside a subquery, and First()/Single()
/// after SqlQuery wraps the SQL in one. The literal SQL must stay a literal - an interpolation hole here becomes a
/// SQL parameter, not a schema name. Runs on the command's open transaction (same DbContext).</summary>
public sealed class SqlCustomerNumberGenerator(CustomersDbContext context) : ICustomerNumberGenerator
{
    public async Task<CustomerNumber> NextAsync(CancellationToken cancellationToken = default)
    {
        List<long> values = await context.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR [customers].[CustomerNumbers] AS [Value]")
            .ToListAsync(cancellationToken);

        return CustomerNumber.FromSequence(values[0]);
    }
}
```

Prove R2 on first run.

**Create file: `Persistence/CustomersSeeder.cs`** — `public sealed class CustomersSeeder(CustomersDbContext context,
ICustomerNumberGenerator numbers, IDateTime dateTime) : IModuleSeeder` (Write tool — Arabic). For each of the three
`CustomersDevSeed` customers, skip when
`await context.Customers.IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName, ModuleDbContext.ScopeFilterName]).AnyAsync(c => c.Id == id, ct)`
(SampleSeeder lines 18–25), otherwise create it with `await numbers.NextAsync(ct)`, add contacts/notes with the
well-known ids, `context.Customers.Add(...)`, and `SaveChangesAsync` once per customer:

| Customer | Type / legal name | Language | Department | Contacts (primary first) | Notes |
|---|---|---|---|---|---|
| `أحمد القحطاني` / `Ahmed Al-Qahtani` | Individual / — | `ar` | `RiyadhCareDepartmentId` (branch `RiyadhBranchId`) | Ahmed Al-Qahtani, `ahmed@example.com`, `+966501234567`, portal access **on** (`AhmedContactId`) | — |
| `نجد للخدمات اللوجستية` / `Najd Logistics` | Organisation / `Najd Logistics Company Ltd.` | `en` | `RiyadhCareDepartmentId` | Sara Al-Otaibi, Procurement Manager, `sara@najd.example.com` (`NajdSaraContactId`, primary); Omar Haddad, Accounts, `+966551112233` (`NajdOmarContactId`) | `NajdPinnedNoteId`: "Key account - escalate billing questions to the supervisor.", author `OrganisationDevSeed.AdminUserId`, pinned |
| `تجارة البحر الأحمر` / `Red Sea Trading` | Organisation / `Red Sea Trading Est.` | `ar` | `JeddahCareDepartmentId` (branch `JeddahBranchId`) | Fahad Al-Ghamdi, `fahad@redsea.example.com`, `+966122223344` (`RedSeaFahadContactId`) | — |

Seeders run with no `HttpContext` (`OrganisationScope.Unrestricted`) — the write guard passes (SampleSeeder
comment lines 27–28).

**Create file: `CustomersModule.cs`** (shape of `SampleModule.cs` + `IdentityModule.cs` lines 35–37):

```csharp
public sealed class CustomersModule : IModule
{
    public string Name => "Customers";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CustomersDbContext, ICustomersUnitOfWork>(
            configuration, Name, CustomersDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<Customer, CustomerId, CustomersDbContext>();
        services.AddScoped<ICustomerNumberGenerator, SqlCustomerNumberGenerator>();
        services.AddScoped<IModuleSeeder, CustomersSeeder>();

        // Validation messages that are server text follow Accept-Language.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.Modules.Customers.Application.Localization.Resources.CustomersMessages", ApplicationAssembly));

        // The module's public face: what other modules may reference through Crm.Modules.Customers.Contracts.
        services.AddScoped<ICustomersModuleApi, CustomersModuleApi>();
    }
}
```

### B8 — Migration

No database is needed to generate it (the design-time factory builds the model). Run `dotnet tool restore`, then `pwsh backend/scripts/ef.ps1 -Module Customers migrations add Initial`. **Read the
generated `Persistence/Migrations/*_Initial.cs` before building on it** (Test Plan row 3): `EnsureSchema("customers")`;
`CreateSequence<long>("CustomerNumbers", schema: "customers")`; tables `Customers` (`Number` nvarchar(20) with a
**unique** index, `DisplayNameAr`/`DisplayNameEn` nvarchar(200) not null, `Type`/`Status` nvarchar(20),
`LegalName` nvarchar(300) null, `PreferredLanguage` nvarchar(2), `TenantId`/`BranchId`/`DepartmentId`,
`Revision` int, `RowVersion` rowversion, soft-delete and audit columns), `CustomerContacts` (`CustomerId` FK cascade,
`Name`, `JobTitle` null, **`Email` nvarchar(254) null**, **`Phone` nvarchar(16) null**, `IsPrimary`, `HasPortalAccess`,
non-unique indexes on `Email` and `Phone`), `CustomerNotes` (`CustomerId` FK cascade, `Body` nvarchar(4000), `IsPinned`,
`AuthorId`, `CreatedAtUtc`, `PinnedAtUtc` null), `outbox_messages`, `outbox_message_consumers`; indexes
`IX_Customers_IsDeleted`, `IX_Customers_TenantId_DepartmentId`. **No other schema appears.** Never hand-edit it — fix the
configuration, `migrations remove`, re-add (Story 02 rule).

### B9 — Presentation (`Crm.Modules.Customers.Presentation`)

**Create file: `AssemblyReference.cs`** (copy of Sample's). **Create file: `Controllers/Agent/CustomersController.cs`**:

```csharp
/// <summary>The agent's customer surface (Story 11). customers.read for reads, customers.write for writes (declared
/// on each request); every row is organisation-scoped. RowVersion is base64 over the wire - send back exactly what
/// the last response returned.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CustomersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<CustomerListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> Search(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? type, [FromQuery] Guid? departmentId,
        [FromQuery] Guid? branchId, [FromQuery] string? sort, [FromQuery] bool descending = true, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new SearchCustomersQuery(search, status, type, departmentId, branchId, sort, descending, page, pageSize), cancellationToken));

    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<CustomerLookupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CustomerLookupDto>>> Lookup(
        [FromQuery] string? search, [FromQuery] int take = 10, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new LookupCustomersQuery(search, take), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetCustomerByIdQuery(id), cancellationToken));

    /// <summary>200 with created = false and duplicateCandidates when an in-scope customer already has one of the
    /// contact emails/phones; re-send with confirmDuplicate = true to create anyway.</summary>
    [HttpPost]
    [ProducesResponseType<CreateCustomerOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateCustomerOutcome>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateCustomerCommand(
            request.DisplayNameAr, request.DisplayNameEn, request.Type, request.LegalName, request.PreferredLanguage,
            [.. (request.Contacts ?? []).Select(c => new NewContact(c.Name, c.JobTitle, c.Email, c.Phone, c.IsPrimary, c.HasPortalAccess))],
            request.ConfirmDuplicate, request.DepartmentId), cancellationToken));

    // PUT {id}                                  UpdateCustomerRequest        -> UpdateCustomerCommand        (400/404/409/422)
    // POST {id}/deactivate                      CustomerRowVersionRequest    -> DeactivateCustomerCommand    (404/409/422)
    // POST {id}/reactivate                      CustomerRowVersionRequest    -> ReactivateCustomerCommand    (404/409/422)
    // POST {id}/contacts                        AddContactRequest            -> AddContactCommand            (400/404/409/422)
    // PUT  {id}/contacts/{contactId:guid}       UpdateContactRequest         -> UpdateContactCommand         (400/404/409/422)
    // POST {id}/contacts/{contactId:guid}/primary CustomerRowVersionRequest  -> SetPrimaryContactCommand     (404/409)
    // POST {id}/contacts/{contactId:guid}/remove  CustomerRowVersionRequest  -> RemoveContactCommand         (404/409/422)
    // POST {id}/notes                           AddNoteRequest               -> AddNoteCommand               (400/404/422)
    // PUT  {id}/notes/{noteId:guid}/pin         PinNoteRequest               -> PinNoteCommand               (404)
    // DELETE {id}/notes/{noteId:guid}           (no body)                    -> RemoveNoteCommand            (404)
    // Every one returns ActionResult<CustomerDto> via ToActionResult, with [ProducesResponseType<CustomerDto>(200)]
    // plus the listed statuses. Route ids use {id:guid} like SamplesController line 42.
}

public sealed record CreateCustomerRequest(
    string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage,
    IReadOnlyList<CreateCustomerContactRequest>? Contacts = null, bool ConfirmDuplicate = false, Guid? DepartmentId = null);

public sealed record CreateCustomerContactRequest(
    string Name, string? JobTitle = null, string? Email = null, string? Phone = null, bool IsPrimary = false, bool HasPortalAccess = false);

public sealed record UpdateCustomerRequest(
    string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage, byte[] RowVersion);

public sealed record CustomerRowVersionRequest(byte[] RowVersion);

public sealed record AddContactRequest(
    string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess, byte[] RowVersion);

public sealed record UpdateContactRequest(
    string Name, string? JobTitle, string? Email, string? Phone, bool HasPortalAccess, byte[] RowVersion);

public sealed record AddNoteRequest(string Body);

public sealed record PinNoteRequest(bool IsPinned);
```

Write every commented action out in full (same shape as `Get`/`Create`). Removal of a contact is a `POST …/remove`
with the rowversion body (the `/deactivate` precedent) — no DELETE with a body. Prove R5.

### B10 — Host

- **File: `Crm.Api/Crm.Api.csproj`** — replace lines 14–15 (Sample) with the Customers Infrastructure and Presentation
  `ProjectReference`s.
- **File: `Crm.Api/Modules/ModuleCatalog.cs`** — replace lines 20–21 with
  `new(new Crm.Modules.Customers.Infrastructure.CustomersModule(), Crm.Modules.Customers.Presentation.AssemblyReference.Assembly),`
  (after Identity; no comment line about Sample remains).

### B11 — Delete the `Sample` module

1. `dotnet sln backend/Crm.sln remove` the five `backend/src/Modules/Sample/**/*.csproj`, then delete the leftover
   `Project(...) = "Sample", "Sample", "{6A173257-F0CB-840E-F874-18C1C6133E74}"` block (lines 22–23) and every
   `NestedProjects` line naming `{6A173257-F0CB-840E-F874-18C1C6133E74}` by hand. `dotnet sln backend/Crm.sln list`
   shows no Sample project.
2. `git rm -r backend/src/Modules/Sample`, then `rm -rf backend/src/Modules/Sample` (the untracked `bin/`/`obj/`).
3. **Comments in `backend/src` that name Sample** — rewrite each to name the Customers equivalent:
   `BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/IUnitOfWork.cs` line 13 (`ICustomersUnitOfWork`),
   `Caching/ICacheableQuery.cs` line 5 (`"customers:customers:{id}"`), `Modules/ModuleUnitOfWork.cs` line 7
   (`ICustomersUnitOfWork`), `Crm.BuildingBlocks.Infrastructure/Modules/IModule.cs` line 9 (`CustomersModule`),
   `Modules/ModuleDesignTimeDbContextFactory.cs` line 9 (`CustomersDbContextFactory`),
   `Persistence/Interceptors/AuditingSaveChangesInterceptor.cs` lines 72 and 75 (`CustomerName`-style owned value →
   write "an owned same-table value object (e.g. Customer.DisplayName via OwnsOne)" and "an UpdateProfile() that only
   replaced the owned name"), `Crm.BuildingBlocks.Presentation/AREAS.md` line 25 (B13),
   `Modules/Organisation/Crm.Modules.Organisation.Contracts/DevSeed/OrganisationDevSeed.cs` lines 3–5 ("shared with
   Story 07's user seed and Story 11's customer seed"), `Modules/Organisation/…/Configurations/MembershipConfiguration.cs`
   line 16 (`(like UserId)`).
4. Prove R6 (Test Plan row 13).

### B12 — Request samples

All `.http` files use the Story 07 login block (`auth.http` lines 1–20 shape) and `Authorization: Bearer {{…Token}}`.
Use the **Write tool** (Arabic, `$`). Percent-encode Arabic in query strings (`أحمد` = `%D8%A3%D8%AD%D9%85%D8%AF`) and
put the Arabic in the comment.

**Create file: `backend/requests/customers.http`** — logins for agent A (`agent.a@crm.local`), agent B
(`agent.b@crm.local`), supervisor (`supervisor@crm.local`), password `Crm_Dev_2026!`; `@ryCare`, `@ryBill`, `@jeCare`
and the `CustomersDevSeed` ids as variables. One request per line below, each with its expected status/code in the
`###` title; capture ids/rowversions with `# @name` and `{{name.response.body.…}}` and always send the **latest**
`rowVersion`:

| # | Request (as agent A unless stated) | Expected |
|---|---|---|
| 1 | `GET /api/Agent/Customers` | 200; Ahmed and Najd, **not** Red Sea; EF log WHERE has `[c].[IsDeleted] = CAST(0 AS bit)`, `[c].[TenantId] = @…`, `[c].[BranchId] = @…`, `[c].[DepartmentId] = @…` (AC scope, SQL) |
| 2 | `?search=%D8%A3%D8%AD%D9%85%D8%AF` (أحمد) | 200; Ahmed (R8, AC Arabic) |
| 3a–3d | `?search=Najd` · `?search=C-00000` · `?search=najd.example` · `?search=50 123 4567` (encode spaces) | 200; English name · number · email · phone matches (AC); 3c proves R1's `EXISTS` |
| 4 | `?status=Active&sort=nameEn&descending=false&page=1&pageSize=2` then `page=2` | 2 distinct items, `total` = unpaged count; `ORDER BY … [DisplayNameEn], … [Number]` + `OFFSET … FETCH` (R4, R9, AC paged) |
| 5 | `GET /api/Agent/Customers/lookup?search=na&take=5` | 200; active customers only |
| 6 | `POST /api/Agent/Customers` individual "Layla Hassan", `preferredLanguage` `en`, contacts: `{name:"Layla Hassan", email:"Layla.Hassan@Example.com"}`, `{name:"Layla mobile", phone:"+966 55 000 1111"}` | 200 `created: true`; `number` `C-000004` on a fresh DB (R2); contact 1 primary automatically; email stored lower-case; phone `+966550001111`; contact 2 `email: null` (R1) |
| 7 | Create with a contact `email: "ahmed@example.com"` (seeded), no confirm | 200 `created: false`, `customer: null`, `duplicateCandidates[0].number` = Ahmed's, `matchedEmails: ["ahmed@example.com"]` (AC) |
| 8 | Same body + `confirmDuplicate: true` | 200 `created: true` (AC "confirmed through") |
| 9 | Request 7's body **as agent B** | 200 `created: true` in JED-CARE — Ahmed is out of B's scope, so no warning ("in scope") |
| 10 | Organisation `"type": "Organisation"` without `legalName` | **422** `urn:crm:problem:domain-rule`, `code` `Customers.LegalNameRequired` (AC 1) |
| 11 | Individual with `legalName` | 422 `Customers.LegalNameNotAllowed` |
| 12a/b | Contact email `not-an-email` · phone `0501234567` | 422 `Customers.EmailInvalid` · 422 `Customers.PhoneInvalid` (AC value objects) |
| 13 | Two contacts `a@x.example` and `A@X.example` | 422 `Customers.DuplicateContactEmail` (AC) |
| 14a/b | Contact with neither email nor phone · `hasPortalAccess: true` with phone only | 422 `Customers.ContactChannelRequired` · 422 `Customers.PortalAccessRequiresEmail` |
| 15a–c | No names · `type: "Robot"` · `preferredLanguage: "fr"` | 400 `urn:crm:problem:validation`: `errors.DisplayNameEn` (custom text), `errors.Type`, `errors.PreferredLanguage` (custom text) |
| 16 | `GET /api/Agent/Customers/{{layla}}` twice | 200; one SELECT across both (R10) |
| 17 | `PUT /api/Agent/Customers/{{layla}}` new `displayNameAr` + latest rowVersion; then request 16 | 200, `modifiedBy` set, new rowVersion; 16 shows a fresh SELECT; EF log shows `INSERT INTO [customers].[outbox_messages]` inside the transaction |
| 17b | Same PUT with `"rowVersion": "AAAAAAAAB9E="` | **409** `Persistence.ConcurrencyConflict` |
| 18a–d | deactivate · deactivate again · reactivate · reactivate again | 200 `Inactive` · 422 `Customers.AlreadyInactive` · 200 `Active` · 422 `Customers.AlreadyActive` |
| 19 | `POST {{layla}}/contacts` `{name:"Layla work", email:"layla@work.example", isPrimary:true}` | 200; the new contact `isPrimary: true`, the previous primary `false` — in one response (AC demote); EF log per R3 |
| 20a/b | Add contact with `layla.hassan@example.com` · with `+966550001111` | 422 `Customers.DuplicateContactEmail` · 422 `Customers.DuplicateContactPhone` |
| 21a/b | `PUT …/contacts/{{contactId}}` rename · change its email to another contact's | 200 · 422 `Customers.DuplicateContactEmail` |
| 22 | `POST …/contacts/{{otherContactId}}/primary` | 200; primary moved, exactly one `isPrimary` |
| 23a/b | `POST …/contacts/{{primaryId}}/remove` · remove a non-primary | 422 `Customers.PrimaryContactRemoval` · 200 |
| 24 | `POST …/contacts/00000000-0000-0000-0000-00000000dead/primary` | 404 `Customers.ContactNotFound` |
| 25a–e | Add note "first" · add note "second" · `PUT …/notes/{{first}}/pin {isPinned:true}` · `GET` · unpin, `DELETE` a note, `DELETE` an unknown note id | 200 · 200 · 200 `notes[0]` is "first" (pinned above the newer "second" — AC) · same · 200 · 200 · 404 `Customers.NoteNotFound` |
| 26 | `POST {{layla}}/contacts` with a stale rowVersion | **409** (R3 — child change is concurrency-checked) |
| 27a–e | As agent B: `GET {{ahmed}}` · `PUT {{ahmed}}` · `POST {{ahmed}}/notes` · `POST {{ahmed}}/deactivate` · create with `departmentId: {{ryCare}}` | 403 `Organisation.OutOfScope` each (AC scope) |
| 28 | `GET /api/Agent/Customers/00000000-0000-0000-0000-00000000dead` | 404 `Customers.CustomerNotFound` (never confused with 403) |
| 29a/b | Supervisor, no `X-Crm-Scope`, create without `departmentId` · with `X-Crm-Scope: {{ryBill}}` | 400 `Organisation.DepartmentRequired` · 200 in RYD-BILL |
| 30 | `GET /api/Agent/Customers` with no token | 401 `urn:crm:problem:unauthorized` |

**Port the Sample-based requests** (`POST/GET/PUT/DELETE /api/Admin/Samples`, `/api/Agent/Sample`) — the endpoints
no longer exist:

- **`backend/requests/diagnostics.http`** — lines 6–8: "Customer rows are department-scoped; the Customers requests
  run as agent A". Setup (22–30) → create a customer, `@customerId = {{created.response.body.customer.id}}`. 2/3
  (35–43) → `GET /api/Agent/Customers/{{customerId}}` with the correlation headers. 4 → create with no names (400,
  `errors.DisplayNameEn`). 5 → malformed JSON against `POST /api/Agent/Customers`. 6 → `GET
  /api/Agent/Customers/…dead` (404 `Customers.CustomerNotFound`). 8 → organisation without legal name (422
  `Customers.LegalNameRequired` — "the validator allows it; the aggregate refuses"). 9 → `PUT
  /api/Agent/Customers/{{customerId}}` with `"rowVersion": "AAAAAAAAB9E="` and `displayNameEn: "Stale"` (409). 14 →
  `GET /api/Agent/Customers/{{customerId}}` twice, one `SELECT … FROM [customers].[Customers]`. 15 → `PUT` with the
  cached rowVersion: `INSERT INTO [customers].[outbox_messages]` between BEGIN and COMMIT; then 14 again (fresh
  SELECT). **16** → within ~2 s the log shows the dispatcher's no-subscriber line for
  `…CustomerUpdatedIntegrationEvent` and `SELECT ProcessedAtUtc FROM customers.outbox_messages` is set. **Remove 17,
  18 and 20** (retry, dead-letter, redelivery-not-reapplied need a subscriber; replace them with one comment: "Retry,
  dead-letter and consumer idempotency are re-verified when the first cross-module subscriber lands — Sample's
  FlakySampleRenamedConsumer left with Story 11."). **19** → `SELECT * FROM customers.outbox_messages WHERE Content
  LIKE '%"displayNameEn":"Stale"%';` returns no row. Every `sample.` in the SQL comments → `customers.`.
- **`backend/requests/organisation.http`** lines 216–295 (requests 14–23): the same matrix on customers — 14 create
  "A only" as agent A (`@customerAId`, `@customerARowVersion` from `customer.id`/`customer.rowVersion`); 15 agent B
  create with `departmentId: {{ryCare}}` → 403; 16 GET → 403; 17 `PUT` → 403; **18 `POST
  /api/Agent/Customers/{{customerAId}}/deactivate` → 403** (replaces DELETE); 19 unknown id → 404
  `Customers.CustomerNotFound`; 20/21/22b–d `GET /api/Agent/Customers?pageSize=100` (the scoped SQL per request 20's
  comment, alias `[c]` instead of `[s]`); 22a/23 create with/without `X-Crm-Scope`.
- **`backend/requests/persistence.http`** — create → 200 with `createdBy`/`createdAtUtc`; read back (value objects
  + contacts); `PUT` profile (modifiedBy, new rowVersion); stale → 409; Arabic round-trip (`displayNameAr: "عميل
  تجريبي"`, contact name `"جهة اتصال"`); replace the soft-delete pair with a comment: "Soft delete is exercised by
  organisation.http request 24a (membership removal) — Customers has no delete endpoint (merge/delete is post-MVP)."
  Drop the two `/api/Agent/Sample` requests (canonical query → `GET /api/Agent/Customers?search=Ahmed`; validation →
  `?pageSize=0` → 400 `errors.PageSize`).
- **`backend/requests/localization.http`** — line 7 comment; requests 1–5 → `POST /api/Agent/Customers` with body
  `{ "displayNameEn": "", "type": "", "preferredLanguage": "en" }`: 1 (`ar`) → title "فشل التحقق من صحة البيانات",
  `errors.Type[0]` = "'Type' لا يجب أن يكون فارغاً." and `errors.DisplayNameEn[0]` = "أدخل اسم العميل بالعربية أو
  الإنجليزية.", `Content-Language: ar`; 2 (`en`) → "Validation failed", "'Type' must not be empty.", "Enter the
  customer's name in Arabic or English."; 3–5 unchanged semantics.
- **`backend/requests/health.http`** — delete lines 9–16 (the three `/api/Agent/Sample` requests; they have needed a
  token since Story 07) and add one comment line: "Authenticated API checks: customers.http."

### B13 — Documentation

- **`backend/README.md`** — lines 42–45: keep the `dbo.*` note and add a paragraph **"Upgrading a dev database from
  before Story 11"** with the documented drop:

  ```sql
  -- Story 11 deleted the Sample module. Its schema is no longer migrated or read. Drop it once:
  DROP TABLE IF EXISTS [sample].[SampleLines];
  DROP TABLE IF EXISTS [sample].[Samples];
  DROP TABLE IF EXISTS [sample].[outbox_message_consumers];
  DROP TABLE IF EXISTS [sample].[outbox_messages];
  DROP TABLE IF EXISTS [sample].[__EFMigrationsHistory];
  DROP SCHEMA IF EXISTS [sample];
  ```

  with the run command `docker exec -it crm-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P
  "Crm_Local_Dev_2026!" -C -I -d Crm -Q "…"` (or SSMS), or `docker compose down -v`. Lines 57–63: the tree shows
  `Customers/` ("reference module — copy this shape") with its five projects. Lines 68–74 and 140–143: "Copy the shape
  of `src/Modules/Customers/`". Lines 106–116: `-Module Customers`. Line 165: `ICustomersModuleApi`; line 171:
  `CustomerCreatedIntegrationEvent`. Line 207: `Customer.AddNote(…, createdAtUtc)`. Lines 217–219:
  `UpdateCustomerCommandHandler`; add one bullet: "**A child-only change must still UPDATE the root** — see
  `Customer.Revision`: contacts live in their own table, so the aggregate bumps `Revision` in every contact method,
  which puts the root row (and its rowversion check) into the UPDATE." Line 225: `CustomerNumber`'s constructor takes
  `value`. Line 279: `Customer` first. Leave line 355 (Swashbuckle "sample online") as is — it is not about the module.
- **`backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md`** — Agent Area: add "Story 11: the
  `Customers` module's `CustomersController` (`customers.read`/`customers.write`)." Admin Area line 25: delete
  "Story 02's demonstration Sample CRUD endpoints, deleted in Story 11, and" so the sentence starts with "(Story 06) the
  `Organisation` module's…".
- **`CLAUDE.md`** lines 71–72 — documentation line only: replace "The `Sample` module is the canonical template and is
  deleted in Story 11." with "`Customers` is the reference module to copy (Story 11 deleted the `Sample` template)."
- **`.squad/plans/customer-management/00-overview.md`** — already lists this plan (planning session).

---

## Frontend Tasks

### F1 — Regenerate the API types

With the API running in Development: `pnpm --dir frontend gen:api` (R11). Expected diff: `frontend/openapi/agent.json`
and `app/api/generated/agent.ts` gain `/api/Agent/Customers…` and lose `/api/Agent/Sample`; `openapi/admin.json` and
`app/api/generated/admin.ts` lose `/api/Admin/Samples…`. Commit both snapshots and both generated files. Read the
generated `CreateCustomerRequest` schema in `app/api/generated/agent.ts` before F2 — send exactly its required
properties; no `as` casts, no hand-written DTO type.

### F2 — `/_dev/api` off Sample (`frontend/app/pages/_dev/api.vue`)

Dev-only page, so bare strings stay allowed (`pnpm lint` exempts `/_dev`).

- Lines 22–29 (form): one `UiInput v-model="customerName" label="Customer name (English)"
  :error="formErrors.errorFor('displayNameEn')"`, drop the lines textarea, button "Create customer".
- Lines 133–158: rename state to `customerName`; `submitCustomer()` posts
  `api.clients.agent.POST('/api/Agent/Customers', { body: { displayNameEn: customerName.value, type: 'Individual', preferredLanguage: 'en' } })`
  (plus whatever F1 shows as required); on `result.ok` toast `'Customer created'` with
  `result.data.customer?.number ?? null` when `result.data.created`, else toast `'Possible duplicate'`; on failure
  `formErrors.apply(result.problem)` (an empty name → field error under the input).
- Lines 83, 89, 99–102, 290–308: "Load customers (skeleton → data)" calls
  `api.clients.agent.GET('/api/Agent/Customers/lookup', { params: { query: { search: 'a', take: 5 } } })` with
  `{ toastOnError: false }`; an empty array → `dataState = 'empty'` (title "No customers"); data shows the first item's
  `number` and `displayName.en`. Type the ref from the generated `components['schemas']['CustomerLookupDto']`.

No other frontend file references Sample (`grep -rn "Sample" frontend/app` shows only `pages/_dev/ui.vue`'s table demo
labels, which are unrelated and stay). No new i18n keys, no new UI kit component, no `/_dev/ui` section.

---

## Edge Cases & Failure Modes

- **Organisation without legal name** → `Customer.NormaliseLegalName` (B5) throws → `GlobalExceptionHandler.DomainRule`
  (`GlobalExceptionHandler.cs` lines 53–57) → 422 `Customers.LegalNameRequired`. The validator deliberately allows it.
  Switching an organisation to individual requires sending `legalName: null` (else 422 `LegalNameNotAllowed`).
- **Second primary contact** → `PromoteToPrimary` clears every other contact before marking the new one (B5) — one
  SaveChanges, one response with exactly one `isPrimary: true`.
- **Removing the primary** while others exist → 422 `Customers.PrimaryContactRemoval`; removing the only contact →
  allowed, zero contacts (the invariant is "exactly one primary **when any contact exists**").
- **Duplicates differing only by case/format** (`A@X.example` vs `a@x.example`; `+966 55…` vs `+96655…`) → equal
  after value-object normalisation → 422.
- **Self-duplicate inside a create request that also matches another customer** → the duplicate warning comes first
  (the check runs before the aggregate is built); after `confirmDuplicate: true` the aggregate rejects it (422).
- **A value that is malformed and also a "duplicate"** → 422 first (value objects are built before the lookup).
- **Duplicate check sees only the caller's scope** (Story 06 filter) — an identical email in another department never
  warns (`customers.http` 9). Accepted: that is the intake's rule.
- **Two agents change contacts of one customer at the same time** → both UPDATE the root (`Revision`) with their
  loaded rowversion; the second gets 409 (R3). Two agents adding **notes** at once never conflict (notes do not bump
  `Revision`); two agents removing the same note → the second's DELETE affects 0 rows → `DbUpdateConcurrencyException`
  → 409.
- **Stale client rowversion on a profile/contact command** → `SetOriginalRowVersion` → 409 before any state is
  committed; the integration event staged by the handler is discarded (`TransactionBehavior.cs` lines 91–97).
- **A failed `Result`** (404, 403 from `ResolveWriteTarget`) → rolled back, nothing published (lines 53–58). A
  `DomainException` escapes the handler; the transaction is disposed with the scope (rolled back) — the Sample
  DuplicateLine precedent.
- **Sequence gaps** — a create that draws a number and then fails (422 on a contact) leaves a gap. By design.
- **Out-of-scope id** → `Repository.GetByIdAsync` re-asks with only the scope filter lifted and throws →
  `OrganisationScopeBehavior` → 403 (`Repository.cs` lines 47–58). An unknown id → 404. A cached `GetCustomerById` hit
  never crosses scopes: `CachingBehavior` prefixes the caller's tenant/department/branch.
- **Search filters outside scope** (`departmentId` of another department) → empty page, not 403.
- **Search term with LIKE wildcards** (`%`, `_`) → EF Core escapes `Contains` arguments; `50%` matches literally.
- **Arabic variants** (`أحمد` vs `احمد`) do not match each other — no normalisation in the MVP (fuzzy deferred).
- **Sort on Arabic names** uses the database collation's code-point order — acceptable for the MVP.
- **Non-deterministic paging** on equal names → prevented by `ApplyThenBy(Number)` (B3).
- **Phone search with spaces** → the phone side strips separators (`PhoneNumber.StripSeparators`); names/number/email
  use the raw trimmed term.
- **`AddNote` with no parseable user id** (only possible for an in-process system caller) → `Guid.Empty` → 422
  `Customers.NoteAuthorRequired`.
- **`FindContactByEmailAsync` from an anonymous or portal caller** → 401/403 from `AuthorizationBehavior` — Story 47's
  problem to solve with a reviewed path (Prerequisites, Coordination). A malformed email → empty list.
- **Soft delete + owned children** — `Customer` is `ISoftDeletable`; no endpoint deletes one yet. When a later story
  does, `AuditingSaveChangesInterceptor.SoftDelete` (lines 39–65) already rescues owned entries cascaded into Deleted.
- **Removing a contact while the same SaveChanges soft-deletes a root** cannot happen in this story (no delete
  endpoint); note for the merge story: the interceptor's owned-rescue loop would resurrect the removed contact row.
- **Swagger schema clash** (R5) — a new DTO named like an existing Agent-document type makes `/swagger/agent/swagger.json`
  500; `gen:api` then fails loudly.
- **Stale build output** — `grep -r Sample backend/src` matches binaries in `bin/`/`obj/` until they are deleted (R6).
- **Old dev database** — the `sample` schema stays until the documented drop runs; nothing reads it, so the API is
  unaffected either way (R7).
- **Logs** — handlers log nothing new; request payloads (names, emails, phones) appear only at Debug through the
  redacting logger. Contact PII is never written with string interpolation.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the `CRM0001` rules, the startup guard, the
aggregate's invariants, `backend/requests/customers.http`, and the ported request files replace the suite. Run every
row; record the results and which R1/R2 path shipped in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001`, no `RS0030` |
| 2 | Boundaries | read the five Customers `.csproj` | Domain/Contracts → SharedKernel only; Infrastructure → Organisation.Contracts only across modules; Presentation has `DisableTransitiveProjectReferences` |
| 3 | Migration | read `Customers/…/Migrations/*_Initial.cs` | B8 checklist; schema `customers` only; nullable `Email`/`Phone`; unique `Number`; the sequence |
| 4 | No child DbSet (AC) | `grep -rn "DbSet<" backend/src/Modules/Customers` | exactly one hit: `DbSet<Customer> Customers` |
| 5 | Startup guard | run the API | starts (every new request declares `[RequirePermission]`); log shows `customers` migrated and seeded |
| 6 | Risks R1–R11 | as stated in Prerequisites | each proven or its fallback recorded |
| 7 | Use cases + invariants (AC) | `customers.http` 1–30 top to bottom | every row's expected status/code |
| 8 | Scope in SQL (AC) | `customers.http` 1 and 27 + EF log | scope predicates on every Customers SELECT; 403s |
| 9 | Pinned notes (AC) | `customers.http` 25c | the pinned note is `notes[0]` |
| 10 | Old request files | `diagnostics.http`, `organisation.http`, `persistence.http`, `localization.http`, `health.http` | ported requests give the stated results; no request targets `/Samples` or `/Sample` |
| 11 | Contracts API compiles for a consumer | read `ICustomersModuleApi` | primitives + contract records only; no Domain type |
| 12 | Swagger | `/swagger/agent/swagger.json`, `/swagger/admin/swagger.json` | 200; Customers paths in Agent; no Samples in Admin (R5) |
| 13 | Sample gone (AC) | `find backend/src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +`, `dotnet build backend/Crm.sln`, then `grep -r Sample backend/src` | build clean; **grep prints nothing** (R6); `dotnet sln backend/Crm.sln list` has no Sample |
| 14 | Schema drop | R7 | no `sample` schema; API starts clean |
| 15 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0 |
| 16 | `/_dev/api` | `pnpm --dir frontend dev`, sign in, open `/_dev/api` in `en` and `ar` | empty name → field error under the input; a name → "Customer created" toast; "Load customers" → skeleton → data; panel 2 scenarios A–E PASS |

---

## Migration / Rollback

- One new migration: `Customers/Initial` (schema `customers`: sequence `CustomerNumbers`, `Customers`,
  `CustomerContacts`, `CustomerNotes`, the outbox pair). No change to the `organisation` or `identity` schemas.
  Applied automatically in Development at startup; seeded by `CustomersSeeder`.
- **Removed:** the Sample module and its three migrations. The `sample` schema is **not** dropped by any migration (a
  module never touches another schema, and the Sample migrations are gone) — B13's documented SQL drops it once per
  dev database.
- **Rollback:** `pwsh backend/scripts/ef.ps1 -Module Customers database update 0`, then revert the commit (Sample
  returns; its schema still exists unless the drop ran — then `docker compose -f backend/docker-compose.yml down -v`
  and let every module re-migrate). No production data exists.
- **Half-applied state:** a failed `Customers` migration fails `MigrateAndSeedModulesAsync` at startup (loud). If the
  seeder fails mid-way, re-running is safe (keyed on well-known ids); a drawn sequence value is simply skipped. Never
  hand-edit `__EFMigrationsHistory`.

---

## Verification Steps

1. **Backend builds:** `dotnet tool restore`; `dotnet build backend/Crm.sln` → Test Plan rows 1–4; prove R4's build
   half.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, run the README drop SQL (R7), then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` → row 5; prove R1–R3, R5, R8–R10 in order
   through `customers.http`; run `customers.http` top to bottom → rows 7–9, 12; re-run `users.http` request 1 (R4)
   and the five ported files → row 10.
3. **Regression:** row 13 (delete `bin`/`obj`, rebuild, `grep -r Sample backend/src` empty); row 14;
   `grep -rn "IgnoreOrganisationScope()" backend/src` still shows the definition and exactly one caller
   (`MembershipsOfUserSpecification`).
4. **Frontend types:** with the API up, R11, then `pnpm --dir frontend gen:api` → only the four expected files change.
5. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` →
   row 15.
6. **Frontend runs:** `pnpm --dir frontend dev` with the API up → row 16 in `en` and `ar`.

---

## Done Criteria

- [ ] An organisation customer without a legal name is rejected by the aggregate with 422 `domain-rule`
      (`customers.http` 10; `diagnostics.http` 8).
- [ ] Adding a second primary contact demotes the previous one in the same operation (`customers.http` 19, 22).
- [ ] A duplicate contact email within one customer is rejected (13, 20, 21b); a malformed email or non-E.164 phone
      is rejected at value-object construction (12a/b — 422 from `EmailAddress`/`PhoneNumber`, not 400).
- [ ] There is no `DbSet<Contact>` or `DbSet<CustomerNote>`; both are reached only through `Customer` (row 4; no
      repository registered for either in `CustomersModule`).
- [ ] Creating a customer whose contact email/phone already exists in scope returns a duplicate warning with the
      candidates, and can be confirmed through (7, 8, 9).
- [ ] Search matches Arabic and English names, customer number, email, and phone, paged server-side (2, 3a–d, 4).
- [ ] Every customer read and write is scoped to the caller's branch and department, visible in the SQL (1, 27, 29;
      `organisation.http` 14–23).
- [ ] Pinned notes sort above unpinned ones (25c).
- [ ] `Sample` is gone: `grep -r Sample backend/src` finds nothing and the build is clean (row 13); the `sample` schema
      drop is documented in `backend/README.md` and was run (row 14).
- [ ] Every use case and rejected invariant exercised from `backend/requests/customers.http`; the four older files and
      `health.http` no longer reference Sample.
- [ ] `ICustomersModuleApi` exposes `GetCustomerSummaryAsync` and `FindContactByEmailAsync` (primitives/contract
      records); permissions `customers.read` / `customers.write` declared on every request.
- [ ] `dotnet build backend/Crm.sln` clean with 0 warnings and no `CRM0001`; the Customers `Initial` migration is
      included; `pnpm typecheck`/`lint`/`build` clean; `gen:api` output committed.
- [ ] `backend/README.md`, `AREAS.md`, and the `CLAUDE.md` Sample line updated (B13).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/customer-management/00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 14.**
