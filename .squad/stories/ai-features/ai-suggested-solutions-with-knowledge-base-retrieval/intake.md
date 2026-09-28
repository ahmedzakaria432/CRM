# Story 45 — AI suggested solutions with knowledge base retrieval

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ai-features/ai-suggested-solutions-with-knowledge-base-retrieval/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai`; retrieves through `KnowledgeBase.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-45`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI suggested solutions with knowledge base retrieval
```

---

## Description

Retrieval over the knowledge base and resolved-ticket history, so answers are grounded in what the organisation
actually knows. This is the retrieval layer that stories 43 and 46 both depend on.

- An embedding index over published knowledge-base articles from story 37 and the resolutions of closed tickets,
  chunked with overlap, embedded per locale, and stored as SQL Server vector columns with the metadata needed for
  filtering.
- Hybrid retrieval combining the vector search with story 38's full-text search and reciprocal-rank fusion,
  because keyword search wins on product codes and error strings while vectors win on paraphrase.
- Retrieval filters applied before ranking: audience, department scope, article status, locale, and effective date,
  so an internal or archived article can never leak into a customer-facing answer.
- Cross-lingual retrieval: an Arabic question finds a relevant English article and the answer is composed in Arabic.
- A grounded answer with mandatory citations, plus an explicit insufficient-evidence response when retrieval does
  not support an answer.
- Incremental indexing driven by domain events: publishing, updating, archiving, or deleting an article updates the
  index within a bounded delay, plus a full rebuild command for recovery.
- Presentation to the agent as a suggested-solutions panel on the ticket, ranked with relevance and citations, with
  actions to insert, open the article, or mark it as not relevant.
- A deflection signal: when a suggested article resolves a ticket, record it, so story 53 can report which content
  is actually doing the work.

## Acceptance criteria

- [ ] Publishing an article makes it retrievable within the stated indexing delay; archiving it removes it.
- [ ] Hybrid retrieval returns better results than either search alone on a hand-built set of at least thirty
      representative questions, judged and recorded.
- [ ] An exact error code or product code query retrieves the right article, where pure vector search would not.
- [ ] An Arabic question retrieves a relevant English article and produces an Arabic answer with correct citations.
- [ ] Every answer cites its sources, and every citation resolves to real, currently published content.
- [ ] When retrieval finds nothing sufficient, the response says so and offers no invented answer.
- [ ] An internal-only or archived article never appears in a customer-facing answer, verified directly against the
      endpoint with an anonymous token.
- [ ] Department-scoped content is never retrieved for a caller outside that scope.
- [ ] A full index rebuild reproduces the same retrieval results as the incrementally maintained index.
- [ ] The suggested-solutions panel ranks results with citations and records not-relevant feedback.
- [ ] Deflection is recorded when a suggested article precedes a resolution.
- [ ] With AI unavailable, the panel degrades to story 38's keyword search rather than disappearing.

## Dependencies

- **Blocked by / related ids:** CRM-41, CRM-37, CRM-38, CRM-15.
- **Depends on code areas or other stories:** the AI seam from story 41, articles from story 37, full-text search
  from story 38, and ticket resolutions from story 15. Stories 43 and 46 consume this retrieval.

## Technical hints

- Consult the `claude-api` skill for the current embedding options and token accounting before choosing a chunk
  size. Do not assume remembered limits.
- Keep a written evaluation set of at least thirty questions with expected articles and re-run it by hand after
  every prompt or chunking change. With no automated tests, this is the only way retrieval quality stays honest.
- Filter before ranking, in the query. Post-filtering both leaks the existence of restricted content and ruins
  relevance.

## Out of scope

- The chatbot conversation loop. That is story 46.
- Reply composition. That is story 43.
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
