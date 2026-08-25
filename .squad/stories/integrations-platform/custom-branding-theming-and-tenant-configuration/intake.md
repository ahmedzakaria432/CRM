# Story 58 — Custom branding theming and tenant configuration

Story intake for the **Integrations and Platform** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/integrations-platform/custom-branding-theming-and-tenant-configuration/intake.md`.

---

## Feature

- **Feature name (display):** Integrations and Platform
- **Feature slug (folder under `plans/`):** `integrations-platform`

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

## Description

Making the product look like the organisation that bought it, and giving administrators the switches they need
without a deployment. This is the last story because it themes everything the previous fifty-seven built.

- Branding configuration per organisation and per branch: logo (light, dark, and favicon variants), colour palette,
  typography including an Arabic font choice, border radius scale, and an email template header and footer.
- Delivery through the CSS custom properties established in story 04, so a rebrand needs no component change. A
  branding change that requires editing a component means story 04's tokens were bypassed and must be fixed.
- Contrast validation on save: a chosen palette is checked against WCAG AA for text and interactive elements, and
  a failing combination is rejected with an explanation rather than shipped as an unreadable button.
- Branded surfaces: the agent workspace, the customer portal, the public help centre, the chat widget, web forms,
  email and SMS templates, PDF exports, and the survey pages. Every customer-facing surface, not only the app shell.
- A custom domain for the portal and help centre with certificate handling, plus a per-branch subdomain option.
- System configuration in one administered place: business hours defaults, ticket number format, default locale and
  timezone, file size and type limits, retention windows, feature flags per module, and every threshold earlier
  stories left configurable, each with a description, a validation rule, a default, and a change history.
- Feature flags per department, so a module such as AI or live chat can be enabled for one team first.
- A configuration export and import, so a tested configuration can be promoted between environments rather than
  re-entered by hand.
- A live preview of branding before publishing, and a revert to the previous version.

## Acceptance criteria

- [ ] Uploading a logo and setting a palette rebrands the agent workspace, portal, help centre, chat widget, and
      web forms with no code change.
- [ ] A palette failing WCAG AA contrast for text or interactive elements is rejected with an explanation naming
      the failing pair.
- [ ] Email, SMS, PDF, and survey templates all carry the branding, in both locales.
- [ ] The Arabic font choice applies wherever Arabic renders, with a working fallback stack.
- [ ] Branding is per organisation and can be overridden per branch, and the correct one applies by context.
- [ ] A custom domain serves the portal and help centre over HTTPS with a valid certificate.
- [ ] Every configuration setting has a description, validation, and a default, and an invalid value is rejected
      with a clear message rather than saved.
- [ ] Changing a configuration value takes effect without a deployment or a restart.
- [ ] Configuration changes are recorded in the audit log with actor and before and after values.
- [ ] A feature flag disabled for a department hides that module for its users and rejects its endpoints for them.
- [ ] Configuration export and import round-trips faithfully between two environments.
- [ ] Live preview shows the branding before publishing, and revert restores the previous version exactly.
- [ ] Both light and dark themes remain readable under a custom palette.

## Dependencies

- **Blocked by / related ids:** CRM-04, CRM-05, CRM-06, CRM-40, CRM-47.
- **Depends on code areas or other stories:** the design tokens from story 04, localisation from story 05,
  organisation scoping from story 06, and every customer-facing surface from stories 26, 27, 40, and 47 to 50.

## Technical hints

- If any surface needs a code change to rebrand, the fix belongs in that surface's use of story 04's tokens, not in
  a branding special case here.
- Validate contrast programmatically on save. Leaving it to judgement produces an inaccessible portal that nobody
  notices until a customer complains.
- Keep configuration in one typed, validated store bound through the options pattern with change notification, not
  scattered across `appsettings` files and database rows.

## Out of scope

- A full theme designer with arbitrary layout editing.
- Per-user personal theming beyond the light and dark switch from story 04.
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
