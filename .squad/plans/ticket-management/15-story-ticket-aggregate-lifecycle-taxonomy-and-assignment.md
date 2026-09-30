# Story 15 — Ticket aggregate lifecycle taxonomy and assignment

## Prerequisites

- **Stories 01–07, 11 and 14 are implemented and committed** (latest `464bb28`). Read the "Product rules" of
  [`../customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md`](../customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md)
  (the reference module shape, the sequence-number generator, `OwnsMany` + `ValueGeneratedNever`, `Revision`,
  rowversion over the wire, the scope rules 403-never-404) — this plan copies all of them. Story 11's coordination
  bullet: "Story 15 (Tickets) reads `ICustomersModuleApi.GetCustomerSummaryAsync` (403 out of scope, 404 unknown — both
  failed `Result`s) and checks the contact id against `CustomerSummary.Contacts`. It may seed tickets against
  `CustomersDevSeed` ids."
- **Modules:**
  - **Creates `Tickets`** (five projects, schema `tickets`): the `Ticket` aggregate (with `TicketActivity` children),
    the `Category` aggregate, queues and search, admin category CRUD.
  - **Touches `Organisation`** — two new read methods on `IOrganisationModuleApi` (department reference, department
    membership check), each a new `[RequireAuthenticatedUser]` query. No schema change.
  - **Touches `Identity`** — a new `IIdentityModuleApi` (staff display names) backed by one new
    `[RequireAuthenticatedUser]` query with the explicit tenant compare. No schema change.
  - **Touches the building blocks** in exactly one security-relevant place: a new scope marker
    `ITransferableOrganisationScoped` in `Crm.SharedKernel.Scoping` and one branch in the write guard
    (`ModuleDbContext.OrganisationScope.cs`) so a transfer can move a ticket **out of** the sender's scope (B1, R1).
  - Cross-module work is **only** Contracts calls: `Tickets.Application` → `Customers.Contracts`,
    `Organisation.Contracts`, `Identity.Contracts`; `Tickets.Infrastructure` sees the `*DevSeed` ids through the same
    references. Tickets publishes six integration events (no subscribers yet).
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md`).** Absorbs **16** (flat localised categories, fixed priorities)
  and **17** (assign, claim, transfer, queue queries). **Deferred:** SLA clock, merge/links, ticket types, custom
  fields, tags, resolution codes, category hierarchy, team assignment, workload balancing, bulk assign, auto-routing,
  messages and notes (18), the inbox and ticket detail UI (20), portal submission (47), caching of ticket reads.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`,
  rollForward latestFeature); EF Core **10.0.11**, `dotnet-ef` **10.0.11** (`.config/dotnet-tools.json`, local tool —
  `dotnet tool restore` first); MediatR **12.4.1** (licence pin — do not bump); FluentValidation **12.1.1**;
  Swashbuckle **10.2.3** (`backend/Directory.Packages.props`); `AnalysisLevel` `latest-recommended`,
  `TreatWarningsAsErrors` (`backend/Directory.Build.props`). **No new NuGet package. No new npm package.** Frontend
  pins unchanged (`frontend/package.json`: Nuxt 4.5.2, openapi-typescript 7.13.0, openapi-fetch 0.17.0, pnpm 12.6.0).
  This machine has **no Python** and **no `pnpm` on PATH** until
  `corepack enable pnpm --install-directory "$APPDATA/npm"`; Arabic text and `$` break Bash heredocs — use the
  **Write tool** for every file with Arabic in it (`.resx`, seeder, `.http`, locale JSON).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — the transfer exception in the write guard (security).** `EnsureWritesInScope`
    (`ModuleDbContext.OrganisationScope.cs` lines 70–87) refuses any Added/Modified scoped row outside the caller's
    scope — so today a transfer to a department the sender does not belong to is a 403. B1 adds one branch: a
    **Modified** `ITransferableOrganisationScoped` row passes when its **original** tenant/branch/department are in the
    caller's scope and its tenant did not change. **Prove:** `tickets.http` 52 (supervisor transfers RYD-CARE → JED-CARE
    → 200), 53 (supervisor and agent A then GET it → 403), 54 (agent B GETs it → 200); **and the regressions:**
    `organisation.http` 14–23 and `customers.http` 27e (create into a foreign department) still answer 403 —
    `Customer` is not transferable, and an **Added** row never takes the new branch. Read the branch once more against
    `OrganisationScope.Allows` (`OrganisationScope.cs` lines 94–114) before committing. **Fallback:** none — if the
    OriginalValue read does not return `TenantId`/`BranchId`/`DepartmentId` instances (it should: the CLR property
    type, pre-conversion), fix the cast; never widen the rule.
  - **R2 — nested module queries inside a command, and the pending-event trap.** Every Tickets command calls other
    modules' Contracts (a MediatR **query** in that module) and `TicketDtoAssembler` calls Identity's. A nested query
    runs `TransactionBehavior`'s query path, which calls `ThrowIfPublishedOutsideCommand("a query")`
    (`TransactionBehavior.cs` lines 34–39) against the **request-scoped** `OutboxEventBus` — so a nested query after
    `eventBus.Publish(...)` throws `InvalidOperationException` (500). **Rule for every handler: all nested calls
    (customers, organisation, identity, the assembler) happen before `eventBus.Publish`, and `Publish` is the last
    statement before `return`.** **Prove:** `tickets.http` 12 (create → 200, one `INSERT INTO [tickets].[outbox_messages]`
    in the EF log) and 42 (claim → 200). **Fallback:** none — reorder the handler.
  - **R3 — claim contention.** Claim takes **no** client rowversion; the loaded rowversion is the concurrency token.
    Two simultaneous claims: the loser either loads the ticket after the winner committed (pre-check → 409) or loses
    the `UPDATE … WHERE [RowVersion] = @…` (0 rows → `ConcurrencyConflictException`, caught in
    `ClaimTicketCommandHandler`, re-read, → 409). Both must answer **409 `Tickets.AlreadyClaimed` naming the winner**.
    **Prove:** Verification step 4 (the Bash race snippet) run on three fresh tickets: every pair gives exactly one 200
    and one 409, every ticket ends with one assignee and one `Claimed` activity; the log shows `Claim of ticket … lost
    the race` (`LogClaimLostRace`, B6) at least once. If ten pairs never hit the race path, record that and show the
    EF log's `UPDATE [tickets].[Tickets] SET … WHERE [Id] = @p… AND [RowVersion] = @p…` for a claim instead.
    **Fallback:** none.
  - **R4 — the ticket-number sequence.** `SqlTicketNumberGenerator` copies Story 11's proven
    `SqlCustomerNumberGenerator` (`SqlCustomerNumberGenerator.cs` lines 10–19) with the `tickets` schema. **Prove:**
    `tickets.http` 12 → `number` `T-000004` on a fresh database (the seed takes 1–3); EF log shows
    `SELECT NEXT VALUE FOR [tickets].[TicketNumbers]` between `BEGIN TRANSACTION` and `INSERT INTO [tickets].[Tickets]`.
    **Fallback:** Story 11's R2 fallback (a raw `DbCommand` on the open transaction).
  - **R5 — list projection without the owned activities.** `TicketReadModel` (B7) projects
    `SpecificationEvaluator.GetQuery(...)` straight to `TicketListRow` with `.Select`, so the owned `TicketActivities`
    collection is **not** joined; `.Value` on the converted ids is client-evaluated in the top-level projection.
    **Prove:** `tickets.http` 57 → the EF log's SELECT lists only `[t].[…]` columns of `[tickets].[Tickets]` (no
    `TicketActivities` join), with the scope predicates (`[t].[TenantId] = @…`, `[t].[BranchId] IN …`,
    `[t].[DepartmentId] IN …`) and `[t].[IsDeleted] = CAST(0 AS bit)`. **Fallback:** project the ids as
    `EF.Property<Guid>(t, "Id")`-style shadow reads is **not** allowed (ids are converted, not shadow) — instead select
    the whole converted property (`t.Id`, `t.CategoryId`) into the row and unwrap in `TicketDtoAssembler`.
  - **R6 — enum-list filters.** `statuses.Contains(t.Status)` (string conversion) and `priorities.Contains(t.Priority)`
    (int conversion) must translate through the converters. **Prove:** `tickets.http` 59 and 61 → the EF log shows
    `[t].[Status] IN (N'Cancelled')`-style (or `OPENJSON`) with the **converted** values, and
    `[t].[Priority] IN (3, 4)`. **Fallback:** convert to `IReadOnlyList<string>` / `IReadOnlyList<int>` in the
    specification and compare `EF.Property`-free on the enum by building one `OR` chain per value (max 7 statuses).
  - **R7 — nullable strongly-typed `CategoryId`.** `Ticket.CategoryId` is `CategoryId?` mapped with a
    `ValueConverter<CategoryId, Guid>` class (a lambda `id => id.Value` on a nullable reference is CS8602). **Prove:**
    the `Initial` migration has `CategoryId uniqueidentifier NULL`; `tickets.http` 12 (with category) and 39 (created
    without one) round-trip; `?categoryId=` (62) translates to `[t].[CategoryId] = @…`.
  - **R8 — owned activity rows INSERT, not UPDATE.** `TicketActivity.Id` is client-generated; without
    `ValueGeneratedNever` EF treats an activity added to a tracked ticket as existing (`RefreshTokenFamilyConfiguration.cs`
    line 28 comment). **Prove:** `tickets.http` 23 → EF log shows `INSERT INTO [tickets].[TicketActivities]` and
    `UPDATE [tickets].[Tickets] SET … [Revision] = @…, [Status] = @… WHERE [Id] = @… AND [RowVersion] = @…` in one
    transaction.
  - **R9 — Swagger in both documents.** New schema names must not clash with existing ones in the Agent document
    (`CustomerNameDto`, `CustomerDto`, `MeDto`, …) or the Admin document (`LocalizedTextDto`, `DepartmentDto`, …);
    two controllers named `TicketCategoriesController` live in one assembly (namespaces `…Controllers.Agent` and
    `…Controllers.Admin`, different Areas). **Prove:** `GET /swagger/agent/swagger.json` and
    `/swagger/admin/swagger.json` → 200; `tickets.http` 1 and 10 both route. **Fallback:** rename the Admin one
    `TicketCategoryAdminController` (route `api/Admin/TicketCategoryAdmin`) and record it; rename a clashing record with
    a `Ticket` prefix.
  - **R10 — localised, formatted refusals.** `TicketRefusals` (B6) builds the 422/409 detail with
    `string.Format(CultureInfo.CurrentCulture, localization.Get(key), arg)` (CA1305 satisfied). **Prove:** `tickets.http`
    21 in `en` → detail "The ticket is New; that action is not allowed in this status." and in `ar` → "حالة التذكرة
    «جديدة»، ولا يُسمح بهذا الإجراء في هذه الحالة."; 43 in both languages names agent A.
  - **R11 — cross-module queries pass the startup guard.** The three new queries in Organisation/Identity declare
    `[RequireAuthenticatedUser]` and return `Result<T>` (`RequestAuthorizationGuard.cs` lines 57–89). **Prove:** the API
    starts; `tickets.http` 45 (assign to a non-member → 422) exercises the membership query and 52 the department query.
  - **R12 — `pnpm` and `gen:api`.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, then
    `pnpm --version` prints 12.6.0 before F1.
- **Coordination:**
  - **Story 18** (messages, notes, history) adds `TicketMessage`/`TicketNote` inside `Ticket`, sets
    `FirstRespondedAtUtc` (column exists, never set here), and renders `TicketActivityDto` (kind + from/to + actor,
    **no pre-rendered text** — the UI maps `kind` to `tickets.activity.<kind>` i18n keys and resolves ids through
    `TicketDto.people`, statuses and priorities through their own keys).
  - **Story 20** (inbox + detail UI) consumes exactly the Agent endpoints in B9: `GET /api/Agent/Tickets?queue=…`
    (plus filters), `GET /api/Agent/Tickets/queues`, `GET /api/Agent/Tickets/{id}`, `POST /api/Agent/Tickets`, the
    action endpoints, and `GET /api/Agent/TicketCategories`. It offers only `TicketDto.allowedActions`, intersected with
    the caller's permissions (`tickets.write` for Claim/status/priority/category, `tickets.assign` for
    Assign/Unassign/Transfer) — the domain does not know permissions. Customer names in the inbox need a batch lookup on
    `ICustomersModuleApi` — **not** in this story. The customer page's `CustomerTicketsPanel` uses
    `GET /api/Agent/Tickets?customerId=…&queue=all`.
  - **Story 47** (portal) creates tickets with `TicketChannel.Portal` through its own command (the domain already
    supports the channel) and lists only `IsCustomerVisible` categories (`CategoriesSpecification`'s
    `customerVisibleOnly`). `ICustomersModuleApi` requires `customers.read`, so the portal path needs its own reviewed
    customer check (Story 11 coordination).
  - **Stories 32/34/51** subscribe to the six integration events in B2.

---

## Story Goal

The Ticket bounded context exists, with its lifecycle as domain behaviour, before any ticket screen is built.

When this story is done:

1. A new `Tickets` module (`Crm.Modules.Tickets.*`, schema `tickets`) holds the `Ticket` aggregate: generated
   `TicketNumber` (`T-000123`), subject, description, customer and contact ids (checked through `Customers.Contracts`),
   channel (`Agent`/`Portal`), optional category, `Priority` (Low, Normal, High, Urgent), status, owning branch and
   department (`IOrganisationScoped`, transferable), assigned agent id, and created / first-responded / resolved /
   closed timestamps.
2. The status machine (New, Open, PendingCustomer, OnHold, Resolved, Closed, Cancelled, with Reopen) is **one
   transition table inside `Ticket`**; there is no public status setter; `TicketDto.allowedActions` is derived from
   the same table.
3. `Open`, `AssignTo`, `Claim`, `Unassign`, `TransferTo`, `ChangePriority`, `ChangeCategory`, `AwaitCustomer`,
   `PutOnHold`, `Resolve`, `Close`, `Reopen`, `Cancel` each raise one domain event and append exactly one
   `TicketActivity` (actor, kind, from, to, timestamp — a kind plus parameters, never rendered text); the handler writes
   exactly one outbox row.
4. The `Category` aggregate (bilingual name, owning department, active flag, display order, customer-visible flag)
   with Admin CRUD and an Agent picker that shows active categories only.
5. Claim is safe under concurrency (one winner, the loser gets a localised 409 naming the winner); transfer moves the
   ticket out of the sender's scope and into the receiver's, keeping its history.
6. Queue queries (mine, unassigned, all in scope) and a paged, sorted, filtered search, projected server-side and
   scoped by Story 06's filter.
7. An admin screen `/admin/ticket-categories` renders category names in the active locale (en/ar).
8. `backend/requests/tickets.http` exercises every endpoint, transition, rejection, contention and transfer.

**Not in scope:** everything deferred in Prerequisites and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith" (catalogue: `Tickets` owns 15–19, 48), "Domain-driven design", "Application,
   API, and persistence" (**Results**, **Localization keys**, **Time**, **Scoping** at line 202 — B13 extends it),
   "Testing policy".
