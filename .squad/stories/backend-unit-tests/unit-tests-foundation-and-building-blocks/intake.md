# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/backend-unit-tests/unit-tests-foundation-and-building-blocks/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Unit tests - foundation and building blocks
- **Feature slug (folder under `plans/`):** `backend-unit-tests`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `UT-01`` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Technical story``
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Unit tests foundation and building blocks
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As the project owner I want an automated backend unit-test suite, starting with the shared foundation,
so that regressions are caught by `dotnet test` instead of in manual verification or production.

This story reverses the "automated tests are out of scope" policy in CLAUDE.md for the backend and lays the
groundwork every module's test story (UT-02..UT-05) builds on: packages, test project conventions, the
build rule for test projects, the run command, and the documentation. It then covers the building blocks:
Crm.SharedKernel, Crm.BuildingBlocks.Application, and the unit-testable parts of
Crm.BuildingBlocks.Infrastructure and Crm.BuildingBlocks.Presentation.

Each module gets its own separate story and squad-kit flow afterwards: Organisation (UT-02),
Identity (UT-03), Customers (UT-04), Tickets (UT-05). This story does not write module tests.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Test infrastructure finished and documented: backend/tests/Directory.Build.props, the "Tests" package
      group in Directory.Packages.props, MTP opt-in in backend/global.json, the "Tests" layer accepted by
      Directory.Build.targets (Crm.Modules.<M>.Tests obeys rule 1).
- [ ] Six test projects exist in backend/tests and in Crm.sln (solution folder "tests"):
      Crm.SharedKernel.Tests, Crm.BuildingBlocks.Tests, Crm.Modules.{Organisation,Identity,Customers,Tickets}.Tests.
      The four module projects hold no tests yet - their own stories fill them.
- [ ] Crm.SharedKernel.Tests: Result / Result<T> / Error, Entity and AggregateRoot (identity equality,
      domain event raise/clear), ValueObject equality, StronglyTypedId, DomainException/DomainError,
      AuditableEntity, scoping ids.
- [ ] Crm.BuildingBlocks.Tests: every MediatR pipeline behaviour (validation, authorisation, organisation
      scope, logging, transaction incl. integration-event outbox write and cache invalidation, caching incl.
      scope-prefixed keys, VaryByUser, failures not cached), RequestAuthorizationGuard, OrganisationScope
      (ResolveWriteTarget, narrowing via active scope, out-of-scope rejection), Specification, permission
      catalogue / role map, cache key building, and - where unit-testable without a database - the
      ModuleDbContext write guard, auditing/soft-delete interceptor, OutboxDispatcher retry/dead-letter
      decisions, log redaction, GlobalExceptionHandler / Result-to-ProblemDetails mapping (ProblemTypes).
- [ ] `dotnet build backend/Crm.sln` clean: zero warnings, no CRM0001, no suppressed analyzer.
- [ ] `dotnet test --solution Crm.sln` (from backend/) passes, zero failures, zero skipped.
- [ ] CLAUDE.md updated: Testing policy section rewritten (backend unit tests required for new/changed
      backend code; frontend still without tests), Verification section adds the test command, repository
      layout lists backend/tests; Directory.Build.targets header comment and backend/README.md updated.
- [ ] .claude/commands/squad-implement.md: the "No automated tests" rule is replaced by "add/update backend
      unit tests for changed backend code; `dotnet test --solution Crm.sln` from backend/ must pass".
- [ ] .squad/UNIT-TEST-STORIES.md row UT-01 set to Done with plan and commit linked, Session log entry added.
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
- **Depends on code areas or other stories:** all implemented stories (06-13, 15-19, 47, 58, 59). Blocks UT-02, UT-03, UT-04, UT-05.

## Extra notes (optional)

- Conventions for every unit-test story (set by UT-01):
  - xUnit v3 + NSubstitute, plain `Assert` only (FluentAssertions 8+ is a paid licence - never add it).
  - Analyzers, banned APIs and TreatWarningsAsErrors apply to tests: no analyzer suppression, no `!`,
    no DateTime/DateTimeOffset.UtcNow - use fixed DateTimeOffset values and a substituted IDateTime.
  - Test names PascalCase without underscores (CA1707). One test class per production type,
    folders mirror the production project (Domain/, Application/Commands/<UseCase>/, ...).
  - Run from backend/: `dotnet test --project tests/<Project>`; whole suite `dotnet test --solution Crm.sln`.
- A local Crm.Api dev instance may be running and lock src/Crm.Api/bin; build the test project on its own
  or stop the API before a full solution build.- Already scaffolded in the working tree (uncommitted - keep and build on it):
  - Directory.Packages.props "Tests" group: xunit.v3 4.0.2, NSubstitute 6.2.0,
    NSubstitute.Analyzers.CSharp 1.0.17, Microsoft.EntityFrameworkCore.InMemory 10.0.11.
  - backend/global.json "test": {"runner": "Microsoft.Testing.Platform"} - xunit.v3 4.x refuses VSTest on
    the .NET 10 SDK, so plain `dotnet test` without it fails.
  - backend/tests/Directory.Build.props (imports parent props, OutputType Exe, IsTestProject, global usings
    Xunit + NSubstitute); Directory.Build.targets accepts the "Tests" layer.
  - The six test projects exist and are in Crm.sln; Crm.SharedKernel.Tests/Results/ResultTests.cs (3 passing).
  - Full solution build with these changes not yet verified.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C# and TypeScript`.

## Out of scope

- What this story explicitly does **not** cover: module tests (UT-02..UT-05), frontend tests, integration tests against SQL Server / Testcontainers, API end-to-end tests, coverage gates in CI, the Audit module (no code yet).
