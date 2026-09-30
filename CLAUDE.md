# CRM — Customer Support CRM

Multi-department, multi-branch, bilingual (Arabic/English) customer support CRM. Scope is defined by
`azm_squad_customer_support_crm.pdf` (12 feature areas). The story map lives in `.squad/BACKLOG.md`.

## Repository layout

```
backend/
  Crm.sln
  Directory.Build.props      shared build settings + banned-API lists wired for every project
  Directory.Build.targets    module boundary rules (fails the build with CRM0001)
  build/                     BannedSymbols.*.txt
  scripts/ef.ps1             dotnet ef wrapper, one module at a time (-Module <Name>)
  src/
    Crm.Api/                 host: composition root, Swagger, health checks, ModuleCatalog, Development-only
                             Diagnostics controllers   (-> modules' Infrastructure + Presentation)
    BuildingBlocks/
      Crm.SharedKernel/                   Entity, AggregateRoot, ValueObject, StronglyTypedId, domain events,
                                          IIntegrationEvent, Result/Error
      Crm.BuildingBlocks.Application/     MediatR pipeline behaviours, ICommand, IRepository, IUnitOfWork, IDateTime,
                                          ICurrentUserService, CrmClaimTypes, ICorrelationIdAccessor, Specification,
                                          ICacheService/ICacheableQuery, IEventBus/IIntegrationEventConsumer, CrmDiagnostics
      Crm.BuildingBlocks.Infrastructure/  ModuleDbContext (+ outbox tables), auditing/soft-delete interceptor, Repository,
                                          IModule, AddModuleDbContext, SystemDateTime, OutboxDispatcher, Serilog +
                                          OpenTelemetry (AddCrmObservability), correlation id, Redis cache, AddCrmResilience
      Crm.BuildingBlocks.Presentation/    ApiControllerBase, CrmAreas, ProblemTypes, GlobalExceptionHandler, rate limiting
    Modules/
      <Module>/
        Crm.Modules.<Module>.Domain/          aggregates, entities, value objects, domain events, domain services
        Crm.Modules.<Module>.Application/     CQRS handlers, DTOs, validators, I<Module>UnitOfWork, Contracts implementation
        Crm.Modules.<Module>.Contracts/       the module's public face: API interfaces, contract DTOs, integration events
        Crm.Modules.<Module>.Infrastructure/  <Module>DbContext (own schema), EF configurations, migrations, adapters, <Module>Module : IModule
        Crm.Modules.<Module>.Presentation/    MVC controllers, one folder per Area
  requests/                  committed .http files, one per feature, for manual verification
frontend/                    Nuxt 4 app (Vue 3 + TypeScript + Tailwind)
.squad/                      squad-kit stories, plans, and backlog
```

## Backend — .NET 10, modular monolith, DDD + Clean Architecture per module

### Modular monolith

- **One deployable, many modules.** The backend is a single ASP.NET host (`Crm.Api`) running a set of
  independent **modules**, one per bounded context. A module owns its domain model, its use cases, its database
  schema, and its HTTP endpoints. Modules could be split into services later; nothing in the design may make that
  harder than moving a folder and its schema.
- **Module catalogue.** Create a module the first time a story needs it, never earlier. Each new module lives
  under `backend/src/Modules/<Module>/` with all five projects and is registered in
  `Crm.Api/Modules/ModuleCatalog.cs`.

  | Module | Owns | Stories |
  |--------|------|---------|
  | `Organisation` | tenants, branches, departments, teams, memberships, business hours, holiday calendars, tenant configuration and branding | 06, 58 |
  | `Identity` | users, roles, permissions, authentication, refresh tokens, portal customer accounts | 07, 08, 09, 47 |
  | `Audit` | the audit log, built from other modules' integration events | 10 |
  | `Customers` | customers, contacts, customer notes and attachments, interaction timeline | 11, 12, 13 |
  | `Tickets` | tickets and their messages, notes, SLA clock, history; taxonomy; queues and assignment | 15–19, 48 |
  | `Communication` | channel abstraction, inbound pipeline, email, WhatsApp, SMS, live chat, web forms | 22–27 |
  | `Workspace` | agent tasks, reminders, quick replies, mentions, watchers, dashboard | 28–31 |
  | `Sla` | SLA policies, response/resolution targets, breach detection | 32, 33 |
  | `Automation` | routing and assignment rules, escalation and business rules engine | 34, 35 |
  | `Notifications` | notification preferences, delivery, and alerts | 36 |
  | `KnowledgeBase` | articles, authoring workflow, bilingual search, help centre | 37–40 |
  | `Ai` | AI service abstraction, prompts and guardrails, summaries, suggestions, categorisation, chatbot | 41–46 |
  | `Feedback` | customer satisfaction surveys and feedback | 50 |
  | `Reporting` | reporting read models, reports, dashboards, exports | 51–54 |
  | `Integrations` | public API versioning, API keys, outbound webhooks, ERP | 55–57 |

  The customer portal (47–49) is a **persona** (the `Portal` Area), not a module: its endpoints live in the modules
  that own the data (Identity, Tickets, Customers, KnowledgeBase). The `Sample` module is the canonical template
  and is deleted in Story 11.
