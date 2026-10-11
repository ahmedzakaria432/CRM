# CRM — Customer Support CRM

A multi-department, multi-branch, bilingual (Arabic / English, full RTL) customer support CRM. Staff work tickets
and customers in an agent workspace; customers raise and follow their requests in a branded self-service portal.

The MVP covers **customer management**, **ticket management** (lifecycle, categories, assignment, conversation
thread, internal notes, inbox), the **customer portal**, **organisation scoping** by department and branch, and
**custom branding**. The build order and status are in [`.squad/MVP-STORIES.md`](.squad/MVP-STORIES.md).

## Running it

### Option 1 — everything in Docker

Requires Docker with Compose v2.20+.

```bash
docker compose up -d --build
```

Then open **http://localhost:8080**:

| URL | What |
|---|---|
| `/` | Landing: choose staff sign-in or the customer portal |
| `/login` | Staff sign-in (agent workspace and admin screens) |
| `/portal/login` | Customer portal |
| `/swagger` | API documentation, one document per Area |
| `http://localhost:18888` | Aspire dashboard: traces, metrics, structured logs |

The stack is SQL Server, Redis, the Aspire dashboard, the API, the Nuxt app, and nginx in front of the API and
the Nuxt app so the browser sees one origin. It is a **local demo stack, not a production deployment**: the API runs
in the `Development` environment, so on start it migrates every module's schema and seeds demo data.

Settings you can override with environment variables or a `.env` file next to `docker-compose.yml`:

| Variable | Default | Purpose |
|---|---|---|
| `CRM_HTTP_PORT` | `8080` | Host port for nginx |
| `MSSQL_SA_PASSWORD` | `Crm_Local_Dev_2026!` | SQL Server `sa` password |
| `CRM_ORGANISATION_CODE` | `DEMO` | Organisation whose branding the sign-in and portal pages show |

The root compose file shares its project name (`backend`) with [`backend/docker-compose.yml`](backend/docker-compose.yml).
Both use the same SQL Server and Redis containers and the same database volume, whichever one you started first.
`docker compose down` stops everything, and `docker compose down -v` also deletes the database.

### Option 2 — local development

Requires .NET SDK 10.0.301+, Node 24+, pnpm 12 and Docker (for SQL Server and Redis).

```bash
# 1. Infrastructure: SQL Server (localhost:14330), Redis (16379), Aspire dashboard (18888)
cd backend && docker compose up -d && cd ..

# 2. API on http://localhost:5187 - migrates and seeds on start in Development
dotnet tool restore
dotnet run --project backend/src/Crm.Api --launch-profile http

# 3. Frontend on http://localhost:3000 - proxies /api to the API
corepack enable pnpm --install-directory "$APPDATA/npm"   # Windows without admin; elsewhere: corepack enable
pnpm --dir frontend install
pnpm --dir frontend dev
```

Swagger UI is at `http://localhost:5187/swagger`. Every feature has worked requests in
[`backend/requests/`](backend/requests) (`.http` files) for checking endpoints by hand.

More detail: [`backend/README.md`](backend/README.md) (database, migrations, telemetry, configuration) and
[`frontend/README.md`](frontend/README.md) (scripts, API client generation, the `/_dev/ui` component gallery).

### Demo accounts

Seeded in Development only. Every password is `Crm_Dev_2026!`.

Staff sign in at `/login`:

| Email | Role: department (branch) | Demonstrates |
|---|---|---|
| `admin@crm.local` | Administrator: RYD-CARE, RYD-BILL (Riyadh), JED-CARE (Jeddah) | Admin nav and home quick links (Branding, Users); every department |
| `supervisor@crm.local` | Supervisor: RYD-CARE; Agent: RYD-BILL | Two roles in two departments: assign in Care, not in Billing; scope switcher |
| `agent.a@crm.local` | Agent: RYD-CARE (Riyadh) | A busy agent: My tickets and recent list; no admin links; cannot assign |
| `agent.b@crm.local` | Agent: JED-CARE (Jeddah) | Branch isolation: sees none of Riyadh's customers or tickets |
| `agent.c@crm.local` | Agent: RYD-BILL (Riyadh) | Department isolation in the same branch; no tickets assigned, so the home and inbox empty states |
| `supervisor.b@crm.local` | Supervisor: RYD-CARE (Riyadh) and JED-CARE (Jeddah) | Supervisor across two branches: scope switcher, assign and transfer in both |
| `agent.inactive@crm.local` | Agent: RYD-CARE (Riyadh), deactivated | Sign-in refused (401); shown inactive in Admin, Users |

