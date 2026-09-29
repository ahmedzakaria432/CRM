# Story 55 — Public API versioning and API key management

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Integrations and Platform** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/integrations-platform/public-api-versioning-and-api-key-management/intake.md`.

---

## Feature

- **Feature name (display):** Integrations and Platform
- **Feature slug (folder under `plans/`):** `integrations-platform`
- **Module (backend):** `Integrations` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-55`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `integrations-platform`

---

## Title

```
Public API versioning and API key management
```

---

## Description

The CRM as an integration target. Once an external system depends on this API, its contract is no longer ours to
change casually, so versioning and documentation are part of the feature rather than an afterthought.

- A versioned public API surface (`/api/v1`) distinct from the internal endpoints the frontend uses, exposing
  customers, contacts, tickets, messages, attachments, categories, agents, and knowledge articles.
- OpenAPI documentation generated from the code with real examples, a published changelog, and a documented
  deprecation policy stating notice periods and sunset headers.
- Machine-to-machine authentication: API keys hashed at rest with a shown-once secret, scoped to a department and
  to a permission subset from story 09, with an expiry, an optional IP allow-list, rotation without downtime, and
  immediate revocation.
- Per-key rate limiting and quotas with `X-RateLimit` headers and a 429 carrying `Retry-After`, plus a per-key
  usage log so a noisy integration is identifiable rather than mysterious.
- Consistent conventions across every endpoint: cursor pagination, sparse field selection, filtering, sorting,
  `If-Match` and ETag concurrency, and RFC 9457 problem details identical to the internal API.
- Idempotency keys on every write, so a retrying client cannot create a ticket twice. This is the difference
  between a usable API and a support burden.
- Bulk endpoints for import and update with per-record results, so one bad record does not fail a batch of a
  thousand.
- A sandbox mode flag on a key, so an integrator can develop against non-production data.
- Developer experience: a request collection, an `.http` file, and a documented quick start, because an API nobody
  can call is not integrated.

## Acceptance criteria

- [ ] Every public endpoint is versioned, documented in OpenAPI with an example, and the document validates.
- [ ] An API key authenticates, is scoped to its department and permissions, and cannot exceed them.
- [ ] A key's secret is shown once at creation and is unrecoverable afterwards; only its hash is stored.
- [ ] Revoking a key takes effect immediately on the next request.
- [ ] Rotation issues a new secret while the old one remains valid for the configured overlap, then stops working.
- [ ] An IP outside a key's allow-list is rejected.
- [ ] Exceeding the key's rate limit returns 429 with `Retry-After`, and the rate-limit headers are present on
      every response.
- [ ] Replaying a write with the same idempotency key returns the original result and creates nothing new.
- [ ] Cursor pagination is stable while data changes underneath it: no duplicated and no skipped records.
- [ ] A stale `If-Match` on an update returns 409.
- [ ] A bulk request of 1,000 records reports per-record success and failure without aborting on the first error.
- [ ] Error responses are identical in shape to the internal API, with the same stable type URIs.
- [ ] A sandbox key cannot read or write production data.
- [ ] Every endpoint is exercised from `backend/requests/public-api.http` including the idempotency and concurrency
      paths.

## Dependencies

- **Blocked by / related ids:** CRM-09, CRM-03, CRM-11, CRM-15, CRM-37.
- **Depends on code areas or other stories:** permissions from story 09, rate limiting and error handling from
  story 03, and the customer, ticket, and article models. Story 56 delivers the outbound half.

## Technical hints

- Add idempotency keys before the first integrator arrives, not after. Retrofitting them means reconciling
  duplicate tickets by hand.
- Reuse story 09's permission catalogue for key scopes rather than inventing a second permission model.
- Keep the public DTOs separate from the internal ones. Sharing them means an internal refactor silently breaks a
  customer's integration, and there is no contract test here to catch it.

## Out of scope

- Outbound webhooks. That is story 56.
- The ERP integration itself. That is story 57.
- GraphQL.
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