2. [Story 11's plan](../customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md) — B1
   (projects + `dotnet sln add`), B6 (handler shape), B7 (DbContext, configuration, generator, seeder), B8 (migration),
   B9 (controller). This plan says "copy Story 11 B…" instead of repeating them.
3. **The whole `backend/src/Modules/Customers/` module — the reference to copy.** Especially:
   `Crm.Modules.Customers.Domain/Customer.cs` lines 8–71 (scoped aggregate shape, EF constructor `null!`, `Revision`,
   soft delete), 222–228 (`Find…`/`MarkChanged`), `CustomerNumber.cs` (copy as `TicketNumber`),
   `CustomerDomainErrors.cs` (one `internal static` class of `DomainError`s); `Application/Commands/DeactivateCustomer/*`
   (command + `ICacheInvalidatingCommand`, handler lines 17–35: load → `SetOriginalRowVersion` → domain call → save →
   publish → return), `Commands/AddNote/AddNoteCommandHandler.cs` line 25 (actor id from `ICurrentUserService.UserId`),
   `Commands/CreateCustomer/CreateCustomerCommandHandler.cs` (the `ResolveWriteTarget` block), `CustomersErrors.cs`
   lines 16–20 (`DepartmentOutOfScope`/`DepartmentRequired` — copy verbatim), `CustomersModuleApi.cs`,
   `Queries/SearchCustomers/*` (list + count with one specification), `Specifications/CustomerSearchSpecification.cs`
   and `CustomerSort.cs` (one combined `Where`, sort parser, `ApplyThenBy`), `Validation/CustomersRules.cs`,
   `Localization/CustomersMessageKeys.cs` and both `.resx` (copy the header); `Infrastructure/Persistence/CustomersDbContext.cs`
   (sequence), `CustomersDbContextFactory.cs`, `Configurations/CustomerConfiguration.cs` (`OwnsOne` number,
   `OwnsMany` + `ValueGeneratedNever`, `IsRowVersion`, indexes), `SqlCustomerNumberGenerator.cs` lines 10–19,
   `CustomersSeeder.cs` lines 13–24 and 79–91 (well-known ids, both filters lifted), `CustomersModule.cs`;
   `Presentation/Controllers/Agent/CustomersController.cs` (attribute style, `ToActionResult`); all five `.csproj`.
4. `backend/src/BuildingBlocks/`:
   - `Crm.SharedKernel/Scoping/ITenantScoped.cs` lines 10–17 (B1 adds the marker below), `AggregateRoot.cs`
     (`Raise`), `AuditableEntity.cs`, `DomainException.cs`, `DomainError.cs`, `IIntegrationEvent.cs` lines 8–15.
   - `Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.OrganisationScope.cs` lines 60–87 (scope filter and
     `EnsureWritesInScope` — B1 changes lines 80–86), `ModuleDbContext.cs` lines 56–67 (`SaveChangesAsync` wraps
     `DbUpdateConcurrencyException`), `Repositories/Repository.cs` lines 23–25 and 49–58 (403-on-miss),
     `Repositories/SpecificationEvaluator.cs` (public static — the read model reuses it),
     `Interceptors/AuditingSaveChangesInterceptor.cs` (`Audit` stamps the root only when the root row changes — why
     `Revision` exists).
   - `Crm.BuildingBlocks.Application/Abstractions/OrganisationScope.cs` lines 94–148 (`Allows`, `ResolveWriteTarget`),
     `Behaviors/TransactionBehavior.cs` lines 34–39 (R2), 53–58 (failed Result rolls back), 91–98 (concurrency → 409),
     `Authorization/CrmPermissions.cs` lines 10–29 (`tickets.read`/`tickets.write` for Agent, `tickets.assign` for
     Supervisor and Administrator — **already defined, do not add**), `Messaging/IEventBus.cs`,
     `Specifications/Specification.cs`, `Paging/PagedResult.cs`, `Exceptions/ConcurrencyConflictException.cs`,
     `Localization/ILocalizationService.cs`.
   - `Crm.BuildingBlocks.Presentation/ApiControllerBase.cs` (Conflict → 409, Domain → 422 via `ErrorTypeMapping`),
     `ErrorHandling/GlobalExceptionHandler.cs` lines 53–57 (`DomainException` → 422 with `code`), `AREAS.md`.
5. `backend/src/Modules/Organisation/` — `Contracts/IOrganisationModuleApi.cs` lines 7–16, `Application/OrganisationModuleApi.cs`,
   `Queries/GetMyScope/GetMyScopeQuery.cs` (a `[RequireAuthenticatedUser]` query), `Specifications/ActiveMembershipSpecification.cs`,
   `Commands/AddMembership/AddMembershipCommandHandler.cs` lines 17–24 (the `Organisation.DepartmentNotFound` error
   text), `Domain/Department.cs` (`ITenantScoped`, `IsActive`, `BranchId`), `Domain/LocalizedText.cs` (both sides
   required — copy for categories), `Contracts/DevSeed/OrganisationDevSeed.cs`.
6. `backend/src/Modules/Identity/` — `Domain/User.cs` lines 6–9 (not `ITenantScoped`; tenant compared explicitly),
   `Application/Specifications/UsersSpecification.cs` (the hand-written tenant filter), `Queries/GetUser/GetUserQueryHandler.cs`
   lines 23–26, `Contracts/DevSeed/IdentityDevSeed.cs` line 5 (comment to update), `Infrastructure/IdentityModule.cs`
   lines 30–37, `Domain/UserEvents.cs` (domain-event record shape).
7. Host: `backend/src/Crm.Api/Modules/ModuleCatalog.cs` lines 12–21, `Crm.Api.csproj` lines 14–15 of the module
   `ItemGroup` (lines 36–41 of the file); `backend/Crm.sln` (the `Customers` solution folder, lines 46–56);
   `backend/Directory.Build.targets` rules 1–7.
8. `backend/requests/customers.http` lines 1–40 (header, variables, logins — copy) and `organisation.http` lines
   156–183 (create and deactivate a department).
9. Frontend: `frontend/app/pages/admin/departments.vue` (the page to copy), `app/composables/useDepartments.ts`,
   `app/layouts/agent.vue` lines 97–109 (nav), `app/utils/scope.ts` (`localizedName`), `i18n/locales/{en,ar}/organisation.json`
   (reused keys), `i18n/locales/{en,ar}/errors.json` (`errors.code.<Module>.<Code>`), `scripts/check-locales.ts` (one
   top-level key per file = file name).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **Ticket number.** `T-` + next value of the SQL Server sequence `tickets.TicketNumbers`, zero-padded to six digits;
  globally unique (unique index), never reused, gaps allowed, not editable.
- **Subject** 1–200, **description** 1–4000 (trimmed; aggregate rules, validators check length only too).
- **Customer and contact.** `CustomerId` required; must be visible to the caller through
  `ICustomersModuleApi.GetCustomerSummaryAsync` (out of scope → 403 `Organisation.OutOfScope`, unknown → 404
  `Customers.CustomerNotFound`, both passed through) and **active** (422 `Tickets.CustomerInactive`). `ContactId`
  optional; when given it must be one of `CustomerSummary.Contacts` (422 `Tickets.ContactNotOfCustomer`). Both are plain
  `Guid`s on the ticket — no FK.
- **Channel.** `Agent` for every ticket created here; `Portal` exists for Story 47.
- **Priority.** `Low`, `Normal` (default), `High`, `Urgent`; stored as an int (1–4) so it sorts by rank.
- **Category.** Optional on a ticket. When set it must be **active** and owned by **the ticket's department** (422
  `Tickets.CategoryInactive` / `Tickets.CategoryDepartmentMismatch` — aggregate rules, `Ticket` reads the `Category`).
  A category is `ITenantScoped` (not department-scoped): the name is visible tenant-wide so a list never loses it; its
  `DepartmentId` decides whose picker shows it. Deactivating a category hides it from the picker and from new
  assignments; existing tickets keep it and still show its name (with `isActive: false`). Names: both Arabic and
  English required, ≤ 200 each. Display order 0–9999. No delete — deactivate.
- **Status machine** (the table in `Ticket`, B4):

  | Action | Allowed from | To |
  |---|---|---|
  | `Open` | New, PendingCustomer, OnHold | Open |
  | `AwaitCustomer` | Open, OnHold | PendingCustomer |
  | `PutOnHold` | Open, PendingCustomer | OnHold |
  | `Resolve` | New, Open, PendingCustomer, OnHold | Resolved (sets `ResolvedAtUtc`) |
  | `Close` | Resolved | Closed (sets `ClosedAtUtc`) |
  | `Reopen` | Resolved, Closed | Open (clears `ResolvedAtUtc` and `ClosedAtUtc`) |
  | `Cancel` | New, Open, PendingCustomer, OnHold | Cancelled (terminal) |
  | `Assign`, `Claim`, `Unassign`, `Transfer`, `ChangePriority`, `ChangeCategory` | New, Open, PendingCustomer, OnHold ("working") | status unchanged |

  An action not allowed in the current status is 422 `Tickets.TransitionNotAllowed` with a **localised** detail naming
  the current status (the handler asks the aggregate's table first; the aggregate throws the same code as the
  backstop). A no-op change is 422: `Tickets.PriorityUnchanged`, `Tickets.CategoryUnchanged`,
  `Tickets.AssigneeUnchanged`, `Tickets.TransferToSameDepartment`, `Tickets.NotAssigned` (unassign an unassigned
  ticket).
- **Allowed actions** = every action whose status rule passes, minus `Claim` when assigned and `Unassign` when
  unassigned. Permission filtering is the UI's job (Story 20).
- **Assignment.** `Claim` (`tickets.write`) assigns the caller; only an **unassigned** working ticket can be claimed —
  a claimed ticket is 409 `Tickets.AlreadyClaimed` naming the assignee (also when it is the caller). `Assign`
  (`tickets.assign`) requires an active staff user who is a member of the ticket's department (422
  `Tickets.AssigneeInactive` / `Tickets.AssigneeNotInDepartment`); it may replace an existing assignee.
  `Unassign` requires `tickets.assign`.
- **Transfer** (`tickets.assign`) to another **active** department of the tenant (unknown → 404
  `Organisation.DepartmentNotFound`, inactive → 422 `Tickets.TargetDepartmentInactive`). It moves `DepartmentId` and
  `BranchId`, **clears the assignee and the category** (the receiving department triages it), keeps every activity,
  and appends one `Transferred` activity (from/to department ids). Afterwards the sender reads it as 403 unless they
  are also a member of the receiving department.
- **Activities.** One per successful action, including create (`Created`). Fields: `Kind`
  (`Created, Opened, AwaitingCustomer, PutOnHold, Resolved, Closed, Reopened, Cancelled, Assigned, Claimed, Unassigned,
  Transferred, PriorityChanged, CategoryChanged`), `ActorId`, `From`/`To` (machine values: status names, priority
  names, user/department/category ids, or null), `OccurredAtUtc`. Returned oldest first.
- **Concurrency.** Every ticket command except `Create` and `Claim` takes the client's last-seen `rowVersion`
  (base64) → stale = 409 `Persistence.ConcurrencyConflict`. `Claim` uses the loaded rowversion (R3). Every activity
  append bumps `Ticket.Revision`, so the root row is always in the UPDATE. Category update/deactivate/reactivate take
  the category's `rowVersion`.
- **Queues and search.** `queue` = `mine` (assigned to the caller), `unassigned`, `all` (default). `mine` and
  `unassigned` default to the working statuses; any `status` filter replaces that default. Filters: `status` (repeat
  for several), `priority` (repeat), `categoryId`, `assigneeId`, `departmentId`, `branchId`, `customerId`; text
  `search` (≤ 200) matches `Number` or `Subject` with `LIKE %term%`. Sort `createdAt` (default, descending),
  `modifiedAt`, `priority`, `number`, always tie-broken by `Number`. `page` ≥ 1, `pageSize` 1–100 (default 20).
  Filters outside the caller's scope give an empty page, not 403. `GET /api/Agent/Tickets/queues` returns the three
  counts.
- **No caching** of ticket or category reads in this story (they change on every action; nothing is `ICacheableQuery`).
- **Scope.** `Ticket` is `ITransferableOrganisationScoped` (tenant + branch + department, movable only by
  `TransferTo`); an existing out-of-scope id is 403 `Organisation.OutOfScope`, an unknown id 404
  `Tickets.TicketNotFound`. Create resolves its department with `ResolveWriteTarget` (optional `departmentId`).
- **Integration events** (Contracts, append-only, primitives, no subscribers yet): `TicketCreatedIntegrationEvent`,
  `TicketStatusChangedIntegrationEvent`, `TicketAssignedIntegrationEvent` (assign, claim, unassign),
  `TicketTransferredIntegrationEvent`, `TicketPriorityChangedIntegrationEvent`, `TicketCategoryChangedIntegrationEvent`.
  Exactly one per ticket command. Category commands publish nothing.
