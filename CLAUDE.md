# CRM — Customer Support CRM

Multi-department, multi-branch, bilingual (Arabic/English) customer support CRM. Scope is defined by
`azm_squad_customer_support_crm.pdf` (12 feature areas). The story map lives in `.squad/BACKLOG.md`.

## Repository layout

```
backend/
  Crm.sln
  src/
    Crm.Domain/          aggregates, entities, value objects, domain events, domain services, invariants
    Crm.Application/     CQRS handlers, DTOs, validators, abstractions            (-> Domain)
    Crm.Infrastructure/  EF Core, SQL Server, channel adapters, AI, storage       (-> Application)
    Crm.Api/             MVC controllers in per-persona Areas, Swagger, auth, DI composition root  (-> Infrastructure)
  requests/              committed .http files, one per feature, for manual verification
frontend/                Nuxt 4 app (Vue 3 + TypeScript + Tailwind)
.squad/                  squad-kit stories, plans, and backlog
```

## Backend — .NET 10, DDD + Clean Architecture

- **Architecture:** Clean Architecture with a domain-driven core. Dependency direction is strictly inward:
  `Api -> Infrastructure -> Application -> Domain`. `Crm.Domain` references nothing.
- **Domain-Driven Design is the organising principle of `Crm.Domain`:**
  - **Bounded contexts** follow the 12 feature areas. `Customer`, `Ticket`, `KnowledgeBase`, `Sla`,
    `Organisation`, `Identity`, `Communication`. Each gets its own folder and its own model — a `Customer` in
    the ticket context is a reference, not a shared class.
  - **Aggregates** guard their own invariants. `Ticket` is an aggregate root; `TicketMessage`, `TicketNote`, and
    `SlaClock` are inside it and are reachable only through it. Never load or save a child directly.
  - **One transaction, one aggregate.** Cross-aggregate work goes through a domain event, never a second
    aggregate mutated in the same handler.
  - **Reference other aggregates by id only** (`CustomerId`, `AgentId`), never by navigation property.
  - **Value objects** for every concept with no identity: `EmailAddress`, `PhoneNumber`, `TicketNumber`,
    `SlaTarget`, `BusinessHours`, `Money`, `LocalizedText`. Immutable, self-validating, equality by value.
  - **No public setters and no parameterless public constructors on entities.** State changes happen through
    intention-revealing methods (`ticket.Escalate(reason, by)`, not `ticket.Priority = High`).
  - **Always-valid aggregates.** A constructor or factory method that cannot satisfy the invariants fails; there
    is no half-built entity. Guard clauses live in the domain, not in the handler.
  - **Domain events** are raised inside aggregate methods and dispatched after `SaveChanges`. Integration events
    leave through the outbox.
  - **Ubiquitous language.** Type and method names match the words in the story intake. If the intake says
    "escalate", the method is `Escalate` — not `UpdateStatusToLevel2`.
  - **Domain services** only for logic that spans aggregates and has no natural home (SLA target resolution,
    assignment strategy selection). Anything expressible on an aggregate belongs on the aggregate.
  - **Specifications** express reusable query intent (`OverdueTicketsSpecification`). Repositories expose
    aggregate roots only, one repository per aggregate.
  - **Anti-corruption layer:** external systems (ERP, WhatsApp, email providers) never leak their models inward.
    `Crm.Infrastructure` translates them into domain types at the boundary.
- **CQRS:** MediatR. One folder per use case under
  `Crm.Application/<Context>/{Commands,Queries}/<UseCase>/`, holding the request record, its handler, and its
  FluentValidation validator. Commands go through aggregates; queries bypass them.
- **API surface:** MVC controllers, not minimal APIs. One **Area per persona** — `Agent`, `Portal`, `Admin`,
  `Public` — under `Crm.Api/Areas/<Area>/Controllers/`. Every controller derives from `ApiControllerBase`,
  carries `[Area(CrmAreas.X)]`, and inherits the `api/[area]/[controller]` route template. Each Area gets its
  own Swagger document at `/swagger/{area}/swagger.json`; the UI at `/swagger` is Development-only.
- **Queries** project straight to DTOs with `.Select(...)`, use `AsNoTracking()`, and never return an entity or
  a domain type across the API boundary.
- **Results:** handlers return `Result` / `Result<T>`. Expected business failures are results, not exceptions.
  The API translates them to RFC 9457 `ProblemDetails`.
- **Pipeline behaviours:** validation -> authorisation -> logging -> transaction -> caching.
- **Persistence:** EF Core 10 + SQL Server. Configuration via `IEntityTypeConfiguration<T>` classes only, never
  data annotations. Value objects map as owned types or with a `ValueConverter`. Migrations ship in the same
  change as the schema move.
- **Auditing:** entities derive from `AuditableEntity` (created/modified by + at). Soft delete via
  `ISoftDeletable` + a global query filter.
- **Scoping:** every query and command is scoped by tenant, department, and branch through
  `ICurrentUserService`. There is no "global" read path outside explicitly authorised admin endpoints.
- **Patterns in use:** repository + specification, strategy (channel adapters, assignment rules), decorator
  (caching, retry), outbox (outbound integrations), factory (aggregate creation), options pattern
  (configuration). Do not add a pattern without a use case that needs it.

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

## Testing policy — automated tests are out of scope

**This project ships without automated tests.** No unit test projects, no integration test projects, no Vitest
suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a
test framework (xUnit, NUnit, FluentAssertions, Testcontainers, Vitest, Playwright) to any project file.

This is a deliberate decision by the project owner, recorded here so no story or agent re-introduces a suite out
of habit. Worth knowing what it costs: regressions surface in manual verification or in production rather than in
CI, so refactors across the CQRS handlers, the aggregate invariants, and the RTL layouts carry more risk than
they otherwise would. If that trade stops being acceptable, the cheapest recovery is a thin integration suite
over the API endpoints — it does not require retrofitting unit tests first.

Because there is no test net, these take its place and are non-negotiable:

- **The compiler is the test.** `TreatWarningsAsErrors`, nullable reference types, and TypeScript `strict` stay
  on. Never silence an analyzer, and never add `!`, `any`, or a ts-ignore to make something compile.
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

Stories execute in `NN` order. `.squad/BACKLOG.md` is the source of truth for sequence, phase, and dependencies.

## Verification

```
# backend
dotnet build backend/Crm.sln          # clean, zero warnings

# frontend
pnpm --dir frontend typecheck         # clean
pnpm --dir frontend lint              # clean
pnpm --dir frontend build             # succeeds
```

Then verify by hand: run the API, exercise the story's endpoints from its `.http` file, and walk the story's
screens in the browser in both `en` (LTR) and `ar` (RTL). Do not report a story done until the commands above
pass, the manual walkthrough is done, and the intake's acceptance criteria are met.
