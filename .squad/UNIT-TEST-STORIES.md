# Backend unit-test stories — follow-up tracker

Owner decision, 2026-10-11: the backend gets an automated unit-test suite for every feature implemented so far.
This reverses the "automated tests are out of scope" policy in `CLAUDE.md` for the backend (UT-01 rewrites the
policy). The frontend stays without tests. **Each module is its own story with its own squad-kit flow** — never
plan or implement two rows in one session.

This file is the handoff between sessions. Any agent can pick up the work from here alone.

## How to continue

1. Run `/squad-test-story next` (or `/squad-test-story UT-03` for a specific row). It plans the story in one
   subagent and implements it in another, then updates this file. See
   [`../.claude/commands/squad-test-story.md`](../.claude/commands/squad-test-story.md).
2. Doing it by hand instead: `/squad-plan <intake>` → `/squad-implement <plan>`, with the override rules below
   given to the implementer, then update the row here yourself.
3. Before stopping (usage or context limit, failure), write what is in flight into **Session log** below.

## Build order

`NN` is the plan number (global plan sequence; backlog stories end at 59). Plans go to
`.squad/plans/backend-unit-tests/NN-story-<intake-folder-name>.md`.

| # | Id | NN | Story | Status | Intake | Plan | Commit | Depends on |
|---|----|----|-------|--------|--------|------|--------|------------|
| 1 | UT-01 | 60 | Unit tests — foundation and building blocks | ✅ Done | [intake](stories/backend-unit-tests/unit-tests-foundation-and-building-blocks/intake.md) | [plan](plans/backend-unit-tests/60-story-unit-tests-foundation-and-building-blocks.md) | `Implement Story 60 — Unit tests — foundation and building blocks (UT-01)` | — |
| 2 | UT-02 | 61 | Unit tests — Organisation module | ⏳ To do | [intake](stories/backend-unit-tests/unit-tests-for-organisation-module/intake.md) | — | — | UT-01 |
| 3 | UT-03 | 62 | Unit tests — Identity module | ⏳ To do | [intake](stories/backend-unit-tests/unit-tests-for-identity-module/intake.md) | — | — | UT-01 |
| 4 | UT-04 | 63 | Unit tests — Customers module | ⏳ To do | [intake](stories/backend-unit-tests/unit-tests-for-customers-module/intake.md) | — | — | UT-01 |
| 5 | UT-05 | 64 | Unit tests — Tickets module | ⏳ To do | [intake](stories/backend-unit-tests/unit-tests-for-tickets-module/intake.md) | — | — | UT-01 |

Statuses: `⏳ To do` · `🚧 Scaffolded` (setup exists, not planned) · `📝 Planned` (plan on disk, uncommitted) ·
`🔨 In progress` · `✅ Done` (committed) · `⛔ Blocked` (reason in Session log).

**Remaining: 4 stories.** UT-02..UT-05 depend only on UT-01 and on nothing else, but run them one at a time:
they share the solution build and the same `dotnet test` run.

## Existing plans to mine for test cases

Every module was built from a squad-kit plan that already lists its invariants, error codes, edge cases, risks and
acceptance criteria. Planners and implementers of a UT story **read the matching plans (and intakes) first** and
turn their rules into test cases — then confirm against the code, which wins if the two differ (record the
difference as a Follow-up). Paths are under `.squad/plans/`:

| Story | Read these plans |
|-------|------------------|
| UT-01 | `platform-foundation/01-…`, `02-…` (persistence, auditing, soft delete), `03-…` (pipeline, errors, logging, caching), `06-…` (organisation scoping) |
| UT-02 Organisation | `platform-foundation/06-…`, `integrations-platform/58-…` (branding) |
| UT-03 Identity | `identity-access/07-…`, `customer-portal/47-…` (portal accounts) |
| UT-04 Customers | `customer-management/11-…`, `customer-portal/47-…` (portal contacts) |
| UT-05 Tickets | `ticket-management/15-…`, `18-…`, `customer-portal/47-…` (portal tickets) |

Matching intakes sit under `.squad/stories/` (linked from `BACKLOG.md` / `MVP-STORIES.md`).

## Override rules for these stories (the runner passes them to every subagent)

`.claude/commands/squad-implement.md` and `CLAUDE.md` still say "no automated tests" until UT-01 lands. For the
UT stories:

- Writing test projects under `backend/tests/` **is the task**. The "no automated tests" rule does not apply to
  them. Everything else in `CLAUDE.md` still binds.