- **Five projects per module, one direction.** Inside a module the dependency direction is Clean Architecture's:
  `Presentation -> Application -> Domain`, `Infrastructure -> Application`, `Application -> Contracts`.
  `Domain` and `Contracts` reference only `Crm.SharedKernel`. `Presentation` cannot see `Domain` at all
  (`DisableTransitiveProjectReferences`), so a domain type cannot cross the API boundary.
- **Modules talk only through Contracts.** A module may reference another module's `Contracts` project and nothing
  else of it. Two ways across:
  - **Synchronous read** — an interface in `Contracts` (e.g. `ICustomersModuleApi.GetCustomerAsync`), implemented in
    the owning module's `Application` by sending its own query, so the pipeline (validation, authorisation,
    scoping) applies. Use it for data needed now: "does this customer exist?". Primitives and contract DTOs only.
  - **Integration event** — a record in the publishing module's `Contracts`, published through the outbox after the
    publisher's transaction commits and handled in the subscriber's own transaction. Use it for "this happened":
    `TicketCreated` feeding the customer timeline, the audit log, SLA, reporting. Publish with
    `IEventBus.Publish` **from the command handler** (never from a domain-event handler or a query — both throw:
    the event would otherwise be lost). `TransactionBehavior` writes it to the module's own `outbox_messages` in the
    command's transaction; `OutboxDispatcher` delivers it after commit. Subscribe with an
    `IIntegrationEventConsumer<T>` in the subscriber's Application project — discovered automatically, run in the
    subscriber's own transaction, at least once, with a per-consumer idempotency row (`outbox_message_consumers`),
    retried with backoff and dead-lettered after `Outbox:MaxAttempts`. A consumer never sends a MediatR command.
    Event records are **append-only**: an undelivered row still holds the old shape and type name.
  - **Never** call another module to change its state inside your transaction, never share an entity, and never
    read another module's tables.
- **The build enforces the boundaries.** `backend/Directory.Build.targets` fails the build with `CRM0001` when a
  project reference crosses a rule: another module's non-Contracts project; Domain/Contracts referencing anything
  but the shared kernel; Application referencing Infrastructure or Presentation; Presentation referencing Domain or
  Infrastructure; building blocks referencing a module; the host referencing a module's Domain/Application/Contracts.
  A `CRM0001` is a design problem; never weaken the target to make a reference compile.
- **One database, one schema per module.** All modules share the `Crm` connection string. Each module has exactly
  one `ModuleDbContext` subclass with its own schema (lower-case module name: `tickets`, `customers`), its own
  migrations, and its own `__EFMigrationsHistory` in that schema. No cross-schema foreign keys, joins, or views.
  An id that points into another module (`CustomerId` on a ticket) is a plain value with no FK.
- **One transaction, one module.** A command runs in the transaction of the module that owns it; that module's
  unit of work is resolved from the command's assembly (`IUnitOfWorkResolver`). A handler never sends a command
  that belongs to another module.
- **Shared kernel stays small.** `Crm.SharedKernel` holds only what every module genuinely shares: the DDD
  building blocks, `Result`/`Error`, and the org-scope ids and scope markers (`Crm.SharedKernel.Scoping`:
  `TenantId`, `BranchId`, `DepartmentId`, `ITenantScoped`, `IOrganisationScoped`). A concept one module owns
  (`EmailAddress` in Customers, `TicketNumber` in Tickets) lives in that module's Domain; another module that needs
  the value receives a primitive through Contracts.
- **Frontend-facing contract is unchanged.** Controllers from every module merge into the same four per-Area
  Swagger documents. Controller names must be unique within an Area across modules, because routes are
  `api/[area]/[controller]`.

### Domain-driven design (inside every module)

- **Bounded contexts are modules.** Each module has its own model: a `Customer` in the Tickets module is a
  `CustomerId`, not a shared class.
- **Aggregates** guard their own invariants. `Ticket` is an aggregate root; `TicketMessage`, `TicketNote`, and
  `SlaClock` are inside it and are reachable only through it. Never load or save a child directly.
