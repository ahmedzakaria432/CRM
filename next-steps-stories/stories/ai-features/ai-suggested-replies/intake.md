# Story 43 — AI suggested replies

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/ai-features/ai-suggested-replies/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-43`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI suggested replies
```

---

## Description

A drafted reply the agent edits and sends, grounded in this ticket's history and in the knowledge base rather than
invented. The agent stays the author; the AI removes the blank page.

- Reply suggestions generated from the ticket thread, the customer's history, the resolved category, and retrieved
  knowledge-base content from story 45's retrieval, so the answer is grounded rather than plausible.
- Two or three alternatives with distinct tones (concise, detailed, empathetic), in the customer's preferred
  language, matching the channel's constraints: full HTML for email, short plain text for SMS and WhatsApp.
- Grounding transparency: each suggestion lists the knowledge-base articles and prior messages it drew on, so an
  agent can check the claim rather than trust it.
- An explicit no-confident-answer path: when the retrieved content does not support an answer, the feature says so
  instead of producing a fluent guess. This is the most important behaviour in the story.
- Insertion into the composer from story 21 as an editable draft, never auto-sent, sharing the insertion mechanism
  with quick replies from story 30 and articles from story 39.
- Tracking: whether a suggestion was inserted, edited before sending, or discarded, plus the edit distance, so
  usefulness is measurable and reportable in story 53.
- Guardrails specific to this feature: never state a policy, price, commitment, or date not present in the source
  material; never promise a resolution time; and always keep the agent as the sender of record.
- Agent-level opt-out, and a per-department switch, since not every team wants it.

## Acceptance criteria

- [ ] Requesting suggestions on a ticket returns two or three distinct alternatives within the agreed latency.
- [ ] Suggestions are in the customer's preferred language, verified for an Arabic customer with an
      English-speaking agent.
- [ ] The SMS and WhatsApp variants respect the channel's length constraints; the email variant does not.
- [ ] Each suggestion lists the articles and messages it was grounded in, and each citation resolves to real
      content.
- [ ] When retrieval finds no supporting content, the feature reports that no confident answer is available and
      offers no invented one.
- [ ] A suggestion is never sent automatically; it always lands in the composer as an editable draft.
- [ ] Insertion preserves anything the agent has already typed.
- [ ] Insert, edit-then-send, and discard outcomes are recorded per suggestion with the prompt version.
- [ ] A suggestion containing a commitment absent from the source material is caught by the guardrail check in a
      hand-run adversarial pass and does not reach the composer.
- [ ] Internal notes never appear in a suggested customer-facing reply.
- [ ] Turning the feature off per agent or per department removes it entirely from the composer.
- [ ] With AI unavailable, the composer works exactly as it does without this story.

## Dependencies

- **Blocked by / related ids:** CRM-41, CRM-45, CRM-21, CRM-30, CRM-39.
- **Depends on code areas or other stories:** the AI seam from story 41, knowledge retrieval from story 45, the
  composer from story 21, and the insertion mechanism from stories 30 and 39.

## Technical hints

- Ground every suggestion in retrieved content and require citations in the structured output. An ungrounded
  suggestion is a liability, particularly with no test suite to catch a prompt regression.
- Measure edit distance between the suggestion and what was sent. It is the only honest signal of whether this
  feature helps.
- Walk an adversarial pass by hand before shipping: ask for a refund policy, a delivery date, and a discount that
  appear nowhere in the sources, and confirm the feature declines.

## Out of scope

- Sending anything automatically. Every suggestion is human-approved by design.
- The chatbot that answers customers directly. That is story 46.
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
