# Story 57 — ERP and external system integration

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Integrations and Platform** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/integrations-platform/erp-and-external-system-integration/intake.md`.

---

## Feature

- **Feature name (display):** Integrations and Platform
- **Feature slug (folder under `plans/`):** `integrations-platform`
- **Module (backend):** `Integrations`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-57`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `integrations-platform`

---

## Title

```
ERP and external system integration
```

---

## Description

Connecting the CRM to the systems of record, so an agent sees the customer's real account, orders, and invoices
without opening another application. The anti-corruption layer is the point: the ERP's model must not leak inward.

- An `IErpGateway` abstraction in `Crm.Modules.Integrations.Application` with an adapter per ERP in `Crm.Modules.Integrations.Infrastructure`, translating
  the ERP's model into CRM domain types at the boundary. No ERP type appears in any Domain project; other modules receive ERP data only as integration events or Contracts DTOs.
- Customer synchronisation: match by tax number, commercial registration, email, or phone; a configurable direction
  (ERP as master, CRM as master, or bidirectional per field); an explicit conflict policy; and a review queue for
  ambiguous matches rather than a silent merge.
- Read-through context on the ticket and customer screens: account status, credit standing, open orders, recent
  invoices, payment status, contracts, warranty and service entitlement, cached with a short TTL and a visible
  as-of timestamp so an agent knows how fresh it is.
- Write-back where the business requires it, narrowly scoped and audited: raise a service request, update a
  contact detail, or attach a ticket reference to an ERP record.
- Entitlement checking feeding the SLA policy in story 32, so a customer with a premium contract gets premium
  targets from data rather than from a manually maintained tier field.
- Resilience as a first-class requirement: the ERP will be down, slow, and occasionally wrong. Every panel degrades
  to a clear stale-or-unavailable state, and the CRM remains fully usable without it.
- Sync mechanics: scheduled batch for bulk reconciliation, event-driven for immediate changes, a full and a
  delta mode, a run log with per-record outcomes, and a reconciliation report listing mismatches.
- A field mapping configuration with transformation rules, so a new ERP field does not require a code change.
- Secrets and connectivity: credentials outside the database, never returned by an API, never logged, with mutual
  TLS or IP allow-listing where the ERP supports it.

## Acceptance criteria

- [ ] No ERP-specific type appears in any Domain, Application, or Contracts project; translation happens only in the adapter.
- [ ] A second ERP adapter can be added by implementing the gateway interface, with no change to the application
      layer.
- [ ] Customer matching links an ERP record to the correct CRM customer by tax number, and an ambiguous match lands
      in the review queue rather than merging.
- [ ] The configured sync direction is honoured per field, and a conflict is resolved by the configured policy and
      logged.
- [ ] ERP context panels show account status, orders, invoices, and entitlements with an as-of timestamp.
- [ ] With the ERP unreachable, every panel shows an unavailable state, and every other part of the CRM works
      normally.
- [ ] With the ERP slow, the panel times out at the configured limit and does not delay the ticket screen.
- [ ] Cached ERP data is served within its TTL and is visibly marked as cached with its age.
- [ ] A write-back is audited with actor, payload, and ERP response, and a failed write-back is surfaced rather
      than silently swallowed.
- [ ] An entitlement from the ERP selects the corresponding SLA policy from story 32.
- [ ] A delta sync processes only changes since the last run, and a full sync reconciles everything.
- [ ] The run log records per-record outcomes, and the reconciliation report lists every mismatch.
- [ ] ERP credentials never appear in an API response or a log line.
- [ ] A field mapping change takes effect through configuration with no code change.

## Dependencies

- **Blocked by / related ids:** CRM-11, CRM-32, CRM-03, CRM-55.
- **Depends on code areas or other stories:** the customer aggregate from story 11, SLA policies from story 32,
  resilience from story 03, and the integration conventions from story 55.

## Technical hints

- Treat the ERP as hostile: slow, occasionally wrong, and prone to schema drift. Every call gets a timeout, a
  circuit breaker, and a degraded UI state.
- Never block a ticket screen on an ERP call. Load the context panel independently and let it fail alone.
- Log the raw ERP response for a bounded window. Reconciling a mismatch without it is guesswork, and there is no
  test suite to reproduce it.

## Out of scope

- Building or modifying the ERP itself.
- Financial posting, payment processing, and accounting logic.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10 modular monolith. One module per bounded context under `backend/src/Modules/<Module>/` (`Domain` <- `Application` <- `Infrastructure` / `Presentation`, plus a public `Contracts` project), Domain-Driven Design with Clean Architecture inside each module, one host (`Crm.Api`). CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server with one DbContext and schema per module, MVC controllers in per-persona Areas, `Result<T>` returns, RFC 9457 problem details. The clock is read only through `IDateTime`; `System.DateTime` is banned.
- **Module boundaries:** a module reaches another only through that module's `Contracts` - a synchronous API interface for a read, an integration event (via the outbox) for "this happened". No cross-module project references, foreign keys, joins, or shared entities; one command, one module, one transaction. The build fails with `CRM0001` on a forbidden reference. The module catalogue in `CLAUDE.md` names the module each story belongs to.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events inside the module, and as integration events across modules; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
