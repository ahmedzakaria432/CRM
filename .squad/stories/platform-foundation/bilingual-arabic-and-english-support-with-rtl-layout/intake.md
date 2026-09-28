# Story 05 — Bilingual Arabic and English support with RTL layout

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/bilingual-arabic-and-english-support-with-rtl-layout/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`
- **Module (backend):** BuildingBlocks (request localisation) + frontend

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-05`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`

---

## Title

```
Bilingual Arabic and English support with RTL layout
```

---

## Description

Arabic and English are equal first-class locales, not a translation layer bolted on later. Landing this before
feature screens exist is far cheaper than retrofitting it, and on a project without tests a lint rule is the only
thing that will keep RTL from rotting.

- `@nuxtjs/i18n` configured with `en` as LTR and `ar` as RTL, with locale files split per feature domain.
- The `dir` and `lang` attributes set on the html element from the active locale, and Tailwind configured so
  logical utilities resolve correctly in both directions.
- A lint rule that fails the build on physical direction utilities: `ml-`, `mr-`, `pl-`, `pr-`, `text-left`,
  `text-right`, `left-`, `right-`, `border-l`, and `border-r`.
- Locale-aware formatting helpers for dates, times, relative times, numbers, currency in SAR, and file sizes,
  with optional Hijri display alongside Gregorian.
- Locale persistence: the user profile preference first, a cookie second, `Accept-Language` last.
- The backend counterpart: `Accept-Language` honoured for validation messages, email and SMS templates, and
  enum display names, behind an `ILocalizationService` over resource files.
- A key-parity script, wired into lint, that fails when the `en` and `ar` key sets diverge or a key is unused.
- `/_dev/ui` extended with a locale switcher so the whole kit can be reviewed in Arabic RTL in one pass.

## Acceptance criteria

- [ ] Switching locale flips the html `dir` between ltr and rtl without a full page reload and without layout
      breakage.
- [ ] `pnpm lint` fails on a physical direction utility and passes on the logical equivalent.
- [ ] `pnpm lint` fails when the `en` and `ar` locale files have different key sets.
- [ ] Dates, numbers, and currency render in each locale's conventions, reviewed side by side on `/_dev/ui`.
- [ ] An API validation error is returned in Arabic when `Accept-Language: ar` is sent, verified from an `.http`
      request.
- [ ] The chosen locale survives a browser refresh and a new session for a signed-in user.
- [ ] Arabic text renders with an Arabic-capable font and a tested fallback stack.
- [ ] Mixed-direction content, such as an Arabic sentence containing a Latin product code, renders without bidi
      artifacts.
- [ ] The agent and portal layouts and the full UI kit are walked in `ar` at 375px, 768px, and 1280px.

## Dependencies

- **Blocked by / related ids:** CRM-03, CRM-04.
- **Depends on code areas or other stories:** the frontend scaffold, layouts, and UI kit from story 04.

## Technical hints

- Prefer the `Intl` APIs over a date library; pull in a library only for Hijri conversion.
- Put the direction-utility ban in ESLint rather than in review guidance. It is the only enforcement this project
  will get.

## Out of scope

- Translating feature copy that does not exist yet. Each feature story owns its own locale keys.
- Custom branding and white-labelling. That is story 58.
- Any test suite.


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
