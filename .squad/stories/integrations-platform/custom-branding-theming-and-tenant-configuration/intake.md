# Story 58 — Custom branding theming and tenant configuration

Story intake for the **Integrations and Platform** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/integrations-platform/custom-branding-theming-and-tenant-configuration/intake.md`.

---

## Feature

- **Feature name (display):** Integrations and Platform
- **Feature slug (folder under `plans/`):** `integrations-platform`
- **Module (backend):** `Organisation` (tenant configuration and branding)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-58`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `integrations-platform`

---

## Title

```
Custom branding theming and tenant configuration
```

---

## MVP scope (re-scoped 2026-09-29)

Part of the 2-day MVP cut (see `.squad/BACKLOG.md`, "MVP re-scope"). Moved from Phase 3 into the MVP, reduced to
per-organisation branding of the agent workspace and the customer portal. It is the last MVP story.

---

## Description

Making the product look like the organisation that bought it, with no deployment and no component change.

- `Branding` on the `Organisation` aggregate (story 06): display name per locale, logo (light and dark variants,
  PNG/SVG, max 512 KB, stored in the database or a local folder), primary and accent colours, and the Arabic font
  choice from a fixed list.
- Delivery through story 04's `--crm-*` CSS custom properties: the server renders the organisation's tokens into
  the page head (SSR for the portal, on shell load for the agent SPA), so a rebrand needs no component change. A
  change that requires editing a component means story 04's tokens were bypassed and must be fixed.
- A full colour scale derived from each chosen colour for both light and dark themes.
- Contrast validation on save: the palette is checked against WCAG AA for text on the primary colour and for
  interactive elements; a failing combination is rejected by the domain with an explanation.
- An anonymous endpoint returning the public branding (name, logo URLs, tokens) for the portal's sign-in pages.
- Admin screen (Admin Area, `admin.branding`): logo upload, colour pickers, a live preview of the agent shell and a
  portal page before saving, and revert to the previous version.

## Acceptance criteria

- [ ] Changing the primary colour and logo re-themes the agent workspace and the portal after a refresh, with no
      code change.
- [ ] `grep` finds no literal colour in `app/components/` or `app/layouts/`.
- [ ] A palette failing WCAG AA contrast is rejected with a localised explanation.
- [ ] The portal sign-in page shows the organisation's logo and colours before login, server-rendered.
- [ ] Logo uploads over the limit or not PNG/SVG (by content sniffing) are rejected.
- [ ] Revert restores the previous branding.
- [ ] Branding is walked in `en` and `ar`, light and dark, at 375px and 1280px.
- [ ] Every endpoint exercised from `backend/requests/branding.http`.

## Dependencies

- **Blocked by / related ids:** CRM-06, CRM-47.
- **Depends on code areas or other stories:** the `Organisation` aggregate (06), the token system (04), the portal
  layout and pages (47).

## Technical hints

- Sanitise uploaded SVGs (strip scripts and event attributes) or rasterise them; never serve user SVG unsanitised.
- Cache the public branding response (`ICacheableQuery`) and invalidate it on save.

## Out of scope (deferred post-MVP)

- Per-branch branding, custom domains and certificates, email/SMS/PDF template branding, system configuration
  screen, feature flags, configuration export/import. Any test project.

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
