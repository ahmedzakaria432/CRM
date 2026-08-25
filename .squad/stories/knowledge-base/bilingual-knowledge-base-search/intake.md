# Story 38 — Bilingual knowledge base search

Story intake for the **Knowledge Base** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/knowledge-base/bilingual-knowledge-base-search/intake.md`.

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-38`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `knowledge-base`

---

## Title

```
Bilingual knowledge base search
```

---

## Description

Search is the knowledge base. If it does not find the right article in Arabic and in English, the content might as
well not exist.

- SQL Server full-text search over article titles, bodies, and tags, with separate indexing per locale and the
  correct language configuration for Arabic and English stemming.
- Arabic-specific normalisation done deliberately: alef, ya, and ta-marbuta variants folded, diacritics stripped,
  tatweel removed, and Arabic-Indic digits normalised, so a query typed one way finds content written the other.
- Relevance ranking weighted by field (title above body), by article status and freshness, by view count and
  helpful votes, and by whether the article is linked to the searcher's ticket category.
- Query features: multi-word AND semantics by default, exact phrase in quotes, prefix matching for typeahead, and
  a fuzzy fallback when a strict search returns nothing.
- Result presentation: highlighted snippets around the match, the article's category and audience, and its
  last-updated date.
- Audience filtering enforced in the query, never in the UI: anonymous callers can only match public articles,
  portal users public and customer-authenticated, agents everything within their scope.
- Faceted filtering by category, tag, language, and article type, with counts.
- Search analytics: logged queries, zero-result queries, and click-through per result, feeding a content-gap
  report that tells the KB owner what to write next.
- Typeahead suggestions with a strict latency budget, so it stays usable while typing.

## Acceptance criteria

- [ ] An Arabic query finds an Arabic article whose text uses different alef or ya variants than the query.
- [ ] A query with diacritics finds content written without them, and the reverse.
- [ ] An Arabic-Indic digit query matches content written with Western digits.
- [ ] An English query stems correctly, so a singular query finds a plural in the body.
- [ ] Title matches rank above body matches for the same term.
- [ ] An exact phrase in quotes returns only true phrase matches.
- [ ] A query with no strict match falls back to fuzzy and says that it did, rather than returning nothing.
- [ ] An anonymous search never returns an internal or customer-authenticated article, verified directly against
      the endpoint.
- [ ] An agent's search results are scoped to their departments for department-restricted content.
- [ ] Facet counts match the filtered result counts.
- [ ] Typeahead responds within the stated budget on a seeded set of 5,000 articles.
- [ ] Zero-result queries are logged and appear in the content-gap report.

## Dependencies

- **Blocked by / related ids:** CRM-37.
- **Depends on code areas or other stories:** the article model from story 37. Stories 39, 40, 45, and 46 all
  search through this service.

## Technical hints

- Do the Arabic normalisation at index time and at query time with the same function, or the two will diverge and
  the failures will look random.
- Put audience filtering in the SQL predicate. Filtering after ranking both leaks counts and corrupts relevance.
- Seed 5,000 realistic bilingual articles before judging relevance. Search tuned on ten articles tells you nothing,
  and there is no test suite to hold the behaviour still.

## Out of scope

- Semantic and vector search. Story 45 adds retrieval over this content for AI suggestions.
- The search UI. Those are stories 39 and 40.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10. Domain-Driven Design inside Clean Architecture (`Crm.Domain` <- `Crm.Application` <- `Crm.Infrastructure` / `Crm.Api`), CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server, minimal APIs, `Result<T>` returns, RFC 9457 problem details.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
