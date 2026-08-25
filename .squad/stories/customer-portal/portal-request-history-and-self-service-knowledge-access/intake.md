# Story 49 — Portal request history and self service knowledge access

Story intake for the **Customer Portal** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-portal/portal-request-history-and-self-service-knowledge-access/intake.md`.

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-49`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-portal`

---

## Title

```
Portal request history and self service knowledge access
```

---

## Description

The rest of the portal: everything the customer has ever asked, and the self-service content that stops them
needing to ask again.

- Full request history across all channels, not only portal-submitted tickets: email, WhatsApp, chat, phone, and
  web form all appear, so the portal is the complete record from the customer's side.
- History filters by status, category, date range, and channel, with search across subject and public message
  content, and CSV or PDF export of the customer's own history.
- A closed-ticket archive with the resolution shown in customer-appropriate language, and a reopen action where the
  window from story 48 still allows it.
- An embedded knowledge base: the story 40 help centre inside the authenticated portal, plus articles restricted to
  authenticated customers that the public help centre does not serve.
- Personalised content: articles related to the customer's ticket history and product or service profile, and the
  articles that resolved their previous tickets.
- Account-level view for an account-administrator contact: every ticket for the customer with the submitting
  contact shown, and per-contact filtering.
- Announcements and known issues: a banner and a page for planned maintenance or a live incident, targetable by
  customer segment, so a widespread outage does not generate five hundred identical tickets.
- Downloadable documents shared with the customer, reusing story 12's storage and its permission checks.

## Acceptance criteria

- [ ] History shows tickets from every channel, not only those submitted through the portal.
- [ ] Filters and search narrow the history correctly and combine, and search matches only public content.
- [ ] Export produces only the signed-in contact's entitled data, and the export is audited.
- [ ] A closed ticket shows its resolution in customer-appropriate localised language, with no internal
      resolution code exposed.
- [ ] Customer-authenticated articles are visible in the portal and are not served by the public help centre.
- [ ] Personalised recommendations are drawn from the customer's own history and never reference another
      customer's data.
- [ ] An account-administrator contact sees all contacts' tickets with the submitter identified; an ordinary
      contact sees only their own.
- [ ] An announcement targeted at a segment appears only for customers in that segment.
- [ ] A document shared with the customer is downloadable by them and by nobody else, verified by requesting it
      with another customer's token.
- [ ] Every screen renders correctly in Arabic RTL on mobile and passes a keyboard-only walkthrough.
- [ ] The history list pages server-side and stays responsive for a customer with 500 tickets.

## Dependencies

- **Blocked by / related ids:** CRM-48, CRM-40, CRM-13, CRM-12.
- **Depends on code areas or other stories:** portal tickets from story 48, the help centre from story 40, the
  interaction timeline from story 13, and storage from story 12.

## Technical hints

- Reuse story 13's timeline projection for the cross-channel history rather than querying each channel's tables
  again, and filter it to customer-visible entries.
- Put the announcement banner behind segment targeting from the start. Retrofitting targeting during an incident is
  the wrong moment to discover it is missing.
- Re-check document permissions on every download request rather than trusting a previously issued link.

## Out of scope

- Satisfaction surveys. That is story 50.
- Billing, invoices, and orders, which belong to the ERP integration in story 57.
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