- **One transaction, one aggregate.** Cross-aggregate work inside a module goes through a domain event, never a
  second aggregate mutated in the same handler. Across modules it goes through an integration event.
- **Reference other aggregates by id only** (`CustomerId`, `AgentId`), never by navigation property.
- **Value objects** for every concept with no identity: `EmailAddress`, `PhoneNumber`, `TicketNumber`,
  `SlaTarget`, `BusinessHours`, `Money`, `LocalizedText`. Immutable, self-validating, equality by value.
- **No public setters and no parameterless public constructors on entities.** State changes happen through
  intention-revealing methods (`ticket.Escalate(reason, by)`, not `ticket.Priority = High`).
- **Always-valid aggregates.** A constructor or factory method that cannot satisfy the invariants fails; there
  is no half-built entity. Guard clauses live in the domain, not in the handler.
- **Domain events** are raised inside aggregate methods, dispatched after the transaction commits, and handled
  **inside the same module**. They never leave it. What other modules need to know is republished as an integration
  event through the module's Contracts and the outbox.
- **Ubiquitous language.** Type and method names match the words in the story intake. If the intake says
  "escalate", the method is `Escalate` — not `UpdateStatusToLevel2`.
- **Domain services** only for logic that spans aggregates and has no natural home (SLA target resolution,
  assignment strategy selection). Anything expressible on an aggregate belongs on the aggregate.
- **Specifications** express reusable query intent (`OverdueTicketsSpecification`). Repositories expose
  aggregate roots only, one repository per aggregate, registered with `AddRepository<TAggregate, TId, TContext>()`.
- **Anti-corruption layer:** external systems (ERP, WhatsApp, email providers) never leak their models inward.
  The owning module's `Infrastructure` translates them into domain types at the boundary.

### Application, API, and persistence

- **CQRS:** MediatR, registered **once** by the host over every module's Application assembly. A module never
  calls `AddMediatR`. One folder per use case under
  `Crm.Modules.<Module>.Application/{Commands,Queries}/<UseCase>/`, holding the request record, its handler, and its
  FluentValidation validator. Commands implement `ICommand`/`ICommand<T>` and go through aggregates; queries bypass
  them. Handlers inject their module's `I<Module>UnitOfWork`, never the bare `IUnitOfWork`.
- **API surface:** MVC controllers, not minimal APIs, in the module's `Presentation` project under
  `Controllers/<Area>/`. One **Area per persona** — `Agent`, `Portal`, `Admin`, `Public`. Every controller derives
  from `ApiControllerBase`, carries `[Area(CrmAreas.X)]`, and inherits the `api/[area]/[controller]` route template.
  Each Area gets its own Swagger document at `/swagger/{area}/swagger.json`; the UI at `/swagger` is
  Development-only. Area purposes and policies: `Crm.BuildingBlocks.Presentation/AREAS.md`.
- **Queries** project straight to DTOs with `.Select(...)`, use `AsNoTracking()`, and never return an entity or
  a domain type across the API boundary.
- **Results:** handlers return `Result` / `Result<T>`. Expected business failures are results, not exceptions.
  The API translates them to RFC 9457 `ProblemDetails`. Error `type` URIs are the constants in
  `Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs` (`urn:crm:problem:*`) — never inline a type string,
  never rename one (the frontend maps them to localised messages). A `DomainException` from an aggregate becomes 422
  `domain-rule`; a validator failure 400 `validation`; an unhandled exception 500 `internal` with **no** exception text.
- **Localisation:** `Accept-Language` (`en`, `ar`) sets the request culture (`UseCrmRequestLocalization`, before
  the exception handler). FluentValidation built-in messages and problem titles follow it. Custom server text
  goes through `ILocalizationService` and a `.resx` pair in the owning module's Application project, registered
  with `AddLocalizationResource`. Cached query results are culture-neutral: return both languages
  (`LocalizedText`) rather than caching a translated string.
- **Pipeline behaviours:** validation -> authorisation -> organisation scope -> logging -> transaction -> caching.
  "Logging" is also the
  OpenTelemetry span and the `crm.request.duration` metric per request.
- **Logging:** Serilog, compact JSON in every environment; every line carries `CorrelationId` (from
  `X-Correlation-Id`, generated when absent) and `UserId`/`TenantId`/`DepartmentId`/`BranchId` when authenticated
  (claim names in `CrmClaimTypes`). Log only through `ILogger<T>` with a `[LoggerMessage]` method — CA1848/CA2254 are
  errors and `System.Console` is banned (`BannedSymbols.Logging.txt`). Anything logged with `{@X}` is redacted by
  property name (password, secret, token, apikey, …); never log a payload any other way.
