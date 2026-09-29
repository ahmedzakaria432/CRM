# Story 40 — Public help centre and FAQ experience

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Knowledge Base** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/knowledge-base/public-help-centre-and-faq-experience/intake.md`.

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`
- **Module (backend):** `KnowledgeBase`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-40`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `knowledge-base`

---

## Title

```
Public help centre and FAQ experience
```

---

## Description

The self-service surface. Every question answered here is a ticket that never gets created, which makes this the
highest-leverage screen in the product.

- A public, SSR-rendered help centre: a landing page with featured and popular articles, category browsing, and a
  prominent search box, fully bilingual with a locale switcher and correct RTL layout.
- Article reading view: bilingual content with a language toggle when both exist, a table of contents for long
  articles, step-by-step rendering for guides, attachments, and related articles.
- Search from story 38 with typeahead, highlighted snippets, facets, and a genuinely useful zero-result state that
  offers the nearest matches and a path to open a ticket.
- An FAQ view grouped by category with expand and collapse, deep-linkable to a single question.
- Feedback per article: was this helpful, an optional comment, and a still-need-help action that opens the web form
  from story 27 pre-filled with the article context, so the KB feeds the ticket rather than dead-ending.
- SEO and shareability: server-rendered content, per-locale canonical URLs, `hreflang` between the Arabic and
  English versions, Open Graph tags, structured data for FAQs, and a sitemap.
- Performance and accessibility as acceptance criteria rather than aspirations, since this page is the public face
  of the product and is often reached on a slow mobile connection.
- Branding from story 58, so the help centre looks like the customer's own site.

## Acceptance criteria

- [ ] The help centre is reachable without authentication and serves only public articles.
- [ ] Content is server-rendered and readable with JavaScript disabled.
- [ ] The locale switcher moves between the Arabic and English versions of the same article and preserves the
      reading position where possible.
- [ ] An article existing in only one language is served in that language with a clear notice, not a blank page.
- [ ] `hreflang` and canonical tags are correct for both locales, and the sitemap lists both.
- [ ] Search typeahead works on mobile, and results highlight the matched terms.
- [ ] A zero-result search offers nearest matches and a clear path to open a ticket.
- [ ] The still-need-help action opens the web form pre-filled with the article and search context.
- [ ] Helpful and unhelpful votes are recorded per article and per locale, and one visitor cannot inflate a count
      by repeat clicking.
- [ ] The Lighthouse performance and accessibility scores both meet the agreed threshold on a throttled mobile
      profile.
- [ ] The whole experience is walked in Arabic RTL at 375px, 768px, and 1280px.
- [ ] Branding tokens from story 58 apply without a code change.

## Dependencies

- **Blocked by / related ids:** CRM-37, CRM-38, CRM-27, CRM-58.
- **Depends on code areas or other stories:** article content from story 37, search from story 38, the web form
  from story 27, and branding from story 58. Story 49 links to it from the authenticated portal.

## Technical hints

- Cache rendered articles aggressively and invalidate on publish. This surface is read-heavy and public.
- Rate-limit the vote and feedback endpoints by IP and article, or the counts become meaningless.
- Verify with JavaScript disabled. It is the fastest way to confirm the SSR path is real rather than a hydration
  illusion.

## Out of scope

- The authenticated portal. Those are stories 47 to 50.
- The AI chatbot on the help centre. That is story 46.
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
