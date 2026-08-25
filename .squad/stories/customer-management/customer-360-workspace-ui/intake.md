# Story 14 — Customer 360 workspace UI

Story intake for the **Customer Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-management/customer-360-workspace-ui/intake.md`.

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-14`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-management`

---

## Title

```
Customer 360 workspace UI
```

---

## Description

The agent-facing screens for customers: finding one fast, and then seeing everything about them on one page.

- Customer list: server-side paged table with a debounced search box, filters for status, type, tier, department,
  branch, and language, sortable columns, saved filter presets per user, and CSV export.
- Customer detail as a 360 page: a header with name, customer number, tier, status, owner, and the counters from
  story 13; a contacts panel; the interaction timeline with infinite scroll; a notes and attachments panel; an
  open-tickets list; and a history tab reusing the audit component from story 10.
- Create and edit as a drawer form with inline validation driven by the API's problem-details response, so
  server-side domain errors land on the right field rather than in a toast.
- Duplicate warning surfaced during create, with a side-by-side comparison and a merge action.
- A reusable customer picker component with async search, used by ticket creation and anywhere else a customer
  must be chosen.
- Quick actions from the header: new ticket, send email, send WhatsApp, and call, each pre-filled with the
  customer context.
- Every panel has explicit loading, empty, and error states, and the whole page is verified in `ar` and RTL.

## Acceptance criteria

- [ ] The list pages, sorts, and filters entirely server-side; no client-side filtering of a partial page.
- [ ] Search is debounced, cancels in-flight requests, and never renders results from a stale keystroke.
- [ ] Saved filter presets persist per user across sessions and are shareable by URL.
- [ ] The 360 page loads its panels independently: a failing timeline does not blank the page, it shows an error
      state inside that panel with a retry.
- [ ] The timeline scrolls infinitely with the cursor from story 13 and does not duplicate rows.
- [ ] A domain error returned as 422 lands on the specific field that caused it, in the active locale.
- [ ] The duplicate warning appears before the record is created and offers both proceed and merge.
- [ ] The customer picker searches asynchronously, is keyboard operable, and is usable with a screen reader.
- [ ] Quick actions carry the customer and contact context into the target screen with no re-selection.
- [ ] Every panel has a loading skeleton, an empty state with a next action, and an error state with a retry.
- [ ] The full page is walked in `ar` at 375px, 768px, and 1280px with no physical-direction utility in the diff.

## Dependencies

- **Blocked by / related ids:** CRM-04, CRM-05, CRM-11, CRM-12, CRM-13.
- **Depends on code areas or other stories:** the UI kit from story 04, localisation from story 05, and the
  customer, notes, and timeline stories 11 to 13. Story 21 reuses the picker and the history tab.

## Technical hints

- Fetch each panel with its own composable so one slow panel cannot block the rest of the page.
- Keep the filter state in the URL query string. It makes presets, sharing, and the browser back button work for
  free.
- Build the picker as a generic async-select in `app/components/ui/` with a customer-specific wrapper, since
  tickets, agents, and articles all need the same behaviour.

## Out of scope

- The ticket inbox and ticket detail screens. Those are stories 20 and 21.
- Portal-facing customer screens. Those are stories 47 to 50.
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
