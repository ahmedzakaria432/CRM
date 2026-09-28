# Story 42 — AI ticket summarisation

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ai-features/ai-ticket-summarisation/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai`; reads the thread through `Tickets.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-42`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI ticket summarisation
```

---

## Description

A long ticket that has passed through three agents and twenty messages takes minutes to understand. A good summary
takes seconds, which is the difference between a fast handover and a repeated question.

- On-demand and automatic summarisation of a ticket thread: the customer's problem, what has been tried, the
  current state, what is blocking, and the next action, produced as structured fields rather than a paragraph.
- Handover summaries generated at reassignment and department transfer, attached to the transfer so the receiving
  agent starts informed.
- Incremental summarisation: re-summarise only the messages added since the last summary and merge, so a long
  thread does not re-bill the whole history on every request.
- Bilingual output: the summary is produced in the reader's locale regardless of the thread's language, and a
  mixed Arabic and English thread summarises coherently in either.
- Caching keyed by ticket and last-message id, so re-opening a ticket does not regenerate an unchanged summary.
- Presentation as a collapsible panel in the ticket header from story 21, clearly labelled as AI-generated, with
  the generation timestamp, a regenerate action, and a stale marker once newer messages exist.
- Feedback: thumbs up or down with an optional reason, recorded per summary with the prompt version, so quality is
  measurable rather than anecdotal.
- Closure summaries stored on the ticket at resolution, used by the reports in story 52 and as training context
  for story 45.

## Acceptance criteria

- [ ] Requesting a summary of a twenty-message ticket returns the structured fields within the agreed latency.
- [ ] The summary is produced in the reader's locale, verified for an Arabic reader on an English thread and the
      reverse.
- [ ] A thread mixing Arabic and English produces a coherent single-language summary.
- [ ] Internal notes are included for an agent reader and never included in any customer-facing surface.
- [ ] Re-opening a ticket with no new messages serves the cached summary without calling the provider.
- [ ] Adding a message marks the summary stale, and regenerating updates only from the new content.
- [ ] Reassigning a ticket generates a handover summary attached to the transfer record.
- [ ] The panel is unmistakably labelled AI-generated and shows its generation time.
- [ ] Thumbs up and down are recorded against the summary with its prompt version.
- [ ] With AI disabled by the kill switch or the budget, the panel shows an unavailable state and the ticket
      remains fully workable.
- [ ] A malformed provider response leaves the previous summary intact and reports a retryable failure.

## Dependencies

- **Blocked by / related ids:** CRM-41, CRM-18, CRM-21.
- **Depends on code areas or other stories:** the AI seam from story 41, the message thread from story 18, and the
  ticket detail screen from story 21.

## Technical hints

- Ask for structured fields rather than prose. A five-field summary is scannable in two seconds; a paragraph is not.
- Key the cache on the last-message id, not on a timestamp, so an edit-free reopen is always free.
- Include the resolution and internal notes for the agent-facing summary, and never reuse that prompt for anything
  customer-facing. Keep them as two registered prompts.

## Out of scope

- Suggested replies. That is story 43.
- Customer-facing summaries in the portal.
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