- **Error codes** (the frontend's `errors.code.*` contract — never rename one): 404 `Tickets.TicketNotFound`,
  `Tickets.CategoryNotFound`; 409 `Tickets.AlreadyClaimed`; 422 `Tickets.TransitionNotAllowed`,
  `Tickets.ScopeRequired`, `Tickets.NumberInvalid`, `Tickets.SubjectRequired`, `Tickets.SubjectTooLong`,
  `Tickets.DescriptionRequired`, `Tickets.DescriptionTooLong`, `Tickets.CustomerRequired`, `Tickets.ActorRequired`,
  `Tickets.ChannelUnsupported`, `Tickets.PriorityUnsupported`, `Tickets.PriorityUnchanged`, `Tickets.AlreadyAssigned`,
  `Tickets.AssigneeRequired`, `Tickets.AssigneeUnchanged`, `Tickets.NotAssigned`, `Tickets.TransferToSameDepartment`,
  `Tickets.CategoryInactive`, `Tickets.CategoryDepartmentMismatch`, `Tickets.CategoryUnchanged`,
  `Tickets.CategoryNameRequired`, `Tickets.CategoryNameTooLong`, `Tickets.CategoryDisplayOrderInvalid`,
  `Tickets.CategoryAlreadyActive`, `Tickets.CategoryAlreadyInactive`, `Tickets.CategoryScopeRequired`,
  `Tickets.CustomerInactive`, `Tickets.ContactNotOfCustomer`, `Tickets.AssigneeInactive`,
  `Tickets.AssigneeNotInDepartment`, `Tickets.TargetDepartmentInactive`, `Tickets.CategoryDepartmentInactive`; plus the
  existing `Organisation.OutOfScope` (403), `Organisation.DepartmentRequired` (400), `Organisation.DepartmentNotFound`
  (404), `Customers.CustomerNotFound` (404), `Persistence.ConcurrencyConflict` (409).

---

## Backend Tasks

All paths are under `backend/src/` unless they start with `backend/` or `frontend/`. File-scoped namespaces,
`sealed` classes, primary constructors for handlers, `IDateTime` for every clock read,
`ArgumentNullException.ThrowIfNull` on public methods' reference parameters, `CultureInfo.InvariantCulture` on every
`Guid.TryParse`. EF-only private constructors may assign `null!` exactly as `Customer.cs` lines 52–57 do; **no other
`!`** in new code. Every class the container instantiates is `public` (an `internal` one trips CA1812). Tickets
namespaces: `Crm.Modules.Tickets.{Domain,Application,Contracts,Infrastructure,Presentation}`.

### B1 — Building blocks: the transferable scope marker (security-relevant, R1)

**File: `BuildingBlocks/Crm.SharedKernel/Scoping/ITenantScoped.cs`** — append after line 17:

```csharp
/// <summary>An organisation-scoped row that one intention-revealing method may hand to another department of the
/// same tenant (Ticket.TransferTo, Story 15). The write guard judges a MODIFIED row of this kind by its ORIGINAL
/// tenant/branch/department, so the sender can move it out of their own scope; its tenant can never change, and an
/// ADDED row is judged like any other. Implement it only on an aggregate whose DepartmentId/BranchId have private
/// setters changed by exactly one method - adding an implementer is a security review.</summary>
public interface ITransferableOrganisationScoped : IOrganisationScoped;
```

**File: `BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.OrganisationScope.cs`** — replace
lines 80–86 (the loop in `EnsureWritesInScope`) with:

```csharp
        foreach (EntityEntry<ITenantScoped> entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.State == EntityState.Modified && entry.Entity is ITransferableOrganisationScoped && WasInScopeBeforeChange(entry))
            {
                continue; // a transfer: the row was the caller's, stays in the tenant, and may leave the caller's scope
            }

            if (!Scope.Allows(entry.Entity))
            {
                throw new OrganisationScopeViolationException(entry.Entity.GetType().Name, "Write");
            }
        }
```

and add below `EnsureWritesInScope`:

```csharp
    /// <summary>Original (loaded) scope columns in the caller's scope, and the tenant unchanged. OriginalValue is the
    /// CLR property value (TenantId/BranchId/DepartmentId), before conversion.</summary>
    private bool WasInScopeBeforeChange(EntityEntry<ITenantScoped> entry)
    {
        if (entry.Property(nameof(ITenantScoped.TenantId)).OriginalValue is not TenantId originalTenant
            || entry.Property(nameof(IOrganisationScoped.BranchId)).OriginalValue is not BranchId originalBranch
            || entry.Property(nameof(IOrganisationScoped.DepartmentId)).OriginalValue is not DepartmentId originalDepartment)
        {
            return false;
        }

        return originalTenant.Equals(entry.Entity.TenantId)
            && Scope.Allows(new OriginalScope(originalTenant, originalBranch, originalDepartment));
    }

    private sealed record OriginalScope(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId) : IOrganisationScoped;
```

Also update the `EnsureWritesInScope` summary (lines 70–72): append "A Modified ITransferableOrganisationScoped row is
judged by its original scope (Story 15 transfer)." Nothing else in the building blocks changes.

### B2 — Projects, solution, and Contracts

**Projects** — copy Story 11 B1 with `Customers` → `Tickets`:

- `Modules/Tickets/Crm.Modules.Tickets.Domain/Crm.Modules.Tickets.Domain.csproj` — `Crm.SharedKernel` only.
- `Crm.Modules.Tickets.Contracts/Crm.Modules.Tickets.Contracts.csproj` — `Crm.SharedKernel` only.
- `Crm.Modules.Tickets.Application/Crm.Modules.Tickets.Application.csproj` — `Crm.BuildingBlocks.Application`, own
  Domain, own Contracts, and three Contracts references with one comment ("Synchronous reads across modules - rule 1
  allowed."): `..\..\Customers\Crm.Modules.Customers.Contracts\Crm.Modules.Customers.Contracts.csproj`,
  `..\..\Organisation\Crm.Modules.Organisation.Contracts\Crm.Modules.Organisation.Contracts.csproj`,
  `..\..\Identity\Crm.Modules.Identity.Contracts\Crm.Modules.Identity.Contracts.csproj`.
- `Crm.Modules.Tickets.Infrastructure/Crm.Modules.Tickets.Infrastructure.csproj` — the EF Design package block
  (comment `ef.ps1 -Module Tickets`), `Crm.BuildingBlocks.Infrastructure`, own Application (the dev-seed ids of
  Organisation and Customers arrive transitively through Application).
- `Crm.Modules.Tickets.Presentation/Crm.Modules.Tickets.Presentation.csproj` — copy of the Customers one
  (`DisableTransitiveProjectReferences`, SharedKernel, BB.Application, BB.Presentation, own Application).

Then `dotnet sln backend/Crm.sln add` the five projects (Story 11 B1 command with `Tickets`) and confirm they sit
under a `Tickets` solution folder inside `Modules`.

**Create file: `Crm.Modules.Tickets.Contracts/IntegrationEvents/TicketIntegrationEvents.cs`** — one file, six records
(shape of `CustomerCreatedIntegrationEvent.cs`), summary on each "Published by …Handler. Primitives only;
append-only.":

```csharp
public sealed record TicketCreatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid CustomerId, Guid? ContactId, string Channel,
    Guid? CategoryId, string Priority, string Status, Guid? AssignedAgentId, Guid ActorId, Guid TenantId, Guid BranchId,
    Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record TicketStatusChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, string FromStatus, string ToStatus, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Assign, Claim (AssignedAgentId = the actor) and Unassign (AssignedAgentId = null).</summary>
public sealed record TicketAssignedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid? PreviousAgentId, Guid? AssignedAgentId,
    Guid ActorId, Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record TicketTransferredIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid FromBranchId, Guid FromDepartmentId,
    Guid ToBranchId, Guid ToDepartmentId, Guid? PreviousAgentId, Guid ActorId, Guid TenantId) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record TicketPriorityChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, string FromPriority, string ToPriority, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record TicketCategoryChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid? FromCategoryId, Guid ToCategoryId, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);
```

(CA1711/one-type-per-file analyzers are not enabled at `latest-recommended`; if the build flags it, split into six
files with the same names.)

**Create file: `Crm.Modules.Tickets.Contracts/DevSeed/TicketsDevSeed.cs`** (shape of `CustomersDevSeed.cs`):

```csharp
/// <summary>Well-known Development ticket and category ids, as primitives - for Stories 18, 20, 47 and the .http
/// files. Never used outside Development. The seed takes T-000001..T-000003.</summary>
public static class TicketsDevSeed
{
    public static readonly Guid RyCareGeneralCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000001");
    public static readonly Guid RyCareTechnicalCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000002");
    public static readonly Guid RyCareComplaintCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000003");
    /// <summary>Active, not customer-visible.</summary>
    public static readonly Guid RyCareInternalCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000004");
    /// <summary>Inactive - never in a picker.</summary>
    public static readonly Guid RyCareLegacyCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000005");
    public static readonly Guid RyBillInvoiceCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000006");
    public static readonly Guid JeCareGeneralCategoryId = Guid.Parse("15000000-0000-0000-0001-000000000007");

    /// <summary>T-000001: Ahmed, RYD-CARE, New, Normal, General, unassigned, Arabic subject.</summary>
    public static readonly Guid AhmedNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000001");
    /// <summary>T-000002: Najd (contact Sara), RYD-CARE, Open, High, Technical, assigned to agent A.</summary>
    public static readonly Guid NajdOpenTicketId = Guid.Parse("15000000-0000-0000-0000-000000000002");
    /// <summary>T-000003: Red Sea (contact Fahad), JED-CARE, New, Urgent, JED General, unassigned.</summary>
    public static readonly Guid RedSeaNewTicketId = Guid.Parse("15000000-0000-0000-0000-000000000003");
}
```

### B3 — Organisation and Identity: the Contracts reads Tickets needs

**File: `Modules/Organisation/Crm.Modules.Organisation.Contracts/IOrganisationModuleApi.cs`** — add to the interface
(lines 7–10) and the file:

```csharp
    /// <summary>Story 15: a department of the caller's tenant (Department is ITenantScoped). Unknown -> failed Result
    /// (404 Organisation.DepartmentNotFound).</summary>
    Task<Result<DepartmentReference>> GetDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);

    /// <summary>Story 15: does this user hold a (non-removed) membership in this department of the caller's tenant.</summary>
    Task<Result<bool>> IsDepartmentMemberAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken = default);
```

```csharp
public sealed record DepartmentReference(Guid Id, Guid TenantId, Guid BranchId, string Code, string NameAr, string NameEn, bool IsActive);
```

**Create** in `Crm.Modules.Organisation.Application/Queries/`:

- `GetDepartmentReference/GetDepartmentReferenceQuery.cs` — `[RequireAuthenticatedUser] public sealed record
  GetDepartmentReferenceQuery(Guid DepartmentId) : IRequest<Result<DepartmentReference>>;` (summary: "In-process only
  (IOrganisationModuleApi); no HTTP endpoint."). Handler: `IRepository<Department, DepartmentId>.GetByIdAsync` → null
  → `Error.NotFound("Organisation.DepartmentNotFound", $"Department '{id}' was not found.")` (text of
  `AddMembershipCommandHandler.cs` line 23); else `new DepartmentReference(d.Id.Value, d.TenantId.Value, d.BranchId.Value,
  d.Code.Value, d.Name.Arabic, d.Name.English, d.IsActive)`. Validator: `DepartmentId` NotEmpty.
- `IsDepartmentMember/IsDepartmentMemberQuery.cs` — `[RequireAuthenticatedUser] public sealed record
  IsDepartmentMemberQuery(Guid UserId, Guid DepartmentId) : IRequest<Result<bool>>;`. Handler:
  `await memberships.CountAsync(new ActiveMembershipSpecification(request.UserId, new DepartmentId(request.DepartmentId)), ct) > 0`
  (the tenant filter applies; soft-deleted memberships are excluded). Validator: both NotEmpty.

**File: `Crm.Modules.Organisation.Application/OrganisationModuleApi.cs`** — add the two methods, each
`sender.Send(new …Query(…), cancellationToken)`.

**Create file: `Modules/Identity/Crm.Modules.Identity.Contracts/IIdentityModuleApi.cs`**

```csharp
using Crm.SharedKernel.Results;

namespace Crm.Modules.Identity.Contracts;

/// <summary>What another module may ask the Identity module, synchronously (Story 15: assignee names and the
/// "already claimed by" message). Only users of the caller's tenant are returned; unknown ids are simply absent.</summary>
public interface IIdentityModuleApi
{
    Task<Result<IReadOnlyList<StaffUserSummary>>> GetStaffUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record StaffUserSummary(Guid Id, string DisplayName, bool IsActive);
```

**Create** in `Crm.Modules.Identity.Application/`:

- `Specifications/UsersByIdsSpecification.cs` — `(TenantId tenant, IReadOnlyList<UserId> ids)`:
  `Where(u => u.TenantId == tenant && ids.Contains(u.Id)); AsReadOnly();` with the summary "A second hand-written tenant
  filter, next to UsersSpecification: User is not ITenantScoped (see User.cs)." (`ids.Contains(d.Id)` precedent:
  Organisation `DepartmentsByIdsSpecification`.)
- `Queries/GetStaffUsers/GetStaffUsersQuery.cs` — `[RequireAuthenticatedUser] public sealed record
  GetStaffUsersQuery(IReadOnlyList<Guid> UserIds) : IRequest<Result<IReadOnlyList<StaffUserSummary>>>;`; handler lists
  `new UsersByIdsSpecification(currentUser.Scope.Tenant, [.. request.UserIds.Distinct().Select(id => new UserId(id))])`
  and maps `new StaffUserSummary(u.Id.Value, u.DisplayName.Value, u.IsActive)`; an empty id list returns
  `Result.Success<IReadOnlyList<StaffUserSummary>>([])` without a query. Validator: `UserIds` NotNull,
  `Must(ids => ids.Count <= 200)`.
- `IdentityModuleApi.cs` — `public sealed class IdentityModuleApi(ISender sender) : IIdentityModuleApi` sending
  `new GetStaffUsersQuery([.. userIds])` (shape of `OrganisationModuleApi.cs`).

**File: `Crm.Modules.Identity.Infrastructure/IdentityModule.cs`** — after line 33 add
`services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();` with the comment "The module's public face (Story 15)."
**File: `Crm.Modules.Identity.Contracts/DevSeed/IdentityDevSeed.cs`** line 5 — replace "No module API in Story 07 - no
other module needs one yet." with "Story 15 added IIdentityModuleApi (staff display names)."

### B4 — Domain (`Crm.Modules.Tickets.Domain`)

**Create files (small):**

- `TicketId.cs`, `CategoryId.cs` — `public sealed record TicketId(Guid Value) : StronglyTypedId(Value) { public static TicketId New() => new(Guid.NewGuid()); }` (same for `CategoryId`).
- `TicketStatus.cs` — `public enum TicketStatus { New, Open, PendingCustomer, OnHold, Resolved, Closed, Cancelled }`.
- `TicketPriority.cs` — `public enum TicketPriority { Low = 1, Normal = 2, High = 3, Urgent = 4 }`.
- `TicketChannel.cs` — `public enum TicketChannel { Agent, Portal }`.
- `TicketAction.cs` — `public enum TicketAction { Open, AwaitCustomer, PutOnHold, Resolve, Close, Reopen, Cancel, Assign, Claim, Unassign, Transfer, ChangePriority, ChangeCategory }`.
- `TicketActivityKind.cs` — `public enum TicketActivityKind { Created, Opened, AwaitingCustomer, PutOnHold, Resolved, Closed, Reopened, Cancelled, Assigned, Claimed, Unassigned, Transferred, PriorityChanged, CategoryChanged }`.
- `TicketNumber.cs` — copy `CustomerNumber.cs` with `Prefix = "T-"`, `MaxLength = 20`, error `TicketDomainErrors.NumberInvalid`, summary naming `tickets.TicketNumbers`.
- `LocalizedText.cs` — copy `Organisation.Domain/LocalizedText.cs` (both sides required, ≤ 200, `MaxLength = 200` const) with `TicketDomainErrors.CategoryNameRequired` / `CategoryNameTooLong`.
- `TicketEvents.cs` — domain events (shape of `Identity.Domain/UserEvents.cs`), no handlers in this story:

  ```csharp
  public sealed record TicketCreated(TicketId TicketId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  public sealed record TicketStatusChanged(TicketId TicketId, TicketStatus From, TicketStatus To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  public sealed record TicketAssignmentChanged(TicketId TicketId, Guid? From, Guid? To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  public sealed record TicketTransferred(TicketId TicketId, DepartmentId From, DepartmentId To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  public sealed record TicketPriorityChanged(TicketId TicketId, TicketPriority From, TicketPriority To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  public sealed record TicketCategoryChanged(TicketId TicketId, CategoryId? From, CategoryId To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
  ```

- `TicketDomainErrors.cs` — `internal static class TicketDomainErrors`, every 422 listed in Product rules that the
  domain raises, as `public static readonly DomainError X = new("Tickets.X", "<English>")`, e.g.
  `AlreadyAssigned = new("Tickets.AlreadyAssigned", "The ticket is already assigned; unassign it first.")`,
  `CategoryDepartmentMismatch = new("Tickets.CategoryDepartmentMismatch", "The category belongs to another department.")`,
  plus one factory:

  ```csharp
  public static DomainError TransitionNotAllowed(TicketAction action, TicketStatus status) =>
      new("Tickets.TransitionNotAllowed", $"'{action}' is not allowed while the ticket is {status}.");
  ```

  (`CultureInfo` is irrelevant for enum interpolation; if CA1305 fires on the interpolation, use
  `string.Create(CultureInfo.InvariantCulture, $"...")`.) No inline code string anywhere else in the Domain project.

**Create file: `TicketActivity.cs`**

```csharp
/// <summary>Child of Ticket - no DbSet, no repository, reached only through Ticket.Activities. A kind plus machine
/// parameters, never rendered text: the UI turns Kind/From/To into either language (Story 18).</summary>
public sealed class TicketActivity : Entity<Guid>
{
    public const int ValueMaxLength = 100;

    public TicketActivityKind Kind { get; private set; }

    public Guid ActorId { get; private set; }

    public string? From { get; private set; }

    public string? To { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketActivity()
    {
    }

    internal TicketActivity(Guid id, TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset occurredAtUtc)
        : base(id)
    {
        Kind = kind;
        ActorId = actorId;
        From = from;
        To = to;
        OccurredAtUtc = occurredAtUtc;
    }
}
```

**Create file: `Category.cs`**

```csharp
/// <summary>A flat, bilingual ticket category owned by one department (Story 15; hierarchy deferred). Tenant-scoped,
/// not department-scoped: its name stays readable on every ticket that carries it.</summary>
public sealed class Category : AuditableEntity<CategoryId>, ITenantScoped, IHasRowVersion
{
    public const int DisplayOrderMax = 9999;

    public TenantId TenantId { get; private set; } = TenantId.None;
    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);
    public LocalizedText Name { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsCustomerVisible { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = [];

    private Category() { Name = null!; }

    private Category(CategoryId id, TenantId tenantId, DepartmentId departmentId, LocalizedText name, int displayOrder, bool isCustomerVisible)
        : base(id) { TenantId = tenantId; DepartmentId = departmentId; Name = name; DisplayOrder = displayOrder; IsCustomerVisible = isCustomerVisible; }

    public static Category Create(TenantId tenantId, DepartmentId departmentId, LocalizedText name, int displayOrder, bool isCustomerVisible, CategoryId? id = null)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(departmentId);
        ArgumentNullException.ThrowIfNull(name);
        if (tenantId == TenantId.None || departmentId.Value == Guid.Empty) throw new DomainException(TicketDomainErrors.CategoryScopeRequired);
        return new Category(id ?? CategoryId.New(), tenantId, departmentId, name, EnsureOrder(displayOrder), isCustomerVisible);
    }

    public void Update(LocalizedText name, int displayOrder, bool isCustomerVisible) { ArgumentNullException.ThrowIfNull(name); Name = name; DisplayOrder = EnsureOrder(displayOrder); IsCustomerVisible = isCustomerVisible; }

    public void Deactivate() { if (!IsActive) throw new DomainException(TicketDomainErrors.CategoryAlreadyInactive); IsActive = false; }

    public void Reactivate() { if (IsActive) throw new DomainException(TicketDomainErrors.CategoryAlreadyActive); IsActive = true; }

    private static int EnsureOrder(int order) =>
        order is >= 0 and <= DisplayOrderMax ? order : throw new DomainException(TicketDomainErrors.CategoryDisplayOrderInvalid);
}
```

(Write it with normal brace formatting — compressed here for length.) The department never changes.

**Create file: `Ticket.cs`**

```csharp
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Domain;

/// <summary>The aggregate root of the Tickets context. The lifecycle is the transition table below - declared once,
/// read by every method and by AllowedActions(). Every method appends exactly one TicketActivity and raises one
/// domain event. There is no public status setter.</summary>
public sealed class Ticket : AuditableEntity<TicketId>, ISoftDeletable, IHasRowVersion, ITransferableOrganisationScoped
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private sealed record StatusTransition(IReadOnlyList<TicketStatus> From, TicketStatus To);

    private static readonly IReadOnlyList<TicketStatus> WorkingStatuses =
        [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

    // THE transition table (Story 15 product rules). Actions not listed here are the "working" actions: allowed in
    // WorkingStatuses, status unchanged.
    private static readonly IReadOnlyDictionary<TicketAction, StatusTransition> StatusTransitions =
        new Dictionary<TicketAction, StatusTransition>
        {
            [TicketAction.Open] = new([TicketStatus.New, TicketStatus.PendingCustomer, TicketStatus.OnHold], TicketStatus.Open),
            [TicketAction.AwaitCustomer] = new([TicketStatus.Open, TicketStatus.OnHold], TicketStatus.PendingCustomer),
            [TicketAction.PutOnHold] = new([TicketStatus.Open, TicketStatus.PendingCustomer], TicketStatus.OnHold),
            [TicketAction.Resolve] = new(WorkingStatuses, TicketStatus.Resolved),
            [TicketAction.Close] = new([TicketStatus.Resolved], TicketStatus.Closed),
            [TicketAction.Reopen] = new([TicketStatus.Resolved, TicketStatus.Closed], TicketStatus.Open),
            [TicketAction.Cancel] = new(WorkingStatuses, TicketStatus.Cancelled),
        };

    private readonly List<TicketActivity> _activities = [];

    public TicketNumber Number { get; private set; }
    public string Subject { get; private set; }
    public string Description { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? ContactId { get; private set; }
    public TicketChannel Channel { get; private set; }
    public CategoryId? CategoryId { get; private set; }
    public TicketPriority Priority { get; private set; } = TicketPriority.Normal;
    public TicketStatus Status { get; private set; } = TicketStatus.New;
    public Guid? AssignedAgentId { get; private set; }
    public TenantId TenantId { get; private set; } = TenantId.None;
    public BranchId BranchId { get; private set; } = new(Guid.Empty);
    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);
    /// <summary>Set by Story 18 (first agent reply). Never set in Story 15.</summary>
    public DateTimeOffset? FirstRespondedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    /// <summary>Bumped by every activity append, so the root row (and its rowversion check) is always in the UPDATE -
    /// Customer.Revision's reasoning. Do not remove.</summary>
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public string? DeletedBy { get; private set; }
    public IReadOnlyCollection<TicketActivity> Activities => _activities.AsReadOnly();

    private Ticket()
    {
        Number = null!;
        Subject = string.Empty;
        Description = string.Empty;
    }

    private Ticket(
        TicketId id, TicketNumber number, string subject, string description, Guid customerId, Guid? contactId,
        TicketChannel channel, TicketPriority priority, TenantId tenantId, BranchId branchId, DepartmentId departmentId) : base(id)
    {
        Number = number;
        Subject = subject;
        Description = description;
        CustomerId = customerId;
        ContactId = contactId;
        Channel = channel;
        Priority = priority;
        TenantId = tenantId;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary><paramref name="category"/> is read, not mutated (the Department.Create(Branch) precedent).</summary>
    public static Ticket Create(
        TicketNumber number, string subject, string description, Guid customerId, Guid? contactId, TicketChannel channel,
        Category? category, TicketPriority priority, TenantId tenantId, BranchId branchId, DepartmentId departmentId,
        Guid actorId, DateTimeOffset nowUtc, TicketId? id = null)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(departmentId);

        if (tenantId == TenantId.None) throw new DomainException(TicketDomainErrors.ScopeRequired);
        if (customerId == Guid.Empty) throw new DomainException(TicketDomainErrors.CustomerRequired);
        EnsureActor(actorId);

        var ticket = new Ticket(
            id ?? TicketId.New(), number, NormaliseSubject(subject), NormaliseDescription(description), customerId,
            contactId is { } c && c != Guid.Empty ? c : null, EnsureDefined(channel), EnsureDefined(priority),
            tenantId, branchId, departmentId);

        if (category is not null)
        {
            ticket.EnsureCategoryFits(category);
            ticket.CategoryId = category.Id;
        }

        ticket.Record(TicketActivityKind.Created, actorId, null, TicketStatus.New.ToString(), nowUtc);
        ticket.Raise(new TicketCreated(ticket.Id, nowUtc));
        return ticket;
    }

    public bool IsAllowedInCurrentStatus(TicketAction action) =>
        StatusTransitions.TryGetValue(action, out StatusTransition? transition)
            ? transition.From.Contains(Status)
            : WorkingStatuses.Contains(Status);

    public bool CanPerform(TicketAction action) => action switch
    {
        TicketAction.Claim => AssignedAgentId is null && IsAllowedInCurrentStatus(action),
        TicketAction.Unassign => AssignedAgentId is not null && IsAllowedInCurrentStatus(action),
        _ => IsAllowedInCurrentStatus(action),
    };

    /// <summary>Derived from the same table - what Story 20's UI offers (before permission filtering).</summary>
    public IReadOnlyList<TicketAction> AllowedActions() => [.. Enum.GetValues<TicketAction>().Where(CanPerform)];

    public void Open(Guid actorId, DateTimeOffset nowUtc) => ChangeStatus(TicketAction.Open, TicketActivityKind.Opened, actorId, nowUtc);

    public void AwaitCustomer(Guid actorId, DateTimeOffset nowUtc) => ChangeStatus(TicketAction.AwaitCustomer, TicketActivityKind.AwaitingCustomer, actorId, nowUtc);

    public void PutOnHold(Guid actorId, DateTimeOffset nowUtc) => ChangeStatus(TicketAction.PutOnHold, TicketActivityKind.PutOnHold, actorId, nowUtc);

    public void Resolve(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Resolve, TicketActivityKind.Resolved, actorId, nowUtc);
        ResolvedAtUtc = nowUtc;
    }

    public void Close(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Close, TicketActivityKind.Closed, actorId, nowUtc);
        ClosedAtUtc = nowUtc;
    }

    public void Reopen(Guid actorId, DateTimeOffset nowUtc)
    {
        ChangeStatus(TicketAction.Reopen, TicketActivityKind.Reopened, actorId, nowUtc);
        ResolvedAtUtc = null;
        ClosedAtUtc = null;
    }

    public void Cancel(Guid actorId, DateTimeOffset nowUtc) => ChangeStatus(TicketAction.Cancel, TicketActivityKind.Cancelled, actorId, nowUtc);

    public void AssignTo(Guid agentId, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Assign);
        if (agentId == Guid.Empty) throw new DomainException(TicketDomainErrors.AssigneeRequired);
        if (AssignedAgentId == agentId) throw new DomainException(TicketDomainErrors.AssigneeUnchanged);

        Guid? previous = AssignedAgentId;
        AssignedAgentId = agentId;
        Record(TicketActivityKind.Assigned, actorId, previous?.ToString(), agentId.ToString(), nowUtc);
        Raise(new TicketAssignmentChanged(Id, previous, agentId, nowUtc));
    }

    /// <summary>The caller takes an unassigned ticket. Two concurrent claims are settled by the rowversion; the
    /// handler turns both refusals into 409 Tickets.AlreadyClaimed.</summary>
    public void Claim(Guid agentId, DateTimeOffset nowUtc)
    {
        EnsureActor(agentId);
        EnsureAllowed(TicketAction.Claim);
        if (AssignedAgentId is not null) throw new DomainException(TicketDomainErrors.AlreadyAssigned);

        AssignedAgentId = agentId;
        Record(TicketActivityKind.Claimed, agentId, null, agentId.ToString(), nowUtc);
        Raise(new TicketAssignmentChanged(Id, null, agentId, nowUtc));
    }

    public void Unassign(Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Unassign);
        if (AssignedAgentId is not { } previous) throw new DomainException(TicketDomainErrors.NotAssigned);

        AssignedAgentId = null;
        Record(TicketActivityKind.Unassigned, actorId, previous.ToString(), null, nowUtc);
        Raise(new TicketAssignmentChanged(Id, previous, null, nowUtc));
    }

    /// <summary>The only method that changes DepartmentId/BranchId (ITransferableOrganisationScoped contract). The
    /// receiving department triages it: assignee and category are cleared; the activity history stays.</summary>
    public void TransferTo(DepartmentId departmentId, BranchId branchId, Guid actorId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(departmentId);
        ArgumentNullException.ThrowIfNull(branchId);
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.Transfer);
        if (departmentId.Equals(DepartmentId)) throw new DomainException(TicketDomainErrors.TransferToSameDepartment);

        DepartmentId previous = DepartmentId;
        DepartmentId = departmentId;
        BranchId = branchId;
        AssignedAgentId = null;
        CategoryId = null;
        Record(TicketActivityKind.Transferred, actorId, previous.Value.ToString(), departmentId.Value.ToString(), nowUtc);
        Raise(new TicketTransferred(Id, previous, departmentId, nowUtc));
    }

    public void ChangePriority(TicketPriority priority, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.ChangePriority);
        TicketPriority next = EnsureDefined(priority);
        if (next == Priority) throw new DomainException(TicketDomainErrors.PriorityUnchanged);

        TicketPriority previous = Priority;
        Priority = next;
        Record(TicketActivityKind.PriorityChanged, actorId, previous.ToString(), next.ToString(), nowUtc);
        Raise(new TicketPriorityChanged(Id, previous, next, nowUtc));
    }

    public void ChangeCategory(Category category, Guid actorId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(category);
        EnsureActor(actorId);
        EnsureAllowed(TicketAction.ChangeCategory);
        if (category.Id == CategoryId) throw new DomainException(TicketDomainErrors.CategoryUnchanged);
        EnsureCategoryFits(category);

        CategoryId? previous = CategoryId;
        CategoryId = category.Id;
        Record(TicketActivityKind.CategoryChanged, actorId, previous?.Value.ToString(), category.Id.Value.ToString(), nowUtc);
        Raise(new TicketCategoryChanged(Id, previous, category.Id, nowUtc));
    }

    private void ChangeStatus(TicketAction action, TicketActivityKind kind, Guid actorId, DateTimeOffset nowUtc)
    {
        EnsureActor(actorId);
        EnsureAllowed(action);

        TicketStatus previous = Status;
        Status = StatusTransitions[action].To;
        Record(kind, actorId, previous.ToString(), Status.ToString(), nowUtc);
        Raise(new TicketStatusChanged(Id, previous, Status, nowUtc));
    }

    private void EnsureAllowed(TicketAction action)
    {
        if (!IsAllowedInCurrentStatus(action))
        {
            throw new DomainException(TicketDomainErrors.TransitionNotAllowed(action, Status));
        }
    }

    private void EnsureCategoryFits(Category category)
    {
        if (!category.IsActive) throw new DomainException(TicketDomainErrors.CategoryInactive);
        if (!category.TenantId.Equals(TenantId) || !category.DepartmentId.Equals(DepartmentId))
        {
            throw new DomainException(TicketDomainErrors.CategoryDepartmentMismatch);
        }
    }

    private void Record(TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset nowUtc)
    {
        _activities.Add(new TicketActivity(Guid.NewGuid(), kind, actorId, from, to, nowUtc));
        Revision++;
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty) throw new DomainException(TicketDomainErrors.ActorRequired);
    }

    private static string NormaliseSubject(string subject) { /* ThrowIfNull, Trim, empty -> SubjectRequired, > SubjectMaxLength -> SubjectTooLong */ }

    private static string NormaliseDescription(string description) { /* same with DescriptionRequired / DescriptionTooLong */ }

    private static TicketChannel EnsureDefined(TicketChannel channel) =>
        Enum.IsDefined(channel) ? channel : throw new DomainException(TicketDomainErrors.ChannelUnsupported);

    private static TicketPriority EnsureDefined(TicketPriority priority) =>
        Enum.IsDefined(priority) ? priority : throw new DomainException(TicketDomainErrors.PriorityUnsupported);
}
```

Write the two `Normalise…` bodies out (shape of `Contact.Change`'s name checks) and use braces on every `if` (the
repo style — compressed above for length). Subject and description are not editable in this story.

### B5 — Application scaffolding (`Crm.Modules.Tickets.Application`)

**Create files:**

- `AssemblyReference.cs`, `ITicketsUnitOfWork.cs` — copies of the Customers ones.
- `Abstractions/ITicketNumberGenerator.cs` — `Task<TicketNumber> NextAsync(CancellationToken cancellationToken = default);`
  (summary as Customers').
- `Abstractions/ITicketReadModel.cs`:

  ```csharp
  /// <summary>Queue and search projections (CLAUDE.md: queries project straight to DTOs). The specification supplies
  /// criteria, order and page; the implementation projects with .Select, so the owned activities are never loaded.
  /// Story 06's filters still apply - the implementation queries the module DbContext.</summary>
  public interface ITicketReadModel
  {
      Task<IReadOnlyList<TicketListRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default);
  }

  public sealed record TicketListRow(
      Guid Id, string Number, string Subject, TicketStatus Status, TicketPriority Priority, TicketChannel Channel,
      Guid? CategoryId, Guid CustomerId, Guid? ContactId, Guid? AssignedAgentId, Guid BranchId, Guid DepartmentId,
      DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc);
  ```

- `CurrentActor.cs` — `internal static class CurrentActor { public static Guid Id(ICurrentUserService user) =>
  Guid.TryParse(user.UserId, CultureInfo.InvariantCulture, out Guid id) ? id : Guid.Empty; }` (Guid.Empty → 422
  `Tickets.ActorRequired` from the aggregate; `AddNoteCommandHandler.cs` line 25 precedent).
- `TicketsErrors.cs` — `internal static class`, shape of `CustomersErrors.cs`: `TicketNotFound(Guid)`,
  `CategoryNotFound(Guid)` (NotFound); `DepartmentOutOfScope()`, `DepartmentRequired()` (verbatim from
  `CustomersErrors.cs` lines 16–20); `Error.Domain(...)` factories `CustomerInactive()`,
  `ContactNotOfCustomer(Guid contactId)`, `AssigneeInactive(Guid)`, `AssigneeNotInDepartment(Guid)`,
  `TargetDepartmentInactive()`, `CategoryDepartmentInactive()` with the codes in Product rules;
  `AlreadyClaimed(string detail) => Error.Conflict("Tickets.AlreadyClaimed", detail)`;
  `TransitionNotAllowed(string detail) => Error.Domain("Tickets.TransitionNotAllowed", detail)`.
- `Localization/TicketsMessageKeys.cs`:

  ```csharp
  public static class TicketsMessageKeys
  {
      public static class Validation
      {
          public const string QueueUnsupported = "Tickets.Validation.QueueUnsupported";
          public const string SortUnsupported = "Tickets.Validation.SortUnsupported";
          public const string StatusUnsupported = "Tickets.Validation.StatusUnsupported";
          public const string PriorityUnsupported = "Tickets.Validation.PriorityUnsupported";
          public const string ActionUnsupported = "Tickets.Validation.ActionUnsupported";
      }

      public static class Refusal
      {
          public const string TransitionNotAllowed = "Tickets.Refusal.TransitionNotAllowed";
          public const string AlreadyClaimed = "Tickets.Refusal.AlreadyClaimed";
      }

      public static class Status
      {
          public const string New = "Tickets.Status.New";
          public const string Open = "Tickets.Status.Open";
          public const string PendingCustomer = "Tickets.Status.PendingCustomer";
          public const string OnHold = "Tickets.Status.OnHold";
          public const string Resolved = "Tickets.Status.Resolved";
          public const string Closed = "Tickets.Status.Closed";
          public const string Cancelled = "Tickets.Status.Cancelled";
      }
  }
  ```

- `Localization/Resources/TicketsMessages.resx` and `TicketsMessages.ar.resx` (**Write tool**; header copied from
  `CustomersMessages.ar.resx`):

  | Key | en | ar |
  |---|---|---|
  | `Tickets.Validation.QueueUnsupported` | Queue must be one of mine, unassigned, all. | يجب أن تكون قائمة الانتظار إحدى القيم mine أو unassigned أو all. |
  | `Tickets.Validation.SortUnsupported` | Sort must be one of createdAt, modifiedAt, priority, number. | يجب أن يكون الترتيب أحد القيم createdAt أو modifiedAt أو priority أو number. |
  | `Tickets.Validation.StatusUnsupported` | Unknown ticket status. | حالة التذكرة غير معروفة. |
  | `Tickets.Validation.PriorityUnsupported` | Priority must be Low, Normal, High or Urgent. | يجب أن تكون الأولوية Low أو Normal أو High أو Urgent. |
  | `Tickets.Validation.ActionUnsupported` | Action must be one of Open, AwaitCustomer, PutOnHold, Resolve, Close, Reopen, Cancel. | يجب أن يكون الإجراء أحد القيم Open أو AwaitCustomer أو PutOnHold أو Resolve أو Close أو Reopen أو Cancel. |
  | `Tickets.Refusal.TransitionNotAllowed` | The ticket is {0}; that action is not allowed in this status. | حالة التذكرة «{0}»، ولا يُسمح بهذا الإجراء في هذه الحالة. |
  | `Tickets.Refusal.AlreadyClaimed` | This ticket is already claimed by {0}. | استلم {0} هذه التذكرة بالفعل. |
  | `Tickets.Status.New` | New | جديدة |
  | `Tickets.Status.Open` | Open | مفتوحة |
  | `Tickets.Status.PendingCustomer` | Pending customer | بانتظار العميل |
  | `Tickets.Status.OnHold` | On hold | معلّقة |
  | `Tickets.Status.Resolved` | Resolved | محلولة |
  | `Tickets.Status.Closed` | Closed | مغلقة |
  | `Tickets.Status.Cancelled` | Cancelled | ملغاة |

- `TicketRefusals.cs` — the localised refusals (R10):

  ```csharp
  /// <summary>Asks the aggregate's transition table BEFORE the domain call, so an illegal action is a 422 whose
  /// detail names the current status in the request's language. The aggregate throws the same code (English) as the
  /// backstop if a handler forgets to ask.</summary>
  internal static class TicketRefusals
  {
      public static Error? NotAllowed(Ticket ticket, TicketAction action, ILocalizationService localization) =>
          ticket.IsAllowedInCurrentStatus(action)
              ? null
              : TicketsErrors.TransitionNotAllowed(string.Format(
                  CultureInfo.CurrentCulture, localization.Get(TicketsMessageKeys.Refusal.TransitionNotAllowed),
                  StatusName(ticket.Status, localization)));

      public static Error AlreadyClaimed(string assigneeName, ILocalizationService localization) =>
          TicketsErrors.AlreadyClaimed(string.Format(
              CultureInfo.CurrentCulture, localization.Get(TicketsMessageKeys.Refusal.AlreadyClaimed), assigneeName));

      private static string StatusName(TicketStatus status, ILocalizationService localization) => localization.Get(status switch
      {
          TicketStatus.New => TicketsMessageKeys.Status.New,
          TicketStatus.Open => TicketsMessageKeys.Status.Open,
          TicketStatus.PendingCustomer => TicketsMessageKeys.Status.PendingCustomer,
          TicketStatus.OnHold => TicketsMessageKeys.Status.OnHold,
          TicketStatus.Resolved => TicketsMessageKeys.Status.Resolved,
          TicketStatus.Closed => TicketsMessageKeys.Status.Closed,
          _ => TicketsMessageKeys.Status.Cancelled,
      });
  }
  ```

- `Validation/TicketsRules.cs` — shape of `CustomersRules.cs`: `SupportedQueue`, `SupportedSort` (via the parsers in
  `Specifications/`), `SupportedStatus` (`Enum.GetNames<TicketStatus>()` ignore-case), `SupportedPriority`
  (`Enum.GetNames<TicketPriority>()`), `SupportedTransitionAction` (the seven status-action names of `TicketAction`,
  ignore-case), each with its `TicketsMessageKeys.Validation.*` message.

**Create file: `Dtos.cs`** — primitives only:

```csharp
public sealed record TicketCategoryNameDto(string Ar, string En)
{
    public static TicketCategoryNameDto FromDomain(LocalizedText name) => new(name.Arabic, name.English);
}

/// <summary>The admin row and the picker item.</summary>
public sealed record TicketCategoryDto(
    Guid Id, Guid DepartmentId, TicketCategoryNameDto Name, int DisplayOrder, bool IsCustomerVisible, bool IsActive, byte[] RowVersion)
{
    public static TicketCategoryDto FromDomain(Category c) => new(
        c.Id.Value, c.DepartmentId.Value, TicketCategoryNameDto.FromDomain(c.Name), c.DisplayOrder, c.IsCustomerVisible, c.IsActive, c.RowVersion);
}

/// <summary>What a ticket shows of its category - also when the category was deactivated later.</summary>
public sealed record TicketCategoryRefDto(Guid Id, TicketCategoryNameDto Name, bool IsActive);

public sealed record TicketPersonDto(Guid Id, string DisplayName);

public sealed record TicketActivityDto(Guid Id, string Kind, Guid ActorId, string? From, string? To, DateTimeOffset OccurredAtUtc);

public sealed record TicketDto(
    Guid Id, string Number, string Subject, string Description, string Status, string Priority, string Channel,
    Guid CustomerId, Guid? ContactId, TicketCategoryRefDto? Category, Guid? AssignedAgentId, string? AssignedAgentName,
    Guid TenantId, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, string? CreatedBy, DateTimeOffset? ModifiedAtUtc, DateTimeOffset? FirstRespondedAtUtc,
    DateTimeOffset? ResolvedAtUtc, DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<string> AllowedActions, IReadOnlyList<TicketActivityDto> Activities, IReadOnlyList<TicketPersonDto> People,
    byte[] RowVersion);

public sealed record TicketListItemDto(
    Guid Id, string Number, string Subject, string Status, string Priority, string Channel, Guid CustomerId, Guid? ContactId,
    TicketCategoryRefDto? Category, Guid? AssignedAgentId, string? AssignedAgentName, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc);

public sealed record TicketQueueCountsDto(int Mine, int Unassigned, int All);
```

**Create file: `TicketDtoAssembler.cs`** — `public sealed class TicketDtoAssembler(IRepository<Category, CategoryId>
categories, IIdentityModuleApi identity)`, registered scoped (B7). Nested module calls live here, so **every handler
calls it before `eventBus.Publish`** (R2).

- `Task<TicketDto> ToDtoAsync(Ticket ticket, CancellationToken ct)`: category = `ticket.CategoryId is { } id ? await
  categories.GetByIdAsync(id, ct) : null` → `TicketCategoryRefDto`; people ids = assignee ∪ every activity `ActorId` ∪
  every `From`/`To` of `Assigned`/`Claimed`/`Unassigned` activities that `Guid.TryParse`s; one
  `identity.GetStaffUsersAsync(ids, ct)` (a failed Result → empty list, never a failure of the ticket read);
  `AssignedAgentName` from that list; `AllowedActions = [.. ticket.AllowedActions().Select(a => a.ToString())]`;
  activities ordered by `OccurredAtUtc` then insertion (oldest first); enums `.ToString()`; ids `.Value`.
- `Task<IReadOnlyList<TicketListItemDto>> ToListItemsAsync(IReadOnlyList<TicketListRow> rows, CancellationToken ct)`:
  one `categories.ListAsync(new CategoriesByIdsSpecification([...distinct ids]))`, one identity call for the distinct
  assignees, then map.

**Create folder `Specifications/`:**

- `TicketByIdSpecification.cs` — `Where(t => t.Id == id); AsReadOnly();`.
- `TicketQueue.cs` — `public enum TicketQueue { All, Mine, Unassigned }` + `TicketQueueParser.TryParse` (`all`, `mine`,
  `unassigned`, ordinal ignore-case).
- `TicketSort.cs` — `public enum TicketSort { CreatedAt, ModifiedAt, Priority, Number }` + `TicketSortParser.TryParse`
  (`createdAt`, `modifiedAt`, `priority`, `number`).
- `TicketSearchSpecification.cs`:

  ```csharp
  /// <summary>Queues and search. No scope criteria on purpose - Story 06's filter adds tenant/branch/department.
  /// Paging only when both page arguments are given (the count passes neither).</summary>
  public sealed class TicketSearchSpecification : Specification<Ticket>
  {
      public static readonly IReadOnlyList<TicketStatus> WorkingStatuses =
          [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

      public TicketSearchSpecification(
          TicketQueue queue, Guid currentUserId, string? search, IReadOnlyList<TicketStatus> statuses,
          IReadOnlyList<TicketPriority> priorities, Guid? categoryId, Guid? assigneeId, Guid? departmentId, Guid? branchId,
          Guid? customerId, TicketSort sort, bool descending, int? page, int? pageSize)
      {
          ArgumentNullException.ThrowIfNull(statuses);
          ArgumentNullException.ThrowIfNull(priorities);

          string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
          IReadOnlyList<TicketStatus> effectiveStatuses = statuses.Count == 0 && queue != TicketQueue.All ? WorkingStatuses : statuses;
          bool anyStatus = effectiveStatuses.Count == 0;
          bool anyPriority = priorities.Count == 0;
          bool mine = queue == TicketQueue.Mine;
          bool unassigned = queue == TicketQueue.Unassigned;
          Guid? me = currentUserId;
          CategoryId? category = categoryId is { } c ? new CategoryId(c) : null;
          DepartmentId? department = departmentId is { } d ? new DepartmentId(d) : null;
          BranchId? branch = branchId is { } b ? new BranchId(b) : null;

          // One combined Where - Specification.Where replaces (DepartmentsSpecification precedent).
          Where(t =>
              (term == null || t.Number.Value.Contains(term) || t.Subject.Contains(term))
              && (anyStatus || effectiveStatuses.Contains(t.Status))
              && (anyPriority || priorities.Contains(t.Priority))
              && (!mine || t.AssignedAgentId == me)
              && (!unassigned || t.AssignedAgentId == null)
              && (assigneeId == null || t.AssignedAgentId == assigneeId)
              && (category == null || t.CategoryId == category)
              && (department == null || t.DepartmentId.Equals(department))
              && (branch == null || t.BranchId.Equals(branch))
              && (customerId == null || t.CustomerId == customerId));

          Expression<Func<Ticket, object>> key = sort switch
          {
              TicketSort.ModifiedAt => t => t.ModifiedAtUtc ?? DateTimeOffset.MinValue,
              TicketSort.Priority => t => t.Priority,
              TicketSort.Number => t => t.Number.Value,
              _ => t => t.CreatedAtUtc ?? DateTimeOffset.MinValue,
          };

          if (descending) { ApplyOrderByDescending(key); } else { ApplyOrderBy(key); }
          ApplyThenBy(t => t.Number.Value);

          if (page is { } p && pageSize is { } size) { ApplyPaging((p - 1) * size, size); }

          AsReadOnly();
      }
  }
  ```

  (Prove R5, R6, R7 against this.)
- `CategoriesByIdsSpecification.cs` — `(IReadOnlyList<CategoryId> ids)`: `Where(c => ids.Contains(c.Id)); AsReadOnly();`.
- `CategoriesSpecification.cs` — `(IReadOnlyList<DepartmentId>? departments, bool activeOnly, bool customerVisibleOnly)`:
  `Where(c => (departments == null || departments.Contains(c.DepartmentId)) && (!activeOnly || c.IsActive) && (!customerVisibleOnly || c.IsCustomerVisible));`
  `ApplyOrderBy(c => c.DisplayOrder); ApplyThenBy(c => c.Name.English); AsReadOnly();`.

### B6 — Application use cases

Every ticket command: `[RequirePermission(...)]` as listed, `ICommand<TicketDto>`. Handlers inject
`IRepository<Ticket, TicketId> tickets`, `ITicketsUnitOfWork unitOfWork`, `ICurrentUserService currentUser`,
`IDateTime dateTime`, `IEventBus eventBus`, `TicketDtoAssembler assembler`, `ILocalizationService localization`, plus
what the table says. **The standard shape** (every row marked "std"):

```csharp
Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
if (ticket is null)
{
    return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
}

tickets.SetOriginalRowVersion(ticket, request.RowVersion);   // not in Claim

if (TicketRefusals.NotAllowed(ticket, TicketAction.X, localization) is { } refusal)
{
    return Result.Failure<TicketDto>(refusal);
}

// (any nested module read the row lists) ... then the domain call:
ticket.X(..., CurrentActor.Id(currentUser), dateTime.UtcNow);

await unitOfWork.SaveChangesAsync(cancellationToken);
TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);   // nested query - BEFORE Publish (R2)
eventBus.Publish(new …IntegrationEvent(Guid.NewGuid(), dateTime.UtcNow, …));   // last statement before return
return Result.Success(dto);
```

`GetByIdAsync` throws for an existing out-of-scope ticket → `OrganisationScopeBehavior` → 403. Capture the "from"
values (status, assignee, department, branch, priority, category) into locals before the domain call for the event.

| Folder / record | Permission | Fields | Specifics |
|---|---|---|---|
| `CreateTicket/CreateTicketCommand` | `TicketsWrite` | `Guid CustomerId, Guid? ContactId, string Subject, string Description, string? Priority, Guid? CategoryId, Guid? DepartmentId` | see below |
| `TransitionTicket/TransitionTicketCommand` | `TicketsWrite` | `Guid Id, string Action, byte[] RowVersion` | std; `TicketAction action = Enum.Parse<TicketAction>(Action, true)`; refusal for `action`; `switch` to `Open`/`AwaitCustomer`/`PutOnHold`/`Resolve`/`Close`/`Reopen`/`Cancel`; publish `TicketStatusChangedIntegrationEvent(from, ticket.Status)` |
| `ClaimTicket/ClaimTicketCommand` | `TicketsWrite` | `Guid Id` | see below |
| `AssignTicket/AssignTicketCommand` | `TicketsAssign` | `Guid Id, Guid AgentId, byte[] RowVersion` | std + inject `IOrganisationModuleApi organisation`, `IIdentityModuleApi identity`; after the refusal: `identity.GetStaffUsersAsync([AgentId])` → no active match → `AssigneeInactive` (unknown id falls through to the next check); `organisation.IsDepartmentMemberAsync(AgentId, ticket.DepartmentId.Value)` false/failed → `AssigneeNotInDepartment`; then `AssignTo`; publish `TicketAssignedIntegrationEvent` |
| `UnassignTicket/UnassignTicketCommand` | `TicketsAssign` | `Guid Id, byte[] RowVersion` | std; `Unassign`; publish `TicketAssignedIntegrationEvent(previous, null)` |
| `TransferTicket/TransferTicketCommand` | `TicketsAssign` | `Guid Id, Guid DepartmentId, byte[] RowVersion` | std + `IOrganisationModuleApi`; after the refusal: `GetDepartmentAsync(DepartmentId)` failure → pass it through (`Result.Failure<TicketDto>(result.Error)`); `!IsActive` → `TargetDepartmentInactive`; `TransferTo(new DepartmentId(d.Id), new BranchId(d.BranchId), …)`; save (R1); publish `TicketTransferredIntegrationEvent` |
| `ChangeTicketPriority/ChangeTicketPriorityCommand` | `TicketsWrite` | `Guid Id, string Priority, byte[] RowVersion` | std; `Enum.Parse<TicketPriority>(Priority, true)`; publish `TicketPriorityChangedIntegrationEvent` |
| `ChangeTicketCategory/ChangeTicketCategoryCommand` | `TicketsWrite` | `Guid Id, Guid CategoryId, byte[] RowVersion` | std + `IRepository<Category, CategoryId> categories`; `GetByIdAsync` null → `CategoryNotFound`; `ChangeCategory(category, …)`; publish `TicketCategoryChangedIntegrationEvent` |

**`CreateTicketCommandHandler`** (injects the standard set + `IRepository<Category, CategoryId> categories`,
`ICustomersModuleApi customers`, `ITicketNumberGenerator numbers`):

```csharp
ScopeWriteTarget? target = currentUser.Scope.ResolveWriteTarget(request.DepartmentId);
if (target is null)
{
    return Result.Failure<TicketDto>(request.DepartmentId is not null ? TicketsErrors.DepartmentOutOfScope() : TicketsErrors.DepartmentRequired());
}

Result<CustomerSummary> customer = await customers.GetCustomerSummaryAsync(request.CustomerId, cancellationToken);
if (customer.IsFailure)
{
    return Result.Failure<TicketDto>(customer.Error);            // 403 Organisation.OutOfScope / 404 Customers.CustomerNotFound
}

if (!customer.Value.IsActive)
{
    return Result.Failure<TicketDto>(TicketsErrors.CustomerInactive());
}

if (request.ContactId is { } contactId && !customer.Value.Contacts.Any(c => c.Id == contactId))
{
    return Result.Failure<TicketDto>(TicketsErrors.ContactNotOfCustomer(contactId));
}

Category? category = null;
if (request.CategoryId is { } categoryId)
{
    category = await categories.GetByIdAsync(new CategoryId(categoryId), cancellationToken);
    if (category is null)
    {
        return Result.Failure<TicketDto>(TicketsErrors.CategoryNotFound(categoryId));
    }
}

TicketPriority priority = request.Priority is null ? TicketPriority.Normal : Enum.Parse<TicketPriority>(request.Priority, ignoreCase: true);
TicketNumber number = await numbers.NextAsync(cancellationToken);
Ticket ticket = Ticket.Create(
    number, request.Subject, request.Description, request.CustomerId, request.ContactId, TicketChannel.Agent, category,
    priority, target.TenantId, target.BranchId, target.DepartmentId, CurrentActor.Id(currentUser), dateTime.UtcNow);

tickets.Add(ticket);
await unitOfWork.SaveChangesAsync(cancellationToken);
TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);
eventBus.Publish(new TicketCreatedIntegrationEvent(/* all fields from ticket */));
return Result.Success(dto);
```

**`ClaimTicketCommandHandler`** (standard set + `IIdentityModuleApi identity`, `ILogger<ClaimTicketCommandHandler>`;
`public sealed partial class` for the `[LoggerMessage]`):

```csharp
Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
if (ticket is null) { return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id)); }

if (ticket.AssignedAgentId is { } assignee)
{
    return Result.Failure<TicketDto>(TicketRefusals.AlreadyClaimed(await NameOfAsync(assignee, cancellationToken), localization));
}

if (TicketRefusals.NotAllowed(ticket, TicketAction.Claim, localization) is { } refusal) { return Result.Failure<TicketDto>(refusal); }

Guid me = CurrentActor.Id(currentUser);
ticket.Claim(me, dateTime.UtcNow);

try
{
    await unitOfWork.SaveChangesAsync(cancellationToken);   // WHERE [RowVersion] = <loaded> (R3)
}
catch (ConcurrencyConflictException)
{
    Ticket? current = await tickets.FirstOrDefaultAsync(new TicketByIdSpecification(ticket.Id), cancellationToken);   // no-tracking re-read
    if (current?.AssignedAgentId is not { } winner)
    {
        throw;   // some other change won - TransactionBehavior answers the generic 409
    }

    LogClaimLostRace(ticket.Number.Value, winner);
    return Result.Failure<TicketDto>(TicketRefusals.AlreadyClaimed(await NameOfAsync(winner, cancellationToken), localization));
}

TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);
eventBus.Publish(new TicketAssignedIntegrationEvent(Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value,
    null, me, me, ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));
return Result.Success(dto);
```

`NameOfAsync(Guid id, ct)` = the `DisplayName` from `identity.GetStaffUsersAsync([id], ct)`, falling back to
`id.ToString()`. `[LoggerMessage(Level = LogLevel.Information, Message = "Claim of ticket {TicketNumber} lost the race to {WinnerId}")] private partial void LogClaimLostRace(string ticketNumber, Guid winnerId);`.
The failed Result rolls back the half-written activity INSERT (`TransactionBehavior.cs` lines 53–58).

**Category commands** — `[RequirePermission(CrmPermissions.AdminOrganisation)]`, `ICommand<TicketCategoryDto>`,
handlers inject `IRepository<Category, CategoryId>`, `ITicketsUnitOfWork`; no events:

| Folder / record | Fields | Handler |
|---|---|---|
| `CreateCategory/CreateCategoryCommand` | `Guid DepartmentId, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible` | + `IOrganisationModuleApi`; `GetDepartmentAsync` failure → pass through; `!IsActive` → `CategoryDepartmentInactive`; `Category.Create(new TenantId(d.TenantId), new DepartmentId(d.Id), LocalizedText.Create(NameAr, NameEn), DisplayOrder, IsCustomerVisible)`; add, save |
| `UpdateCategory/UpdateCategoryCommand` | `Guid Id, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible, byte[] RowVersion` | load (null → `CategoryNotFound`), `SetOriginalRowVersion`, `Update`, save |
| `DeactivateCategory/DeactivateCategoryCommand` | `Guid Id, byte[] RowVersion` | load, RV, `Deactivate`, save |
| `ReactivateCategory/ReactivateCategoryCommand` | `Guid Id, byte[] RowVersion` | load, RV, `Reactivate`, save |

**Queries** — each `IRequest<Result<T>>`:

| Folder / record | Permission | Returns | Handler |
|---|---|---|---|
| `GetTicketById/GetTicketByIdQuery(Guid Id)` | `TicketsRead` | `TicketDto` | `tickets.FirstOrDefaultAsync(new TicketByIdSpecification(...))` → null → `TicketNotFound`; `assembler.ToDtoAsync` |
| `SearchTickets/SearchTicketsQuery(string? Queue, string? Search, IReadOnlyList<string>? Statuses, IReadOnlyList<string>? Priorities, Guid? CategoryId, Guid? AssigneeId, Guid? DepartmentId, Guid? BranchId, Guid? CustomerId, string? Sort, bool Descending = true, int Page = 1, int PageSize = 20)` | `TicketsRead` | `PagedResult<TicketListItemDto>` | parse queue/sort (null → `All`/`CreatedAt`), statuses/priorities with `Enum.Parse(…, true)`; `readModel.ListAsync(spec with page)` + `tickets.CountAsync(spec without page)`; `assembler.ToListItemsAsync` (ListUsers/SearchCustomers shape) |
| `GetTicketQueueCounts/GetTicketQueueCountsQuery` | `TicketsRead` | `TicketQueueCountsDto` | three `tickets.CountAsync(new TicketSearchSpecification(queue, me, null, [], [], null, …, TicketSort.CreatedAt, true, null, null))` for `Mine`, `Unassigned`, `All` |
| `ListTicketCategories/ListTicketCategoriesQuery(Guid? DepartmentId)` (the agent picker) | `TicketsRead` | `IReadOnlyList<TicketCategoryDto>` | `departments = DepartmentId is { } d ? [new DepartmentId(d)] : currentUser.Scope.Departments`; `CategoriesSpecification(departments, activeOnly: true, customerVisibleOnly: false)` |
| `ListCategories/ListCategoriesQuery(Guid? DepartmentId, bool ActiveOnly)` (admin) | `AdminOrganisation` | `IReadOnlyList<TicketCategoryDto>` | `CategoriesSpecification(DepartmentId is { } d ? [new DepartmentId(d)] : null, ActiveOnly, false)` |
| `GetCategory/GetCategoryQuery(Guid Id)` (admin) | `AdminOrganisation` | `TicketCategoryDto` | `GetByIdAsync` → null → `CategoryNotFound` |

**Validators** (built-in FluentValidation messages follow `Accept-Language`; custom ones use `TicketsMessageKeys`):

- `CreateTicketCommandValidator(ILocalizationService)`: `CustomerId` NotEmpty; `Subject` NotEmpty, MaximumLength(200);
  `Description` NotEmpty, MaximumLength(4000); `Priority` `SupportedPriority` when not null.
- `TransitionTicketCommandValidator`: `Id`, `RowVersion` NotEmpty; `Action` NotEmpty + `SupportedTransitionAction`.
- `ClaimTicketCommandValidator`: `Id` NotEmpty. `AssignTicketCommandValidator`: `Id`, `AgentId`, `RowVersion` NotEmpty.
  `UnassignTicketCommandValidator`: `Id`, `RowVersion`. `TransferTicketCommandValidator`: `Id`, `DepartmentId`,
  `RowVersion`. `ChangeTicketPriorityCommandValidator`: + `Priority` NotEmpty + `SupportedPriority`.
  `ChangeTicketCategoryCommandValidator`: `Id`, `CategoryId`, `RowVersion`.
- `CreateCategoryCommandValidator`: `DepartmentId` NotEmpty; `NameAr`, `NameEn` NotEmpty, MaximumLength(200);
  `DisplayOrder` InclusiveBetween(0, 9999). `UpdateCategoryCommandValidator`: + `Id`, `RowVersion`.
  Deactivate/Reactivate: `Id`, `RowVersion`.
- `SearchTicketsQueryValidator(ILocalizationService)`: `Page` ≥ 1, `PageSize` 1–100, `Search` ≤ 200, `Queue`
  `SupportedQueue`, `Sort` `SupportedSort`, `RuleForEach(q => q.Statuses).SupportedStatus(…)`,
  `RuleForEach(q => q.Priorities).SupportedPriority(…)`.
- `GetTicketByIdQueryValidator`, `GetCategoryQueryValidator`: `Id` NotEmpty.

### B7 — Infrastructure (`Crm.Modules.Tickets.Infrastructure`)

**Create file: `Persistence/TicketsDbContext.cs`** — copy `CustomersDbContext.cs` with `SchemaName = "tickets"`,
`TicketNumberSequence = "TicketNumbers"`, `DbSet<Ticket> Tickets`, `DbSet<Category> Categories` (comment: "Aggregate
roots only. TicketActivity has no DbSet - reachable only through Ticket (AC).").
**Create file: `Persistence/TicketsDbContextFactory.cs`** — copy of `CustomersDbContextFactory.cs`.

**Create file: `Persistence/Configurations/TicketConfiguration.cs`**

```csharp
public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(x => x.Id);
        // TenantId/BranchId/DepartmentId: ModuleDbContext convention - never configure them here.
        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new TicketId(value)).ValueGeneratedNever();

        builder.OwnsOne(x => x.Number, number =>
        {
            number.Property(p => p.Value).HasColumnName("Number").HasMaxLength(TicketNumber.MaxLength).IsRequired();
            number.HasIndex(p => p.Value).IsUnique();
        });

        builder.Property(x => x.Subject).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Priority).HasConversion<int>();   // sorts by rank
        builder.Property(x => x.CategoryId).HasConversion<CategoryIdConverter>();   // nullable reference: a class converter (R7)

        builder.OwnsMany(x => x.Activities, activities =>
        {
            activities.ToTable("TicketActivities");
            activities.WithOwner().HasForeignKey("TicketId");
            activities.HasKey(a => a.Id);
            activities.Property(a => a.Id).ValueGeneratedNever();   // R8
            activities.Property(a => a.Kind).HasConversion<string>().HasMaxLength(30);
            activities.Property(a => a.From).HasMaxLength(TicketActivity.ValueMaxLength);
            activities.Property(a => a.To).HasMaxLength(TicketActivity.ValueMaxLength);
        });
        builder.Navigation(x => x.Activities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId, x.Status });
        builder.HasIndex(x => x.AssignedAgentId);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CategoryId);
        // No FK to Categories and none to another module (CLAUDE.md: ids only).
    }

    private sealed class CategoryIdConverter() : ValueConverter<CategoryId, Guid>(v => v.Value, v => new CategoryId(v));
}
```

(If EF cannot instantiate a private nested converter, make it `internal sealed class CategoryIdConverter` at file
level — the `ModuleDbContext.OrganisationScope.cs` lines 89–93 converters are private nested and work.)

**Create file: `Persistence/Configurations/CategoryConfiguration.cs`** — `ToTable("Categories")`; `Id` converted +
`ValueGeneratedNever`; `OwnsOne(Name)` with `NameAr`/`NameEn` nvarchar(200) required (`DepartmentConfiguration.cs` lines
22–26); `IsRowVersion`; `HasIndex(x => new { x.TenantId, x.DepartmentId })`.

**Create file: `Persistence/SqlTicketNumberGenerator.cs`** — copy `SqlCustomerNumberGenerator.cs` with
`SELECT NEXT VALUE FOR [tickets].[TicketNumbers] AS [Value]` and `TicketNumber.FromSequence` (R4).

**Create file: `Persistence/TicketReadModel.cs`**

```csharp
public sealed class TicketReadModel(TicketsDbContext context) : ITicketReadModel
{
    public async Task<IReadOnlyList<TicketListRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.GetQuery(context.Tickets, specification)
            .Select(t => new TicketListRow(
                t.Id.Value, t.Number.Value, t.Subject, t.Status, t.Priority, t.Channel,
                t.CategoryId == null ? null : t.CategoryId.Value,
                t.CustomerId, t.ContactId, t.AssignedAgentId, t.BranchId.Value, t.DepartmentId.Value,
                t.CreatedAtUtc, t.ModifiedAtUtc))
            .ToListAsync(cancellationToken);
}
```

(R5 — prove the SELECT; the nullable-flow in the ternary compiles without `!`. If it does not, use the R5 fallback.)

**Create file: `Persistence/TicketsSeeder.cs`** — `public sealed class TicketsSeeder(TicketsDbContext context,
ITicketNumberGenerator numbers, IDateTime dateTime) : IModuleSeeder` (**Write tool** — Arabic). Categories first, then
tickets; each skipped when its well-known id exists with both named filters lifted (`CustomersSeeder.cs` lines 79–85);
one `SaveChangesAsync` per aggregate; actor `OrganisationDevSeed.AdminUserId`; runs `OrganisationScope.Unrestricted`.

| Category id (`TicketsDevSeed`) | Department | ar / en | Order | Customer-visible | Active |
|---|---|---|---|---|---|
| `RyCareGeneralCategoryId` | RYD-CARE | استفسار عام / General enquiry | 10 | yes | yes |
| `RyCareTechnicalCategoryId` | RYD-CARE | مشكلة تقنية / Technical issue | 20 | yes | yes |
| `RyCareComplaintCategoryId` | RYD-CARE | شكوى / Complaint | 30 | yes | yes |
| `RyCareInternalCategoryId` | RYD-CARE | متابعة داخلية / Internal follow-up | 40 | no | yes |
| `RyCareLegacyCategoryId` | RYD-CARE | تصنيف قديم / Legacy | 90 | no | **no** (`Deactivate()` after create) |
| `RyBillInvoiceCategoryId` | RYD-BILL | استفسار عن فاتورة / Invoice question | 10 | yes | yes |
| `JeCareGeneralCategoryId` | JED-CARE | استفسار عام / General enquiry | 10 | yes | yes |

| Ticket id | Customer / contact | Dept (branch) | Subject | Priority, category | After create |
|---|---|---|---|---|---|
| `AhmedNewTicketId` | `AhmedCustomerId` / `AhmedContactId` | RYD-CARE (Riyadh) | لا أستطيع تسجيل الدخول إلى البوابة | Normal, General | — (New, unassigned) |
| `NajdOpenTicketId` | `NajdLogisticsCustomerId` / `NajdSaraContactId` | RYD-CARE (Riyadh) | Delivery tracking shows the wrong status | High, Technical | `Open(admin)`, `AssignTo(AgentAUserId, admin)` |
| `RedSeaNewTicketId` | `RedSeaTradingCustomerId` / `RedSeaFahadContactId` | JED-CARE (Jeddah) | Invoice total does not match the quote | Urgent, JED General | — |

Descriptions: one sentence each in the subject's language. Domain events raised by the seeder are simply dropped
(nothing dispatches outside `TransactionBehavior`).

**Create file: `TicketsModule.cs`** (shape of `CustomersModule.cs`):

```csharp
services.AddModuleDbContext<TicketsDbContext, ITicketsUnitOfWork>(configuration, Name, TicketsDbContext.SchemaName, ApplicationAssembly);
services.AddRepository<Ticket, TicketId, TicketsDbContext>();
services.AddRepository<Category, CategoryId, TicketsDbContext>();
services.AddScoped<ITicketNumberGenerator, SqlTicketNumberGenerator>();
services.AddScoped<ITicketReadModel, TicketReadModel>();
services.AddScoped<TicketDtoAssembler>();
services.AddScoped<IModuleSeeder, TicketsSeeder>();
services.AddLocalizationResource(new ResourceManager(
    "Crm.Modules.Tickets.Application.Localization.Resources.TicketsMessages", ApplicationAssembly));
```

`Name => "Tickets"`. No Contracts API is registered — Tickets exposes none yet.

### B8 — Migration

`dotnet tool restore`, then `pwsh backend/scripts/ef.ps1 -Module Tickets migrations add Initial`. **Read the generated
`Persistence/Migrations/*_Initial.cs`** (Test Plan row 3): `EnsureSchema("tickets")`;
`CreateSequence<long>("TicketNumbers", schema: "tickets")`; `Tickets` (`Number` nvarchar(20) unique, `Subject`
nvarchar(200), `Description` nvarchar(4000), `CustomerId`, `ContactId` null, `Channel`/`Status` nvarchar(20),
`Priority` int, **`CategoryId` uniqueidentifier null**, `AssignedAgentId` null, `TenantId`/`BranchId`/`DepartmentId`,
`FirstRespondedAtUtc`/`ResolvedAtUtc`/`ClosedAtUtc` null, `Revision` int, `RowVersion` rowversion, soft-delete and audit
columns); `TicketActivities` (`TicketId` FK cascade to `Tickets` — the one owned-collection FK inside the aggregate,
`Kind` nvarchar(30), `ActorId`, `From`/`To` nvarchar(100) null, `OccurredAtUtc`); `Categories` (`NameAr`/`NameEn`,
`DepartmentId`, `DisplayOrder`, `IsCustomerVisible`, `IsActive`, `RowVersion`, audit columns); `outbox_messages`,
`outbox_message_consumers`; the B7 indexes. **No other schema, no FK from `Tickets` to `Categories`.** Never hand-edit —
fix the configuration, `migrations remove`, re-add.

### B9 — Presentation (`Crm.Modules.Tickets.Presentation`)

**Create file: `AssemblyReference.cs`** (copy). Every controller: `[Area(...)]`,
`[ProducesResponseType(401)]`, `[ProducesResponseType(403)]`, actions `ToActionResult(await Sender.Send(...))`, route
ids `{id:guid}`, `[ProducesResponseType<T>(200)]` plus the listed statuses (CustomersController style).

**`Controllers/Agent/TicketsController.cs`** — summary: "The agent's ticket surface (Story 15). tickets.read /
tickets.write / tickets.assign are declared on each request; every ticket is organisation-scoped. RowVersion is
base64 — send back what the last response returned."

| Verb + route | Body | → Request | Statuses |
|---|---|---|---|
| `GET /api/Agent/Tickets` | query: `queue, search, status (repeated), priority (repeated), categoryId, assigneeId, departmentId, branchId, customerId, sort, descending = true, page = 1, pageSize = 20` | `SearchTicketsQuery` | 200 `PagedResult<TicketListItemDto>`, 400 |
| `GET /api/Agent/Tickets/queues` | — | `GetTicketQueueCountsQuery` | 200 |
| `GET /api/Agent/Tickets/{id}` | — | `GetTicketByIdQuery` | 200, 404 |
| `POST /api/Agent/Tickets` | `CreateTicketRequest` | `CreateTicketCommand` | 200, 400, 404, 422 |
| `POST /api/Agent/Tickets/{id}/transitions` | `TransitionTicketRequest(string Action, byte[] RowVersion)` | `TransitionTicketCommand` | 200, 400, 404, 409, 422 |
| `POST /api/Agent/Tickets/{id}/claim` | — | `ClaimTicketCommand` | 200, 404, 409, 422 |
| `POST /api/Agent/Tickets/{id}/assign` | `AssignTicketRequest(Guid AgentId, byte[] RowVersion)` | `AssignTicketCommand` | 200, 400, 404, 409, 422 |
| `POST /api/Agent/Tickets/{id}/unassign` | `TicketRowVersionRequest(byte[] RowVersion)` | `UnassignTicketCommand` | 200, 404, 409, 422 |
| `POST /api/Agent/Tickets/{id}/transfer` | `TransferTicketRequest(Guid DepartmentId, byte[] RowVersion)` | `TransferTicketCommand` | 200, 400, 404, 409, 422 |
| `PUT /api/Agent/Tickets/{id}/priority` | `ChangeTicketPriorityRequest(string Priority, byte[] RowVersion)` | `ChangeTicketPriorityCommand` | 200, 400, 404, 409, 422 |
| `PUT /api/Agent/Tickets/{id}/category` | `ChangeTicketCategoryRequest(Guid CategoryId, byte[] RowVersion)` | `ChangeTicketCategoryCommand` | 200, 400, 404, 409, 422 |

`CreateTicketRequest(Guid CustomerId, string Subject, string Description, Guid? ContactId = null, string? Priority =
null, Guid? CategoryId = null, Guid? DepartmentId = null)`. The search action binds the repeated parameters as
`[FromQuery(Name = "status")] string[]? status, [FromQuery(Name = "priority")] string[]? priority` and passes
`status ?? []`, `priority ?? []`.

**`Controllers/Agent/TicketCategoriesController.cs`** — `GET /api/Agent/TicketCategories?departmentId=` →
`ListTicketCategoriesQuery` (200). Summary: "The category picker: active categories only (a deactivated one
disappears here but stays on its tickets)."

**`Controllers/Admin/TicketCategoriesController.cs`** — `[Area(CrmAreas.Admin)]`:
`GET ?departmentId=&activeOnly=false` → `ListCategoriesQuery`; `GET {id}` → `GetCategoryQuery`; `POST`
`CreateTicketCategoryRequest(Guid DepartmentId, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible)`;
`PUT {id}` `UpdateTicketCategoryRequest(string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible, byte[] RowVersion)`;
`POST {id}/deactivate` and `POST {id}/reactivate` with `TicketCategoryRowVersionRequest(byte[] RowVersion)` — all
return `TicketCategoryDto`. Prove R9.

### B10 — Host

- **File: `Crm.Api/Crm.Api.csproj`** — after line 41 add the Tickets Infrastructure and Presentation
  `ProjectReference`s.
- **File: `Crm.Api/Modules/ModuleCatalog.cs`** — after line 20 add
  `new(new Crm.Modules.Tickets.Infrastructure.TicketsModule(), Crm.Modules.Tickets.Presentation.AssemblyReference.Assembly),`
  (after Customers — its seed points at customer ids).

### B11 — Request samples

**Create file: `backend/requests/tickets.http`** (**Write tool**). Header copied from `customers.http` lines 1–40
(host, run instructions, "tokens live 2 minutes — re-run the logins", rowVersion chaining with `# @name`), logins for
agent A, agent B, supervisor, admin (`admin@crm.local`), variables `@ryCare`, `@ryBill`, `@jeCare`, `@ahmed`, `@najd`,
`@redSea`, `@ahmedContact = 11000000-0000-0000-0001-000000000001`, `@saraContact = …0001-000000000002`, `@agentA =
06000000-0000-0000-0003-000000000003`, `@agentB = …0003-000000000004`, every `TicketsDevSeed` id, `@unknown`. Header
line: "On a fresh database the seed takes T-000001..T-000003, so request 12 is T-000004. Request 55 creates department
RYD-TKT; on a second run it answers 409 Organisation.DepartmentCodeTaken — skip 55a." One request per row, expected
status/code in the `###` title; always send the **latest** rowVersion.

| # | Request (as agent A unless stated) | Expected |
|---|---|---|
| 1 | Admin `GET /api/Admin/TicketCategories?departmentId={{ryCare}}` | 200; five RYD-CARE categories incl. inactive Legacy, ordered by displayOrder |
| 2 | Admin same `&activeOnly=true` | 200; Legacy absent |
| 3 | Admin `POST /api/Admin/TicketCategories` `{departmentId: ryCare, nameAr: "استرداد", nameEn: "Refund", displayOrder: 50, isCustomerVisible: true}` | 200 (`@refund`) |
| 4 | Admin create with `nameAr: ""` · `displayOrder: 10000` | 400 `errors.NameAr` · 400 `errors.DisplayOrder` |
| 5 | Admin create with `departmentId: {{unknown}}` | 404 `Organisation.DepartmentNotFound` |
| 6 | Admin `PUT …/{{refund}}` rename "Refunds", order 55 + latest rowVersion · same with `"rowVersion": "AAAAAAAAB9E="` | 200 · 409 `Persistence.ConcurrencyConflict` |
| 7a–d | Admin deactivate · deactivate again · reactivate · reactivate again | 200 `isActive: false` · 422 `Tickets.CategoryAlreadyInactive` · 200 · 422 `Tickets.CategoryAlreadyActive` |
| 8 | Agent A `GET /api/Admin/TicketCategories` | 403 naming `admin.organisation` |
| 9 | `GET /api/Agent/TicketCategories?departmentId={{ryCare}}` | 200; active only (no Legacy), both name languages |
| 10 | `GET /api/Agent/TicketCategories` (no filter) | 200; RYD-CARE categories only (agent A's scope) |
| 11 | Same as 10 with `Accept-Language: ar` | identical body (culture-neutral) |
| 12 | `POST /api/Agent/Tickets` Ahmed, contact Ahmed, `subject: "طلب استرداد مبلغ"`, description, `priority: "High"`, `categoryId: {{ryCareGeneral}}` | 200 (`@t1`); `number` `T-000004` (R4); `status` `New`; one `Created` activity; `allowedActions` contains `Open`, `Resolve`, `Cancel`, `Claim`, `Assign`, not `Close`/`Unassign`; EF log per R2/R4 |
| 13 | Create for `{{redSea}}` (JED-CARE) | 403 `Organisation.OutOfScope` |
| 14 | Create for `{{unknown}}` customer | 404 `Customers.CustomerNotFound` |
| 15 | Create for `{{najd}}` with `contactId: {{ahmedContact}}` | 422 `Tickets.ContactNotOfCustomer` |
| 16 | Create with `categoryId: {{ryCareLegacy}}` | 422 `Tickets.CategoryInactive` |
| 17 | Create with `categoryId: {{ryBillInvoice}}` | 422 `Tickets.CategoryDepartmentMismatch` |
| 18a/b | Create with `subject: ""` · `priority: "Critical"` | 400 `errors.Subject` · 400 `errors.Priority` (custom text) |
| 19a–c | Create a customer (`customers.http` request-6 body, new email) · deactivate it · create a ticket for it | 200 · 200 · 422 `Tickets.CustomerInactive` |
| 20 | Supervisor, no `X-Crm-Scope`, create without `departmentId` | 400 `Organisation.DepartmentRequired` |
| 21a/b | `POST {{t1}}/transitions {action: "Close"}` with `Accept-Language: en` · `ar` | 422 `Tickets.TransitionNotAllowed`, detail names "New" · Arabic detail names «جديدة» (R10) |
| 22 | `AwaitCustomer` on New | 422 `Tickets.TransitionNotAllowed` |
| 23 | `Open` | 200 `Open`; activity `Opened` from `New` to `Open` (R8 EF log) |
| 24 | `Open` again | 422 |
| 25 | `AwaitCustomer` | 200 `PendingCustomer` |
| 26 | `PutOnHold` | 200 `OnHold` |
| 27 | `Open` (resume) | 200 `Open` |
| 28 | `Resolve` | 200 `Resolved`, `resolvedAtUtc` set, `allowedActions` = `Close`, `Reopen` |
| 29a–c | `PutOnHold` · `PUT {{t1}}/priority {priority: "Low"}` · `POST {{t1}}/claim` on Resolved | 422 `Tickets.TransitionNotAllowed` each |
| 30 | `Reopen` | 200 `Open`, `resolvedAtUtc` null |
| 31a/b | `Resolve` · `Close` | 200 · 200 `Closed`, `closedAtUtc` set |
| 32a/b | `Resolve` · `Cancel` on Closed | 422 · 422 |
| 33 | `Reopen` from Closed | 200 `Open`, `closedAtUtc` null |
| 34 | `Cancel` | 200 `Cancelled`, `allowedActions` empty |
| 35a/b | `Reopen` · `Open` on Cancelled | 422 · 422 |
| 36 | `{action: "Explode"}` | 400 `errors.Action` |
| 37 | Any transition with `"rowVersion": "AAAAAAAAB9E="` | 409 `Persistence.ConcurrencyConflict` |
| 38 | `GET {{t1}}` + SQL `SELECT COUNT(*) FROM tickets.outbox_messages WHERE Content LIKE '%<t1 id>%'` | 11 activities (Created + 10 successful transitions) and **11** outbox rows (AC) |
| 39a/b | Create `@t2` for Najd (contact Sara, no category) · `PUT {{t2}}/priority {priority: "Urgent"}` then again | 200 · 200 `PriorityChanged` Normal→Urgent · 422 `Tickets.PriorityUnchanged` |
| 40a–c | `PUT {{t2}}/category` Technical · Legacy · `{{unknown}}` | 200 · 422 `Tickets.CategoryInactive` · 404 `Tickets.CategoryNotFound` |
| 41 | `POST {{t2}}/assign {agentId: agentA}` as agent A | 403 naming `tickets.assign` |
| 42 | `POST {{t2}}/claim` | 200; `assignedAgentId` = agent A, `assignedAgentName` set, activity `Claimed`; `allowedActions` has `Unassign`, not `Claim` |
| 43a/b | Supervisor `POST {{t2}}/claim` in `en` · `ar` | 409 `urn:crm:problem:conflict`, `code` `Tickets.AlreadyClaimed`, detail names agent A (R10) |
| 44a/b | Supervisor `unassign` · again | 200 activity `Unassigned` · 422 `Tickets.NotAssigned` |
| 45 | Supervisor `assign {agentId: agentB}` (not in RYD-CARE) | 422 `Tickets.AssigneeNotInDepartment` (R11) |
| 46a/b | Supervisor `assign {agentId: agentA}` · again | 200 `Assigned` · 422 `Tickets.AssigneeUnchanged` |
| 47 | Supervisor `assign {agentId: {{unknown}}}` | 422 `Tickets.AssigneeNotInDepartment` |
| 48 | Create `@race` (agent A) — then run Verification step 4's snippet | see R3 |
| 49 | Agent A `POST {{t2}}/transfer {departmentId: jeCare}` | 403 naming `tickets.assign` |
| 50 | Supervisor transfer to `{{ryCare}}` | 422 `Tickets.TransferToSameDepartment` |
| 51 | Supervisor transfer to `{{unknown}}` | 404 `Organisation.DepartmentNotFound` |
| 52 | Supervisor transfer `{{t2}}` to `{{jeCare}}` | 200; `departmentId` JED-CARE, `branchId` Jeddah, `assignedAgentId` null, `category` null, last activity `Transferred` (from ryCare to jeCare), all earlier activities present (R1) |
| 53a/b | Supervisor `GET {{t2}}` · agent A `GET {{t2}}` | 403 `Organisation.OutOfScope` each (AC "out of the sender's scope") |
| 54a/b | Agent B `GET {{t2}}` · agent B `POST {{t2}}/claim` | 200 same history · 200 (AC "into the receiver's") |
| 55a–c | Admin `POST /api/Admin/Departments` `{branchId: Riyadh, code: "RYD-TKT", nameAr: "قسم التذاكر", nameEn: "Ticket Desk"}` · deactivate it · supervisor transfers a fresh RYD-CARE ticket there | 200 · 200 · 422 `Tickets.TargetDepartmentInactive` |
| 56 | `GET /api/Agent/Tickets/queues` | 200 `{mine, unassigned, all}` consistent with 57–59 |
| 57 | `GET /api/Agent/Tickets?queue=mine` | T-000002 (seeded, assigned to A) and `@race` if A won; EF log per R5 |
| 58 | `?queue=unassigned` | T-000001 and other unassigned working tickets; never T-000003 (JED) |
| 59 | `?queue=all&status=Cancelled` | `@t1` (R6) |
| 60a/b | `?search=T-00000` · `?search=%D8%A7%D8%B3%D8%AA%D8%B1%D8%AF%D8%A7%D8%AF` (استرداد) | number matches · `@t1` (Arabic subject) |
| 61 | `?priority=High&priority=Urgent&sort=priority&descending=true&page=1&pageSize=2`, then `page=2` | distinct items per page, `total` = unpaged count; `ORDER BY [t].[Priority] DESC, [t].[Number]` + `OFFSET … FETCH` (R6) |
| 62a–d | `?customerId={{ahmed}}` · `?categoryId={{ryCareGeneral}}` · `?assigneeId={{agentA}}` · `?departmentId={{jeCare}}` | filtered pages · the last is an **empty** page, not 403 |
| 63a–d | `?queue=bogus` · `?sort=bogus` · `?status=Bogus` · `?pageSize=0` | 400 `errors.Queue` · `errors.Sort` · `errors.Statuses[0]` · `errors.PageSize` |
| 64 | Agent B `GET /api/Agent/Tickets?queue=all` | only JED-CARE tickets (T-000003, `@t2`) |
| 65a–d | Admin deactivate Technical (`{{ryCareTechnical}}`) · agent A `GET` T-000002 · agent A picker (9) · admin reactivate | 200 · 200 with `category.isActive: false` and its name · Technical absent · 200 (AC "without breaking existing tickets") |
| 66 | `GET /api/Agent/Tickets` with no token | 401 |
| 67a/b | `GET /api/Agent/Tickets/{{unknown}}` · `GET` T-000003 as agent A | 404 `Tickets.TicketNotFound` · 403 |

### B12 — Documentation

- **`CLAUDE.md`** "Scoping" (line 202 on): after the sentence ending "…refuses to save a row outside the caller's
  scope." add: "The one exception is a transfer: a Modified `ITransferableOrganisationScoped` row (only `Ticket`, moved
  only by `Ticket.TransferTo`) is judged by its original scope, so the sender can hand it to another department of the
  same tenant (Story 15). Adding an implementer is a security review."
- **`backend/README.md`** "Organisation scope and sign-in" — add the same bullet after the "Scope markers" bullet;
  "Talking to another module" — add "`IOrganisationModuleApi.GetDepartmentAsync`/`IsDepartmentMemberAsync` and
  `IIdentityModuleApi.GetStaffUsersAsync` (Story 15) are the in-tenant reference reads other modules use."
- **`backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md`** — Agent Area (line 347) append "Story 15: the
  `Tickets` module's `TicketsController` and `TicketCategoriesController` (`tickets.read`/`tickets.write`/`tickets.assign`)."
  Admin Area (line 361) append "(Story 15) the `Tickets` module's `TicketCategoriesController` (`admin.organisation`)."
- **`.squad/plans/ticket-management/00-overview.md`** — already lists this plan (planning session).

---

## Frontend Tasks

### F1 — Regenerate the API types

With the API running in Development: `pnpm --dir frontend gen:api` (R12). Expected diff: `frontend/openapi/agent.json`
+ `app/api/generated/agent.ts` gain `/api/Agent/Tickets…` and `/api/Agent/TicketCategories`; `openapi/admin.json` +
`app/api/generated/admin.ts` gain `/api/Admin/TicketCategories…`. Commit all four. Read the generated
`TicketCategoryDto` and `CreateTicketCategoryRequest` schemas before F2 — no hand-written DTO type, no `as` casts.

### F2 — Admin screen `/admin/ticket-categories`

- **Create file: `frontend/app/composables/useTicketCategories.ts`** — copy `useDepartments.ts`:
  `export type TicketCategoryDto = components['schemas']['TicketCategoryDto']` (admin client);
  `list(departmentId: Ref<string | undefined>, activeOnly: Ref<boolean>)` (key `'admin-ticket-categories'`, throws
  the problem like `useDepartments.ts` lines 18–21), `create(body)`, `update(id, body)`, `deactivate(id, rowVersion)`,
  `reactivate(id, rowVersion)` — every call `{ toastOnError: false }` except deactivate/reactivate.
- **Create file: `frontend/app/pages/admin/ticket-categories.vue`** — copy `app/pages/admin/departments.vue` and
  change: title/create/empty keys → `tickets.categories.*`; a department filter `UiSelect` next to "Show inactive"
  (options: "All departments" + active departments from `useDepartments().list(undefined, true)`, labels via
  `localizedName`); columns `name` (`localizedName(row.name, locale)`), `department`
  (`localizedName` of the department), `displayOrder` (`useFormat()` number), `customerVisible` (`UiBadge` tone
  `info`/`neutral`), `status` (reuse `organisation.status.*`), `actions` (Edit; Deactivate when active, Reactivate when
  inactive); modal fields: department `UiSelect` (create only), `nameEn` (`dir="ltr"`), `nameAr` (`dir="rtl"
  lang="ar"`), `displayOrder` `UiInput type="number"`, `isCustomerVisible` `UiCheckbox`, errors through
  `useFormErrors()` (`errorFor('nameAr')`, `'displayOrder'`, …). Deactivate confirm text `tickets.categories.confirm.deactivate`.
  Loading (`UiTable :loading`), empty (`UiEmptyState`), error (`UiErrorState` with retry) and forbidden states exactly
  as departments.vue lines 14–38. Logical utilities only (`ms-`/`me-`/`text-start`).
- **File: `frontend/app/layouts/agent.vue`** — in the `can(Permissions.AdminOrganisation)` block (lines 99–104) add
  `{ to: '/admin/ticket-categories', label: t('tickets.nav.categories') }` after Departments.
- **Create files: `frontend/i18n/locales/en/tickets.json` and `ar/tickets.json`** (**Write tool**; top-level key
  `tickets`; Story 20 extends this file):

  | Key | en | ar |
  |---|---|---|
  | `tickets.nav.categories` | Ticket categories | تصنيفات التذاكر |
  | `tickets.categories.title` | Ticket categories | تصنيفات التذاكر |
  | `tickets.categories.create` | New category | تصنيف جديد |
  | `tickets.categories.edit` | Edit category | تعديل التصنيف |
  | `tickets.categories.empty` | No categories yet | لا توجد تصنيفات بعد |
  | `tickets.categories.allDepartments` | All departments | جميع الأقسام |
  | `tickets.categories.fields.department` | Department | القسم |
  | `tickets.categories.fields.displayOrder` | Display order | ترتيب العرض |
  | `tickets.categories.fields.customerVisible` | Visible to customers | مرئي للعملاء |
  | `tickets.categories.visibility.visible` | Customers see it | يراه العملاء |
  | `tickets.categories.visibility.internal` | Internal | داخلي |
  | `tickets.categories.actions.edit` | Edit | تعديل |
  | `tickets.categories.actions.reactivate` | Reactivate | إعادة التفعيل |
  | `tickets.categories.confirm.deactivate` | Deactivate {name}? It disappears from pickers; tickets that use it keep it. | تعطيل {name}؟ سيختفي من قوائم الاختيار، وتحتفظ به التذاكر التي تستخدمه. |

  Reuse `organisation.fields.{name,nameEn,nameAr,status,actions}`, `organisation.status.*`,
  `organisation.actions.{deactivate,save,cancel,close,showInactive}`, `organisation.load.*`.
- **Files: `frontend/i18n/locales/{en,ar}/errors.json`** — add `errors.code.Tickets` with **every** code in Product
  rules' error list (Story 20 relies on them). English = the domain message; Arabic written by the executor in the
  same register as `errors.code.Customers` (e.g. `TransitionNotAllowed`: "This action is not allowed in the ticket's
  current status." / "لا يُسمح بهذا الإجراء في الحالة الحالية للتذكرة."; `AlreadyClaimed`: "Another agent already
  claimed this ticket." / "استلم موظف آخر هذه التذكرة بالفعل."; `CategoryInactive`: "The category is inactive." /
  "التصنيف غير نشط."). `pnpm lint` fails if the key sets diverge.

No new UI kit component, no `/_dev/ui` section.

---

## Edge Cases & Failure Modes

- **Illegal transition** → handler asks `Ticket.IsAllowedInCurrentStatus` (`TicketRefusals.NotAllowed`, B5) → 422
  `Tickets.TransitionNotAllowed` with a localised detail naming the status; a handler that forgets gets the aggregate's
  `DomainException` (same code, English) via `GlobalExceptionHandler.cs` lines 53–57.
- **Status-irrelevant refusals** (unassign an unassigned ticket, same priority/category/assignee/department) → the
  aggregate's specific 422, not `TransitionNotAllowed`.
- **Two concurrent claims** → R3: exactly one wins; the other 409 `Tickets.AlreadyClaimed` naming the winner, via the
  pre-check or the caught `ConcurrencyConflictException`. A claim that loses to a **different** change (priority) with
  the ticket still unassigned → rethrown → generic 409 `Persistence.ConcurrencyConflict` (`TransactionBehavior.cs`
  lines 91–98).
- **Claim of a ticket assigned to the caller** → 409 naming the caller (the rule is "only unassigned tickets can be
  claimed").
- **Stale client rowVersion** on any other command → 409 before commit; the staged integration event is discarded.
- **A nested Contracts call after `Publish`** → 500 (`InvalidOperationException` from `ThrowIfPublishedOutsideCommand`).
  Prevented by the handler shape in B6 (R2).
- **Transfer** → the ticket leaves the sender's scope in the same transaction (write guard, R1); the response is built
  from memory. The sender's next GET is 403; a supervisor who is a member of both departments still sees it. Assignee
  and category cleared (receiving department triages). Transferring within the same department → 422.
- **A Modified non-transferable row moved out of scope** (a future bug in another module) → still 403 — the guard's new
  branch requires `ITransferableOrganisationScoped`. An **Added** ticket in a foreign department (create with
  `departmentId` outside scope) → `ResolveWriteTarget` returns null → 403 before the guard.
- **Supervisor with "all departments" scope** holds Supervisor in RYD-CARE and Agent in RYD-BILL; effective
  permissions are the union (Story 07 rule), so they can assign in RYD-BILL too. Accepted (Story 07's model).
- **Category of another department** → 422 `Tickets.CategoryDepartmentMismatch` (aggregate). After a transfer the
  category is null, so the receiving department never inherits a foreign category.
- **Deactivated category** → hidden from `GET /api/Agent/TicketCategories`; existing tickets still carry and render it
  (`TicketCategoryRefDto.IsActive = false`); choosing it again is 422.
- **Customer deactivated after the ticket exists** → no effect on the ticket; new tickets for it → 422.
- **Customer out of scope / unknown** → `ICustomersModuleApi`'s failed Result passed through (403 / 404); the Customers
  query is cached per scope, so a hit never crosses scopes (`CachingBehavior` key prefix).
- **Identity lookup failure** (should not happen) → names missing (`assignedAgentName: null`, `people` empty); the
  ticket read still succeeds. The 409 falls back to the winner's id.
- **Assignee not a member / inactive / unknown** → 422 (`AssigneeNotInDepartment` / `AssigneeInactive`).
- **Search filters outside scope** → empty page. **LIKE wildcards** in `search` are escaped by EF (`Contains`).
- **Non-deterministic paging on equal sort keys** → prevented by `ApplyThenBy(Number)`.
- **Sequence gaps** — a create that draws a number and then fails (422 category) leaves a gap. By design.
- **Activity growth** — every command loads all activities (owned collection). Fine for the MVP; Story 18 must decide
  whether messages stay owned.
- **Seeder** runs Unrestricted, is keyed on well-known ids, and draws T-000001..3 only on an empty schema.
- **Logs** — handlers log only the claim race (`LogClaimLostRace`, number + winner id); no subject/description text is
  logged.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the `CRM0001` rules, the startup guard, the
aggregate's transition table, `backend/requests/tickets.http`, and the browser walk replace the suite. Run every row;
record the results and which R-fallbacks (if any) shipped in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001`, no `RS0030` |
| 2 | Boundaries | read the five Tickets `.csproj` and the Organisation/Identity changes | Domain/Contracts → SharedKernel only; Application → other modules' Contracts only; Presentation `DisableTransitiveProjectReferences` |
| 3 | Migration | read `Tickets/…/Migrations/*_Initial.cs` | B8 checklist; schema `tickets` only; nullable `CategoryId`; unique `Number`; the sequence |
| 4 | No child DbSet (AC) | `grep -rn "DbSet<" backend/src/Modules/Tickets` | exactly `DbSet<Ticket>` and `DbSet<Category>` |
| 5 | No public status setter (AC) | `grep -n "Status {" backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/Ticket.cs` | `public TicketStatus Status { get; private set; }` only |
| 6 | Startup guard | run the API | starts; log shows `tickets` migrated and seeded (R11) |
| 7 | Risks R1–R12 | as stated in Prerequisites | each proven or its fallback recorded |
| 8 | Endpoints + rules (AC) | `tickets.http` 1–67 top to bottom | every row's expected status/code |
| 9 | One activity + one outbox row per transition (AC) | `tickets.http` 38 | 11 and 11 |
| 10 | Claim contention (AC) | Verification step 4 | one 200 + one 409 naming the winner, per pair |
| 11 | Transfer (AC) | `tickets.http` 52–54 | out of sender's scope, into receiver's, history kept |
| 12 | Scoped SQL (AC) | EF log for 57, 61, 64 | scope predicates, ORDER BY, OFFSET/FETCH, COUNT |
| 13 | Regressions | `organisation.http` 14–23, `customers.http` 27a–e, `users.http` 1 | unchanged results (R1) |
| 14 | Swagger | `/swagger/agent/swagger.json`, `/swagger/admin/swagger.json` | 200 both (R9) |
| 15 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0 |
| 16 | Admin screen | `pnpm --dir frontend dev`, admin signs in, `/admin/ticket-categories` in `en` and `ar` at 375 / 768 / 1280 | names in the active locale; skeleton → rows; "Show inactive" reveals Legacy; create with an empty Arabic name → field error under the input; deactivate → row shows Inactive and disappears with "Show inactive" off; agent A sees no nav item and gets the forbidden state on the URL |

---

## Migration / Rollback

- One new migration: `Tickets/Initial` (schema `tickets`: sequence `TicketNumbers`, `Tickets`, `TicketActivities`,
  `Categories`, the outbox pair). No change to any other schema. Applied at startup in Development; seeded by
  `TicketsSeeder`.
- **Rollback:** `pwsh backend/scripts/ef.ps1 -Module Tickets database update 0`, then revert the commit (the write-guard
  branch and the Organisation/Identity reads go with it). No production data exists.
- **Half-applied state:** a failed migration fails startup loudly; a half-run seeder is safe to re-run (well-known ids;
  a drawn sequence value is skipped). Never hand-edit `__EFMigrationsHistory`.

---

## Verification Steps

1. **Backend builds:** `dotnet tool restore`; `dotnet build backend/Crm.sln` → Test Plan rows 1–5.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` → row 6; row 14; prove R1, R2, R4, R8, R10 first
   (`tickets.http` 12, 21, 23, 43, 52–54), then run `tickets.http` 1–67 top to bottom → rows 8, 9, 11, 12.
3. **Regression:** row 13; `grep -rn "IgnoreOrganisationScope()" backend/src` still shows the definition and exactly
   one caller (`MembershipsOfUserSpecification`); `grep -rn "ITransferableOrganisationScoped" backend/src` shows the
   declaration, the guard, and `Ticket` only.
4. **Claim race (R3):** after `tickets.http` 48 (and two more fresh tickets), in Git Bash with `A` and `S` set to agent
   A's and the supervisor's access tokens and `T` to the ticket id:

   ```bash
   H=http://localhost:5187
   curl -s -o a.json -w "A %{http_code}\n" -X POST -H "Authorization: Bearer $A" "$H/api/Agent/Tickets/$T/claim" &
   curl -s -o s.json -w "S %{http_code}\n" -X POST -H "Authorization: Bearer $S" "$H/api/Agent/Tickets/$T/claim" &
   wait; cat a.json; echo; cat s.json
   ```

   Expected per ticket: one `200`, one `409` whose body has `"code":"Tickets.AlreadyClaimed"` and the winner's name;
   `GET` the ticket → one assignee, exactly one `Claimed` activity. Record whether `LogClaimLostRace` appeared.
5. **Frontend types:** with the API up, R12, then `pnpm --dir frontend gen:api` → only the four expected files change.
6. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` → row 15.
7. **Frontend runs:** `pnpm --dir frontend dev` with the API up → row 16 in `en` and `ar`.

---

## Done Criteria

- [ ] Every legal transition succeeds and every illegal one is rejected by the aggregate's table with 422
      `domain-rule` and a localised message naming the current status (`tickets.http` 21–37).
- [ ] No public status setter exists; `TicketActivity` has no `DbSet` and is reached only through `Ticket` (rows 4, 5).
- [ ] Creating a ticket for a customer outside the caller's scope (13) or a nonexistent customer (14) is rejected;
      inactive customer (19c) and foreign contact (15) too.
- [ ] Two concurrent claims produce exactly one assignee; the loser gets a 409 naming the winner (43, Verification 4).
- [ ] Transfer moves the ticket out of the sender's scope and into the receiver's, with the transfer in the activity
      (52–54).
- [ ] Every transition appends exactly one `TicketActivity` and writes one outbox row (38).
- [ ] Queue queries and search page, sort and filter server-side and are scoped, predicates visible in the SQL
      (56–64).
- [ ] Category names render in the active locale (`/admin/ticket-categories` in en/ar); a deactivated category
      disappears from pickers without breaking existing tickets (9, 65).
- [ ] Every path exercised from `backend/requests/tickets.http`.
- [ ] Permissions `tickets.read` / `tickets.write` / `tickets.assign` (and `admin.organisation` for category admin)
      declared on every request; the app starts.
- [ ] The write-guard exception is limited to `ITransferableOrganisationScoped` and documented in `CLAUDE.md`, README
      and the XML summaries (B1, B12).
- [ ] `dotnet build backend/Crm.sln` clean with 0 warnings and no `CRM0001`; the Tickets `Initial` migration is included;
      `pnpm typecheck`/`lint`/`build` clean; `gen:api` output committed.
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/ticket-management/00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 18.**
