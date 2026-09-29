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

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). This story also owns the **web and mobile
friendly** requirement for the shell: every layout and the UI kit are verified at 375 / 768 / 1280.

---

## Description

Arabic and English are equal first-class locales. Landing this before feature screens exist is far cheaper than
retrofitting it.

- `@nuxtjs/i18n` configured with `en` (LTR) and `ar` (RTL), locale files split per feature domain
  (`i18n/locales/{en,ar}/<domain>.json`), with the shell and UI-kit copy from story 04 moved into keys.
- The `dir` and `lang` attributes on the html element follow the active locale; logical Tailwind utilities
  resolve correctly in both directions.
- The physical-direction lint ban from story 04 extended to `left-`, `right-`, `border-l`, `border-r`, `rounded-l`,
  `rounded-r` (keep every existing rule).
- Locale-aware formatting helpers over `Intl`: date, time, relative time, number, SAR currency, file size.
- Locale persistence: cookie first, `Accept-Language` last (the user-profile preference is wired in story 07 once
  `GET /me` exists).
- A language switcher in the agent topbar and the portal header.
- Backend counterpart: `Accept-Language` (`en`, `ar`) sets the request culture; FluentValidation messages and the
  fixed problem-details `title`s are returned in that language, through resource files behind an
  `ILocalizationService` in `Crm.BuildingBlocks.Application`.
- A key-parity check wired into `pnpm lint` that fails when the `en` and `ar` key sets differ.
- `/_dev/ui` gets a locale switcher so the whole kit is reviewed in Arabic RTL in one pass.
- Responsive pass: the agent sidebar collapses to a drawer below 768px, the portal header stacks, tables scroll
  horizontally inside their container, and no page scrolls horizontally at 375px.

## Acceptance criteria

- [ ] Switching locale flips `dir` between ltr and rtl without a full reload and without layout breakage.
- [ ] `pnpm lint` fails on a physical-direction utility and on diverging `en`/`ar` key sets.
- [ ] Dates, numbers, and SAR currency render in each locale's conventions, shown side by side on `/_dev/ui`.
- [ ] `POST /api/Admin/Samples` with an empty name and `Accept-Language: ar` returns the validation message in
      Arabic, recorded in `backend/requests/localization.http`.
- [ ] The chosen locale survives a browser refresh.
- [ ] Arabic text renders with an Arabic-capable font (self-hosted or Google Fonts `IBM Plex Sans Arabic`) and a
      fallback stack.
- [ ] Mixed-direction content (an Arabic sentence with a Latin ticket number) renders without bidi artifacts.
- [ ] Agent layout, portal layout, and the UI kit are walked in `ar` and `en` at 375px, 768px, and 1280px with no
      horizontal page scroll.

## Dependencies

- **Blocked by / related ids:** CRM-03, CRM-04.
- **Depends on code areas or other stories:** the frontend scaffold, layouts, and UI kit from story 04; the
  problem-details pipeline from story 03.

## Technical hints

- Prefer `Intl` over a date library. No Hijri calendar in the MVP.
- Put every direction ban in ESLint, not review guidance.

## Out of scope (deferred post-MVP)

- Hijri calendar display; localised email and SMS templates (no channels in the MVP).
- Translating feature copy that does not exist yet — each feature story owns its locale keys.
- Custom branding (story 58). Any test suite.

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