- **Caching:** mark a query `ICacheableQuery` (key and tags `{module}:{aggregate-plural}:{id}`, built by a
  `<Module>CacheKeys` class) and every command that changes its data `ICacheInvalidatingCommand` with the same tag.
  `CachingBehavior` prefixes the caller's tenant/department/branch scope; `TransactionBehavior` invalidates after
  commit. Set `VaryByUser` when the result depends on who asks. Failures are never cached; a Redis outage degrades
  to uncached reads.
- **Outbound HTTP:** every `HttpClient` is registered with `.AddCrmResilience(configuration)` (correlation id +
  retry, circuit breaker, timeouts from `Resilience:Outbound`). No hand-rolled retry loops.
- **Localization keys:** a C# resource key is never a string literal at a call site. Every key is a `const` in a
  static keys class next to its `.resx` (`CrmMessageKeys` in `Crm.BuildingBlocks.Application/Localization`; a
  module that adds its own resources gets `<Module>MessageKeys`), nested by area (`CrmMessageKeys.ProblemDetail.X`),
  and callers pass the constant: `localization.Get(CrmMessageKeys.ProblemDetail.Validation)`. No key built by
  concatenation.
- **Time:** the clock is read only through `IDateTime` (`Crm.BuildingBlocks.Application.Abstractions`), whose
  one implementation is `SystemDateTime`. `System.DateTime` is banned outright, and `DateTimeOffset.UtcNow`/`Now`
  and `System.TimeProvider` are banned everywhere else (`backend/build/BannedSymbols.*.txt`). Values are
  `DateTimeOffset` in UTC. Domain methods take a timestamp as a parameter instead of reading the clock.
- **Persistence:** EF Core 10 + SQL Server. Each module DbContext derives from `ModuleDbContext` and is registered
  only through `AddModuleDbContext<TContext, TUnitOfWork>()`. Configuration via `IEntityTypeConfiguration<T>`
  classes in the module's Infrastructure only, never data annotations. Value objects map as owned types or with a
  `ValueConverter`. Migrations ship in the same change as the schema move, per module:
  `backend/scripts/ef.ps1 -Module <Module> migrations add <Name>`.
- **Auditing:** entities derive from `AuditableEntity` (created/modified by + at). Soft delete via
  `ISoftDeletable` + a global query filter, applied to every module by `ModuleDbContext`.
- **Scoping:** two markers in `Crm.SharedKernel.Scoping` — `ITenantScoped` (tenant only) and
  `IOrganisationScoped : ITenantScoped` (tenant + branch + department). `ModuleDbContext` applies
  `ModuleDbContext.ScopeFilterName`, a second **named** query filter next to the soft-delete filter, to every
  such entity in every module automatically — nothing to opt into. The write guard in
  `ModuleDbContext.OrganisationScope.cs` refuses to save a row outside the caller's scope. An out-of-scope
  read or write returns 403 `urn:crm:problem:forbidden` (code `Organisation.OutOfScope`), never an accidental
  404. The one permitted lift is `Specification.IgnoreOrganisationScope()`, used only by the sign-in membership
  lookup (`Organisation`'s `MembershipsOfUserSpecification`) — it lifts only the scope filter, never soft
  delete. `ICurrentUserService.Scope` is the caller's resolved `OrganisationScope` (tenant, memberships, and
  the active scope chosen via the `X-Crm-Scope` header, which only narrows — a department the caller does not
  belong to is rejected with 403, never silently widened). Creating a scoped row resolves its target department
  with `OrganisationScope.ResolveWriteTarget`. There is no "global" read path outside explicitly authorised
  admin endpoints.
- **Patterns in use:** modular monolith, repository + specification, strategy (channel adapters, assignment rules),
  decorator (caching, retry), outbox (integration events between modules and outbound integrations), factory
  (aggregate creation), options pattern (configuration). Do not add a pattern without a use case that needs it.

## Frontend — Nuxt + Vue 3 + TypeScript + Tailwind

