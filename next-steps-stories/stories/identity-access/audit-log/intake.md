# Story 10 — Audit log

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Identity and Access** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/identity-access/audit-log/intake.md`.

---

## Feature

- **Feature name (display):** Identity and Access
- **Feature slug (folder under `plans/`):** `identity-access`
- **Module (backend):** `Audit` (new module), fed by other modules' integration events

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-10`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `identity-access`

---

## Title

```
Audit log
```

---

## Description

A queryable, tamper-evident record of who did what, needed for support disputes, for compliance, and, on a
project without automated tests, as the primary forensic trail when something goes wrong in production.

- An append-only audit store: actor, impersonated-by where applicable, action, entity type and id, before and
  after values as JSON, correlation id, IP address, user agent, department, branch, and UTC timestamp.
- Automatic capture from the `SaveChangesInterceptor` for aggregate changes, plus explicit capture for
  non-entity events: sign-in, sign-out, failed sign-in, permission denied, export, impersonation, and outbound
  integration calls.
- A field-level redaction list so passwords, tokens, and configured sensitive customer fields never reach the
  before and after payload.
- Retention policy with a configurable window and an archive job.
- Frontend: an audit log screen with filters for actor, action, entity type, entity id, date range, department,
  and correlation id, a before-and-after diff viewer, and CSV export.
- An entity-history tab component that the customer and ticket detail pages reuse in stories 14 and 19.

## Acceptance criteria

- [ ] Creating, updating, and soft-deleting any auditable aggregate writes exactly one audit entry with a correct
      before and after diff.
- [ ] Audit entries cannot be updated or deleted through any API, including admin endpoints; the attempt returns
      405 or 403 rather than succeeding.
- [ ] Failed sign-in, permission denied, and data export each produce an audit entry.
- [ ] Changing a password produces an audit entry whose payload shows a redaction marker, never plaintext.
- [ ] The audit screen filters by every listed dimension and paginates server-side over a seeded high-volume
      data set.
- [ ] Searching by correlation id returns every audit entry produced by that one request.
- [ ] Only holders of the audit read permission can view the log, and the view itself is audited.
- [ ] The archive job moves entries past the retention window and leaves the live table queryable.
- [ ] An audit write failure never fails the business transaction it describes, confirmed by forcing the audit
      sink to error and observing the business operation still succeed.

## Dependencies

- **Blocked by / related ids:** CRM-02, CRM-03, CRM-09.
- **Depends on code areas or other stories:** the interceptor from story 02, the correlation id from story 03,
  and permissions from story 09.

## Technical hints

- Write entity audits inside the business transaction, and read and export events out of band so they never slow
  a request.
- Store before and after as JSON columns and index the entity-type plus entity-id pair, or the history tab will
  crawl once there is real data.
- Seed a few hundred thousand rows locally before judging the screen's performance. Without a test suite,
  volume problems will otherwise surface in production.

## Out of scope

- Application logs and traces, which are story 03. This is a business audit trail, not a log stream.
- The ticket activity timeline, which is story 19 and builds on the history component delivered here.
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
