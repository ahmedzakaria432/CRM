# Story 50 — Customer satisfaction surveys and feedback

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Customer Portal** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/customer-portal/customer-satisfaction-surveys-and-feedback/intake.md`.

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`
- **Module (backend):** `Feedback` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-50`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-portal`

---

## Title

```
Customer satisfaction surveys and feedback
```

---

## Description

Measuring whether the support actually helped. Response rate depends almost entirely on how little the survey asks,
so the design constraint is brevity.

- Survey types: CSAT as a rating with an optional comment, CES as an effort question, and NPS at a configurable
  interval rather than per ticket.
- Trigger configuration: on resolution, on closure, after N days, or sampled at a configurable rate, per department
  and per channel, with a suppression window so one customer is not surveyed twice in a week.
- One-click response from the notification itself, where the rating is submitted from the email or message without
  a login and the follow-up comment is optional, because a second click halves the response rate.
- Delivery on the customer's preferred channel through story 36: email, SMS, WhatsApp, or in-portal, in their
  language.
- Single-use, expiring tokenised survey links, valid for one submission and not guessable, since the endpoint is
  unauthenticated by design.
- Negative-response handling: a score below a threshold raises an alert and can open a follow-up ticket through the
  story 35 rule engine, so a complaint reaches a human within minutes rather than a monthly report.
- Attribution captured with each response: ticket, customer, contact, agent, department, branch, channel, category,
  and whether the SLA was met, so story 53 can report satisfaction by every dimension that matters.
- Comment analysis through story 41: sentiment, theme extraction, and grouping so a hundred comments become five
  actionable themes.
- A public-facing thank-you page with the option to add a comment, and an unsubscribe-from-surveys preference that
  is honoured absolutely.

## Acceptance criteria

- [ ] Resolving a ticket sends a survey on the customer's preferred channel in their language, per the configured
      trigger.
- [ ] The rating can be submitted in one click from the message without authentication.
- [ ] A survey link is single-use and expires; a second submission or an expired link shows a clear localised
      message rather than an error.
- [ ] Survey links are not guessable, and a brute-force attempt is rate-limited and audited.
- [ ] The suppression window prevents a second survey to the same customer inside it.
- [ ] Sampling at a configured rate surveys approximately that share of resolutions, verified by counting over a
      seeded batch.
- [ ] A score below the alert threshold raises an alert and, where configured, opens a follow-up ticket attributed
      to the rule.
- [ ] Each response records ticket, customer, contact, agent, department, branch, channel, category, and SLA
      outcome.
- [ ] Comment themes and sentiment are extracted and grouped, and the feature degrades gracefully with AI
      unavailable.
- [ ] A customer who unsubscribes from surveys receives none, on any channel.
- [ ] The survey page and thank-you page render correctly in Arabic RTL on mobile.
- [ ] A response is never attributable to the wrong agent when a ticket changed hands; the attribution rule is
      explicit and documented.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-36, CRM-35, CRM-41.
- **Depends on code areas or other stories:** ticket resolution events from story 15, delivery from story 36, the
  rule engine from story 35, and AI comment analysis from story 41. Story 53 reports on this data.

## Technical hints

- Decide the agent-attribution rule for a reassigned ticket before building the reports, and write it down. Last
  assignee, first responder, and resolver all give defensible but different numbers, and story 53 must match.
- Put the rating in the message as one-click links per score. Anything requiring a page load first will halve the
  response rate.
- Honour unsubscribe at the notification service, not only in this feature, so no other story can route around it.

## Out of scope

- Market research surveys unrelated to support.
- Satisfaction reporting and dashboards. Those are stories 53 and 54.
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