- UT-01 must also update the rule itself: `CLAUDE.md` (Testing policy, Verification, repository layout),
  `.claude/commands/squad-implement.md` (replace the "No automated tests" rule with "add/update backend unit
  tests for changed backend code; `dotnet test --solution Crm.sln` from `backend/` must pass"),
  `backend/Directory.Build.targets` header comment, `backend/README.md`. After UT-01 these overrides become
  redundant.
- Tests touch only `backend/tests/**` (plus docs/config in UT-01). A test story that needs a production code
  change (a bug found, an untestable seam) does not make it: it records the issue in the story's Follow-ups and
  in Session log below.
- Stack: xUnit v3 (`xunit.v3` 4.0.2) + NSubstitute 6.2.0 (+ analyzers), plain `Assert`; EF Core InMemory only
  for query logic. **No FluentAssertions** (8+ is a paid licence). Versions live in `backend/Directory.Packages.props`.
- Analyzers, banned APIs and warnings-as-errors apply to tests. No suppressions, no `!`, no
  `DateTime`/`DateTimeOffset.UtcNow` — fixed timestamps and a substituted `IDateTime`. PascalCase test names
  without underscores (CA1707).
- Run from `backend/` (its `global.json` opts into Microsoft.Testing.Platform; xunit.v3 4.x refuses VSTest):
  `dotnet test --project tests/<Project>` for one project, `dotnet test --solution Crm.sln` for all.
- A developer's `Crm.Api` may be running and locking `src/Crm.Api/bin`. Do not kill it; build the test project on
  its own (`dotnet build tests/<Project>`), and say in the report if the full solution build could not run.
- Commit message: `Implement Story NN — <title> (UT-0X)`, one commit holding the plan, `00-overview.md` /
  `00-index.md` updates, this file's row, and the tests.

## Already in the working tree (uncommitted, from the 2026-10-11 setup session)

Belongs to UT-01's commit:

- `backend/Directory.Packages.props` — "Tests" package group.
- `backend/global.json` — `"test": { "runner": "Microsoft.Testing.Platform" }`.
- `backend/tests/Directory.Build.props` — imports the parent props; `OutputType Exe`, `IsTestProject`, global
  usings `Xunit` + `NSubstitute`.
- `backend/Directory.Build.targets` — `Tests` accepted as a module layer, so `Crm.Modules.<M>.Tests` obeys rule 1.
- Six test projects in `backend/tests/`, added to `backend/Crm.sln` (solution folder `tests`):
  `Crm.SharedKernel.Tests` (has `Results/ResultTests.cs`, 3 passing), `Crm.BuildingBlocks.Tests`,
  `Crm.Modules.{Organisation,Identity,Customers,Tickets}.Tests` (each references its module's Domain,
  Application, Infrastructure).
- Not yet verified: the full `dotnet build backend/Crm.sln` with these changes. A running Crm.Api locked the host
  output when it was last tried.

## Session log

Newest first. One entry per session: date, row, what happened, what is left.

- 2026-10-11 — **UT-01 done** (Story 60, one commit `Implement Story 60 — Unit tests — foundation and building blocks (UT-01)`). Slices 1-7 all green: `dotnet test --solution Crm.sln` from `backend/` = **826 tests, 0 failed, 0 skipped** (`Crm.SharedKernel.Tests` 96, `Crm.BuildingBlocks.Tests` 730; the four module projects are empty and build). Nothing under `backend/src/` changed. Docs/policy rewritten: `CLAUDE.md`, `.claude/commands/squad-implement.md`, `backend/Directory.Build.targets` header, `backend/README.md`, root `README.md`. The Override rules section above is now redundant (CLAUDE.md and squad-implement.md say the same) but is left in place because UT-02..UT-05 prompts still reference it.
  - **How to write the next project** (copy `Crm.BuildingBlocks.Tests`): fixtures and doubles in `Support/` (real `OrganisationScope`s via `Scopes`, `CurrentUsers`, `Clock`, `RecordingLogger<T>`, `NextProbe`, `TestDbContext` over EF InMemory with `Seed`, `NullArguments` for guard-clause tests because a literal null does not compile warning-free and `!`/suppression are banned, hand-written doubles for `IProblemDetailsService`/`IDistributedCache` because `ValueTask` setup trips CA2012 and extension methods cannot be substituted).
  - **R2 resolution (differs from the plan's fallback):** the four empty module test projects carry `<TestingPlatformCommandLineArguments>--ignore-exit-code 8</TestingPlatformCommandLineArguments>` in their own `.csproj` (MTP exits 8 for "zero tests ran"). It is deliberately not in `tests/Directory.Build.props`, so an accidentally empty SharedKernel/BuildingBlocks run still fails. **UT-02..UT-05: remove that property from your module's csproj in the same change as its first test.**
  - **Solution build:** a developer's `Crm.Api` (pid 46588) was running and locked `src/Crm.Api/bin`, so `dotnet build backend/Crm.sln` cannot finish its copy step on this machine (only MSB3021/3026/3027 lock messages, no compiler diagnostic, no CRM0001). Instead: every test project (6) built clean with 0 warnings, `Crm.Api` built clean with 0 warnings into a scratch `-p:OutDir`, and `dotnet test --solution Crm.sln` built and ran all six. Re-run `dotnet build backend/Crm.sln` once the API is stopped.
  - **Not unit-testable here:** `ModuleDatabaseExtensions.MigrateAndSeedModulesAsync` (needs a relational provider), `ModuleDesignTimeDbContextFactory`, `ModuleSqlServer`, `IModule`/`IModuleSeeder` and the other pure interfaces/markers, `PagedResult<T>`, `CrmDiagnostics` (exercised through `LoggingBehaviorTests`), the claim/scheme/role constant classes (exercised by the tests that use them), `AddCrmObservability` beyond registration (resolving it would start Serilog's console sink and OpenTelemetry exporters), and the `ArgumentNullException` guard in `CorrelationIdDelegatingHandler.SendAsync` (unreachable through the public API: `HttpMessageInvoker.SendAsync` rejects null first).
  - **Follow-ups (production behaviour found by the tests; nothing was changed under `backend/src/`):**
    1. `StronglyTypedId.ToString()` is dead code: every derived record (`TenantId`, `CustomerId`, ...) gets its own compiler-synthesised `ToString` (`TenantId { Value = ... }`), so interpolating an id gives the record form, not the bare guid. Fix: make the base override `sealed`. Pinned in `StronglyTypedIdTests` and `ScopeIdsTests`; update them with the fix.
    2. `ValidationBehavior` hands one `ValidationContext` to every validator, so with more than one validator for a request FluentValidation repeats earlier failures in later validators' results and the error description lists duplicates (3 validators, 2 failing rules gave 6 entries). Single-validator requests are fine. Fix: a fresh context per validator. Not pinned (the test asserts presence, not exact text).
    3. `TransactionBehavior` calls no `RollbackTransactionAsync` and does not drain pending integration events for an exception other than `ConcurrencyConflictException`/`OrganisationScopeViolationException`; it relies on the request scope being disposed. Likely harmless, undocumented; only "propagates, never commits, never dispatches" is asserted.
    4. `ModuleDbContext`'s write guard ignores `Deleted` entries, and for a Modified `ITransferableOrganisationScoped` row whose original department was not the caller's it judges the destination only, so a caller could pull a foreign row into their own scope if they could ever track it. Unreachable through repositories (reads are scope-filtered). `ADeletedEntryIsNotGuarded` pins the first; the second is untested. Security-review candidates.
    5. `OrganisationScope.Allows` tests branch and department independently (column-wise), in C# and in the SQL filters alike: a row whose branch is in scope and whose department is in scope but that pairs them differently is allowed (pinned). Also `OrganisationScope.None` allows a tenant-scoped row whose `TenantId` is `TenantId.None`; no real row has that.
    6. A rejected active scope still sees tenant-scoped rows of its tenant (only departmental rows are hidden); the middleware answers 403 first, so this is by design (pinned).
    7. `AddCrmResilience` throws `ArgumentException` ("Configuration section cannot be empty") at registration when `Resilience:Outbound` is absent. Fail-fast and the host's appsettings carry the section, but it is undocumented (pinned).
    8. `OutboxDispatcher`'s loop ends with a cancelled (not completed) `ExecuteTask` when the stop lands inside a dispatch cycle; harmless to `BackgroundService`.
    9. Existing plans: the platform-foundation plans' error codes (`Organisation.OutOfScope`, `Organisation.ActiveScopeRejected`, `Persistence.ConcurrencyConflict`) were cross-checked against the code and match; the other rules were taken from the UT-01 plan, which had already mined them. No other plan/code difference was found.
- 2026-10-11 — UT-01 planned (13 named risks R1-R13, ~400-480 tests, 6 slices); implementation subagent launching. If it runs out of context: do not commit, log finished slices here, stop at a slice boundary.
- 2026-10-11 — setup session: scaffolding above, five intakes written, this tracker and
  `/squad-test-story` created. Next: `/squad-test-story UT-01`.
