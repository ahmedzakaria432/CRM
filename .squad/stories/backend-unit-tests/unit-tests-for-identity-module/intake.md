# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/backend-unit-tests/unit-tests-for-identity-module/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Unit tests - Identity module
- **Feature slug (folder under `plans/`):** `backend-unit-tests`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `UT-03`` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Technical story``
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Unit tests for Identity module
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As the project owner I want unit tests for everything implemented so far in the Identity module,
so that regressions in its aggregates, handlers and validators are caught by `dotnet test`.

The Identity module currently implements stories 07 (staff sign-in), 08 (roles and permissions), 09 (refresh tokens / sessions) and 47 (portal customer accounts).
Tests go in the existing backend/tests/Crm.Modules.Identity.Tests project (created by UT-01), which references
the module's Domain, Application and Infrastructure projects. Other modules are reachable only through
their Contracts projects (build rule CRM0001, rule 1) - substitute their *ModuleApi interfaces.

Domain types to cover include: User, LockoutPolicy, DisplayName, EmailAddress, PreferredLocale, RefreshToken, RefreshTokenFamily, RefreshTokenRotation, SessionRevocationReason, PortalAccount, PortalSession, plus Infrastructure adapters such as password hashing and JWT/token issuing.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Domain: every aggregate, entity and value object in Crm.Modules.Identity.Domain has tests for its
      factory/constructor invariants (valid input and each rejected input -> DomainException / failure),
      every intention-revealing method (happy path and each guard), and the domain events it raises.
- [ ] Application commands: every command handler in Crm.Modules.Identity.Application has tests for success and
      for each expected failure Result (not found, conflict, forbidden/out-of-scope, domain rule), with
      repositories, IIdentityUnitOfWork, IDateTime, ICurrentUserService, IEventBus and other modules' Contracts
      APIs substituted; integration events published from handlers are asserted.
- [ ] Validators: every FluentValidation validator in the module has valid and invalid cases for each rule.
- [ ] Integration-event consumers and domain-event handlers in the module are tested.
- [ ] Query handlers and the module's Contracts API implementation are tested where they contain logic beyond
      a straight projection (EF Core InMemory allowed for those).
- [ ] Unit-testable Infrastructure adapters of the module (not EF configurations or migrations) are tested.
- [ ] `dotnet build backend/Crm.sln` clean: zero warnings, no CRM0001, no suppressed analyzer.
- [ ] `dotnet test --project tests/Crm.Modules.Identity.Tests` (from backend/) passes, zero failures, zero skipped.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| *(e.g. `attachments/flow.png`)* | *(e.g. UX flow)* |

None.

---

## Dependencies

- **Blocked by / related ids:** (tracker ids only; optional short note)
- **Depends on code areas or other stories:** UT-01 (test foundation must be merged first). Covers the code of stories 07 (staff sign-in), 08 (roles and permissions), 09 (refresh tokens / sessions) and 47 (portal customer accounts).

## Extra notes (optional)

- Conventions for every unit-test story (set by UT-01):
  - xUnit v3 + NSubstitute, plain `Assert` only (FluentAssertions 8+ is a paid licence - never add it).
  - Analyzers, banned APIs and TreatWarningsAsErrors apply to tests: no analyzer suppression, no `!`,
    no DateTime/DateTimeOffset.UtcNow - use fixed DateTimeOffset values and a substituted IDateTime.
  - Test names PascalCase without underscores (CA1707). One test class per production type,
    folders mirror the production project (Domain/, Application/Commands/<UseCase>/, ...).
  - Run from backend/: `dotnet test --project tests/<Project>`; whole suite `dotnet test --solution Crm.sln`.
- A local Crm.Api dev instance may be running and lock src/Crm.Api/bin; build the test project on its own
  or stop the API before a full solution build.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C# and TypeScript`.

## Out of scope

- What this story explicitly does **not** cover: other modules' tests, building-block tests (UT-01), integration tests against SQL Server / Testcontainers, HTTP/controller end-to-end tests, EF configurations and migrations, frontend.
