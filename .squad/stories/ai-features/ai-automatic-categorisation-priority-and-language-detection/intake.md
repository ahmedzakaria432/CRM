# Story 44 — AI automatic categorisation priority and language detection

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ai-features/ai-automatic-categorisation-priority-and-language-detection/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-44`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI automatic categorisation priority and language detection
```

---

## Description

The one AI feature permitted to act without a human, and therefore the one needing the tightest confidence
handling. It classifies an incoming ticket so the routing rules in story 34 have something to route on.

- Classification of an incoming ticket into a category from story 16's taxonomy, a suggested priority, a detected
  language, a detected sentiment, and an intent, each returned with a calibrated confidence score.
- A confidence threshold per field, configurable per department: above it the value is applied automatically and
  attributed to the AI on the timeline; below it the value is suggested and the ticket is flagged for triage
  rather than silently misfiled.
- Urgency and escalation signals: detected frustration, a threatened complaint, or a regulatory or legal mention
  raises a flag for a human, and never silently changes priority on its own.
- Language detection driving the customer's preferred language, the reply language in story 43, and language-based
  routing in story 34.
- Duplicate and known-issue detection: a new ticket resembling an open one links them as related and surfaces the
  match to the agent, rather than merging automatically.
- A correction feedback loop: when an agent changes an AI-applied category, the correction is recorded with the
  original prediction and its confidence, producing a report of where the classifier is wrong.
- Retraining input rather than retraining: corrections feed a few-shot example set held in the prompt registry, so
  accuracy improves through reviewed prompt changes rather than opaque drift.
- A batch reclassification command for a backlog, in dry-run first, so a taxonomy change can be applied
  deliberately.

## Acceptance criteria

- [ ] An incoming ticket receives a category, priority suggestion, language, sentiment, and intent, each with a
      confidence score.
- [ ] A classification above the configured threshold is applied automatically and attributed to the AI on the
      timeline, with the confidence recorded.
- [ ] A classification below the threshold is stored as a suggestion, the ticket is flagged for triage, and no
      field is changed.
- [ ] The category returned is always one from the live taxonomy; an invented category is rejected by schema
      validation.
- [ ] Detected language sets the customer's preferred language when it was previously unknown, and never
      overwrites an explicit customer choice.
- [ ] A frustration, complaint, or legal signal raises a flag for a human and does not itself change priority.
- [ ] A ticket resembling an open one is linked as related and surfaced, never merged automatically.
- [ ] An agent correcting an AI category records the correction with the original prediction and confidence.
- [ ] The accuracy report shows per-category precision against agent corrections over a period.
- [ ] Batch reclassification in dry-run reports what would change without changing anything.
- [ ] With AI unavailable, tickets fall back to the category default from story 16 and the pipeline continues.
- [ ] Arabic and English tickets classify correctly, verified across a hand-checked sample of at least fifty real
      tickets per language.

## Dependencies

- **Blocked by / related ids:** CRM-41, CRM-16, CRM-22, CRM-34.
- **Depends on code areas or other stories:** the AI seam from story 41, taxonomy from story 16, the inbound
  pipeline from story 22, and routing from story 34.

## Technical hints

- Constrain the output to the live taxonomy by passing it in the prompt and validating against it. A free-text
  category will produce categories that do not exist.
- Default the threshold high and lower it once the accuracy report justifies it. This is the only feature that acts
  without a human, and the cost of a wrong automatic action is a misrouted customer.
- Never let sentiment change priority automatically. It is the fastest route to a gamed queue.

## Out of scope

- Routing itself, which is story 34 and consumes this output.
- Suggested replies. That is story 43.
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
