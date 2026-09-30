# Story 18 — Ticket conversation thread messages internal notes and history

## Prerequisites

- **Story 15 is implemented and committed** (`96a76a8`). This story extends its `Tickets` module in place; read
  [`15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md`](15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md)
  "Product rules" (transition table, `Revision`, `TicketRefusals`, the R2 "nested reads before `Publish`" handler
  shape, `OwnsMany` + `ValueGeneratedNever` R8) — every one of them still holds. Story 15's coordination bullet for
  this story: "adds `TicketMessage`/`TicketNote` inside `Ticket`, sets `FirstRespondedAtUtc` (column exists, never set
  here), and renders `TicketActivityDto` (kind + from/to + actor, **no pre-rendered text**)". Story 15's edge case
  "Activity growth — Story 18 must decide whether messages stay owned" is decided below (D1).
- **Module:** **`Tickets` only** (catalogue in `CLAUDE.md`: Tickets owns 15–19). No other module's code changes. The
  only cross-module calls are existing Contracts reads from `Tickets.Application`: `IIdentityModuleApi.GetStaffUsersAsync`
  (names) and `IOrganisationModuleApi.GetDepartmentAsync` (department names on `Transferred` entries). One new
  integration event in `Tickets.Contracts`, no subscribers yet. **No building-block change** — the scope write guard,
  the authorisation behaviour and the repository stay exactly as they are.
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md` row 11).** Absorbs **19** (history = the Story 15 activity entries,
  shown in the same thread). **Deferred:** HTML bodies and sanitisation, attachments, outbound delivery over any
  channel, delivery status, reply-all/forward, mentions, drafts, field-level history of every attribute, derived
  durations, the composer and the full ticket detail workspace (Story 20), portal replies (Story 47), any test project.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`,
  rollForward latestFeature); EF Core **10.0.11** and `dotnet-ef` **10.0.11** (`backend/Directory.Packages.props`
  line 16, `.config/dotnet-tools.json` — `dotnet tool restore` first); MediatR **12.4.1** (licence pin — do not bump);
  FluentValidation **12.1.1**; Swashbuckle **10.2.3**. `Microsoft.EntityFrameworkCore.SqlServer` reaches
  `Tickets.Infrastructure` transitively through `Crm.BuildingBlocks.Infrastructure.csproj` line 11. Frontend pins
  unchanged (`frontend/package.json`: Nuxt 4.5.2, `@nuxtjs/i18n` 10.6.0, openapi-typescript 7.13.0, pnpm 12.6.0).
  **No new NuGet package. No new npm package.** This machine has **no Python** and **no `pnpm` on PATH** until
  `corepack enable pnpm --install-directory "$APPDATA/npm"`; Arabic text and `$` break Bash heredocs — use the
  **Write tool** for every file with Arabic in it (`.resx`, `.http`, locale JSON).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — sequence backfill on the existing dev database.** The migration (B7) adds `TicketActivities.Sequence`,
    back-fills it per ticket with `ROW_NUMBER()` and then creates the unique `(TicketId, Sequence)` index; it also
    raises `Tickets.Revision` to the max sequence where it is lower. **Prove:** on the dev DB that already holds the
    Story 15 rows, the API starts (migration applied), then
    `SELECT TicketId, COUNT(*) c, MIN(Sequence) mn, MAX(Sequence) mx FROM tickets.TicketActivities GROUP BY TicketId`
    gives `mn = 1` and `mx = c` for every ticket, and
    `SELECT COUNT(*) FROM tickets.Tickets t WHERE t.Revision < (SELECT MAX(Sequence) FROM tickets.TicketActivities a WHERE a.TicketId = t.Id)`
    is 0; then `ticket-thread.http` 20 (reply on the **seeded** T-000001) → 200 (no unique-index violation). Also run on
    a fresh database (`docker compose down -v` is **not** required — `database update 0` on the Tickets module then
    restart is enough). **Fallback:** none — fix the SQL.
  - **R2 — three owned collections load as split queries.** `Ticket` now owns `Activities`, `Messages`, `Notes`; EF
    auto-includes all three on every load, which in single-query mode is a cartesian product (message bodies up to
    10,000 chars × activities × notes). B6 sets `QuerySplittingBehavior.SplitQuery` for the Tickets context only,
    through `TicketsDbContext.OnConfiguring`. **Prove:** `ticket-thread.http` 3 (a reply) → the EF log shows one
    `SELECT … FROM [tickets].[Tickets]` followed by separate `SELECT`s over `[TicketActivities]`, `[TicketMessages]`,
    `[TicketNotes]` (joined back to `Tickets` on the id), and **no** `MultipleCollectionIncludeWarning`; the connection
    string, schema-local `__EFMigrationsHistory` and `dotnet ef` still work (`ef.ps1 -Module Tickets migrations list`).
    **Fallback:** delete the `OnConfiguring` override and accept the single query plus the warning line (the Story 11
    R9 precedent for `Customer`'s two owned collections); record it in the story report.
  - **R3 — new owned rows INSERT.** `TicketMessage.Id`/`TicketNote.Id` are client-generated; without
    `ValueGeneratedNever` EF would UPDATE (Story 15 R8, `TicketConfiguration.cs` lines 42–44). **Prove:**
    `ticket-thread.http` 3 and 6 → EF log `INSERT INTO [tickets].[TicketMessages]` / `[TicketNotes]` and
    `UPDATE [tickets].[Tickets] SET … [Revision] = @… WHERE [Id] = @… AND [RowVersion] = @…` in one transaction.
  - **R4 — the thread projection translates.** `TicketThreadReadModel.ListAsync` (B6) projects three filtered,
    ordered, `Take`-limited owned collections (constructing `TicketThreadRow` records) inside one `Select` over
    `Tickets`. **Prove:** `ticket-thread.http` 9 → 200, and the EF log shows SQL over `[TicketMessages]`,
    `[TicketNotes]`, `[TicketActivities]` with `[Sequence] > @…` and a row limit (`TOP`/`ROW_NUMBER() … <= @…`), plus the
    scope predicates (`[t].[TenantId] = @…`, `[t].[BranchId] IN …`, `[t].[DepartmentId] IN …`) and
    `[t].[IsDeleted] = CAST(0 AS bit)` on `Tickets`; no `InvalidOperationException` "could not be translated".
    **Fallback:** one query per kind, `context.Tickets.AsNoTracking().Where(t => t.Id == ticketId).SelectMany(t => t.Messages)`
    `.Where(m => m.Sequence > after).OrderBy(m => m.Sequence).Take(take).Select(m => new TicketThreadRow(…))`, preceded by
    `context.Tickets.AnyAsync(t => t.Id == ticketId)` for the null (not visible) answer; record it.
  - **R5 — the public path never reads notes or activities.** `ListPublicAsync` projects only `t.Messages`.
    **Prove:** `ticket-thread.http` 11 (`?visibility=public`) → the EF log for that request mentions neither
    `[TicketNotes]` nor `[TicketActivities]`; the body holds only `type: "Message"` entries.
  - **R6 — two integration events in one command, both after the nested reads.** A reply that reopens a Resolved
    ticket (or a customer reply that reopens a PendingCustomer one) publishes `TicketMessageAddedIntegrationEvent` **and**
    `TicketStatusChangedIntegrationEvent`. The assembler (nested Identity read) runs before both `Publish` calls
    (Story 15 R2 — a nested query after `Publish` throws). **Prove:** `ticket-thread.http` 25 and 31 → 200 and two
    `INSERT INTO [tickets].[outbox_messages]` in the EF log; the SQL in Verification step 5 → counts as stated there.
  - **R7 — a 10,000-character body.** `HasMaxLength(10000)` exceeds SQL Server's 4000 limit for sized `nvarchar`, so EF
    maps it to `nvarchar(max)`. **Prove:** the generated migration shows `type: "nvarchar(max)"` for both `Body`
    columns; `ticket-thread.http` 37 (exactly 10,000 chars) → 200 and 38 (10,001) → 400 `errors.Body`.
  - **R8 — `.http` file bodies from fixtures.** 37/38 send `< ./data/ticket-thread-body-10000.json` (VS Code REST
    Client and JetBrains HTTP Client both read a request body from a file with `<`). **Prove:** 37 → 200 and the
    thread shows a 10,000-char body. **Fallback:** the curl lines in Verification step 4 — record which one was used.
  - **R9 — concurrent appends.** Replies and notes take **no** client rowVersion (D5); the loaded rowversion is the
    concurrency token, so two simultaneous appends to one ticket give one 200 and either a second 200 (serialised) or
    a 409 `Persistence.ConcurrencyConflict` — never two entries with one `Sequence`. **Prove:** Verification step 6
    (Bash race) five times; the unique `(TicketId, Sequence)` indexes never raise a SQL 2601 (a 500).
  - **R10 — Swagger and the generated client.** New schema names (`TicketThreadPageDto`, `TicketThreadEntryDto`,
    `TicketDepartmentRefDto`, `TicketBodyRequest`) must not clash in the Agent document, and the
    `IReadOnlyDictionary<string, string?>` `parameters` must generate as `{ [key: string]: string | null }`.
    **Prove:** `GET /swagger/agent/swagger.json` → 200; after `pnpm gen:api`, `app/api/generated/agent.ts` contains the
    four schemas and `pnpm --dir frontend typecheck` passes with no cast. **Fallback:** rename a clashing record with a
    `TicketThread` prefix.
  - **R11 — `pnpm` and `gen:api`.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, then
    `pnpm --version` prints 12.6.0 before F1.
- **Coordination:**
  - **Story 20** (inbox + detail UI) replaces `frontend/app/pages/agent/tickets/[id].vue` (F6) with the full workspace
    and keeps `TicketThread` (F5) as the centre column; it adds the composer (Reply / Internal note tabs) calling
    `useTicketThread().reply/addNote/recordCustomerReply` (F3) and, after a 200, `loadNewer()` (the cursor guarantees
    no duplicates). It offers the composer tabs only when `TicketDto.allowedActions` contains `Reply` / `AddNote`
    **and** the caller has `tickets.write`. A 409 `Persistence.ConcurrencyConflict` on a reply keeps the draft and
    offers "send again". Contact names for inbound messages come from the customer context panel it already loads
    (`authorId` is a contact id; `null` = the customer).
  - **Story 47** (portal) adds its own `[AllowAnonymousRequest]`-then-portal-auth query that calls
    **`ITicketThreadReadModel.ListPublicAsync` only** (internal notes and activities are structurally unreachable —
    it never names them) and its own command calling `Ticket.ReceiveCustomerReply(body, contactId, portalActorId, now)`;
    "request closure" and "reopen" reuse `Resolve`/`Reopen`. `TicketMessageAddedIntegrationEvent` already carries
    everything a portal notification needs except the body.
  - **Stories 22–27** (channels) will translate inbound email/WhatsApp into `ReceiveCustomerReply` and subscribe to
    `TicketMessageAddedIntegrationEvent` (`Direction = "Outbound"`) for delivery; they fetch the body through a
    Contracts read they add then — the event deliberately carries no body (D8).

---

## Story Goal

The conversation of a ticket exists as domain behaviour and as one ordered, paged feed, before the ticket workspace
UI is built.

When this story is done:

1. `Ticket` owns `TicketMessage` (direction inbound/outbound, author, plain-text body ≤ 10,000, occurred-at) and
   `TicketNote` (internal, author agent, body) next to Story 15's `TicketActivity`; all three carry a per-ticket
   `Sequence` that orders the thread.
2. `Ticket.ReplyAsAgent`, `Ticket.ReceiveCustomerReply`, `Ticket.AddInternalNote` append exactly one entry each and
   apply the status rules: first agent reply records `FirstRespondedAtUtc`; a customer reply on PendingCustomer
   moves it to Open; a reply to Resolved reopens it; a reply to Closed (or Cancelled) is rejected with 422.
3. `GET /api/Agent/Tickets/{id}/thread` returns messages, notes and activities merged in chronological order,
   cursor-paged by `Sequence`, each activity as a **template key + parameters**, with a `visibility` filter
   (`public` = messages, `internal` = notes, `system` = activities). The public path reads messages only.
4. `POST …/replies`, `POST …/notes`, `POST …/customer-replies` in the Agent Area; public messages publish
   `TicketMessageAddedIntegrationEvent` through the outbox.
5. A minimal `/agent/tickets/[id]` page renders the thread in `en` and `ar` (activity sentences from locale keys,
   bodies as plain text with preserved line breaks, never `v-html`); Story 20 builds the workspace around it.
6. `backend/requests/ticket-thread.http` exercises every endpoint, rule and rejection.

**Not in scope:** everything deferred in Prerequisites and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Domain-driven design" (children reachable only through the root; always-valid aggregates),
   "Application, API, and persistence" (**Results**, **Localization keys**, **Time**, **Scoping**), "Testing policy",
   the Frontend section (RTL logical utilities, `useFormat()`, `<bdi dir="ltr">`, no `v-html`).
2. [Story 15's plan](15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md) — "Product rules" (lines 222–305),
   B6 "the standard shape" (lines 1195–1224), R2 (lines 50–57), R8 (lines 89–93), B11 `.http` header (lines 1557–1564).
3. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/Ticket.cs` — the whole file: `_activities` (line 35),
   `FirstRespondedAtUtc` (lines 63–64, its summary changes), `Revision` (lines 70–72), `Activities` (line 82),
   `IsAllowedInCurrentStatus`/`CanPerform`/`AllowedActions` (lines 146–159 — B1 extends them), `Reopen` (182–187),
   `ChangeStatus` (304–313), `EnsureAllowed` (315–321), `Record` (336–340 — B1 changes it), `EnsureActor` (342–348),
   `NormaliseSubject` (350–361 — the shape of `NormaliseBody`).
4. Same folder: `TicketActivity.cs` (constructor lines 25–33 — B1 adds `sequence`), `TicketAction.cs` (lines 3–18),
   `TicketDomainErrors.cs` (lines 7–37), `TicketEvents.cs` (lines 6–16).
5. `backend/src/Modules/Customers/Crm.Modules.Customers.Domain/CustomerNote.cs` — the child-entity shape (not
   `IAuditable`, trimmed body, own timestamp) copied by `TicketMessage`/`TicketNote`; `Customer.cs` lines 184–189
   (`AddNote` returns the child); `Customers.Application/Commands/AddNote/AddNoteCommand.cs` line 7 (the "no
   RowVersion for an additive child" precedent).
6. `Crm.Modules.Tickets.Application/`: `TicketDtoAssembler.cs` (constructor line 13, activity ordering line 23,
   `LookupAsync` lines 81–90, `ToRef` lines 92–93), `TicketRefusals.cs` lines 14–24 (reused for replies),
   `TicketsErrors.cs`, `CurrentActor.cs`, `Dtos.cs` (lines 29–42), `Localization/TicketsMessageKeys.cs` lines 7–14,
   both `.resx` files, `Validation/TicketsRules.cs` (lines 17–80, shape of the new rule),
   `Commands/ChangeTicketPriority/*` (the handler shape to copy), `Queries/GetTicketById/*`,
   `Queries/SearchTickets/SearchTicketsQueryValidator.cs` (`RuleForEach` → `errors.Statuses[0]`),
   `Abstractions/ITicketReadModel.cs` (the read-model + row shape).
7. `Crm.Modules.Tickets.Contracts/IntegrationEvents/TicketIntegrationEvents.cs` (lines 5–35, record shape).
8. `Crm.Modules.Tickets.Infrastructure/`: `Persistence/Configurations/TicketConfiguration.cs` lines 36–49 (the owned
   collection block to copy), `Persistence/TicketReadModel.cs`, `Persistence/TicketsDbContext.cs` lines 91–113,
   `TicketsModule.cs` lines 135–150, `Persistence/Migrations/20260930154415_Initial.cs` lines 118–159 (the
   `TicketActivities` table and its index).
9. `Crm.Modules.Tickets.Presentation/Controllers/Agent/TicketsController.cs` (whole file; request records lines
   131–145).
10. Building blocks, read-only: `Crm.BuildingBlocks.Infrastructure/Persistence/Repositories/Repository.cs` lines 84–119
    (403-on-miss — why the thread handler falls back to `GetByIdAsync`), `SpecificationEvaluator.cs`,
    `Modules/ModuleServiceCollectionExtensions.cs` lines 33–42 (what `AddModuleDbContext` configures),
    `Modules/ModuleSqlServer.cs`, `Behaviors/TransactionBehavior.cs` lines 34–39 and 91–98,
    `Behaviors/ValidationBehavior.cs` (a nested query's validation failure is a failed `Result`),
    `Messaging/IEventBus.cs` (several `Publish` calls per command are fine).
11. `backend/src/Modules/Identity/Crm.Modules.Identity.Application/Queries/GetStaffUsers/GetStaffUsersQueryValidator.cs`
    (**≤ 200 ids** per call) and `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/IOrganisationModuleApi.cs`
    (`DepartmentReference`).
12. `backend/requests/tickets.http` lines 1–72 (header, variables, logins — copy) and the titles at lines 182, 352,
    424, 524 (B9 updates them).
13. Frontend: `app/components/customer/CustomerNotesPanel.vue` (states, `useFormat`, `break-words`),
    `app/composables/customers/useCustomer.ts` (useAsyncData + throw-the-problem), `app/composables/useTicketCategories.ts`,
    `app/pages/agent/customers/[id].vue` (page meta, `isGuid`), `app/utils/scope.ts` (`localizedName`),
    `app/components/ui/Badge.vue` (tones), `i18n/locales/{en,ar}/tickets.json`, `i18n/locales/{en,ar}/errors.json`
    lines 71–107 (`errors.code.Tickets`), `scripts/check-locales.ts`.

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **D1 — messages and notes are owned children of `Ticket`** (`OwnsMany`, own tables `TicketMessages` and
  `TicketNotes`, no `DbSet`, no repository), like `TicketActivity`. Every ticket command loads the whole thread; the
  Tickets context uses split queries so the three collections do not multiply (R2). Revisit (a separate aggregate or
  a non-owned collection) only when a ticket's thread routinely exceeds a few hundred entries.
- **D2 — one ordering key: `Sequence`.** Every entry (activity, message, note) gets `Sequence = ++Ticket.Revision` when
  it is appended. `Revision` was already bumped once per appended activity, so it continues without renumbering. The
  root row's rowversion makes committed sequences unique and gap-free per ticket, in commit order; a unique
  `(TicketId, Sequence)` index on each child table is the backstop. The thread is ordered by `Sequence` ascending
  (oldest first); `occurredAtUtc` is shown, never used for ordering.
- **D3 — cursor = the last `Sequence` seen.** `GET …/thread?after={n}&limit={1..100, default 50}` returns entries
  with `Sequence > n`, ascending. `nextCursor` is the last item's `sequence` when more entries exist, else `null`.
  Because a new entry always takes a higher sequence than any committed one, reading page 2 after an append never
  duplicates or skips an entry, and `after = <last sequence>` is also how a client fetches entries added since it
  loaded. Only forward paging in this story.
- **D4 — visibility is enforced in the query layer.** Messages are **Public**, notes **Internal**, activities
  **System**. The Agent endpoint accepts `visibility` (repeatable: `public`, `internal`, `system`; default all
  three). When the requested set is exactly `{public}` the handler uses `ITicketThreadReadModel.ListPublicAsync`, whose
  SQL touches only `TicketMessages` — the path Story 47 must use.
- **D5 — replies and notes take no client rowVersion** (additive, like `Customers` `AddNoteCommand`): an agent never
  loses a reply because a colleague changed the priority. Two truly simultaneous appends → one may get 409
  `Persistence.ConcurrencyConflict` (R9); the client resends.
- **D6 — status rules** (in the aggregate):

  | Method | Allowed in | Status effect | Other effect |
  |---|---|---|---|
  | `ReplyAsAgent` | every status except Closed, Cancelled | Resolved → Open (`Reopen`, one `Reopened` activity); otherwise unchanged | `FirstRespondedAtUtc` set if null (the first agent reply, whatever the status) |
  | `ReceiveCustomerReply` | every status except Closed, Cancelled | PendingCustomer → Open (one `Opened` activity); Resolved → Open (`Reopened`); otherwise unchanged | — |
  | `AddInternalNote` | every status, including Closed and Cancelled | none | — |

  Refusal: 422 `Tickets.TransitionNotAllowed` with Story 15's localised detail ("The ticket is Closed; that action is
  not allowed in this status."). The rule lives in the transition checks: `TicketAction.Reply` and
  `TicketAction.AddNote` join `TicketAction`, `IsAllowedInCurrentStatus` answers them, and `TicketDto.allowedActions`
  therefore lists `Reply` (not Closed/Cancelled) and `AddNote` (always). The message entry is appended **before** the
  status activity, so the thread reads "customer replied" then "status changed". An agent reply on New does **not**
  open the ticket (not asked for).
- **D7 — bodies are plain text.** Trimmed; empty after trim → rejected; > 10,000 chars → rejected (both are 400
  `errors.Body` from the validator; the aggregate backstops with 422 `Tickets.BodyRequired`/`Tickets.BodyTooLong`).
  "No HTML" means the body is **never parsed, sanitised, or rendered as HTML**: `<b>` is stored and shown as the five
  characters it is. Line breaks are preserved. The validator checks the raw length (a 10,001-char body that would trim
  to less is still 400 — accepted).
- **D8 — authors.** Outbound message: `AuthorId` = the agent's user id. Inbound: `AuthorId` = a contact id or `null`
  (= the customer). `RecordedById` = who put it into the CRM (the replying agent; the agent transcribing a customer
  reply in this story; the portal account in 47). Note: `AuthorId` = the agent. In this story the Agent endpoint
  records a customer reply with `AuthorId = ticket.ContactId` (no contact picker — Story 20/47 may pass one).
- **D9 — activities are stored as template key + parameters.** The stored `Kind` **is** the template key and
  `From`/`To` its parameters (machine values: status/priority names, user/department/category ids). No rendered text
  is stored or returned. The DTO exposes `templateKey` (= the kind name, e.g. `"PriorityChanged"`) and
  `parameters` (`{ "from": …, "to": … }`); the frontend renders `tickets.activity.<templateKey>` and resolves each
  parameter through the lookups in the page (`people`, `categories`, `departments`) and its own status/priority keys.
  No new activity kinds.
- **D10 — integration event.** `TicketMessageAddedIntegrationEvent` (public messages only — never notes), published
  by `ReplyAsAgentCommandHandler` and `ReceiveCustomerReplyCommandHandler`; primitives only; **no body** (keeps customer
  text out of the outbox; channel stories add a Contracts read for it). When the reply also changed the status, the
  handler additionally publishes Story 15's `TicketStatusChangedIntegrationEvent`. Notes publish nothing.
- **D11 — permissions.** Thread: `tickets.read`. Reply, customer reply, note: `tickets.write`. All organisation-scoped
  through the ticket: out of scope → 403 `Organisation.OutOfScope`, unknown → 404 `Tickets.TicketNotFound`.
- **D12 — `TicketDto` is unchanged in shape.** It keeps `activities` (now ordered by `Sequence`); the thread endpoint
  is the paged source for the UI. Every command in this story returns the updated `TicketDto` (status,
  `firstRespondedAtUtc`, `allowedActions`, `rowVersion`); the new entries are read with `after`.
- **Error codes** added (frontend contract, never rename): `Tickets.BodyRequired`, `Tickets.BodyTooLong` (422 backstops).
  Reused: `Tickets.TransitionNotAllowed` (422), `Tickets.ActorRequired` (422), `Tickets.TicketNotFound` (404),
  `Organisation.OutOfScope` (403), `Persistence.ConcurrencyConflict` (409).

---

## Backend Tasks

All paths are under `backend/src/Modules/Tickets/` unless they start with `backend/` or `frontend/`. Same code rules
as Story 15 (file-scoped namespaces, `sealed`, primary constructors, `IDateTime` for every clock read,
`ArgumentNullException.ThrowIfNull` on public reference parameters, braces on every `if`, **no `!`** outside
EF-only constructors, container-instantiated classes `public`).

### B1 — Domain (`Crm.Modules.Tickets.Domain`)

**Create file: `TicketMessageDirection.cs`** — `public enum TicketMessageDirection { Inbound, Outbound }`.

**Create file: `TicketMessage.cs`** (shape of `CustomerNote.cs`; body already normalised by `Ticket`):

```csharp
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>A public message of the ticket conversation (Story 18). Child of Ticket - no DbSet, no repository,
/// reached only through Ticket.Messages. Not IAuditable: OccurredAtUtc and RecordedById are domain data.
/// Plain text: never parsed or rendered as HTML.</summary>
public sealed class TicketMessage : Entity<Guid>
{
    /// <summary>Position in the ticket's thread, shared with notes and activities (Ticket.Revision at append).</summary>
    public int Sequence { get; private set; }

    public TicketMessageDirection Direction { get; private set; }

    /// <summary>Outbound: the agent's user id. Inbound: a contact id, or null for "the customer".</summary>
    public Guid? AuthorId { get; private set; }

    /// <summary>Who put the message into the CRM: the replying agent, or whoever recorded the customer's reply.</summary>
    public Guid RecordedById { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketMessage()
    {
        Body = string.Empty;
    }

    internal TicketMessage(
        Guid id, int sequence, TicketMessageDirection direction, Guid? authorId, Guid recordedById, string body,
        DateTimeOffset occurredAtUtc) : base(id)
    {
        Sequence = sequence;
        Direction = direction;
        AuthorId = authorId;
        RecordedById = recordedById;
        Body = body;
        OccurredAtUtc = occurredAtUtc;
    }
}
```

**Create file: `TicketNote.cs`** — same shape, summary "An internal note (Story 18): visible to staff only, never
returned by the public thread path. Child of Ticket.", properties `Sequence`, `Guid AuthorId`, `string Body`,
`DateTimeOffset OccurredAtUtc`, `internal TicketNote(Guid id, int sequence, Guid authorId, string body, DateTimeOffset occurredAtUtc)`.

**File: `TicketActivity.cs`** — add after line 9:

```csharp
    /// <summary>Position in the ticket's thread (Story 18), shared with messages and notes.</summary>
    public int Sequence { get; private set; }
```

and change the constructor (lines 25–33) to
`internal TicketActivity(Guid id, int sequence, TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset occurredAtUtc)`
assigning `Sequence = sequence;`. Update the class summary (lines 5–6): "…the UI turns Kind (the template key) and
From/To (its parameters) into either language (Story 18)."

**File: `TicketAction.cs`** — append after `ChangeCategory,` (line 17):

```csharp
    /// <summary>Story 18: ReplyAsAgent / ReceiveCustomerReply. Every status except Closed and Cancelled.</summary>
    Reply,
    /// <summary>Story 18: AddInternalNote. Every status.</summary>
    AddNote,
```

**File: `TicketDomainErrors.cs`** — add after line 33:

```csharp
    public static readonly DomainError BodyRequired = new("Tickets.BodyRequired", "Enter the text of the message or note.");
    public static readonly DomainError BodyTooLong = new("Tickets.BodyTooLong", "A message or note cannot be more than 10000 characters.");
```

**File: `TicketEvents.cs`** — append:

```csharp
public sealed record TicketMessageAdded(TicketId TicketId, Guid MessageId, TicketMessageDirection Direction, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketNoteAdded(TicketId TicketId, Guid NoteId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```

(No handlers — same as Story 15's events.)

**File: `Ticket.cs`** — the changes, in file order:

1. Class summary (lines 6–8): replace "Every method appends exactly one TicketActivity and raises one domain event."
   with "Every lifecycle method appends exactly one TicketActivity; the conversation methods append one message or
   note (plus one status activity when a reply reopens the ticket). Every entry takes the next Sequence."
2. After `DescriptionMaxLength` (line 14): `public const int BodyMaxLength = 10_000;`
3. After line 35: `private readonly List<TicketMessage> _messages = [];` and `private readonly List<TicketNote> _notes = [];`
4. Replace the `FirstRespondedAtUtc` summary (line 63) with "Set by the first agent reply (Story 18), whatever the
   status; never changed afterwards."
5. Replace the `Revision` summary (lines 70–71) with "Bumped by every appended entry (activity, message, note) and
   used as that entry's Sequence, so the root row (and its rowversion check) is always in the UPDATE and the thread
   has one gap-free order per ticket. Do not remove."
6. After `Activities` (line 82): `public IReadOnlyCollection<TicketMessage> Messages => _messages.AsReadOnly();` and
   `public IReadOnlyCollection<TicketNote> Notes => _notes.AsReadOnly();`
7. Replace `IsAllowedInCurrentStatus` (lines 146–149) with:

   ```csharp
   public bool AcceptsReplies => Status is not (TicketStatus.Closed or TicketStatus.Cancelled);

   public bool IsAllowedInCurrentStatus(TicketAction action) => action switch
   {
       TicketAction.Reply => AcceptsReplies,
       TicketAction.AddNote => true,
       _ => StatusTransitions.TryGetValue(action, out StatusTransition? transition)
           ? transition.From.Contains(Status)
           : WorkingStatuses.Contains(Status),
   };
   ```

   (`CanPerform` and `AllowedActions` need no change — they now include `Reply`/`AddNote`.)
8. After `ChangeCategory` (line 302) add the three conversation methods:

   ```csharp
   /// <summary>An agent answers the customer. The first agent reply records FirstRespondedAtUtc; a reply to a
   /// Resolved ticket reopens it; Closed and Cancelled tickets accept no reply.</summary>
   public TicketMessage ReplyAsAgent(string body, Guid agentId, DateTimeOffset nowUtc)
   {
       EnsureActor(agentId);
       EnsureAllowed(TicketAction.Reply);
       string text = NormaliseBody(body);
       bool reopen = Status == TicketStatus.Resolved;

       var message = new TicketMessage(Guid.NewGuid(), NextSequence(), TicketMessageDirection.Outbound, agentId, agentId, text, nowUtc);
       _messages.Add(message);
       FirstRespondedAtUtc ??= nowUtc;
       Raise(new TicketMessageAdded(Id, message.Id, message.Direction, nowUtc));

       if (reopen)
       {
           Reopen(agentId, nowUtc);
       }

       return message;
   }

   /// <summary>The customer answers (recorded by an agent in Story 18, by the portal in 47, by a channel later).
   /// PendingCustomer and Resolved tickets go back to Open; Closed and Cancelled tickets accept no reply.</summary>
   public TicketMessage ReceiveCustomerReply(string body, Guid? contactId, Guid actorId, DateTimeOffset nowUtc)
   {
       EnsureActor(actorId);
       EnsureAllowed(TicketAction.Reply);
       string text = NormaliseBody(body);
       TicketStatus before = Status;

       var message = new TicketMessage(
           Guid.NewGuid(), NextSequence(), TicketMessageDirection.Inbound,
           contactId is { } c && c != Guid.Empty ? c : null, actorId, text, nowUtc);
       _messages.Add(message);
       Raise(new TicketMessageAdded(Id, message.Id, message.Direction, nowUtc));

       if (before == TicketStatus.PendingCustomer)
       {
           ChangeStatus(TicketAction.Open, TicketActivityKind.Opened, actorId, nowUtc);
       }
       else if (before == TicketStatus.Resolved)
       {
           Reopen(actorId, nowUtc);
       }

       return message;
   }

   /// <summary>A private note for the team. Allowed in every status; never changes the status.</summary>
   public TicketNote AddInternalNote(string body, Guid authorId, DateTimeOffset nowUtc)
   {
       EnsureActor(authorId);
       string text = NormaliseBody(body);

       var note = new TicketNote(Guid.NewGuid(), NextSequence(), authorId, text, nowUtc);
       _notes.Add(note);
       Raise(new TicketNoteAdded(Id, note.Id, nowUtc));
       return note;
   }
   ```

   The body is normalised **before** `NextSequence()`, so a refused body never bumps `Revision`.
9. Replace `Record` (lines 336–340) with:

   ```csharp
   private void Record(TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset nowUtc) =>
       _activities.Add(new TicketActivity(Guid.NewGuid(), NextSequence(), kind, actorId, from, to, nowUtc));

   /// <summary>The next thread position. The only place Revision changes.</summary>
   private int NextSequence()
   {
       Revision++;
       return Revision;
   }
   ```
10. After `NormaliseDescription` (line 374) add `NormaliseBody` (the shape of `NormaliseSubject`, lines 350–361):
    `ArgumentNullException.ThrowIfNull(body)`; `Trim()`; empty → `TicketDomainErrors.BodyRequired`; `> BodyMaxLength`
    → `TicketDomainErrors.BodyTooLong`. **No other text processing** (D7).

### B2 — Contracts

**File: `Crm.Modules.Tickets.Contracts/IntegrationEvents/TicketIntegrationEvents.cs`** — append:

```csharp
/// <summary>Published by ReplyAsAgentCommandHandler and ReceiveCustomerReplyCommandHandler (Story 18) for every PUBLIC
/// message - never for an internal note. Direction is "Inbound" or "Outbound"; AuthorId is the agent (outbound) or a
/// contact id / null for the customer (inbound). No body on purpose: subscribers that need it read it through a
/// Contracts API. Primitives only; append-only.</summary>
public sealed record TicketMessageAddedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid MessageId, string Direction, Guid? AuthorId,
    Guid CustomerId, Guid? ContactId, bool IsFirstResponse, Guid ActorId, Guid TenantId, Guid BranchId,
    Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);
```

Also update the `TicketStatusChangedIntegrationEvent` summary (line 11): "Published by TransitionTicketCommandHandler,
and by the reply handlers when a reply reopens the ticket (Story 18)."

### B3 — Application scaffolding (`Crm.Modules.Tickets.Application`)

**Create file: `Abstractions/ITicketThreadReadModel.cs`**

```csharp
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Abstractions;

public enum TicketThreadVisibility
{
    /// <summary>Messages - what the customer may see.</summary>
    Public,
    /// <summary>Internal notes - staff only.</summary>
    Internal,
    /// <summary>Activities (history) - staff only.</summary>
    System,
}

/// <summary>The ticket thread (Story 18), projected straight from the owned tables with the Story 06 scope and
/// soft-delete filters of Tickets applied. Both methods return null when the ticket is not visible (unknown, deleted
/// or out of scope) and at most <paramref name="take"/> rows with Sequence &gt; afterSequence, ascending.</summary>
public interface ITicketThreadReadModel
{
    Task<IReadOnlyList<TicketThreadRow>?> ListAsync(
        TicketId ticketId, IReadOnlyCollection<TicketThreadVisibility> visibilities, int afterSequence, int take,
        CancellationToken cancellationToken = default);

    /// <summary>Messages only - its query never names notes or activities. The ONLY method a customer-facing
    /// (portal, Story 47) query may call.</summary>
    Task<IReadOnlyList<TicketThreadRow>?> ListPublicAsync(
        TicketId ticketId, int afterSequence, int take, CancellationToken cancellationToken = default);
}

/// <summary>One thread entry. Messages: Direction, AuthorId, RecordedById, Body. Notes: AuthorId, Body. Activities:
/// AuthorId (the actor), Kind, From, To.</summary>
public sealed record TicketThreadRow(
    int Sequence, Guid Id, TicketThreadVisibility Visibility, DateTimeOffset OccurredAtUtc, Guid? AuthorId,
    TicketMessageDirection? Direction, Guid? RecordedById, string? Body, TicketActivityKind? Kind, string? From, string? To);
```

**File: `Localization/TicketsMessageKeys.cs`** — add in `Validation` (after line 13):
`public const string VisibilityUnsupported = "Tickets.Validation.VisibilityUnsupported";`

**Files: `Localization/Resources/TicketsMessages.resx` / `TicketsMessages.ar.resx`** (**Write tool** for the Arabic
file) — add after the `ActionUnsupported` row:

| Key | en | ar |
|---|---|---|
| `Tickets.Validation.VisibilityUnsupported` | Visibility must be one of public, internal, system. | يجب أن تكون الرؤية إحدى القيم public أو internal أو system. |

**File: `Validation/TicketsRules.cs`** — add (shape of `SupportedStatus`, lines 39–47):

```csharp
    private static readonly string[] Visibilities = ["public", "internal", "system"];

    /// <summary>A thread visibility: public, internal or system (case-insensitive).</summary>
    public static IRuleBuilderOptions<T, string> SupportedVisibility<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(v => Visibilities.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.VisibilityUnsupported));
    }
```

(The `private static readonly` array goes next to `TransitionActions`, line 11.)

**File: `Dtos.cs`** — append:

```csharp
/// <summary>A department named by a Transferred activity.</summary>
public sealed record TicketDepartmentRefDto(Guid Id, string Code, string NameAr, string NameEn);

/// <summary>One thread entry. Type (Message | Note | Activity) and Visibility (Public | Internal | System) map 1:1.
/// Messages and notes carry Body (plain text - render as text, never as HTML); messages carry Direction
/// (Inbound | Outbound) and RecordedById; activities carry TemplateKey (the activity kind, e.g. "PriorityChanged")
/// and Parameters ("from", "to": machine values the UI resolves). AuthorId: agent id, contact id, or null for the
/// customer (inbound messages); the actor for activities.</summary>
public sealed record TicketThreadEntryDto(
    int Sequence, Guid Id, string Type, string Visibility, DateTimeOffset OccurredAtUtc, Guid? AuthorId,
    string? Direction, Guid? RecordedById, string? Body, string? TemplateKey, IReadOnlyDictionary<string, string?>? Parameters);

/// <summary>A page of the thread, oldest first. NextCursor: pass it as `after` for the next page; null = no more.
/// The lookups name every staff user, category and department the page's entries refer to.</summary>
public sealed record TicketThreadPageDto(
    IReadOnlyList<TicketThreadEntryDto> Items, int? NextCursor, IReadOnlyList<TicketPersonDto> People,
    IReadOnlyList<TicketCategoryRefDto> Categories, IReadOnlyList<TicketDepartmentRefDto> Departments);
```

**File: `TicketDtoAssembler.cs`**

- Constructor (line 13): add `IOrganisationModuleApi organisation` (`using Crm.Modules.Organisation.Contracts;`).
  Update the summary: "…including the nested module reads (Identity display names; Organisation department names for
  the thread)…".
- Line 23: order activities by `a.Sequence` instead of `a.OccurredAtUtc`.
- Add:

  ```csharp
  /// <summary>Maps a read-model page (up to limit + 1 rows) to the DTO. Nested reads - call before any Publish.
  /// Lookup failures never fail the page: the names are simply missing.</summary>
  public async Task<TicketThreadPageDto> ToThreadPageAsync(IReadOnlyList<TicketThreadRow> rows, int limit, CancellationToken cancellationToken)
  ```

  Body:
  1. `List<TicketThreadRow> page = [.. rows.Take(limit)]; int? next = rows.Count > limit ? page[^1].Sequence : null;`
  2. Staff ids (`HashSet<Guid>`): every `AuthorId` of a row that is **not** an inbound message, every `RecordedById`,
     and `From`/`To` of `Assigned`/`Claimed`/`Unassigned` rows (`AddIfGuid`). Pass at most **200** of them to
     `LookupAsync` (`[.. ids.Take(200)]` into a new `HashSet`) — `GetStaffUsersQueryValidator` rejects more.
  3. Category ids: `From`/`To` of `CategoryChanged` rows that parse as a Guid → one
     `categories.ListAsync(new CategoriesByIdsSpecification(ids))` when non-empty → `ToRef` each.
  4. Department ids: `From`/`To` of `Transferred` rows → for each distinct id
     `await organisation.GetDepartmentAsync(id, ct)`; success → `new TicketDepartmentRefDto(d.Id, d.Code, d.NameAr, d.NameEn)`;
     failure → skipped.
  5. Items: `new TicketThreadEntryDto(r.Sequence, r.Id, TypeOf(r.Visibility), r.Visibility.ToString(), r.OccurredAtUtc,
     r.AuthorId, r.Direction?.ToString(), r.RecordedById, r.Body, r.Kind?.ToString(),
     r.Kind is null ? null : new Dictionary<string, string?> { ["from"] = r.From, ["to"] = r.To })` where
     `TypeOf` maps Public → `"Message"`, Internal → `"Note"`, System → `"Activity"`.

### B4 — Application use cases

Commands: `[RequirePermission(CrmPermissions.TicketsWrite)]`, `ICommand<TicketDto>`, **no RowVersion** (D5). Handlers
inject `IRepository<Ticket, TicketId> tickets`, `ITicketsUnitOfWork unitOfWork`, `ICurrentUserService currentUser`,
`TicketDtoAssembler assembler`, `ILocalizationService localization`, `IDateTime dateTime`, and (reply handlers)
`IEventBus eventBus` — the `ChangeTicketPriorityCommandHandler.cs` shape without `SetOriginalRowVersion`:

```csharp
Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
if (ticket is null)
{
    return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
}

if (TicketRefusals.NotAllowed(ticket, TicketAction.Reply, localization) is { } refusal)   // AddNote never refuses
{
    return Result.Failure<TicketDto>(refusal);
}

Guid actor = CurrentActor.Id(currentUser);
TicketStatus from = ticket.Status;
bool firstResponse = ticket.FirstRespondedAtUtc is null;          // ReplyAsAgent only
DateTimeOffset now = dateTime.UtcNow;
TicketMessage message = ticket.ReplyAsAgent(request.Body, actor, now);

await unitOfWork.SaveChangesAsync(cancellationToken);
TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);   // nested read BEFORE Publish (R6)

eventBus.Publish(new TicketMessageAddedIntegrationEvent(
    Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, message.Id, message.Direction.ToString(), message.AuthorId,
    ticket.CustomerId, ticket.ContactId, firstResponse && ticket.FirstRespondedAtUtc is not null, actor,
    ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

if (ticket.Status != from)
{
    eventBus.Publish(new TicketStatusChangedIntegrationEvent(
        Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, from.ToString(), ticket.Status.ToString(), actor,
        ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));
}

return Result.Success(dto);
```

| Folder / record | Domain call | Publishes |
|---|---|---|
| `Commands/ReplyAsAgent/ReplyAsAgentCommand(Guid Id, string Body)` | `ticket.ReplyAsAgent(request.Body, actor, now)` | message (`IsFirstResponse` as above) + status when changed |
| `Commands/ReceiveCustomerReply/ReceiveCustomerReplyCommand(Guid Id, string Body)` — summary: "An agent records a reply the customer sent outside the CRM (phone, email) until channels exist (22-27). Author = the ticket's contact." | `ticket.ReceiveCustomerReply(request.Body, ticket.ContactId, actor, now)` | message (`IsFirstResponse: false`) + status when changed |
| `Commands/AddInternalNote/AddInternalNoteCommand(Guid Id, string Body)` | `ticket.AddInternalNote(request.Body, actor, now)` — no refusal check, no `eventBus` | nothing |

Validators (one per command, `…CommandValidator`): `RuleFor(c => c.Id).NotEmpty();`
`RuleFor(c => c.Body).NotEmpty().MaximumLength(Ticket.BodyMaxLength);` (`NotEmpty` rejects null, `""` and
whitespace-only — the field error lands on `errors.Body`, built-in message in the request language).

**Query: `Queries/GetTicketThread/GetTicketThreadQuery.cs`**

```csharp
/// <summary>The ticket thread, oldest first, cursor-paged by Sequence (Story 18). Visibility: public, internal,
/// system (repeatable; empty = all three).</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record GetTicketThreadQuery(Guid TicketId, int? After, int Limit, IReadOnlyList<string> Visibility)
    : IRequest<Result<TicketThreadPageDto>>;
```

`GetTicketThreadQueryValidator(ILocalizationService localization)`: `TicketId` NotEmpty;
`RuleFor(q => q.After).GreaterThanOrEqualTo(0).When(q => q.After is not null)`; `Limit` InclusiveBetween(1, 100);
`RuleForEach(q => q.Visibility).SupportedVisibility(localization)`.

`GetTicketThreadQueryHandler(ITicketThreadReadModel threads, IRepository<Ticket, TicketId> tickets, TicketDtoAssembler assembler)`:

```csharp
List<TicketThreadVisibility> visibilities = request.Visibility.Count == 0
    ? [TicketThreadVisibility.Public, TicketThreadVisibility.Internal, TicketThreadVisibility.System]
    : [.. request.Visibility.Select(v => Enum.Parse<TicketThreadVisibility>(v, ignoreCase: true)).Distinct()];
var id = new TicketId(request.TicketId);
int after = request.After ?? 0;
int take = request.Limit + 1;   // one extra row tells whether a next page exists

IReadOnlyList<TicketThreadRow>? rows = visibilities is [TicketThreadVisibility.Public]
    ? await threads.ListPublicAsync(id, after, take, cancellationToken)
    : await threads.ListAsync(id, visibilities, after, take, cancellationToken);

if (rows is null)
{
    // Not visible: an existing out-of-scope ticket throws here (-> 403 Organisation.OutOfScope, Story 06);
    // unknown or deleted -> 404.
    _ = await tickets.GetByIdAsync(id, cancellationToken);
    return Result.Failure<TicketThreadPageDto>(TicketsErrors.TicketNotFound(request.TicketId));
}

return Result.Success(await assembler.ToThreadPageAsync(rows, request.Limit, cancellationToken));
```

(No caching — every append changes the thread.)

### B5 — Nothing else in Application changes

`TicketRefusals`, `TicketsErrors`, `CurrentActor`, the Story 15 handlers and validators stay as they are. Story 15's
`TransitionTicketCommandValidator` still accepts only the seven status actions (`TicketsRules.cs` lines 11–15), so
`Reply`/`AddNote` cannot be sent to `/transitions`.

### B6 — Infrastructure (`Crm.Modules.Tickets.Infrastructure`)

**File: `Persistence/Configurations/TicketConfiguration.cs`** — inside the `Activities` block (lines 36–48) add
`activities.HasIndex("TicketId", nameof(TicketActivity.Sequence)).IsUnique();`. After line 49 add:

```csharp
        builder.OwnsMany(x => x.Messages, messages =>
        {
            messages.ToTable("TicketMessages");
            messages.WithOwner().HasForeignKey("TicketId");
            messages.HasKey(m => m.Id);
            messages.Property(m => m.Id).ValueGeneratedNever(); // R3
            messages.Property(m => m.Direction).HasConversion<string>().HasMaxLength(10);
            messages.Property(m => m.Body).HasMaxLength(Ticket.BodyMaxLength).IsRequired(); // nvarchar(max) (R7)
            messages.HasIndex("TicketId", nameof(TicketMessage.Sequence)).IsUnique();
        });
        builder.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Notes, notes =>
        {
            notes.ToTable("TicketNotes");
            notes.WithOwner().HasForeignKey("TicketId");
            notes.HasKey(n => n.Id);
            notes.Property(n => n.Id).ValueGeneratedNever(); // R3
            notes.Property(n => n.Body).HasMaxLength(Ticket.BodyMaxLength).IsRequired();
            notes.HasIndex("TicketId", nameof(TicketNote.Sequence)).IsUnique();
        });
        builder.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
```

**File: `Persistence/TicketsDbContext.cs`** — update the comment at line 101 to "Aggregate roots only. TicketActivity,
TicketMessage and TicketNote have no DbSet - reachable only through Ticket." and add (usings
`Microsoft.EntityFrameworkCore.Infrastructure`):

```csharp
    /// <summary>Ticket owns three collections (activities, messages, notes) that EF loads with every ticket. One SQL
    /// statement would multiply them (a cartesian product of message bodies); split queries load each once (Story 18,
    /// R2). Tickets context only - no other module is affected.</summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);
        new SqlServerDbContextOptionsBuilder(optionsBuilder).UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }
```

(`SqlServerDbContextOptionsBuilder` lives in `Microsoft.EntityFrameworkCore.Infrastructure`; it modifies the SQL Server
options `AddModuleDbContext` already set, keeping the connection string and the schema-local history table. If the
compiler cannot find it, add `using Microsoft.EntityFrameworkCore.Infrastructure;` — never a new package. R2 fallback
applies if it misbehaves.)

**Create file: `Persistence/TicketThreadReadModel.cs`**

```csharp
public sealed class TicketThreadReadModel(TicketsDbContext context) : ITicketThreadReadModel
{
    public async Task<IReadOnlyList<TicketThreadRow>?> ListAsync(
        TicketId ticketId, IReadOnlyCollection<TicketThreadVisibility> visibilities, int afterSequence, int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visibilities);
        bool messages = visibilities.Contains(TicketThreadVisibility.Public);
        bool notes = visibilities.Contains(TicketThreadVisibility.Internal);
        bool activities = visibilities.Contains(TicketThreadVisibility.System);

        var page = await context.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                Messages = t.Messages.Where(m => messages && m.Sequence > afterSequence).OrderBy(m => m.Sequence).Take(take)
                    .Select(m => new TicketThreadRow(
                        m.Sequence, m.Id, TicketThreadVisibility.Public, m.OccurredAtUtc, m.AuthorId,
                        (TicketMessageDirection?)m.Direction, (Guid?)m.RecordedById, m.Body, null, null, null))
                    .ToList(),
                Notes = t.Notes.Where(n => notes && n.Sequence > afterSequence).OrderBy(n => n.Sequence).Take(take)
                    .Select(n => new TicketThreadRow(
                        n.Sequence, n.Id, TicketThreadVisibility.Internal, n.OccurredAtUtc, (Guid?)n.AuthorId,
                        null, null, n.Body, null, null, null))
                    .ToList(),
                Activities = t.Activities.Where(a => activities && a.Sequence > afterSequence).OrderBy(a => a.Sequence).Take(take)
                    .Select(a => new TicketThreadRow(
                        a.Sequence, a.Id, TicketThreadVisibility.System, a.OccurredAtUtc, (Guid?)a.ActorId,
                        null, null, null, (TicketActivityKind?)a.Kind, a.From, a.To))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return page is null
            ? null
            : [.. page.Messages.Concat(page.Notes).Concat(page.Activities).OrderBy(r => r.Sequence).Take(take)];
    }

    public async Task<IReadOnlyList<TicketThreadRow>?> ListPublicAsync(
        TicketId ticketId, int afterSequence, int take, CancellationToken cancellationToken = default)
    {
        var page = await context.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                Messages = t.Messages.Where(m => m.Sequence > afterSequence).OrderBy(m => m.Sequence).Take(take)
                    .Select(m => new TicketThreadRow(
                        m.Sequence, m.Id, TicketThreadVisibility.Public, m.OccurredAtUtc, m.AuthorId,
                        (TicketMessageDirection?)m.Direction, (Guid?)m.RecordedById, m.Body, null, null, null))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return page?.Messages;
    }
}
```

(Keyset merge: the first `take` of the union equals the first `take` of the three per-kind first-`take` lists. The
`null` literals may need explicit types — `(string?)null`, `(TicketActivityKind?)null` — if the compiler asks. R4/R5.)

**File: `TicketsModule.cs`** — after line 143: `services.AddScoped<ITicketThreadReadModel, TicketThreadReadModel>();`.
Update the class summary (lines 127–128): "…tickets, their lifecycle and assignment, the conversation thread (Story
18), and categories…".

`TicketsSeeder.cs` — **no change** (it compiles against the new `Record`; seeded tickets get sequences 1..n).

### B7 — Migration

`dotnet tool restore`, then `pwsh backend/scripts/ef.ps1 -Module Tickets migrations add ConversationThread`. **Read the
generated file** and check: `AddColumn<int>("Sequence", "TicketActivities", schema: "tickets", nullable: false,
defaultValue: 0)`; `CreateTable("TicketMessages")` (`Id`, `Sequence` int, `Direction` nvarchar(10), `AuthorId`
uniqueidentifier **null**, `RecordedById`, `Body` **nvarchar(max)**, `OccurredAtUtc`, `TicketId` FK cascade to
`tickets.Tickets`); `CreateTable("TicketNotes")` (`Id`, `Sequence`, `AuthorId`, `Body` nvarchar(max), `OccurredAtUtc`,
`TicketId` FK cascade); unique indexes `IX_TicketActivities_TicketId_Sequence`, `IX_TicketMessages_TicketId_Sequence`,
`IX_TicketNotes_TicketId_Sequence` (EF may drop the now-redundant `IX_TicketActivities_TicketId` — fine). No other
schema, no FK to another module.

**The one permitted hand edit — the data back-fill (R1).** Insert, immediately after the `AddColumn` and **before**
the `CreateIndex` of `IX_TicketActivities_TicketId_Sequence` (move that `CreateIndex` below it if EF generated it
earlier):

```csharp
            // Story 18: number the Story 15 activities 1..n per ticket (the order they were recorded), then make sure
            // Revision - the next Sequence source - is never below the highest one. Data only; the schema is EF's.
            migrationBuilder.Sql("""
                UPDATE a SET a.[Sequence] = x.[rn]
                FROM [tickets].[TicketActivities] a
                JOIN (SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [TicketId] ORDER BY [OccurredAtUtc], [Id]) AS [rn]
                      FROM [tickets].[TicketActivities]) x ON x.[Id] = a.[Id];

                UPDATE t SET t.[Revision] = s.[MaxSequence]
                FROM [tickets].[Tickets] t
                JOIN (SELECT [TicketId], MAX([Sequence]) AS [MaxSequence] FROM [tickets].[TicketActivities] GROUP BY [TicketId]) s
                  ON s.[TicketId] = t.[Id]
                WHERE t.[Revision] < s.[MaxSequence];
                """);
```

`Down` needs no data step (dropping the column loses only the numbering). Everything else in the file stays as EF
generated it — if the schema is wrong, fix the configuration, `migrations remove`, re-add, re-insert the SQL.

### B8 — Presentation (`Crm.Modules.Tickets.Presentation`)

**File: `Controllers/Agent/TicketsController.cs`** — add usings for the four new folders and these actions after
`ChangeCategory` (line 128); update the class summary (lines 20–21) to "…(Stories 15 and 18)…". Replies and notes
return the updated `TicketDto`; the new entries are read with `GET …/thread?after=`.

```csharp
    /// <summary>The conversation, oldest first. visibility: public (messages), internal (notes), system (history);
    /// repeat it, or omit it for all three. Pass nextCursor as `after` for the next page.</summary>
    [HttpGet("{id:guid}/thread")]
    [ProducesResponseType<TicketThreadPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketThreadPageDto>> Thread(
        Guid id, [FromQuery] int? after, [FromQuery(Name = "visibility")] string[]? visibility, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetTicketThreadQuery(id, after, limit, visibility ?? []), cancellationToken));

    /// <summary>An agent reply (public). No rowVersion: replies never conflict with other edits.</summary>
    [HttpPost("{id:guid}/replies")]
    // 200 TicketDto, 400, 404, 409, 422
    public async Task<ActionResult<TicketDto>> Reply(Guid id, [FromBody] TicketBodyRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReplyAsAgentCommand(id, request.Body), cancellationToken));

    /// <summary>Records a reply the customer sent outside the CRM (until channels exist). Public.</summary>
    [HttpPost("{id:guid}/customer-replies")]
    // 200 TicketDto, 400, 404, 409, 422
    public async Task<ActionResult<TicketDto>> RecordCustomerReply(Guid id, [FromBody] TicketBodyRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReceiveCustomerReplyCommand(id, request.Body), cancellationToken));

    /// <summary>An internal note - staff only, never in the public thread.</summary>
    [HttpPost("{id:guid}/notes")]
    // 200 TicketDto, 400, 404, 409, 422
    public async Task<ActionResult<TicketDto>> AddNote(Guid id, [FromBody] TicketBodyRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new AddInternalNoteCommand(id, request.Body), cancellationToken));
```

Write out the `[ProducesResponseType…]` attributes the comments list, exactly as `Transition` (lines 65–70) does.
Append `public sealed record TicketBodyRequest(string Body);` after line 145.

### B9 — Request samples and documentation

**Create file: `backend/requests/data/ticket-thread-body-10000.json`** and **`ticket-thread-body-10001.json`** — in Git
Bash (no Python):

```bash
cd /d/CRM/backend/requests && mkdir -p data
printf '{ "body": "%s" }\n' "$(printf 'a%.0s' $(seq 1 10000))" > data/ticket-thread-body-10000.json
printf '{ "body": "%s" }\n' "$(printf 'a%.0s' $(seq 1 10001))" > data/ticket-thread-body-10001.json
wc -c data/*.json   # 10015 and 10016 bytes (13 bytes of JSON + newline around the a's)
```

**Create file: `backend/requests/ticket-thread.http`** (**Write tool** — Arabic). Header copied from `tickets.http`
lines 1–11 rewritten for Story 18 ("the conversation thread: replies, customer replies, internal notes, history,
cursor paging; the file creates its own tickets, so it runs on any database state; rowVersion is needed only for the
status transitions; `@after` values chain from the previous response"), the variables of lines 13–35 that are used
(`@ahmed`, `@ahmedContact`, `@redSeaNewTicket`, `@ahmedNewTicket`, `@unknown`), and the logins for agent A, agent B
and supervisor (lines 37–63). One request per row; expected status/code in the `###` title; `# @name` on every
request whose response a later one reads.

| # | Request (as agent A unless stated) | Expected |
|---|---|---|
| 1 | `POST /api/Agent/Tickets` for Ahmed, contact Ahmed, subject "Conversation test", description | 200 (`@th`); `allowedActions` contains `Reply`, `AddNote` |
| 2 | `GET /api/Agent/Tickets/{{th}}/thread` | 200: one item `{sequence: 1, type: Activity, visibility: System, templateKey: Created, parameters: {from: null, to: New}}`, `nextCursor` null, `people` names agent A |
| 3 | `POST {{th}}/replies` `{ "body": "  Hello Ahmed,\nwe are looking into it.  " }` | 200; `status` New; `firstRespondedAtUtc` set (R2, R3 EF log) |
| 4 | `GET {{th}}/thread?after=1` | 200: exactly one item, `sequence` 2, `type` Message, `direction` Outbound, `body` "Hello Ahmed,\nwe are looking into it." (trimmed, line break kept), `authorId` = `recordedById` = agent A |
| 5 | `POST {{th}}/replies` `{ "body": "Second reply" }` | 200; `firstRespondedAtUtc` **equal** to 3's (AC: first reply only) |
| 6 | `POST {{th}}/notes` `{ "body": "Internal: customer is on the VIP list." }` | 200; status unchanged |
| 7 | `POST {{th}}/notes` `{ "body": "<b>not bold</b> <script>alert(1)</script>" }` | 200 (D7: stored verbatim) |
| 8 | `POST {{th}}/replies` with Arabic body "شكراً لتواصلك معنا.\nسنعود إليك قريباً." | 200 |
| 9 | `GET {{th}}/thread` | 200: sequences 1..6 ascending — Activity, Message, Message, Note, Note, Message; notes `visibility` Internal (R4 EF log) |
| 10 | `GET {{th}}/thread?visibility=internal` | 200: the two notes only |
| 11 | `GET {{th}}/thread?visibility=public` | 200: the three messages only — **no Note entry** (AC; R5 EF log) |
| 12 | `GET {{th}}/thread?visibility=system&visibility=public` | 200: Created + three messages |
| 13 | `GET {{th}}/thread?limit=2` (`# @name page1`) | 200: sequences 1, 2; `nextCursor` 2 |
| 14 | `POST {{th}}/notes` `{ "body": "Added while paging" }` | 200 (sequence 7) |
| 15 | `GET {{th}}/thread?limit=2&after={{page1.response.body.nextCursor}}` (`# @name page2`) | 200: sequences 3, 4; `nextCursor` 4 |
| 16 | `GET …&after={{page2.response.body.nextCursor}}&limit=10` | 200: sequences 5, 6, 7 (the note from 14 last), `nextCursor` null — pages 13+15+16 hold 1..7 exactly once (AC) |
| 17 | `GET /api/Agent/Tickets/{{th}}` (`# @name th1`) | 200; `activities` = one `Created`; `rowVersion` for 18 |
| 18 | `POST {{th}}/transitions {action: "AwaitCustomer", rowVersion}` (`# @name th2`) | 200 `PendingCustomer` |
| 19 | `POST {{th}}/customer-replies` `{ "body": "Here is the screenshot you asked for." }` | 200; `status` **Open** (AC) |
| 20 | `POST /api/Agent/Tickets/{{ahmedNewTicket}}/replies` `{ "body": "Reply on a seeded ticket." }` | 200 — the seeded ticket's back-filled sequences accept a new one (R1) |
| 21 | `GET {{th}}/thread?after=8` (18 appended the `AwaitingCustomer` activity as sequence 8) | 200: sequence 9 Message Inbound, `authorId` = `@ahmedContact`, `recordedById` agent A; sequence 10 Activity `Opened` `{from: PendingCustomer, to: Open}` |
| 22 | `POST {{th}}/customer-replies` `{ "body": "Any update?" }` (ticket Open) | 200; status Open; `GET …/thread?after=10` → exactly one new entry, sequence 11 (AC) |
| 23 | `GET {{th}}` (`# @name th3`) then `POST {{th}}/transitions {action: "Resolve"}` (`# @name th4`) | 200 · 200 `Resolved` |
| 24 | `GET {{th}}` → `allowedActions` | contains `Reply`, `AddNote`, `Close`, `Reopen` |
| 25 | `POST {{th}}/replies` `{ "body": "One more detail before we close." }` | 200; `status` **Open**, `resolvedAtUtc` null; `GET …/thread?after=12` (23 appended `Resolved` as 12) → Message 13 then Activity `Reopened` 14 `{from: Resolved, to: Open}`; two outbox INSERTs (R6) |
| 26 | Resolve again (`GET` + transition) · `POST {{th}}/customer-replies` `{ "body": "It works now, thanks!" }` | 200 · 200 `Open` via `Reopened` |
| 27 | Resolve, then `Close` (chain rowVersions) | 200 `Closed`; `allowedActions` has `AddNote` and `Reopen`, **not** `Reply` |
| 28a/b | `POST {{th}}/replies` `{ "body": "Too late" }` with `Accept-Language: en` · `ar` | 422 `urn:crm:problem:domain-rule`, `code` `Tickets.TransitionNotAllowed`, detail "The ticket is Closed; that action is not allowed in this status." · Arabic detail naming «مغلقة» (AC) |
| 29 | `POST {{th}}/customer-replies` on Closed | 422 `Tickets.TransitionNotAllowed` (AC) |
| 30 | `POST {{th}}/notes` `{ "body": "Post-mortem note on a closed ticket." }` | 200 (notes allowed in every status) |
| 31 | `Reopen` (transition), then `AwaitCustomer`, then `POST {{th}}/customer-replies` | 200 · 200 · 200 `Open`; two outbox INSERTs (R6) |
| 32 | `Cancel` a fresh ticket (`@th5`: create + `Cancel`) · `POST {{th5}}/replies` · `POST {{th5}}/notes` | 200 · 422 `Tickets.TransitionNotAllowed` · 200 |
| 33a–c | `POST {{th}}/replies` `{ "body": "" }` · `{ "body": "   \n  " }` · `{}` | 400 `errors.Body` each (AC) |
| 34 | `POST {{th}}/notes` `{ "body": "" }` | 400 `errors.Body` |
| 35 | `POST {{th}}/customer-replies` `{ "body": "" }` | 400 `errors.Body` |
| 36 | `GET {{th}}` | `firstRespondedAtUtc` still equal to 3's |
| 37 | `POST {{th}}/replies` with `< ./data/ticket-thread-body-10000.json` | 200 (R7, R8) |
| 38 | `POST {{th}}/notes` with `< ./data/ticket-thread-body-10001.json` | 400 `errors.Body` (AC) |
| 39a–c | `GET {{th}}/thread?visibility=secret` · `?limit=0` · `?after=-1` | 400 `errors.Visibility[0]` (custom text) · `errors.Limit` · `errors.After` |
| 40 | `GET {{th}}/thread?limit=100` · `?limit=101` | 200 · 400 `errors.Limit` |
| 41a/b | `GET /api/Agent/Tickets/{{unknown}}/thread` · `POST {{unknown}}/replies` | 404 `Tickets.TicketNotFound` each |
| 42a–c | `GET /api/Agent/Tickets/{{redSeaNewTicket}}/thread` · `?visibility=public` · `POST {{redSeaNewTicket}}/notes` | 403 `Organisation.OutOfScope` each (JED-CARE, agent A) |
| 43 | Agent B `GET {{th}}/thread` | 403 `Organisation.OutOfScope` |
| 44 | Supervisor `GET {{th}}/thread` | 200 (RYD-CARE member); `people` names agent A and the supervisor where they acted |
| 45 | `GET {{th}}/thread` with no token | 401 |
| 46 | `GET {{th}}/thread` with `Accept-Language: ar` | 200, body identical to the `en` response — no server-rendered text (D9) |

**File: `backend/requests/tickets.http`** — update the titles Story 18 changes (no request body changes): line 182
("allowedActions has …" — add "Reply, AddNote"), line 352 → "allowedActions = Close, Reopen, Reply, AddNote",
line 424 → "allowedActions = AddNote only", line 524 (add nothing if it only asserts Unassign/Claim).

**File: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md`** line 10 — append "Story 18: the thread
(`GET …/Tickets/{id}/thread`), replies, customer replies and internal notes on `TicketsController`
(`tickets.read`/`tickets.write`)."

---

## Frontend Tasks

### F1 — Regenerate the API types

With the API running in Development: `pnpm --dir frontend gen:api` (R11). Expected diff: `frontend/openapi/agent.json`
and `app/api/generated/agent.ts` gain `/api/Agent/Tickets/{id}/thread`, `/replies`, `/customer-replies`, `/notes` and
the schemas `TicketThreadPageDto`, `TicketThreadEntryDto`, `TicketDepartmentRefDto`, `TicketBodyRequest` (R10). Commit
both. No hand-written DTO type.

### F2 — Locale keys

**Files: `frontend/i18n/locales/en/tickets.json` and `ar/tickets.json`** (**Write tool**) — add under `tickets`
(keep the existing keys):

| Key | en | ar |
|---|---|---|
| `tickets.status.New` | New | جديدة |
| `tickets.status.Open` | Open | مفتوحة |
| `tickets.status.PendingCustomer` | Pending customer | بانتظار العميل |
| `tickets.status.OnHold` | On hold | معلّقة |
| `tickets.status.Resolved` | Resolved | محلولة |
| `tickets.status.Closed` | Closed | مغلقة |
| `tickets.status.Cancelled` | Cancelled | ملغاة |
| `tickets.priority.Low` | Low | منخفضة |
| `tickets.priority.Normal` | Normal | عادية |
| `tickets.priority.High` | High | عالية |
| `tickets.priority.Urgent` | Urgent | عاجلة |
| `tickets.activity.Created` | {actor} created the ticket | أنشأ {actor} التذكرة |
| `tickets.activity.Opened` | {actor} opened the ticket | فتح {actor} التذكرة |
| `tickets.activity.AwaitingCustomer` | {actor} set the ticket to pending customer | جعل {actor} التذكرة بانتظار العميل |
| `tickets.activity.PutOnHold` | {actor} put the ticket on hold | علّق {actor} التذكرة |
| `tickets.activity.Resolved` | {actor} resolved the ticket | حلّ {actor} التذكرة |
| `tickets.activity.Closed` | {actor} closed the ticket | أغلق {actor} التذكرة |
| `tickets.activity.Reopened` | {actor} reopened the ticket | أعاد {actor} فتح التذكرة |
| `tickets.activity.Cancelled` | {actor} cancelled the ticket | ألغى {actor} التذكرة |
| `tickets.activity.Assigned` | {actor} assigned the ticket to {to} | أسند {actor} التذكرة إلى {to} |
| `tickets.activity.Claimed` | {actor} claimed the ticket | استلم {actor} التذكرة |
| `tickets.activity.Unassigned` | {actor} unassigned {from} | ألغى {actor} إسناد التذكرة إلى {from} |
| `tickets.activity.Transferred` | {actor} transferred the ticket from {from} to {to} | نقل {actor} التذكرة من {from} إلى {to} |
| `tickets.activity.PriorityChanged` | {actor} changed the priority from {from} to {to} | غيّر {actor} الأولوية من {from} إلى {to} |
| `tickets.activity.CategoryChanged` | {actor} set the category to {to} | غيّر {actor} التصنيف إلى {to} |
| `tickets.activity.unknown` | {actor} updated the ticket | حدّث {actor} التذكرة |
| `tickets.thread.title` | Conversation | المحادثة |
| `tickets.thread.empty` | No messages yet | لا توجد رسائل بعد |
| `tickets.thread.emptyDescription` | Replies, internal notes and the ticket history appear here. | تظهر هنا الردود والملاحظات الداخلية وسجل التذكرة. |
| `tickets.thread.loadError` | The conversation could not be loaded. | تعذّر تحميل المحادثة. |
| `tickets.thread.retry` | Try again | إعادة المحاولة |
| `tickets.thread.loadMore` | Show more | عرض المزيد |
| `tickets.thread.internalNote` | Internal note | ملاحظة داخلية |
| `tickets.thread.customer` | Customer | العميل |
| `tickets.thread.you` | You | أنت |
| `tickets.thread.colleague` | A colleague | أحد الزملاء |
| `tickets.thread.otherDepartment` | another department | قسم آخر |
| `tickets.thread.aCategory` | a category | تصنيف |
| `tickets.thread.nobody` | nobody | لا أحد |
| `tickets.thread.recordedBy` | Recorded by {name} | سجّله {name} |
| `tickets.detail.back` | Back | رجوع |
| `tickets.detail.notFound` | Ticket not found | لم يتم العثور على التذكرة |
| `tickets.detail.notFoundDescription` | The link may be wrong, or the ticket was removed. | قد يكون الرابط غير صحيح، أو أُزيلت التذكرة. |
| `tickets.detail.loadError` | The ticket could not be loaded. | تعذّر تحميل التذكرة. |

(`{…}` are vue-i18n named parameters; no `|` or `@` in any value.)

**Files: `frontend/i18n/locales/{en,ar}/errors.json`** — add to `errors.code.Tickets` (after line 106):
`BodyRequired`: "Enter the text of the message or note." / "أدخل نص الرسالة أو الملاحظة.";
`BodyTooLong`: "A message or note cannot be more than 10,000 characters." / "لا يمكن أن تزيد الرسالة أو الملاحظة على 10,000 حرف.".
`pnpm lint` fails if the en/ar key sets diverge.

### F3 — Composables (`frontend/app/composables/tickets/`)

**Create file: `useTicket.ts`** — `export type TicketDto = components['schemas']['TicketDto']` (agent client);
`useTicket(id: string)` → `load()` = `useAsyncData(\`agent-ticket-${id}\`, …GET /api/Agent/Tickets/{id}…)`, throwing the
problem like `useCustomer.ts` (`{ toastOnError: false }`). Story 20 extends it.

**Create file: `useTicketThread.ts`**

```ts
import type { components } from '~/api/generated/agent'

export type TicketThreadPageDto = components['schemas']['TicketThreadPageDto']
export type TicketThreadEntryDto = components['schemas']['TicketThreadEntryDto']

/** One ticket's conversation (Story 18). load() = the first page; page(after) = the entries with sequence > after
 * (next page, or everything added since - the cursor never duplicates or skips an entry). */
export function useTicketThread(ticketId: string) {
  const { clients, call } = useApi()

  function fetchPage(after?: number, signal?: AbortSignal) {
    return call(
      clients.agent.GET('/api/Agent/Tickets/{id}/thread', {
        params: { path: { id: ticketId }, query: { after, limit: 50 } },
        signal,
      }),
      { toastOnError: false },
    )
  }

  function load() {
    return useAsyncData(`agent-ticket-thread-${ticketId}`, async (_nuxtApp, { signal }) => {
      const result = await fetchPage(undefined, signal)
      if (!result.ok) throw result.problem
      return result.data
    })
  }

  const body = (text: string) => ({ params: { path: { id: ticketId } }, body: { body: text } })
  // Used by Story 20's composer; a 400 goes to useFormErrors(), never a toast.
  const reply = (text: string) => call(clients.agent.POST('/api/Agent/Tickets/{id}/replies', body(text)), { toastOnError: false })
  const addNote = (text: string) => call(clients.agent.POST('/api/Agent/Tickets/{id}/notes', body(text)), { toastOnError: false })
  const recordCustomerReply = (text: string) =>
    call(clients.agent.POST('/api/Agent/Tickets/{id}/customer-replies', body(text)), { toastOnError: false })

  return { load, page: (after: number) => fetchPage(after), reply, addNote, recordCustomerReply }
}
```

(Match the exact openapi-fetch option shape the generated types demand — `signal` placement as in `useCustomer.ts`.)

### F4 — Activity rendering (`frontend/app/utils/ticketThread.ts`)

Pure functions, no Vue imports:

```ts
export interface ThreadLookups {
  people: ReadonlyMap<string, string>                       // id -> display name
  categories: ReadonlyMap<string, LocalizedTextLike>
  departments: ReadonlyMap<string, LocalizedTextLike>
}
export interface ThreadText { key: string; params: Record<string, string> }
type Translate = (key: string, params?: Record<string, string>) => string
type Te = (key: string) => boolean

/** The locale key and resolved parameters of an activity entry (D9): tickets.activity.<templateKey>. */
export function activityText(entry: TicketThreadEntryDto, lookups: ThreadLookups, meId: string | undefined,
  locale: string, t: Translate, te: Te): ThreadText
```

Resolution: `actor` = `meId === authorId` → `t('tickets.thread.you')`, else the people name, else
`t('tickets.thread.colleague')`. `from`/`to` by kind: status kinds → `t('tickets.status.<value>')` (fall back to the
raw value when `te` is false); `PriorityChanged` → `tickets.priority.<value>`; `Assigned`/`Claimed`/`Unassigned` →
people name / `colleague` / `nobody` when null; `Transferred` → `localizedName(department, locale)` /
`otherDepartment`; `CategoryChanged` → `localizedName(category, locale)` / `aCategory`. Unknown `templateKey` →
`tickets.activity.unknown`. Also export `mergeLookups(pages: ReadonlyArray<TicketThreadPageDto>): ThreadLookups`
building the three maps (`{ ar: nameAr, en: nameEn }` for departments, `name` for categories).

### F5 — `frontend/app/components/ticket/TicketThread.vue`

Props `{ ticketId: string }`. Uses `useTicketThread(props.ticketId).load()` for the first page and `page(after)` for
"Show more" (appends a page; entries de-duplicated by `sequence` in a `Map`, ordered ascending); `mergeLookups` over
all loaded pages; `useFormat().dateTime` for `occurredAtUtc`; `useAuthStore().me?.id` for "You".

- `<section :aria-labelledby>` with heading `tickets.thread.title`.
- States (the `CustomerNotesPanel.vue` pattern): loading `UiSkeleton :lines="4"`; forbidden (`problemOf(error)?.type ===
  ProblemTypes.Forbidden`) `UiErrorState` with `errors.code.Organisation.OutOfScope`; other error `UiErrorState`
  `tickets.thread.loadError` + retry → `refresh()`; empty `UiEmptyState` `tickets.thread.empty` /
  `emptyDescription` (only reachable if a ticket had no entries — kept for the pattern); "Show more" `UiButton
  variant="secondary"` while `nextCursor` of the last loaded page is not null, with a loading flag.
- `<ol class="flex flex-col gap-3">`, one `<li :key="entry.sequence">` per entry:
  - **Message**: a card `max-w-[85%] rounded-md border p-3`; inbound `me-auto border-border bg-surface` with sender
    `tickets.thread.customer`; outbound `ms-auto border-info-500 bg-info-50` with the agent name/You; inbound shows
    `tickets.thread.recordedBy` (name) under the body. Body: `<p class="whitespace-pre-wrap break-words text-sm text-fg">{{ entry.body }}</p>`
    — **text interpolation only, never `v-html`** (D7).
  - **Note**: full-width card `border-warning-500 bg-warning-50` with `UiBadge tone="warning" size="sm"`
    `tickets.thread.internalNote`, author, time, same body paragraph.
  - **Activity**: one muted line `text-xs text-fg-muted` — `t(activityText(...).key, activityText(...).params)` and
    the time.
- Logical utilities only (`ms-`/`me-`/`text-start`), token utilities only. Arabic bodies need no `dir` override —
  they follow the page; add `dir="auto"` on the body paragraph so a Latin body in `ar` (and vice versa) aligns to its
  own script.

### F6 — Minimal detail page `frontend/app/pages/agent/tickets/[id].vue`

Copy `pages/agent/customers/[id].vue`: `definePageMeta({ layout: 'agent', key: (route) => route.path })`, `isGuid`
check → `UiEmptyState` `tickets.detail.notFound`; back link `NuxtLink to="/agent"` `tickets.detail.back`; a header
from `useTicket(id).load()` — `<bdi dir="ltr">{{ ticket.number }}</bdi>`, subject (`h1`), `UiBadge`
`tickets.status.<status>`, loading skeleton / error state (`tickets.detail.loadError` + retry; 404 →
`tickets.detail.notFound`; 403 → forbidden); below it `<TicketThread :ticket-id="id" />`. **No composer, no actions,
no nav item** — Story 20 owns them (comment at the top of the template: "Story 20 replaces this page with the ticket
workspace; TicketThread stays."). Reached by URL: `/agent/tickets/<id>`.

No UI kit component is added, so no `/_dev/ui` section.

---

## Edge Cases & Failure Modes

- **Reply to Closed / Cancelled** → handler `TicketRefusals.NotAllowed(ticket, TicketAction.Reply, …)` → 422
  `Tickets.TransitionNotAllowed`, localised detail naming the status; the aggregate's `EnsureAllowed(TicketAction.Reply)`
  (B1 item 8) is the backstop with the same code. Notes are never refused by status.
- **Reply to Resolved** → message appended, then `Reopen` (one `Reopened` activity, `ResolvedAtUtc`/`ClosedAtUtc`
  cleared), two outbox rows (R6). **Customer reply to PendingCustomer** → `Opened` activity. Customer reply in New,
  Open or OnHold → status unchanged, one entry only.
- **First response** → `FirstRespondedAtUtc ??= nowUtc` in `ReplyAsAgent` only; a customer reply or a note never sets
  it; later agent replies never move it (`ticket-thread.http` 5, 36). A ticket resolved with no reply and then
  replied to gets its first response at that reply.
- **Empty / whitespace / missing / oversize body** → 400 `errors.Body` (validator, B4); if a handler is ever called
  without the validator, `NormaliseBody` throws 422 `Tickets.BodyRequired`/`BodyTooLong` before `Revision` moves.
- **HTML or script in a body** → stored verbatim, returned verbatim, rendered as text (`{{ }}` interpolation in F5). No
  sanitiser exists and none is needed while nothing renders HTML.
- **Two simultaneous appends to one ticket** → both load the same rowversion; the second `UPDATE … WHERE [RowVersion]`
  affects 0 rows → `ConcurrencyConflictException` → 409 `Persistence.ConcurrencyConflict`
  (`TransactionBehavior.cs` lines 91–98); nothing of the loser is written. The unique `(TicketId, Sequence)` indexes
  make a duplicate sequence impossible even if the rowversion check were bypassed (a SQL 2601 would surface as 500 —
  R9 proves it never happens).
- **Paging while entries are added** → new entries take a higher `Sequence`; `after` paging never duplicates or skips
  (D3, `ticket-thread.http` 13–16). A client that polls `after = <last sequence>` sees each new entry once.
- **Visibility filter** → `?visibility=public` uses `ListPublicAsync` (messages only, R5). An unknown value → 400
  `errors.Visibility[0]`. Duplicates (`visibility=public&visibility=public`) are de-duplicated → still the public path.
- **Ticket not visible** → read model returns null → `GetByIdAsync` throws for an existing out-of-scope ticket (403
  `Organisation.OutOfScope`, `Repository.cs` lines 108–119) or returns null (404). A transferred ticket follows the
  same rule: the sender reads its thread as 403.
- **Lookup failures** (Identity/Organisation) → names missing in `people`/`departments`; the page still returns 200;
  the UI falls back to "A colleague" / "another department". More than 200 distinct staff ids on one page (only
  possible with a pathological history) → the first 200 are named.
- **A department or category renamed later** → the thread shows the current name (ids are stored, not names).
- **Old Story 15 rows** → back-filled sequences by `OccurredAtUtc, Id` (R1); two activities with an identical timestamp
  are ordered by id (arbitrary but stable).
- **Split queries without a transaction** (the thread and `GET ticket` queries) → the statements can see a commit
  between them; worst case a just-added entry is missing from `TicketDto.activities` or appears in one collection
  only. Accepted; commands run inside `TransactionBehavior`'s transaction.
- **`TicketDto.allowedActions` gains `Reply` and `AddNote`** → any consumer that treated it as "status actions only"
  must filter; Story 20 is told (Coordination). Story 15's `.http` titles are updated (B9).
- **Seeder** → unchanged; on a fresh database the seeded tickets get sequences 1..n from the same `NextSequence`.
- **Logs** → no body is ever logged (no new `LoggerMessage`); the outbox event carries no body (D10).

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the `CRM0001` rules, the startup guard, the
aggregate's rules, `backend/requests/ticket-thread.http` and the browser walk replace the suite. Run every row;
record results and any R-fallback in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001`, no `RS0030` |
| 2 | No child DbSet / repository | `grep -rn "DbSet<\|AddRepository<" backend/src/Modules/Tickets` | only `Ticket` and `Category` |
| 3 | No public setter on the children | `grep -n "{ get; set; }" backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/*.cs` | no match |
| 4 | Migration | read `…/Migrations/*_ConversationThread.cs` | B7 checklist; `nvarchar(max)` bodies; the back-fill SQL between `AddColumn` and the unique index; schema `tickets` only |
| 5 | Startup + back-fill | run the API on the existing dev DB | starts; R1 SQL checks pass |
| 6 | Risks R1–R11 | as stated in Prerequisites | each proven or its fallback recorded |
| 7 | Endpoints + rules (AC) | `ticket-thread.http` 1–46 top to bottom | every row's expected status/code/body |
| 8 | One entry per append (AC) | 3→4, 19→21, 22, 6→10 | exactly one entry of the right `visibility` per call (plus the status activity where D6 says so) |
| 9 | Notes never public (AC) | 11, 42b; R5 EF log | no `Note` entry, no `TicketNotes` SQL |
| 10 | Outbox (D10) | Verification step 5 | counts as stated |
| 11 | Concurrency | Verification step 6 | R9 |
| 12 | Regressions | `tickets.http` 12, 21–38, 42, 43, 52–54, 57–61 | unchanged except the updated `allowedActions` titles; claim race (Story 15 Verification 4) still one winner |
| 13 | Swagger | `/swagger/agent/swagger.json` | 200 with the four new schemas (R10) |
| 14 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0 |
| 15 | Thread screen | `pnpm --dir frontend dev`, agent A opens `/agent/tickets/<@th id>` in `en` and `ar` at 375 / 768 / 1280 | skeleton → entries; activity sentences in the active language with status names localised; outbound on the end side, inbound on the start side, mirrored in RTL; notes visibly "Internal note"; line breaks kept; the `<b>`/`<script>` note shows as literal text; "Show more" pages; agent B gets the forbidden state; an unknown id the not-found state |

---

## Migration / Rollback

- One new migration: `Tickets/ConversationThread` — `TicketActivities.Sequence` + back-fill + unique index,
  `TicketMessages`, `TicketNotes`. No other schema changes. Applied at startup in Development.
- **Rollback:** `pwsh backend/scripts/ef.ps1 -Module Tickets database update Initial` (drops the two tables and the
  column; messages and notes are lost), then revert the commit. No production data exists.
- **Half-applied state:** the migration runs in one transaction on SQL Server; a failure in the back-fill rolls the
  whole step back and startup fails loudly. Never hand-edit `__EFMigrationsHistory`.

---

## Verification Steps

1. **Backend builds:** `dotnet tool restore`; `dotnet build backend/Crm.sln` → Test Plan rows 1–4.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` (Development, EF SQL logging on) → row 5 (R1) and
   row 13; then prove R2, R3, R4, R5, R6 with `ticket-thread.http` 3, 6, 9, 11, 25 before running the file top to
   bottom (row 7).
3. **Fixtures:** create the two JSON files (B9) and check `wc -c`; run 37 and 38 (R7, R8).
4. **R8 fallback (only if the REST client ignores `<`):** in Git Bash with `A` = agent A's access token and `T` = the
   `@th` id: `curl -s -o /dev/null -w "%{http_code}\n" -X POST -H "Authorization: Bearer $A" -H "Content-Type: application/json" --data @backend/requests/data/ticket-thread-body-10000.json http://localhost:5187/api/Agent/Tickets/$T/replies`
   → 200, and the `10001` file to `/notes` → 400.
5. **Outbox:** after the file ran,
   `SELECT Type, COUNT(*) FROM tickets.outbox_messages WHERE Content LIKE '%<@th id>%' GROUP BY Type` → one
   `TicketMessageAddedIntegrationEvent` row per successful reply or customer reply (3, 5, 8, 19, 22, 25, 26, 31, 37 =
   9), **none** for the notes; one `TicketStatusChangedIntegrationEvent` per transition request plus one per reply
   that changed the status (19, 25, 26, 31); `SELECT Content …` for one message row shows no `body`.
6. **Concurrent appends (R9):** in Git Bash, same `A`/`T`:

   ```bash
   H=http://localhost:5187
   for i in 1 2 3 4 5; do
     curl -s -o /dev/null -w "A$i %{http_code}\n" -X POST -H "Authorization: Bearer $A" -H "Content-Type: application/json" \
       -d '{"body":"race A"}' "$H/api/Agent/Tickets/$T/notes" &
     curl -s -o /dev/null -w "B$i %{http_code}\n" -X POST -H "Authorization: Bearer $A" -H "Content-Type: application/json" \
       -d '{"body":"race B"}' "$H/api/Agent/Tickets/$T/notes" &
     wait
   done
   ```

   Expected: only `200` and `409`, never `500`; then `GET …/thread?visibility=internal&limit=100` → sequences strictly
   increasing with no repeats, one note per 200.
7. **Frontend types:** with the API up, R11, then `pnpm --dir frontend gen:api` → only `openapi/agent.json` and
   `app/api/generated/agent.ts` change.
8. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` →
   row 14.
9. **Frontend runs:** `pnpm --dir frontend dev` with the API up → row 15 in `en` and `ar`.

---

## Done Criteria

- [ ] An agent reply, a customer reply and an internal note each append exactly one entry of the right visibility
      (`ticket-thread.http` 3–4, 19–22, 6–10).
- [ ] Internal notes never appear in the public-visibility query (`ticket-thread.http` 11; R5 EF log).
- [ ] `firstRespondedAtUtc` is set by the first agent reply only (3, 5, 36).
- [ ] A customer reply on PendingCustomer returns the ticket to Open (19); a reply on Closed is 422 (28, 29); a reply
      on Resolved reopens it (25, 26).
- [ ] The thread is chronological (by `Sequence`), cursor-paged, and an entry added mid-read is neither duplicated nor
      skipped (13–16).
- [ ] Activity entries are stored as template key (`Kind`) + parameters (`From`/`To`) and render in `en` and `ar` from
      `tickets.activity.*` keys on `/agent/tickets/[id]`.
- [ ] Empty and over-limit bodies are 400 with `errors.Body` (33–35, 38).
- [ ] `TicketMessageAddedIntegrationEvent` is published for public messages only (Verification 5).
- [ ] Every path exercised from `backend/requests/ticket-thread.http`.
- [ ] `dotnet build backend/Crm.sln` clean with 0 warnings and no `CRM0001`; the `ConversationThread` migration is
      included; `pnpm typecheck`/`lint`/`build` clean; `gen:api` output committed.
- [ ] No building-block file changed (`git diff --stat -- backend/src/BuildingBlocks` shows only `AREAS.md`).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/ticket-management/00-overview.md` and `.squad/MVP-STORIES.md` list this plan; MVP-STORIES row 11
      marked done by the implementation session.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 20.**
