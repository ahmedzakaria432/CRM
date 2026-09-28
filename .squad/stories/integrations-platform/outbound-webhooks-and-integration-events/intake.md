# Story 56 — Outbound webhooks and integration events

Story intake for the **Integrations and Platform** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/integrations-platform/outbound-webhooks-and-integration-events/intake.md`.

---

## Feature

- **Feature name (display):** Integrations and Platform
- **Feature slug (folder under `plans/`):** `integrations-platform`
- **Module (backend):** `Integrations`; publishes other modules' integration events to webhooks

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-56`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `integrations-platform`

---

## Title

```
Outbound webhooks and integration events
```

---

## Description

Letting other systems react to what happens in the CRM, without them polling. Delivery guarantees are the whole
story: a webhook that silently drops events is worse than none.

- A `WebhookSubscription` aggregate: a target URL, a subscribed event set, an HMAC signing secret, an active flag,
  a per-department scope, custom headers, and a retry policy.
- An event catalogue mapped from domain events: ticket created, updated, status changed, assigned, resolved,
  closed, reopened, message added, customer created and updated, satisfaction received, and SLA breached, each
  with a stable, versioned payload schema.
- Transactional outbox delivery: the event is written in the same transaction as the change, and a worker delivers
  it. An event is never lost because the HTTP call failed, and never sent for a transaction that rolled back.
- At-least-once delivery with a documented contract: a monotonic event id, a delivery attempt count, and a
  timestamp, so the consumer can deduplicate. Promising exactly-once over HTTP would be a lie.
- HMAC-SHA256 signature over the raw body with a timestamp, and a documented verification recipe, so a consumer
  can prove the payload came from us and is not a replay.
- Retry with exponential backoff and jitter, a circuit breaker per subscription so one dead endpoint cannot starve
  the queue, automatic suspension after sustained failure with a notification to the owner, and a dead-letter
  queue with manual replay.
- A delivery log per subscription with request, response status, response body excerpt, latency, and attempt count,
  retained for a configurable window, because every webhook conversation starts with "we never received it".
- SSRF protection: an outbound allow-list or a blocked private-address range, HTTPS required, DNS re-resolution
  guarded, and a hard timeout.
- Admin UI: subscription management, a test-send with a sample payload, the delivery log, and manual replay.

## Acceptance criteria

- [ ] Subscribing to an event and triggering it delivers a signed payload to the target URL.
- [ ] The event is written in the same transaction as the change, and a rolled-back transaction delivers nothing.
- [ ] A delivery failure retries with exponential backoff and jitter, and stops at the configured attempt limit.
- [ ] Sustained failure suspends the subscription and notifies its owner rather than retrying forever.
- [ ] A dead endpoint on one subscription does not delay delivery on any other.
- [ ] The HMAC signature verifies against the raw body using the documented recipe, and a modified body fails
      verification.
- [ ] A replayed request outside the timestamp tolerance is rejectable by the documented recipe.
- [ ] Every payload carries a monotonic event id and attempt count so a consumer can deduplicate.
- [ ] Payload schemas are versioned, and a schema change is additive or introduces a new version.
- [ ] A target URL resolving to a private or link-local address is rejected, including via a DNS rebind attempt.
- [ ] A slow endpoint is cut off at the configured timeout and does not hold a worker.
- [ ] The delivery log shows request, response status, latency, and attempts, and a dead-lettered event can be
      replayed successfully.
- [ ] Test-send delivers a realistic sample payload without touching real data.

## Dependencies

- **Blocked by / related ids:** CRM-55, CRM-02, CRM-03.
- **Depends on code areas or other stories:** the integration-event outbox from story 03 and domain-event dispatch from story 02, resilience
  from story 03, and the public API conventions from story 55. Story 35 uses webhook delivery as a rule action.

## Technical hints

- Sign the raw body bytes, not a re-serialised object. A consumer verifying against re-serialised JSON will fail
  intermittently and blame us.
- The SSRF check must happen at connection time against the resolved address, not only at subscription time
  against the hostname. A DNS rebind defeats hostname validation.
- Build the delivery log before the delivery worker. It is the only tool that will resolve a webhook dispute.

## Out of scope

- Inbound webhooks from channel providers, which are story 22.
- A message-broker integration such as Kafka or Service Bus.
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
