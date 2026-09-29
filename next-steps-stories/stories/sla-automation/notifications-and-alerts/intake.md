# Story 36 — Notifications and alerts

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **SLA and Automation** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/sla-automation/notifications-and-alerts/intake.md`.

---

## Feature

- **Feature name (display):** SLA and Automation
- **Feature slug (folder under `plans/`):** `sla-automation`
- **Module (backend):** `Notifications` (new module); sends through `Communication.Contracts`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-36`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `sla-automation`

---

## Title

```
Notifications and alerts
```

---

## Description

One notification service every other feature calls, so preferences, quiet hours, digests, and delivery
reliability are solved once rather than six times.

- An `INotificationService` with a typed notification catalogue: ticket assigned to you, mentioned, SLA at risk,
  SLA breached, escalated, customer replied, task due, assistance requested, satisfaction received, and system
  alerts.
- Delivery channels: in-app (realtime through story 26 plus a persisted inbox), email through story 23, SMS
  through story 25, and optional web push.
- Per-user preferences per notification type per channel, with sensible defaults, plus quiet hours in the user's
  timezone and a digest option (immediate, hourly, or daily) so a busy queue does not produce a hundred emails.
- Critical-override: breach and system alerts bypass quiet hours and digests when configured as critical, because
  a silenced breach defeats the purpose.
- Localised templates per notification type in Arabic and English, with a shared layout, correct RTL rendering,
  and a deep link into the relevant screen.
- Deduplication and coalescing: five updates to one ticket within a window produce one notification, not five.
- Reliability: a persisted queue with bounded retries, per-channel rate limits, a dead-letter queue, and a
  delivery log recording attempted, delivered, and failed per notification and channel.
- An in-app notification centre: unread count in the shell, grouped list, mark-read and mark-all-read, and a
  filter by type.

## Acceptance criteria

- [ ] Assigning a ticket notifies the assignee in-app in realtime and by email according to their preferences.
- [ ] Turning off a notification type for a channel stops delivery on that channel and leaves the others working.
- [ ] Quiet hours suppress non-critical notifications and release them afterwards, in the user's own timezone.
- [ ] A notification marked critical is delivered during quiet hours regardless of the digest setting.
- [ ] The daily digest bundles the period's notifications into one message with correct counts and links.
- [ ] Five rapid updates to one ticket coalesce into one notification within the configured window.
- [ ] Every template renders correctly in Arabic RTL and in English, with a working deep link.
- [ ] A failed delivery is retried per policy and lands in the dead-letter queue after the configured attempts,
      with the failure visible in the delivery log.
- [ ] The unread count in the shell matches the notification centre, and marking read updates both immediately.
- [ ] A user never receives a notification about a ticket outside their scope.
- [ ] An opted-out SMS number from story 25 receives no non-critical notification.
- [ ] Every notification type is triggered by hand once and verified on every enabled channel.

## Dependencies

- **Blocked by / related ids:** CRM-23, CRM-25, CRM-26, CRM-33.
- **Depends on code areas or other stories:** the email and SMS adapters from stories 23 and 25, realtime from
  story 26, and SLA events from story 33. Stories 29, 31, and 35 are its main callers.

## Technical hints

- Build the notification catalogue as a typed enum with metadata rather than free-form strings. Preferences,
  templates, and reporting all key off it.
- Coalesce before delivery, not in the UI. The email is already sent by the time the UI could group it.
- Persist the queue. An in-memory queue loses a breach alert on the one deploy that matters.

## Out of scope

- Marketing or campaign messaging.
- The channel transports themselves, which are stories 23 to 26.
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
