# Story 34 — Automatic assignment and routing rules

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **SLA and Automation** feature of the Customer Support CRM.
Plan it with `/squad-plan next-steps-stories/stories/sla-automation/automatic-assignment-and-routing-rules/intake.md`.

---

## Feature

- **Feature name (display):** SLA and Automation
- **Feature slug (folder under `plans/`):** `sla-automation`
- **Module (backend):** `Automation` (new module)

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-34`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `sla-automation`

---

## Title

```
Automatic assignment and routing rules
```

---

## Description

Routing a ticket to the right agent without a supervisor doing it by hand. The strategy pattern is the whole
design here: several assignment algorithms behind one interface, chosen by configuration.

- `AssignmentRule` as an aggregate root: a localised name, priority order, an active flag, matching conditions,
  and the strategy to apply with its parameters.
- Matching conditions composable over channel, category, priority, customer tier, keywords in subject or body,
  business hours, department, branch, and the submitting form.
- Strategies behind one `IAssignmentStrategy` interface: round-robin within a team, least-open-tickets, weighted
  by priority load, skill-based against agent skills from story 08, language-based matching the customer's
  preferred language, direct-to-agent, direct-to-team queue, and sticky-to-previous-agent for a returning customer.
- Eligibility filters applied before any strategy runs: the agent must be active, available per story 08, not out
  of office, within their maximum concurrent load, and a member of the target department.
- The unassignable path made explicit: when no agent is eligible, the ticket goes to a named fallback queue and
  raises an alert. It must never sit silently unassigned.
- Rule ordering with first-match-wins, plus a dry-run mode and a simulator that explains which rule matched and
  which agent would be chosen and why.
- Reassignment triggers: on category change, on department transfer, on agent deactivation, and on out-of-office
  activation.
- Load fairness over time, so round-robin does not repeatedly favour whoever happened to be online at a quiet hour.

## Acceptance criteria

- [ ] A new ticket matching a rule is assigned automatically within seconds, and the assignment is attributed to
      the rule by name on the timeline.
- [ ] Rules evaluate in priority order with first-match-wins, and the simulator explains the decision.
- [ ] Round-robin distributes evenly across an eligible team over 100 tickets, verified by counting.
- [ ] Least-open-tickets picks the agent with the fewest open tickets, and the weighted variant accounts for
      priority.
- [ ] Skill-based and language-based routing select an agent with the required skill or language and skip those
      without it.
- [ ] An agent at their maximum concurrent load, unavailable, or out of office is never selected.
- [ ] When no agent is eligible, the ticket lands in the fallback queue and an alert is raised.
- [ ] Adding a new strategy requires implementing the interface and registering it, with no change to the engine.
- [ ] Dry-run mode reports what would happen without assigning anything.
- [ ] Two tickets arriving simultaneously are not both assigned to the same agent past their load limit.
- [ ] Deactivating an agent or setting them out of office reassigns per the configured trigger.
- [ ] Every strategy is exercised from `backend/requests/assignment.http` against a seeded team.

## Dependencies

- **Blocked by / related ids:** CRM-17, CRM-08, CRM-16.
- **Depends on code areas or other stories:** manual assignment from story 17, agent skills, load, and
  availability from story 08, and taxonomy from story 16. Story 44 can set the category this engine routes on.

## Technical hints

- Keep the strategy interface narrow: given a ticket and a set of eligible agents, return one agent or nothing.
  Everything else belongs in the engine.
- Persist round-robin position rather than deriving it from counts, or a restart resets fairness.
- Guard the concurrent-assignment case with the aggregate's rowversion, exactly as story 17 does for claims.

## Out of scope

- Escalation rules, which are story 35 and run on a different trigger.
- AI-driven categorisation feeding the rules. That is story 44.
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