Customers sign in at `/portal/login`:

| Email | Customer and contact | Demonstrates |
|---|---|---|
| `ahmed@example.com` | Customer Ahmed Al-Qahtani (primary contact) | Original portal account; account admin of his own requests |
| `mona@alnoor.example.com` | Al-Noor Clinics, primary contact | Account admin: sees all Al-Noor requests, including Yousef's |
| `yousef@alnoor.example.com` | Al-Noor Clinics, non-primary contact | Sees only his own requests |
| `huda@alnoor.example.com` | Al-Noor Clinics, portal access revoked | Sign-in refused (401) |
| `faisal@example.com` | Customer Faisal Al-Dosari (primary contact) | No requests: the portal empty state with New request |

All passwords: `Crm_Dev_2026!` (Development seed only; re-running the API never duplicates a row). Worked sign-ins for
every account are in [`backend/requests/demo-accounts.http`](backend/requests/demo-accounts.http).

Demo walkthrough: sign in to the portal as Ahmed and submit a request in Arabic. Then sign in as `agent.a`, find it
under **Tickets → Unassigned**, claim it, reply, add an internal note and resolve it. Back in the portal, Ahmed sees
the reply and the new status; the internal note is never shown to him.

## Architecture

```
            Browser
               │
               ▼
     nginx  (one origin)
       │              │
     /api/*       everything else
       │              │
       ▼              ▼
    Crm.Api  ◀───  Nuxt 4 app
  (.NET 10)   SSR   portal pages: SSR
       │      calls agent / admin: SPA
       │
       ├── SQL Server: one database, one schema per module
       ├── Redis: query cache
       └── OpenTelemetry ──▶ Aspire dashboard
```

### Backend — a modular monolith

One deployable ASP.NET host ([`backend/src/Crm.Api`](backend/src/Crm.Api)) runs a set of independent **modules**, one per
bounded context. Each module owns its domain model, use cases, database schema and HTTP endpoints, so a module could
later be split into its own service by moving a folder and its schema.

| Module | Owns |
|---|---|
| `Organisation` | Tenants, branches, departments, memberships, tenant branding |
| `Identity` | Staff users, roles and permissions, sign-in and refresh tokens, portal customer accounts |
| `Customers` | Customers, contacts, customer notes |
| `Tickets` | Tickets, lifecycle, categories, assignment and queues, conversation thread, internal notes, activity history |

The full catalogue, including modules planned after the MVP (SLA, communication channels, knowledge base, AI,
reporting and so on), is in [`CLAUDE.md`](CLAUDE.md).

**Inside a module: DDD and Clean Architecture.** Every module has five projects with one dependency direction:

```
Presentation ──▶ Application ──▶ Domain
                    ▲   │
Infrastructure ─────┘   └──▶ Contracts
```

- **Domain:** aggregates, value objects and domain events. Aggregates are always valid: no public setters, and every
  state change goes through an intention-revealing method such as `ticket.Claim(...)` or `ticket.TransferTo(...)`.
- **Application:** CQRS use cases on MediatR (one folder per command or query, each with its validator), DTOs, and
  the module's implementation of its Contracts.
- **Contracts:** the module's only public face to other modules: API interfaces, contract DTOs and integration events.
- **Infrastructure:** the module's EF Core `DbContext` in its own schema, its migrations, and its adapters.
- **Presentation:** MVC controllers, grouped into Areas (below).

**Between modules.** A module may reference only another module's `Contracts`. There are two ways across:

- **Synchronous reads** through a Contracts interface, for example `ICustomersModuleApi`. The call runs the owning
  module's query, so validation, authorisation and scoping all apply.
- **Integration events** for "this happened", for example `TicketCreated`. They are written to the publishing
  module's outbox in the same transaction and delivered after commit, at least once, with idempotent consumers.

No module reads another's tables, and there are no cross-schema foreign keys. The build enforces these rules:
`backend/Directory.Build.targets` fails with `CRM0001` on any project reference that crosses a boundary.

