# Story 39 — Agent facing knowledge base and reply insertion

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Knowledge Base** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/knowledge-base/agent-facing-knowledge-base-and-reply-insertion/intake.md`.

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`
- **Module (backend):** `KnowledgeBase`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-39`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `knowledge-base`

---

## Title

```
Agent facing knowledge base and reply insertion
```

---

## Description

Knowledge where the agent already is. If finding an article means leaving the ticket, agents will answer from
memory instead, and consistency goes with it.

- A contextual knowledge panel inside the ticket detail screen from story 21, pre-searched from the ticket's
  subject, category, and latest customer message, so useful articles are already on screen before the agent asks.
- A full knowledge browser: category tree navigation, search from story 38, faceted filtering, and a reading view
  with related articles.
- Insertion into the composer in three forms: the full article body, a summary, or a public link, each localised
  to the customer's preferred language rather than the agent's, with the correct plain-text variant for SMS and
  WhatsApp.
- Feedback loop from the front line: mark an article helpful or unhelpful for this ticket, propose an edit, and
  flag an article as outdated, all routed to the article owner through story 36.
- Create-from-ticket: turn a well-answered ticket into a draft article, pre-filled with the resolution and the
  relevant messages, landing in story 37's draft state with attribution.
- Usage attribution: record which article was used on which ticket, so story 53 can report deflection and story 45
  can learn which articles actually resolve tickets.
- Personal favourites and a recently-used list, since every agent has a working set of a dozen articles.
- Internal-only articles visible here and never insertable into a customer-facing reply.

## Acceptance criteria

- [ ] Opening a ticket shows contextually relevant articles without the agent searching.
- [ ] Inserting an article into the composer inserts at the cursor, in the customer's preferred language, without
      destroying existing text.
- [ ] Inserting as a link produces a public URL that an anonymous visitor can open, and only for a public article.
- [ ] An internal-only article cannot be inserted into a public reply, and the UI explains why rather than failing
      silently.
- [ ] Inserting into an SMS or WhatsApp reply uses the plain-text variant.
- [ ] Marking an article helpful or unhelpful records the vote against the article and the ticket.
- [ ] Proposing an edit creates a suggestion visible to the article owner and notifies them once.
- [ ] Create-from-ticket produces a draft article pre-filled with the resolution and attributed to the agent.
- [ ] Article usage per ticket is recorded and queryable for reporting.
- [ ] Favourites and recently-used persist per agent across sessions.
- [ ] The panel and browser render correctly in `ar` RTL and are keyboard operable.

## Dependencies

- **Blocked by / related ids:** CRM-21, CRM-37, CRM-38, CRM-30.
- **Depends on code areas or other stories:** the ticket detail composer from story 21, article content from
  story 37, search from story 38, and the insertion mechanism shared with quick replies in story 30.

## Technical hints

- Insert in the customer's language, not the agent's. This is the single most common defect in this feature and no
  test will catch it.
- Reuse story 30's composer insertion point rather than building a second one, or the two will handle the cursor
  differently.
- Record usage on send, not on view, so deflection reporting in story 53 measures something real.

## Out of scope

- The public and portal reading experience. That is story 40.
- AI-suggested solutions. That is story 45.
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