- **Nuxt 4** app directory. SSR for the customer portal, SPA for the agent workspace.
- `<script setup lang="ts">` everywhere. TypeScript strict; no `any`, no ts-ignore.
- **Tailwind only** for styling. No component-scoped CSS unless no utility exists.
- **RTL is a first-class requirement.** Use logical utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`,
  `text-end`, `border-s`, `border-e`). `ml-`/`mr-`/`text-left`/`text-right` are bugs, enforced by lint.
- **i18n:** `@nuxtjs/i18n` with `en` + `ar`. Every user-visible string is a translation key. Arabic is a
  supported locale, not an afterthought — dates, numbers, and calendars localise too.
- **Data:** one composable per use case in `app/composables/`. Pinia only for state shared across routes.
- **State per view:** loading, empty, and error states are required, not polish.
- **Responsive:** mobile-first; verify 375 / 768 / 1280.
- API types come only from `pnpm gen:api` (`openapi/*.json` → `app/api/generated/*.ts`, both committed). Never
  hand-write a DTO type.
- All HTTP goes through `useApi()`. A 400 is shown with `useFormErrors()`, never a toast.
- Styling uses the `--crm-*` token utilities only. `ui-hover:`/`ui-focus:` inside `app/components/ui/`.
- Every new UI kit component, or new state, gets a section on `/_dev/ui`.
- pnpm is enabled with `corepack enable pnpm --install-directory "$APPDATA/npm"` on Windows without admin.
- Locale files are `frontend/i18n/locales/{en,ar}/<domain>.json`, one top-level key per file equal to the file
  name; `pnpm lint` fails on diverging key sets and on bare template strings outside `/_dev`.
- Format dates, numbers, currency, and sizes only through `useFormat()` (`Intl`, Gregorian, `Asia/Riyadh`).
  Identifiers inside Arabic text go in `<bdi dir="ltr">`.

## Testing policy — automated tests are out of scope

**This project ships without automated tests.** No unit test projects, no integration test projects, no Vitest
suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a
test framework (xUnit, NUnit, FluentAssertions, Testcontainers, Vitest, Playwright) to any project file. That
includes architecture-test libraries (NetArchTest, ArchUnitNET): module boundaries are enforced by
`Directory.Build.targets` instead.

This is a deliberate decision by the project owner, recorded here so no story or agent re-introduces a suite out
of habit. Worth knowing what it costs: regressions surface in manual verification or in production rather than in
CI, so refactors across the CQRS handlers, the aggregate invariants, and the RTL layouts carry more risk than
they otherwise would. If that trade stops being acceptable, the cheapest recovery is a thin integration suite
over the API endpoints — it does not require retrofitting unit tests first.

Because there is no test net, these take its place and are non-negotiable:

- **The compiler is the test.** `TreatWarningsAsErrors`, nullable reference types, and TypeScript `strict` stay
  on. Never silence an analyzer, and never add `!`, `any`, or a ts-ignore to make something compile.
- **The build is the architecture test.** The `CRM0001` module boundary rules, the banned-API lists, and the
  Presentation project's hidden Domain reference stay on. Never bypass them.
- **The domain model is the test.** Invariants enforced in aggregate constructors and methods are what a unit
  test would otherwise assert. An always-valid aggregate is worth more here than it is on a tested project.
- **Verify by running.** Every story is verified against the real endpoint (Swagger UI or the feature's `.http`
  file) and the real screen in a browser, in both `en` and `ar`.
- **Commit request samples.** Each feature keeps `backend/requests/<feature>.http` with a worked request per
  endpoint, so the next agent can re-verify without guessing payloads.

## Workflow (squad-kit)

1. `/squad-new-story <feature-slug> --title "..."` — scaffold an intake.
2. `/squad-plan .squad/stories/<feature>/<slug>/intake.md` — generate the executable plan.
3. `/squad-implement .squad/plans/<feature>/NN-story-<slug>.md` — execute it in a fresh scoped session.
4. `/squad-status` — see where the backlog stands.

`/squad-story <NN|next>` runs steps 2 and 3 for one MVP story: a planning subagent on the default model, then an
implementation subagent on Sonnet, each in its own context, handing over only the plan file.

- **Planning is read-only.** A planning session (or planning subagent) reads code and docs and writes the plan —
  no scratch builds, package installs, dev servers, API runs, or browser probes. What needs a run to prove goes
  in the plan as a risk for the implementation session to verify first.
- **A plan is saved with its implementation.** The plan file is not committed on its own; it lands in the same
  commit as the story's code (`Implement Story NN — <title>`).

Stories execute in `NN` order. `.squad/BACKLOG.md` is the source of truth for sequence, phase, and dependencies.
Every plan names the module (or modules) its story touches, using the module catalogue above.

## Verification

```
# backend
dotnet build backend/Crm.sln          # clean, zero warnings, no CRM0001

# frontend
pnpm --dir frontend typecheck         # clean
pnpm --dir frontend lint              # clean
pnpm --dir frontend build             # succeeds
```

Then verify by hand: run the API, exercise the story's endpoints from its `.http` file, and walk the story's
screens in the browser in both `en` (LTR) and `ar` (RTL). Do not report a story done until the commands above
pass, the manual walkthrough is done, and the intake's acceptance criteria are met.
