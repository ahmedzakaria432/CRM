# Story 37 — Knowledge base content model and authoring workflow

Story intake for the **Knowledge Base** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/knowledge-base/knowledge-base-content-model-and-authoring-workflow/intake.md`.

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-37`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `knowledge-base`

---

## Title

```
Knowledge base content model and authoring workflow
```

---

## Description

The articles, FAQs, and guides that let a customer answer their own question and an agent answer consistently.
Bilingual content with a real review workflow, not a wiki with no owner.

- `Article` as an aggregate root: a bilingual title and body, a slug per locale, a category, tags, an audience
  (public, customer-authenticated, or internal only), a status, an owner, a reviewer, an effective-from and
  review-due date, and attachments through story 12.
- Explicit versioning inside the aggregate: every publish creates an immutable version, drafts are separate from
  the live version, and rollback restores a previous version rather than editing history.
- A publishing workflow as a status machine enforced by the aggregate: draft, in review, approved, published,
  needs update, archived, with the legal transitions declared once and permissions per transition.
- Bilingual completeness as a first-class concern: an article can be published in one language while the other is
  still drafting, and the gap is visible in the admin list rather than silently serving English to an Arabic reader.
- A `Faq` variant: a short question-and-answer pair sharing the same aggregate with a different presentation, so
  FAQs are not a second content system.
- Structured content for solutions and guides: ordered steps with an optional image per step, prerequisites, and
  applies-to metadata, so a guide renders consistently rather than as freeform HTML.
- Article relationships: related articles, superseded-by, and a link to the ticket categories from story 16 that
  the article answers, which stories 43 and 45 use for suggestions.
- Review cycle: a review-due date, an owner reminder through story 36, and a stale-content report so the base does
  not quietly rot.
- Rich text authoring with a strict sanitiser, image upload, internal linking between articles, and a preview that
  matches the published rendering in both locales.

## Acceptance criteria

- [ ] An article can be drafted, sent for review, approved, and published, and each transition is permission-gated.
- [ ] An illegal transition, for example published straight back to draft, is rejected by the aggregate.
- [ ] Publishing creates an immutable version; editing afterwards produces a new draft without changing the live
      article.
- [ ] Rollback restores a previous version as the live one and preserves the intervening versions.
- [ ] An internal-only article is never returned by any public or portal endpoint, verified with an anonymous and
      a portal token.
- [ ] An article published in Arabic but not English is served in Arabic and is flagged as missing English in the
      admin list, rather than falling back silently.
- [ ] Authored HTML containing a script tag or an inline event handler is stored and rendered inert.
- [ ] A guide's ordered steps render in order with their images in both locales.
- [ ] An article past its review-due date appears in the stale-content report and notifies its owner once.
- [ ] Archiving an article removes it from search and from public access but keeps existing links resolving to a
      helpful message.
- [ ] Slugs are unique per locale, and changing a slug leaves a redirect from the old one.

## Dependencies

- **Blocked by / related ids:** CRM-09, CRM-12, CRM-16, CRM-36.
- **Depends on code areas or other stories:** permissions from story 09, storage from story 12, ticket categories
  from story 16, and notifications from story 36. Stories 38 to 40 and 45 read this content.

## Technical hints

- Model the version as a child entity of the article aggregate with a copy of the rendered content, not as a diff.
  Storage is cheap; reconstructing a diff chain during an incident is not.
- Treat the two locales as separate publishable units under one aggregate. A single status across both languages
  will force someone to publish untranslated content.
- Sanitise on write and on render, exactly as story 18 does, and share the sanitiser between them.

## Out of scope

- Search. That is story 38.
- The agent and public reading experiences. Those are stories 39 and 40.
- AI-suggested solutions. That is story 45.
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
