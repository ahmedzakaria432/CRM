# Story 30 — Quick replies and canned responses

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **Agent Workspace** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/agent-workspace/quick-replies-and-canned-responses/intake.md`.

---

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`
- **Module (backend):** `Workspace`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-30`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `agent-workspace`

---

## Title

```
Quick replies and canned responses
```

---

## Description

Agents answer the same twenty questions all day. Quick replies make that fast and consistent, and keep the
official wording in both languages under someone's control.

- `QuickReply` as an aggregate root: a localised title, Arabic and English bodies, a shortcut keyword, a category,
  a scope (personal, team, department, or organisation-wide), an active flag, and a usage counter.
- Placeholder substitution with a fixed, validated vocabulary: customer name, contact first name, ticket number,
  agent name, department, branch, SLA due date, and the ticket's category. An unresolvable placeholder is caught
  before send rather than mailed to a customer as raw text.
- Insertion in the composer by picker, by typing a slash shortcut, or by keyboard shortcut, inserting at the
  cursor without destroying what the agent has already written.
- Channel awareness: a reply may carry a rich-text version for email and a plain-text version for SMS and
  WhatsApp, and the correct one is inserted for the active channel.
- Governance: only permitted roles publish organisation-wide replies; personal ones are private to their owner.
  Editing a shared reply is audited.
- Attachments on a quick reply, such as a standard form or a policy PDF, stored through story 12.
- Admin management screen with search, category grouping, a live preview showing substituted placeholders,
  duplicate detection, and a usage report identifying replies nobody uses.

## Acceptance criteria

- [ ] A quick reply can be inserted from the picker, by slash shortcut, and by keyboard shortcut, always at the
      cursor and never replacing existing text.
- [ ] Placeholders resolve against the current ticket, and an unresolvable placeholder blocks insertion with a
      clear message instead of sending raw text.
- [ ] Inserting into an Arabic reply produces correct RTL text with correctly placed substituted values.
- [ ] The plain-text variant is used for SMS and WhatsApp and the rich variant for email, automatically.
- [ ] A personal reply is invisible to other agents; a department reply is invisible outside that department.
- [ ] Only permitted roles can create or edit an organisation-wide reply, and every such edit is audited.
- [ ] Attachments on a quick reply are attached to the outbound message when the reply is inserted.
- [ ] The usage counter increments on send, not on insertion, and the usage report identifies unused replies.
- [ ] Duplicate detection warns when a new reply closely matches an existing one.
- [ ] The picker is searchable by title, keyword, and body in both languages, and is keyboard operable.

## Dependencies

- **Blocked by / related ids:** CRM-18, CRM-21, CRM-12.
- **Depends on code areas or other stories:** the message model from story 18, the composer from story 21, and
  storage from story 12. Story 43 offers AI-generated replies alongside these curated ones.

## Technical hints

- Validate placeholders against a declared vocabulary at save time, not at send time. Catching a typo when the
  reply is authored is worth far more than catching it in front of a customer.
- Count usage on send. Counting insertions rewards indecision and makes the unused-reply report useless.
- Keep the plain-text variant a real field rather than a stripped-HTML derivation. Auto-stripping produces
  unreadable SMS.

## Out of scope

- AI-generated reply suggestions. That is story 43.
- Knowledge-base article insertion, which is story 39, though both share the composer insertion point.
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
