# Story 41 — AI service abstraction prompt infrastructure and guardrails

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ai-features/ai-service-abstraction-prompt-infrastructure-and-guardrails/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-41`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI service abstraction prompt infrastructure and guardrails
```

---

## Description

One AI seam every later AI story plugs into, so model choice, cost control, safety, and observability are solved
once. This is the anti-corruption layer for the model provider: no provider SDK type reaches the application layer.

- `IAiCompletionService` and `IAiEmbeddingService` in `Crm.Modules.Ai.Application`, implemented in `Crm.Modules.Ai.Infrastructure`, and exposed to other modules through `Crm.Modules.Ai.Contracts`
  against the Claude API, with the model id, temperature, and token limits bound through the options pattern per
  use case rather than hard-coded at the call site.
- A prompt registry: versioned, named prompt templates held as files with typed parameters, so a prompt change is
  a reviewable diff and the version used is recorded on every result.
- Structured output as the default: every use case declares a response schema and the response is validated
  against it before anything downstream sees it. An unparseable response is a handled failure, not an exception.
- Guardrails: strip or mask personally identifying data before sending, an allow-list of fields any prompt may
  include, a maximum input size with deterministic truncation of the oldest thread content, prompt-injection
  defences for customer-authored text, and a refusal path when the model declines.
- Cost and quota control: per-request token accounting, a per-department monthly budget, a per-user rate limit, a
  global kill switch, and a cache keyed by prompt version plus input hash so identical requests do not re-bill.
- Resilience: bounded retries with backoff on transient failures, a circuit breaker, a timeout, and a fallback
  that degrades the feature gracefully rather than blocking the agent's workflow.
- Observability: every call logged with use case, prompt version, model, input and output token counts, latency,
  cost, outcome, and correlation id, with the prompt and response retained for a configurable window for debugging.
- Human-in-the-loop by default: every AI output in this product is a suggestion attributed to the AI. Story 44 is
  the only one permitted to act automatically, and only under an explicit confidence threshold.

## Acceptance criteria

- [ ] No provider SDK type appears in any Domain, Application, or Contracts project; the model is reachable only through the
      two interfaces.
- [ ] Swapping the configured model id changes behaviour with no code change outside configuration.
- [ ] Every AI call records use case, prompt version, model, token counts, latency, cost, and outcome.
- [ ] A response that does not match the declared schema is rejected and surfaces as a handled failure with a
      localised message, never as an unhandled exception.
- [ ] Personally identifying fields outside the allow-list are masked before the request leaves the process,
      verified by inspecting a logged outbound payload.
- [ ] Customer-authored text attempting prompt injection does not change the system instruction, verified by hand
      against a set of recorded injection attempts.
- [ ] An input exceeding the size limit is truncated deterministically from the oldest content, and the truncation
      is recorded on the result.
- [ ] Exceeding the per-department budget or the per-user rate limit disables AI features with a clear localised
      message rather than failing silently or billing on.
- [ ] The kill switch disables all AI features immediately without a deployment.
- [ ] An identical request within the cache window does not call the provider again.
- [ ] A provider outage degrades every AI feature to unavailable while leaving the rest of the CRM fully working.
- [ ] Every guardrail is exercised by hand from `backend/requests/ai.http`, including the injection and
      budget-exceeded paths.

## Dependencies

- **Blocked by / related ids:** CRM-03, CRM-09.
- **Depends on code areas or other stories:** the resilience and caching infrastructure from story 03 and
  permissions from story 09. Stories 42 to 46 are all built on this seam.

## Technical hints

- Consult the `claude-api` skill for current model ids, pricing, and the structured-output and caching mechanics
  before writing the infrastructure implementation. Do not rely on remembered model names.
- Use prompt caching on the stable system instruction. It is the largest single cost saving available here.
- Keep prompts in version-controlled files, never in the database. Reviewing a prompt change is exactly as
  important as reviewing a code change, and there is no test suite to catch a regression.

## Out of scope

- Individual AI features. Those are stories 42 to 46.
- Model fine-tuning or self-hosting.
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
