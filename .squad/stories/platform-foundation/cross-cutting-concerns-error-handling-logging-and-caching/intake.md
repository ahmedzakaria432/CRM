# Story 03 — Cross cutting concerns error handling logging and caching

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/cross-cutting-concerns-error-handling-logging-and-caching/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`
- **Module (backend):** BuildingBlocks (cross-cutting; also delivers the in-process integration-event bus and outbox every module uses)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-03`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Cross cutting concerns error handling logging and caching
```

---

## Description

The observability and resilience baseline. With no automated test suite on this project, logs and traces are the
primary diagnostic surface, so this story matters more than it usually would.

- Global exception-handling middleware mapping domain errors, validation failures, not-found, conflict, and
  unauthorised into RFC 9457 problem details, with a stable type URI per error class.
- Structured logging with Serilog: JSON sink, request logging, and enrichers for correlation id, user id,
  tenant, department, and branch.
- Correlation-id propagation: accept `X-Correlation-Id`, generate one when absent, return it on the response,
  and attach it to every log line and outbound integration call.
- OpenTelemetry traces and metrics for HTTP, EF Core, and MediatR handlers, so a slow query is attributable to a
  request without a debugger.
- A distributed caching abstraction `ICacheService` with a Redis implementation, a pipeline behaviour that caches
  queries marked `ICacheableQuery`, and a convention for invalidating them from commands.
- Rate limiting on public endpoints, and a resilience pipeline of retry, circuit breaker, and timeout for
  outbound HTTP applied through a delegating handler.
- Integration events between modules: an `IIntegrationEvent` base in `Crm.SharedKernel`, an `IEventBus` in
  `Crm.BuildingBlocks.Application`, and a transactional outbox per module (an `outbox_messages` table in each module's
  own schema, written by `ModuleDbContext` in the same transaction as the change). A background dispatcher delivers
  each message in-process to every subscribing module's `IIntegrationEventHandler<T>`, each in the subscriber's own
  transaction, with retry and an idempotency record per handler so a redelivered event is not applied twice.
  Integration-event records live in the publishing module's `Contracts` project. This is the only cross-module
  write path in the modular monolith (see `CLAUDE.md`), so every later story that reacts to another module's change
  depends on it.
- A `backend/requests/diagnostics.http` file with a request per failure mode, so every error shape can be
  re-verified by hand in one pass.

## Acceptance criteria

- [ ] An unhandled exception returns 500 with a problem-details body carrying the correlation id, and never a
      stack trace or connection string in any environment.
- [ ] A validation failure returns 400, a missing resource 404, a domain-rule violation 422, and a concurrency
      clash 409, each with a distinct stable type URI.
- [ ] The same correlation id appears in the response header and in every log line for that request.
- [ ] Log output is JSON and includes user id, tenant, department, and branch when the caller is authenticated.
- [ ] A query marked cacheable hits the database once across two identical calls, confirmed from the EF Core log,
      and its cache entry is invalidated by the matching command.
- [ ] Exceeding the configured rate limit returns 429 with a `Retry-After` header.
- [ ] Pointing the resilient HTTP client at an endpoint that fails on demand shows the configured retries in the
      trace, then an open circuit after the configured failure count.
- [ ] No `Console.WriteLine` and no interpolated log strings remain; every log call uses a message template.
- [ ] A destructuring policy redacts password, token, apikey, and secret values from all log output, confirmed by
      posting a credential payload and inspecting the log.
- [ ] An integration event published by a command is written to that module's outbox in the same transaction, is
      not delivered when the command fails or rolls back, and is delivered once to each subscriber after commit;
      a subscriber that throws is retried, and a redelivered event is not applied twice. Confirmed on the Sample
      template module and in the EF Core log.
- [ ] Every failure mode above has a request in `backend/requests/diagnostics.http`.

## Dependencies

- **Blocked by / related ids:** CRM-01, CRM-02.
- **Depends on code areas or other stories:** the MediatR pipeline from story 01. The outbox extends
  `ModuleDbContext` and `TransactionBehavior` from story 02 and the modular-monolith building blocks.

## Technical hints

- Use `Microsoft.Extensions.Http.Resilience`; do not hand-roll retry loops.
- Keep the error type URIs in one static class so the frontend can map them to localised messages.
- Map a domain invariant violation to 422 and a validation failure to 400. Keeping them distinct is what makes
  the domain model debuggable from the outside.

## Out of scope

- Alerting and notification delivery to users. That is story 36.
- The audit log as a queryable business feature. That is story 10.
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