**Request pipeline.** Every MediatR request passes through the same behaviours, in this order:

1. **Validation:** FluentValidation; a failure is a 400.
2. **Authorisation:** every request declares a permission, and the handler never runs without it (401 or 403).
3. **Organisation scope:** the caller's tenant, departments and branches.
4. **Logging and tracing.**
5. **Transaction:** one module's unit of work, plus the outbox.
6. **Caching.**

Expected failures are `Result` values, translated into RFC 9457 problem details with stable `urn:crm:problem:*` types.

**Data isolation.** Operational rows carry tenant, branch and department ids. A query filter in the shared
`ModuleDbContext` restricts every read to the caller's scope, and a write guard refuses to save a row outside it.
An out-of-scope access is a 403, never an accidental 404. Portal customers get a narrower scope: they see only their
own customer's tickets.

**API Areas (one per persona).** Each Area has its own Swagger document:

| Area | For | Authentication |
|---|---|---|
| `Agent` | The staff workspace | Staff JWT |
| `Admin` | Configuration screens | Staff JWT plus admin permissions |
| `Portal` | Customers | Portal JWT (a separate audience) |
| `Public` | Anonymous reads, such as branding before sign-in | None; rate-limited |

**Cross-cutting.**

- **Logging:** Serilog compact JSON with a correlation id on every line.
- **Observability:** OpenTelemetry traces and metrics.
- **Caching:** Redis, with tag invalidation after commit; an outage degrades to uncached reads.
- **Resilience:** policies on every outbound `HttpClient`.
- **Localisation:** server messages follow `Accept-Language` (`en` or `ar`).
- **Time:** the clock is read only through `IDateTime`.

### Frontend — Nuxt 4, Vue 3, TypeScript, Tailwind

- **Rendering:** the customer portal and the sign-in pages are server-rendered, with tenant branding injected as CSS
  variables before first paint. The agent workspace and admin screens run as a single-page app.
- **API types:** generated from the backend's OpenAPI documents (`pnpm gen:api`). No DTO type is written by hand.
- **HTTP:** every call goes through one `useApi()` client, which handles the token refresh contract.
- **Bilingual and RTL by design:** `@nuxtjs/i18n` with `en` and `ar`, logical Tailwind utilities only (`ms-`, `pe-`,
  `text-start`; the linter rejects physical ones), and dates and numbers formatted through `Intl`.
- **Styling:** Tailwind with `--crm-*` design tokens only; branding overrides those tokens per tenant. Layouts are
  mobile-first and checked at 375, 768 and 1280 px.

## Repository layout

```
backend/
  Crm.sln
  src/Crm.Api/                  host: composition root, module catalogue, Swagger, health checks
  src/BuildingBlocks/           shared kernel, application/infrastructure/presentation building blocks
  src/Modules/<Module>/         Domain, Application, Contracts, Infrastructure, Presentation
  requests/                     .http request samples, one file per feature
  docker-compose.yml            local infrastructure (SQL Server, Redis, Aspire dashboard)
  Dockerfile                    API image
frontend/
  app/                          pages, layouts, components, composables, stores
  i18n/locales/{en,ar}/         translations, one file per domain
  openapi/, app/api/generated/  OpenAPI documents and the generated client types
  Dockerfile                    Nuxt image
docker/nginx.conf               reverse proxy for the Docker stack
docker-compose.yml              the whole stack
.squad/                         stories, plans and the MVP backlog
next-steps-stories/             stories deferred beyond the MVP
CLAUDE.md                       binding engineering conventions
```

## Quality gates

The backend has a unit-test suite (xUnit v3 + NSubstitute); the frontend deliberately has none. The compiler, the
analyzers, the boundary rules and always-valid aggregates sit alongside the backend tests, and every story is still
checked by hand against the real API and UI in both languages.

```bash
dotnet build backend/Crm.sln          # zero warnings (warnings are errors), no CRM0001
dotnet test --solution backend/Crm.sln   # or: cd backend && dotnet test --solution Crm.sln
pnpm --dir frontend typecheck
pnpm --dir frontend lint              # includes RTL and locale key checks
pnpm --dir frontend build
```

Conventions, the reasoning behind them, and the story workflow are in [`CLAUDE.md`](CLAUDE.md).
