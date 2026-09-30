# Story 20 — Ticket inbox and ticket detail workspace UI

## Prerequisites

- **Stories 01–07, 11, 14, 15 and 18 are implemented and committed** (latest `65084a6` "Implement Story 18"). Read
  the "Product rules" of [`15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md`](15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md)
  (lines 222–305: transition table, allowed actions, claim/assign/transfer rules, queue and search rules, error codes)
  and of [`18-story-ticket-conversation-thread-messages-internal-notes-and-history.md`](18-story-ticket-conversation-thread-messages-internal-notes-and-history.md)
  (lines 185–248: cursor paging D3, visibility D4, no rowVersion on replies D5, reply rules D6, plain-text bodies D7).
  Their coordination bullets for this story are the contract:
  - Story 15 (lines 115–121): consume exactly the Agent endpoints of `TicketsController`; offer only
    `TicketDto.allowedActions` **intersected with the caller's permissions** (`tickets.write` for Claim, status,
    priority, category; `tickets.assign` for Assign, Unassign, Transfer); customer names in the inbox need a **batch
    lookup on `ICustomersModuleApi`**; the customer page's Tickets panel uses `GET /api/Agent/Tickets?customerId=…&queue=all`.
  - Story 18 (lines 90–96): replace `frontend/app/pages/agent/tickets/[id].vue` with the workspace, keep `TicketThread`
    as the centre column, add the composer (Reply / Internal note) and, after a 200, load the newer entries with the
    cursor; offer a tab only when `allowedActions` contains `Reply` / `AddNote` **and** the caller has `tickets.write`;
    a 409 `Persistence.ConcurrencyConflict` on a reply keeps the draft and offers "send again". Story 18 edge case
    (line 1198): `allowedActions` now also lists `Reply` and `AddNote` — **filter them out of the status action bar**.
  - Story 14 (lines 64–67 of [`../customer-management/14-story-customer-360-workspace-ui.md`](../customer-management/14-story-customer-360-workspace-ui.md)):
    reuse `<CustomerPicker v-model>` (model `CustomerLookupDto | null`) and replace `CustomerTicketsPanel.vue`'s body
    (the `<section>` shell stays).
- **Modules.** The intake says "none (frontend); reads Tickets". Three intake requirements have **no endpoint today**
  (customer names in the inbox, a picker of agents in the ticket's department, a list of transfer targets), so this
  story adds **three read-only Contracts reads and two Agent read endpoints** — decision **D1**:
  - **`Tickets`** — `TicketListItemDto` gains `customer` (number + both names, from the new Customers read);
    `GET /api/Agent/Tickets/agents` and `GET /api/Agent/Tickets/transfer-targets` (two new queries). No command, no
    domain, no schema, no migration change.
  - **`Customers`** — one new `ICustomersModuleApi.GetCustomerNamesAsync` backed by a new
    `[RequirePermission(CustomersRead)]` query (scoped by the normal filter: out-of-scope ids are simply absent).
  - **`Organisation`** — two new `IOrganisationModuleApi` reads (`GetDepartmentMembersAsync`, `ListDepartmentsAsync`),
    each a new in-process `[RequireAuthenticatedUser]` query, exactly like Story 15's `IsDepartmentMemberQuery`.
  - **No building-block change** (`git diff --stat -- backend/src/BuildingBlocks` shows only `AREAS.md`). No new
    project reference: `Tickets.Application` already references `Customers.Contracts`, `Organisation.Contracts`,
    `Identity.Contracts` (`Crm.Modules.Tickets.Application.csproj`). No new integration event. No migration.
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md` row 12).** Absorbs **21** (ticket detail workspace). **Deferred:**
  bulk actions, realtime updates, saved/shared views, column chooser, preview pane, SLA indicators, keyboard shortcuts,
  collision awareness, quick replies, AI suggestions, attachments, related tickets, recording a customer reply from the
  UI (the endpoint exists; not asked by the intake), **any test suite**.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`); EF Core
  10.0.11; MediatR **12.4.1** (licence pin — do not bump); FluentValidation 12.1.1; Swashbuckle 10.2.3.
  `frontend/package.json`: Nuxt **4.5.2**, Vue 3.5.43, `@nuxtjs/i18n` 10.6.0, `openapi-fetch` 0.17.0,
  `openapi-typescript` 7.13.0, TypeScript 5.9.3, `packageManager` pnpm **12.6.0**. **No new NuGet package. No new npm
  package. No new UI kit component** (so no new `/_dev/ui` section). This machine has **no Python** and **no `pnpm` on
  PATH** until `corepack enable pnpm --install-directory "$APPDATA/npm"`; Arabic text and `$` break Bash heredocs —
  use the **Write tool** for every file with Arabic in it (locale JSON, `.http`).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — `pnpm` is available.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, then
    `pnpm --version` prints `12.6.0`, then `pnpm --dir frontend install --frozen-lockfile` exits 0.
  - **R2 — the five new queries pass the startup guard and DI.** Each carries exactly one authorisation attribute
    (`RequestAuthorizationGuard` refuses to start otherwise). **Prove:** after B1–B3, `dotnet build backend/Crm.sln`
    is clean, the API starts, and `ticket-workspace.http` 1, 4 and 8 answer 200.
  - **R3 — `ids.Contains(x.Id)` translates for the three new specifications** (`CustomersByIdsSpecification`,
    `MembershipsOfDepartmentsSpecification`, and the existing `DepartmentsSpecification` reuse). The precedents
    `CategoriesByIdsSpecification` (Tickets) and `DepartmentsByIdsSpecification` (Organisation) already translate a
    list of converted ids. **Prove:** EF SQL log for `ticket-workspace.http` 1 shows `[c].[Id] IN (…)` (or `OPENJSON`)
    on `[customers].[Customers]` plus the customers scope predicates; `ticket-workspace.http` 4 shows
    `[m].[DepartmentId] IN (…)` on `[organisation].[Memberships]` with `[m].[IsDeleted] = CAST(0 AS bit)`.
    **Fallback:** none needed beyond copying `CategoriesByIdsSpecification` exactly (whole converted property in
    `Contains`, never `.Value`).
  - **R4 — customer names degrade, never fail.** Out-of-scope customers (a ticket transferred to a department that
    does not own the customer) come back absent, so `customer` is `null` and the page still answers 200. **Prove:**
    run `tickets.http` top to bottom through request 54 (request 52 — file line 617 — transfers `@t2`, a ticket of a
    RYD-CARE customer, to JED-CARE), then `ticket-workspace.http` 2 as agent
    B → 200 and that ticket's `customer` is `null`; the inbox shows "Customer outside your scope" for it.
  - **R5 — generated client.** After `pnpm gen:api`, `app/api/generated/agent.ts` has `TicketCustomerRefDto`,
    `TicketAgentDto`, `customer?` on `TicketListItemDto`, and the paths `/api/Agent/Tickets/agents` and
    `/api/Agent/Tickets/transfer-targets`; `pnpm --dir frontend typecheck` passes with **no cast**. **Fallback:** rename
    a clashing schema with a `Ticket` prefix (none is expected — grep `agent.ts` for the names first).
  - **R6 — query serialisation and routing.** openapi-fetch 0.17 serialises `status: ['Open']` as `status=Open`
    (form, explode) and `agents`/`transfer-targets` are not captured by `{id:guid}`. **Prove:** inbox with a status
    filter → DevTools Network shows `/api/Agent/Tickets?queue=…&status=Open…` → 200 with only Open tickets;
    `ticket-workspace.http` 4 and 8 → 200 (not 404/405).
  - **R7 — write-through keeps the rowVersion fresh.** Every command's `TicketDto` is written into the page's
    `useAsyncData` data (`data.value = dto`). **Prove:** ticket detail → change priority → change it again **without
    reloading** → both 200 (the second sends the first response's `rowVersion`), then change the category → 200.
    **Fallback:** in `useTicketWorkspace.publish` call `refresh()` instead of assigning; record it.
  - **R8 — sticky header inside the layout's scroll container.** `layouts/agent.vue` makes `<main>` the scroller
    (`flex-1 overflow-y-auto p-4`, line 47), so `sticky top-0` sticks inside `main`'s padding box. **Prove:** a long
    thread at 375 and 1280 px, scroll `main`: the header stays visible and flush with the top of `main`, no content
    shows above it. **Fallback:** `-top-4` (cancels `main`'s `p-4`) on the header; record it.
  - **R9 — server-worded refusals.** `problemMessage` (F1) now prefers the server `detail` for
    `Tickets.AlreadyClaimed` and `Tickets.TransitionNotAllowed` (the detail is already in the request language through
    `Accept-Language`, `app/plugins/api.ts` line 16, and names the winner / the current status —
    `TicketRefusals.cs` lines 14–32). **Prove:** in `ar`, two browsers (agent A and supervisor) open unassigned
    T-000001; both click "إسناد إليّ": one wins, the other sees "استلم Omar Haddad هذه التذكرة بالفعل." (or the
    supervisor's name) inline, and its ticket refreshes to show the winner; in `en`, open a ticket in two tabs, Resolve
    in tab 1, "Put on hold" in tab 2 → "The ticket is Resolved; that action is not allowed in this status." inline.
  - **R10 — vue-i18n message syntax.** Every new value uses only `{name}`-style placeholders; **no `@` or `|`**.
    **Prove:** `pnpm --dir frontend dev`, walk every new screen in `en` and `ar`: no `[intlify]` warning in the console.
  - **R11 — the thread appends without a reload.** `TicketThread` loads the entries after its last sequence when the
    workspace's `version` changes (F5b). **Prove:** reply, then add a note, then Resolve: each entry appears at the
    end within a second, Network shows `GET …/thread?after=<n>&limit=50` (not a first-page reload), and no entry is
    duplicated.
- **Coordination:**
  - **Story 47** (portal) does not use these pages; it must not call `/agents` or `/transfer-targets` (staff only).
  - **Story 58** (branding) themes these screens through the existing `--crm-*` tokens only — no literal colour here.
  - **Deferred 28–31** (agent workspace) may move the queue counts onto the home dashboard; the `agent-ticket-queues`
    key and `GET …/queues` stay as they are.

---

## Story Goal

Agents find tickets and work them from one place, equally usable in Arabic RTL and on a phone.

1. **`/agent/tickets`** — the inbox: built-in views **My tickets** (default), **Unassigned**, **All in scope** with
   their counts; a server-side paged (20), sorted, filtered list (status, priority, category, assignee, department,
   branch, customer) with a debounced search on number and subject; every setting in the URL; status and priority
   badges on every row; a table from 1024 px and cards below it.
2. **New ticket drawer** — `CustomerPicker` → contact → category → subject, description, priority (and department
   when the scope is ambiguous); 400/422 errors on their fields; success lands on the ticket's detail page.
3. **`/agent/tickets/[id]`** — the workspace: a sticky header (number, subject, status, priority, assignee, primary
   actions), the Story 18 thread in the centre with a Reply / Internal note composer, a properties panel (status
   actions, priority, category, assignee, claim, assign, unassign, transfer) and a customer context panel (name,
   contact, link to the customer page). Below 1024 px the side panels become a three-way switch
   (Conversation / Details / Customer). Mirrors in RTL.
4. Only the transitions `allowedActions` allows **and** the caller's permissions allow are offered; a refusal (422, or
   409 claim lost) shows the aggregate's localised reason inline and refreshes the ticket.
5. The customer 360 page's **Tickets** panel lists that customer's tickets with links.
6. Loading, empty (with a next action), and error (with retry) states on every data view; all copy `en` + `ar`.

**Not in scope:** everything deferred in Prerequisites; **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modules talk only through Contracts" (synchronous read, primitives only), "Application, API, and
   persistence" (**Queries**, **Results**, **Pipeline behaviours**, **Scoping**), the Frontend section (RTL logical
   utilities, `useApi`, `useFormErrors`, `useFormat`, `<bdi dir="ltr">`, one composable per use case), "Testing policy".
2. The two Tickets plans in this folder — the Product rules and coordination bullets named in Prerequisites; Story 15
   B6 "the standard shape" (lines 1195–1224) for a query handler with nested Contracts reads.
3. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Presentation/Controllers/Agent/TicketsController.cs` — lines 31–50
   (`Search`, `Queues` — B3 adds two actions after `Queues`), 68–132 (every command route and verb: transitions
   `POST {id}/transitions` `{ action, rowVersion }`; claim `POST {id}/claim` no body; assign `{ agentId, rowVersion }`;
   unassign `{ rowVersion }`; transfer `{ departmentId, rowVersion }`; priority `PUT {id}/priority`
   `{ priority, rowVersion }`; category `PUT {id}/category` `{ categoryId, rowVersion }`), 134–173 (thread, replies,
   notes), request records 176–192.
4. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/Dtos.cs` — `TicketPersonDto` (line 31), `TicketDto`
   (35–42: `allowedActions`, `people`, `rowVersion`, `assignedAgentName`), `TicketListItemDto` (44–47 — B3 adds
   `Customer`), `TicketQueueCountsDto` (49), `TicketDepartmentRefDto` (52 — reused by transfer targets).
5. `…/Tickets.Application/TicketDtoAssembler.cs` — constructor lines 15–16, `ToListItemsAsync` lines 133–156 (B3
   extends it), `LookupAsync` 158–167 (the "lookup failure never fails the page" shape to copy).
6. `…/Tickets.Application/Queries/SearchTickets/SearchTicketsQueryHandler.cs` (whole file; calls the assembler last),
   `Queries/ListTicketCategories/*` (a `[RequirePermission(TicketsRead)]` query using `currentUser.Scope.Departments`),
   `Queries/GetTicketQueueCounts/GetTicketQueueCountsQuery.cs` (a parameterless query record),
   `TicketsErrors.cs` lines 13–14 (`DepartmentOutOfScope`), `TicketRefusals.cs` lines 14–32, `CurrentActor.cs`.
7. `…/Tickets.Domain/TicketAction.cs` lines 3–22 (the 15 action names `allowedActions` can hold) and `Ticket.cs`
   lines 157–174 (`IsAllowedInCurrentStatus`, `CanPerform`, `AllowedActions`).
8. `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/ICustomersModuleApi.cs` lines 9–30,
   `…/Customers.Application/CustomersModuleApi.cs` lines 12–30, `Queries/FindContactsByEmail/*` (a Contracts-backed
   `[RequirePermission(CustomersRead)]` query returning a Contracts record — copy its shape), `Queries/LookupCustomers/*`,
   `Specifications/CustomerByIdSpecification.cs`, `Customers.Domain/LocalizedText.cs` lines 21–30 (both sides always
   filled).
9. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/IOrganisationModuleApi.cs` lines 7–25,
   `…/Organisation.Application/OrganisationModuleApi.cs` lines 12–22, `Queries/IsDepartmentMember/*` and
   `Queries/GetDepartmentReference/*` (in-process `[RequireAuthenticatedUser]` queries), `Queries/ListDepartments/ListDepartmentsQueryHandler.cs`,
   `Specifications/DepartmentsSpecification.cs` (reused), `Specifications/DepartmentsByIdsSpecification.cs` (the
   `Contains` precedent), `Domain/Membership.cs` lines 8–26 (`ITenantScoped`, `ISoftDeletable`, `Role`).
10. `backend/src/Modules/Identity/Crm.Modules.Identity.Contracts/IIdentityModuleApi.cs` (`GetStaffUsersAsync`,
    `StaffUserSummary.IsActive`) and `…/Identity.Application/Queries/GetStaffUsers/GetStaffUsersQueryValidator.cs`
    (**≤ 200 ids**).
11. `backend/requests/tickets.http` lines 1–71 (header, variables, logins — copy) and requests 52–54 (file lines
    617 onward: the transfer and who can read it afterwards).
12. Frontend, the patterns to copy:
    - `app/pages/agent/customers/index.vue` (whole file — the inbox copies its search/filter/sort/table/cards/states
      structure), `app/composables/customers/useCustomerSearch.ts` (URL state + `useAsyncData` + `signal`),
      `app/utils/customers.ts` (`parseCustomerFilters`/`customerFiltersToQuery`, `isGuid` line 27),
      `app/utils/debounce.ts`, `app/utils/table.ts`.
    - `app/components/customer/CustomerFormDrawer.vue` (drawer form, `showDepartment` lines 222–226, the F1b error
      order), `CustomerPicker.vue` (props lines 97–105), `CustomerNotesPanel.vue` (panel states, success toasts),
      `CustomerTicketsPanel.vue` (whole — F9 replaces the body), `app/pages/agent/customers/[id].vue` line 17.
    - `app/composables/customers/useCustomer.ts` (`customerPanelKey`, `load('profile')`),
      `useCustomerLookup.ts` (F2 adds `get`), `app/composables/tickets/useTicket.ts`, `useTicketThread.ts`,
      `app/components/ticket/TicketThread.vue` (whole — F5b adds two props and `loadNewer`), `app/utils/ticketThread.ts`,
      `app/pages/agent/tickets/[id].vue` (whole — F7 replaces it).
    - `app/composables/useApi.ts` lines 18–66 (`call`, `toast`), `useFormErrors.ts` (`apply`, `applyCode`),
      `useCan.ts`, `app/utils/permissions.ts`, `app/api/problem.ts` (`ProblemTypes`, `Problem`, `problemOf`),
      `app/api/problem-message.ts` lines 8–16 (F1 changes it), `app/stores/scope.ts` (`myScope`, `activeDepartments`),
      `app/stores/auth.ts` (`me.id`), `app/stores/toasts.ts` (`push({ tone, title })`), `app/utils/scope.ts`
      (`localizedName`).
    - UI kit: `components/ui/Tabs.vue` (composer tabs; ids are `${tab.id}-tab`/`-panel` — must be unique on the page),
      `Badge.vue` (tones), `Drawer.vue`, `Modal.vue`, `Select.vue` (`options: { value, label, disabled? }[]`),
      `Table.vue` (`Column.sortable`, `v-model:sort`), `Textarea.vue`, `EmptyState.vue`, `ErrorState.vue`,
      `Skeleton.vue`, `Pagination.vue`, `Button.vue`.
    - `app/layouts/agent.vue` line 47 (`main` scroller — R8) and 100–111 (`agentNav`).
    - `i18n/locales/{en,ar}/tickets.json`, `customers.json` lines 76–78, `errors.json` (`errors.code.Tickets`,
      `errors.code.Organisation`), `scripts/check-locales.ts`, `eslint.config.mjs` lines 36–47 (bare-string allowlist
      includes `·`, `—`, `✕`).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **D1 — the missing reads are added as Contracts reads plus two Tickets Agent endpoints**, never as a frontend fan-out
  and never by widening an Admin endpoint to agents:
  - `ICustomersModuleApi.GetCustomerNamesAsync(ids)` → `CustomerNameSummary(Id, Number, DisplayNameAr, DisplayNameEn)`;
    `customers.read` + the normal scope filter; ≤ 100 ids; out-of-scope/unknown ids are absent.
  - `IOrganisationModuleApi.GetDepartmentMembersAsync(departmentIds)` → `DepartmentMember(UserId, DepartmentId, Role)`
    (non-removed memberships of the caller's tenant; ≤ 100 department ids) and
    `IOrganisationModuleApi.ListDepartmentsAsync(activeOnly)` → `DepartmentReference[]` of the caller's tenant.
    Both `[RequireAuthenticatedUser]`, in-process only (no Organisation HTTP endpoint).
  - `GET /api/Agent/Tickets/agents?departmentId=` (`tickets.read`) → active staff who are members of that department,
    or of every department in the caller's active scope when omitted; a `departmentId` outside the active scope is
    403 `Organisation.OutOfScope`. At most 200 users (Identity's limit); ordered by display name.
  - `GET /api/Agent/Tickets/transfer-targets` (`tickets.assign`) → every **active** department of the tenant as
    `TicketDepartmentRefDto` (id, code, both names), ordered by code. Department names are not sensitive inside a
    tenant; the list exists because a transfer's target is by definition usually outside the sender's scope.
- **D2 — inbox URL is the state.** Keys: `view` (`mine`|`unassigned`|`all`, default `mine`), `q`, `status`, `priority`
  (one value each — single-select), `category`, `assignee`, `department`, `branch`, `customer` (uuids), `sort`
  (`createdAt`|`modifiedAt`|`priority`|`number`, default `createdAt`), `dir` (`asc`|`desc`, default `desc`), `page`
  (≥ 1). Defaults are omitted from the URL; malformed values are dropped when parsed. Search keystrokes
  `router.replace` (debounced 300 ms, trimmed, ≤ 200); every other change `router.push`; any change except paging resets
  `page` to 1. Changing the view keeps the other filters.
- **D3 — status filter semantics follow Story 15.** Empty status = the server default: working statuses for `mine` and
  `unassigned` (label "Open work"), every status for `all` (label "Any status"). Choosing a status replaces the
  default. The **assignee** filter shows only in the `all` view (in `mine` it is the caller, in `unassigned` nobody)
  and is sent only there; its options are "Anyone", "Me", then `GET …/agents` (scope-wide).
- **D4 — sorting.** Table headers sort Number, Priority, Updated (`modifiedAt`), Created. A newly chosen column starts
  **descending** for Created, Updated and Priority (newest / most urgent first) and ascending for Number. Below 1024 px a
  sort select offers: newest, oldest, recently updated, highest priority, number.
- **D5 — actions offered = `allowedActions` ∩ permissions** (`app/utils/tickets.ts` `offeredActions`): `Open`,
  `AwaitCustomer`, `PutOnHold`, `Resolve`, `Close`, `Reopen`, `Cancel`, `Claim`, `ChangePriority`, `ChangeCategory`,
  `Reply`, `AddNote` need `tickets.write`; `Assign`, `Unassign`, `Transfer` need `tickets.assign`. Unknown names are
  dropped. The **status action bar** shows only the seven status actions; `Reply`/`AddNote` only decide the composer
  tabs. The API enforces everything again; the UI hides, it does not guard.
- **D6 — one refusal handler** (`useTicketWorkspace().run`): 200 → write the `TicketDto` into the page's data, bump the
  thread `version`, success toast. 409 `Persistence.ConcurrencyConflict` → the standard toast + refresh. Any other 409
  (`Tickets.AlreadyClaimed`), any 422, any 400 → the localised text **inline** in the header's alert (`role="alert"`,
  dismissible) + refresh (so the offered actions follow the real state). Anything else → the standard toast.
- **D7 — server-worded refusals win.** `problemMessage` prefers the server `detail` for `Tickets.AlreadyClaimed` and
  `Tickets.TransitionNotAllowed` (already localised by `Accept-Language` and naming the winner / the current status);
  every other code keeps the `errors.code.*` text. This applies to toasts too (same function).
- **D8 — layout.** ≥ 1024 px (`lg`): two columns — thread + composer (`minmax(0,1fr)`), and a 20rem side column holding
  the properties panel above the customer panel. < 1024 px: one column and a three-button switch
  (Conversation / Details / Customer, `aria-pressed`, Conversation first); only the chosen region shows. Grid and
  flex order follow `dir`, so RTL mirrors with no extra class. The header is `sticky top-0` (R8) and carries the alert.
- **D9 — header primary actions:** "Assign to me" (Claim), "Resolve", "Reopen" when offered. Everything else lives in
  the properties panel. **Cancel** and **Transfer** ask for confirmation. After a transfer whose target department is not
  in the caller's active scope, the page toasts "Ticket transferred to …" and goes to `/agent/tickets` (the ticket would
  now read as 403).
- **D10 — assign picker** = `GET …/agents?departmentId=<ticket department>` minus the current assignee; it is loaded
  only when the caller may Assign. **Category picker** = `GET /api/Agent/TicketCategories?departmentId=<ticket department>`;
  a current category missing from the list (deactivated) shows as a disabled option "<name> (inactive)". A category
  cannot be cleared (the API has no "no category" command).
- **D11 — composer.** Tabs "Reply" and "Internal note" (only the offered ones; none → no composer). One draft per tab.
  Body ≤ 10,000 characters, sent trimmed; blank → the submit button is disabled. 400 → field `body` via
  `useFormErrors().apply`; 422 → `applyCode` (`Tickets.BodyRequired`/`BodyTooLong` → `body`, anything else → the form
  error) + refresh; 409 → the draft stays, an inline alert "…send it again" with a "Send again" button. Notes render as
  internal in the thread (Story 18's warning card + badge).
- **D12 — new ticket drawer.** The department is always **sent explicitly**: the selected one when the Department
  select shows (active scope "All my departments" and more than one active department), otherwise the active
  department or the only active department — so the category list always matches the ticket's department. Contact:
  "No contact" or one of the customer's contacts, default the primary. A customer is required client-side (the field
  error "Choose a customer first"; no request is sent without one). Code → field map in `app/utils/tickets.ts`.
- **D13 — inbox customer column** shows `localizedName` + `<bdi dir="ltr">` number when `customer` is present, else
  "Customer outside your scope" (muted). The **customer filter** is the `CustomerPicker` (active customers only); a
  `customer` id from the URL is labelled by `GET /api/Agent/Customers/{id}`; if that fails the picker stays empty but
  the filter still applies and "Clear filters" removes it.
- **D14 — customer 360 Tickets panel** lists the newest 10 tickets of the customer (`queue=all`, every status,
  `sort=createdAt`, `descending=true`, `pageSize=10`) and, when there are more, "View all N tickets" →
  `/agent/tickets?view=all&customer=<id>`. Empty → "New ticket" (with `tickets.write`) opening the create drawer with
  the customer preselected. The panel renders only with `tickets.read`.
- **D15 — thread additions.** `TicketThread` gets `version` (a change loads the entries after the last loaded
  sequence, up to 5 pages of 50; beyond that "Show more" stays) and `customerLabel` (the ticket's contact name, else the
  customer name, replaces "Customer" on inbound messages). Identifiers (ticket and customer numbers, email, phone) are
  in `<bdi dir="ltr">`; dates through `useFormat()`.
- **Error codes** — no new backend code. New frontend keys: `errors.code.Organisation.DepartmentNotFound` (returned by
  transfer to an unknown department, Story 15). Reused: every `errors.code.Tickets.*` key, `Organisation.OutOfScope`,
  `Organisation.DepartmentRequired`, `Customers.CustomerNotFound`, `Persistence.ConcurrencyConflict`.

---

## Backend Tasks

Copy the neighbouring files' style exactly: file-scoped namespaces, `sealed`, primary constructors, `ArgumentNullException.ThrowIfNull`,
`[.. ]` collection expressions, no `System.DateTime`, no string-literal localisation keys. **All nested Contracts calls in
a query handler are ordinary awaits — there is no `Publish` in any of these handlers**, so Story 15's R2 does not apply.

### B1 — Customers: `GetCustomerNamesAsync`

File: `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/ICustomersModuleApi.cs` — add to the interface
(after line 16) and a record after `CustomerContactMatch`:

```csharp
    /// <summary>Story 20: number and names of each customer the caller may see (customers.read + scope). Unknown and
    /// out-of-scope ids are simply absent - success, never 403. At most 100 ids.</summary>
    Task<Result<IReadOnlyList<CustomerNameSummary>>> GetCustomerNamesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default);

public sealed record CustomerNameSummary(Guid Id, string Number, string DisplayNameAr, string DisplayNameEn);
```

Create file: `backend/src/Modules/Customers/Crm.Modules.Customers.Application/Specifications/CustomersByIdsSpecification.cs`

```csharp
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Specifications;

public sealed class CustomersByIdsSpecification : Specification<Customer>
{
    public CustomersByIdsSpecification(IReadOnlyList<CustomerId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // The whole converted property, never .Value (the CategoriesByIdsSpecification precedent, R3).
        Where(c => ids.Contains(c.Id));
        AsReadOnly();
    }
}
```

Create folder `Crm.Modules.Customers.Application/Queries/GetCustomerNames/` with:

```csharp
// GetCustomerNamesQuery.cs
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Customers.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerNames;

/// <summary>Behind ICustomersModuleApi.GetCustomerNamesAsync (Story 20 inbox). Scoped like every customer read.</summary>
[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record GetCustomerNamesQuery(IReadOnlyList<Guid> CustomerIds) : IRequest<Result<IReadOnlyList<CustomerNameSummary>>>;
```

```csharp
// GetCustomerNamesQueryHandler.cs
public sealed class GetCustomerNamesQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<GetCustomerNamesQuery, Result<IReadOnlyList<CustomerNameSummary>>>
{
    public async Task<Result<IReadOnlyList<CustomerNameSummary>>> Handle(GetCustomerNamesQuery request, CancellationToken cancellationToken)
    {
        if (request.CustomerIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<CustomerNameSummary>>([]);
        }

        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new CustomersByIdsSpecification([.. request.CustomerIds.Distinct().Select(id => new CustomerId(id))]), cancellationToken);

        return Result.Success<IReadOnlyList<CustomerNameSummary>>(
            [.. customers.Select(c => new CustomerNameSummary(c.Id.Value, c.Number.Value, c.DisplayName.Arabic, c.DisplayName.English))]);
    }
}
```

(usings as in `FindContactsByEmailQueryHandler.cs`.)

```csharp
// GetCustomerNamesQueryValidator.cs
public sealed class GetCustomerNamesQueryValidator : AbstractValidator<GetCustomerNamesQuery>
{
    public GetCustomerNamesQueryValidator()
    {
        RuleFor(q => q.CustomerIds).NotNull().Must(ids => ids.Count <= 100);
    }
}
```

File: `…/Customers.Application/CustomersModuleApi.cs` — add (after line 24):

```csharp
    public Task<Result<IReadOnlyList<CustomerNameSummary>>> GetCustomerNamesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default) =>
        sender.Send(new GetCustomerNamesQuery([.. customerIds]), cancellationToken);
```

(`CustomerId` has a public `Guid` constructor — `new CustomerId(id)` is how `GetCustomerByIdQueryHandler` builds it;
confirm the constructor shape in `Customers.Domain/CustomerId.cs` before writing the spec call.)

### B2 — Organisation: department members and the department directory

File: `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/IOrganisationModuleApi.cs` — add after line 16:

```csharp
    /// <summary>Story 20: the non-removed memberships of these departments (caller's tenant). At most 100 department ids.</summary>
    Task<Result<IReadOnlyList<DepartmentMember>>> GetDepartmentMembersAsync(
        IReadOnlyCollection<Guid> departmentIds, CancellationToken cancellationToken = default);

    /// <summary>Story 20: every department of the caller's tenant (transfer targets), ordered by code.</summary>
    Task<Result<IReadOnlyList<DepartmentReference>>> ListDepartmentsAsync(bool activeOnly, CancellationToken cancellationToken = default);
```

and after line 25: `public sealed record DepartmentMember(Guid UserId, Guid DepartmentId, string Role);`

Create file: `…/Organisation.Application/Specifications/MembershipsOfDepartmentsSpecification.cs` — copy
`DepartmentsByIdsSpecification.cs`: `Specification<Membership>`, constructor `(IReadOnlyList<DepartmentId> departmentIds)`,
`Where(m => departmentIds.Contains(m.DepartmentId)); AsReadOnly();`.

Create folder `…/Organisation.Application/Queries/ListDepartmentMembers/`:
- `ListDepartmentMembersQuery.cs` — `/// <summary>In-process only (IOrganisationModuleApi); no HTTP endpoint.</summary>`
  `[RequireAuthenticatedUser] public sealed record ListDepartmentMembersQuery(IReadOnlyList<Guid> DepartmentIds) : IRequest<Result<IReadOnlyList<DepartmentMember>>>;`
- `ListDepartmentMembersQueryHandler.cs` — inject `IRepository<Membership, MembershipId> memberships`; empty input →
  success `[]`; else `ListAsync(new MembershipsOfDepartmentsSpecification([.. request.DepartmentIds.Distinct().Select(id => new DepartmentId(id))]))`
  → `[.. found.Select(m => new DepartmentMember(m.UserId, m.DepartmentId.Value, m.Role.ToString()))]`. (Membership is
  `ITenantScoped` + `ISoftDeletable`: the tenant and soft-delete filters apply automatically.)
- `ListDepartmentMembersQueryValidator.cs` — `RuleFor(q => q.DepartmentIds).NotNull().Must(ids => ids.Count <= 100);`

Create folder `…/Organisation.Application/Queries/ListDepartmentReferences/`:
- `ListDepartmentReferencesQuery.cs` — `[RequireAuthenticatedUser] public sealed record ListDepartmentReferencesQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<DepartmentReference>>>;`
  with the same "In-process only" summary.
- `ListDepartmentReferencesQueryHandler.cs` — inject `IRepository<Department, DepartmentId> departments`;
  `ListAsync(new DepartmentsSpecification(null, request.ActiveOnly))` (already ordered by `Code`) → map each exactly like
  `GetDepartmentReferenceQueryHandler.cs` lines 22–24.

File: `…/Organisation.Application/OrganisationModuleApi.cs` — add the two methods (after line 21), each
`sender.Send(new …Query([.. departmentIds]) / new ListDepartmentReferencesQuery(activeOnly), cancellationToken)`.

### B3 — Tickets: customer names in the list, the agent directory, transfer targets

File: `backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/Dtos.cs`
- Add before `TicketListItemDto`: `/// <summary>The customer a list row belongs to (Story 20). Null on the row when the caller cannot see the customer.</summary>`
  `public sealed record TicketCustomerRefDto(Guid Id, string Number, string NameAr, string NameEn);`
- `TicketListItemDto` (lines 44–47): append a last positional parameter **`TicketCustomerRefDto? Customer`**.
- Add after `TicketDepartmentRefDto`: `/// <summary>An active staff member who can take tickets of the listed departments (Story 20 pickers).</summary>`
  `public sealed record TicketAgentDto(Guid Id, string DisplayName, IReadOnlyList<Guid> DepartmentIds);`

File: `…/Tickets.Application/TicketDtoAssembler.cs`
- Constructor (lines 15–16): add `ICustomersModuleApi customers` (and `using Crm.Modules.Customers.Contracts;`).
- `ToListItemsAsync` (lines 133–156): before the `return`, add

```csharp
        HashSet<Guid> customerIds = [.. rows.Select(r => r.CustomerId)];
        Dictionary<Guid, CustomerNameSummary> customerById = (await CustomerNamesAsync(customerIds, cancellationToken)).ToDictionary(c => c.Id);
```

  and pass as the new last argument
  `customerById.TryGetValue(r.CustomerId, out CustomerNameSummary? c) ? new TicketCustomerRefDto(c.Id, c.Number, c.DisplayNameAr, c.DisplayNameEn) : null`.
- Add next to `LookupAsync` (same shape: empty → `[]`, failure → `[]`, never fail the page — R4):

```csharp
    private async Task<IReadOnlyList<CustomerNameSummary>> CustomerNamesAsync(HashSet<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Result<IReadOnlyList<CustomerNameSummary>> result = await customers.GetCustomerNamesAsync(ids, cancellationToken);
        return result.IsSuccess ? result.Value : [];
    }
```

  (`pageSize` is ≤ 100 — `SearchTicketsQueryValidator` — so the ≤ 100 id limit always holds.)

Create folder `…/Tickets.Application/Queries/ListTicketAgents/`:

```csharp
// ListTicketAgentsQuery.cs
/// <summary>Active staff of one department (must be in the caller's active scope) or of every department in it.
/// Feeds the inbox assignee filter and the ticket's assign picker.</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record ListTicketAgentsQuery(Guid? DepartmentId) : IRequest<Result<IReadOnlyList<TicketAgentDto>>>;
```

```csharp
// ListTicketAgentsQueryHandler.cs
public sealed class ListTicketAgentsQueryHandler(
    ICurrentUserService currentUser, IOrganisationModuleApi organisation, IIdentityModuleApi identity)
    : IRequestHandler<ListTicketAgentsQuery, Result<IReadOnlyList<TicketAgentDto>>>
{
    public async Task<Result<IReadOnlyList<TicketAgentDto>>> Handle(ListTicketAgentsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentId> inScope = currentUser.Scope.Departments;
        List<Guid> departments;
        if (request.DepartmentId is { } requested)
        {
            if (!inScope.Contains(new DepartmentId(requested)))
            {
                return Result.Failure<IReadOnlyList<TicketAgentDto>>(TicketsErrors.DepartmentOutOfScope());
            }

            departments = [requested];
        }
        else
        {
            departments = [.. inScope.Select(d => d.Value)];
        }

        Result<IReadOnlyList<DepartmentMember>> members = await organisation.GetDepartmentMembersAsync(departments, cancellationToken);
        if (members.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TicketAgentDto>>(members.Error);
        }

        Dictionary<Guid, List<Guid>> departmentsByUser = members.Value
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.DepartmentId).Distinct().ToList());

        // GetStaffUsersQueryValidator rejects more than 200 ids per call.
        Result<IReadOnlyList<StaffUserSummary>> users = await identity.GetStaffUsersAsync([.. departmentsByUser.Keys.Take(200)], cancellationToken);
        if (users.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TicketAgentDto>>(users.Error);
        }

        return Result.Success<IReadOnlyList<TicketAgentDto>>(
        [
            .. users.Value
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName, StringComparer.CurrentCulture)
                .Select(u => new TicketAgentDto(u.Id, u.DisplayName, departmentsByUser[u.Id])),
        ]);
    }
}
```

(usings: `Crm.BuildingBlocks.Application.Abstractions`, `Crm.Modules.Identity.Contracts`, `Crm.Modules.Organisation.Contracts`,
`Crm.SharedKernel.Results`, `Crm.SharedKernel.Scoping`, `MediatR`. No validator: `Guid?` needs none — the
`GetTicketQueueCountsQuery` precedent has none.)

Create folder `…/Tickets.Application/Queries/ListTransferTargets/`:
- `ListTransferTargetsQuery.cs` — `/// <summary>Every active department of the tenant - where a ticket can be transferred (tickets.assign).</summary>`
  `[RequirePermission(CrmPermissions.TicketsAssign)] public sealed record ListTransferTargetsQuery : IRequest<Result<IReadOnlyList<TicketDepartmentRefDto>>>;`
- `ListTransferTargetsQueryHandler.cs` — inject `IOrganisationModuleApi organisation`;
  `ListDepartmentsAsync(activeOnly: true)`; failure → failure; success →
  `[.. result.Value.Select(d => new TicketDepartmentRefDto(d.Id, d.Code, d.NameAr, d.NameEn))]`.

File: `…/Tickets.Presentation/Controllers/Agent/TicketsController.cs` — after `Queues` (line 50) add (usings for the two
query namespaces):

```csharp
    /// <summary>Active staff of the department (in your scope), or of every department in your scope. Pickers only.</summary>
    [HttpGet("agents")]
    [ProducesResponseType<IReadOnlyList<TicketAgentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketAgentDto>>> Agents([FromQuery] Guid? departmentId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListTicketAgentsQuery(departmentId), cancellationToken));

    /// <summary>Every active department of the tenant a ticket can be transferred to (tickets.assign).</summary>
    [HttpGet("transfer-targets")]
    [ProducesResponseType<IReadOnlyList<TicketDepartmentRefDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketDepartmentRefDto>>> TransferTargets(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListTransferTargetsQuery(), cancellationToken));
```

Update the class summary (line 24) to "(Stories 15, 18 and 20)".

### B4 — Request samples and documentation

Create file: `backend/requests/ticket-workspace.http` (**Write tool** — Arabic names in comments). Header copied from
`tickets.http` lines 1–11 (host, run instructions, token lifetime) with a Story 20 description; variables `@ryCare`,
`@ryBill`, `@jeCare`, `@unknown` and the agent A / agent B / supervisor logins from `tickets.http` lines 13–71. Then:

| # | Request | Expected |
|---|---|---|
| 1 | Agent A `GET /api/Agent/Tickets?queue=all` | 200; each RYD-CARE row has `customer` `{ number: "C-00000n", nameAr, nameEn }` (T-000001 → Ahmed, T-000002 → Najd) |
| 2 | Agent B `GET /api/Agent/Tickets?queue=all` | 200; T-000003 has `customer` Red Sea; a ticket transferred by `tickets.http` 52 (RYD customer) has `customer: null` (R4) |
| 3 | Agent A `GET /api/Agent/Tickets?queue=mine&status=Open&status=PendingCustomer` | 200; only those statuses (R6) |
| 4 | Agent A `GET /api/Agent/Tickets/agents` | 200; the active members of RYD-CARE: "Omar Haddad", "سارة القحطاني", "System Administrator", each with `departmentIds` |
| 5 | Agent A `GET /api/Agent/Tickets/agents?departmentId={{ryCare}}` | 200; same list |
| 6 | Agent A `GET /api/Agent/Tickets/agents?departmentId={{jeCare}}` | 403 `Organisation.OutOfScope` |
| 7 | Supervisor (scope all) `GET /api/Agent/Tickets/agents` | 200; RYD-CARE + RYD-BILL members, the supervisor's `departmentIds` holds both |
| 8 | Supervisor `GET /api/Agent/Tickets/transfer-targets` | 200; RYD-BILL, RYD-CARE, JED-CARE (+ RYD-TKT if `tickets.http` 55 ran), ordered by code, active only |
| 9 | Agent A `GET /api/Agent/Tickets/transfer-targets` | 403 naming `tickets.assign` |
| 10 | No token `GET /api/Agent/Tickets/agents` | 401 |
| 11 | Agent A `GET /api/Agent/Tickets?customerId=11000000-0000-0000-0000-000000000001&queue=all&pageSize=10` | 200; Ahmed's tickets only (the customer 360 panel request) |

Each request is `### N. <title> - <expected>` like `tickets.http`.

File: `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md` line 10 — append: "Story 20: the pickers
`GET …/Tickets/agents` (`tickets.read`) and `GET …/Tickets/transfer-targets` (`tickets.assign`) on `TicketsController`."

---

## Frontend Tasks

All paths are under `frontend/`. `<script setup lang="ts">`; no `any`, no `!`, no ts-ignore, no `as` cast except
`as const`; logical utilities only; token utilities only; every visible string a key; plain `hover:`/`focus-visible:`
outside `app/components/ui/`. `noUncheckedIndexedAccess` is on: index reads are `T | undefined`. Composables under
`app/composables/tickets/` are **imported explicitly** (Nuxt auto-imports only the top level). Components under
`app/components/ticket/` auto-register as `Ticket…` (the file names already start with `Ticket`, like `TicketThread`).

### F0 — Regenerate the API types

With B1–B3 running in Development: R1, then `pnpm --dir frontend gen:api`. Expected diff: only
`frontend/openapi/agent.json` and `app/api/generated/agent.ts` (R5). Commit both. No hand-written DTO type.

### F1 — Shared helpers

#### F1a — server-worded refusals (D7)

File: `app/api/problem-message.ts` — above `problemMessage` add

```ts
/** Codes whose server detail is already in the request language (Accept-Language) and names what a static key cannot:
 * the agent who won a claim, the ticket's current status (Story 15 TicketRefusals). */
const serverWordedCodes: ReadonlySet<string> = new Set(['Tickets.AlreadyClaimed', 'Tickets.TransitionNotAllowed'])
```

and make `description` (line 14) prefer the detail for them:

```ts
    description:
      problem.code !== null && serverWordedCodes.has(problem.code) && problem.detail
        ? problem.detail
        : codeKey !== '' && i18n.te(codeKey) ? i18n.t(codeKey) : problem.detail,
```

(Prettier may reformat the nested ternary; keep the logic.)

#### F1b — ticket utilities

Create file: `app/utils/tickets.ts`

```ts
import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import { isGuid } from '~/utils/customers'
import { Permissions, type Permission } from '~/utils/permissions'

export const ticketViews = ['mine', 'unassigned', 'all'] as const
export type TicketView = (typeof ticketViews)[number]
export const ticketStatuses = ['New', 'Open', 'PendingCustomer', 'OnHold', 'Resolved', 'Closed', 'Cancelled'] as const
export type TicketStatusName = (typeof ticketStatuses)[number]
export const ticketPriorities = ['Low', 'Normal', 'High', 'Urgent'] as const
export type TicketPriorityName = (typeof ticketPriorities)[number]
export const ticketSorts = ['createdAt', 'modifiedAt', 'priority', 'number'] as const
export type TicketSortKey = (typeof ticketSorts)[number]
export const ticketsPageSize = 20

export interface TicketFilters {
  view: TicketView
  q: string
  status: TicketStatusName | ''
  priority: TicketPriorityName | ''
  categoryId: string
  assigneeId: string
  departmentId: string
  branchId: string
  customerId: string
  sort: TicketSortKey
  descending: boolean
  page: number
}

function oneOf<T extends string>(values: ReadonlyArray<T>, value: string): T | undefined {
  return values.find((candidate) => candidate === value)
}
function first(value: LocationQuery[string] | undefined): string {
  const item = Array.isArray(value) ? value[0] : value
  return typeof item === 'string' ? item.trim() : ''
}
function guid(value: LocationQuery[string] | undefined): string {
  const text = first(value)
  return isGuid(text) ? text : ''
}

export function parseTicketFilters(query: LocationQuery): TicketFilters {
  const page = Number.parseInt(first(query.page), 10)
  return {
    view: oneOf(ticketViews, first(query.view)) ?? 'mine',
    q: first(query.q).slice(0, 200),
    status: oneOf(ticketStatuses, first(query.status)) ?? '',
    priority: oneOf(ticketPriorities, first(query.priority)) ?? '',
    categoryId: guid(query.category),
    assigneeId: guid(query.assignee),
    departmentId: guid(query.department),
    branchId: guid(query.branch),
    customerId: guid(query.customer),
    sort: oneOf(ticketSorts, first(query.sort)) ?? 'createdAt',
    descending: first(query.dir) !== 'asc',
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

/** Only non-default values reach the URL (D2). */
export function ticketFiltersToQuery(f: TicketFilters): LocationQueryRaw {
  const query: LocationQueryRaw = {}
  if (f.view !== 'mine') query.view = f.view
  if (f.q !== '') query.q = f.q
  if (f.status !== '') query.status = f.status
  if (f.priority !== '') query.priority = f.priority
  if (f.categoryId !== '') query.category = f.categoryId
  if (f.assigneeId !== '') query.assignee = f.assigneeId
  if (f.departmentId !== '') query.department = f.departmentId
  if (f.branchId !== '') query.branch = f.branchId
  if (f.customerId !== '') query.customer = f.customerId
  if (f.sort !== 'createdAt') query.sort = f.sort
  if (!f.descending) query.dir = 'asc'
  if (f.page !== 1) query.page = String(f.page)
  return query
}

/** The seven actions of the status action bar (D5), in display order. */
export const ticketStatusActions = ['Open', 'AwaitCustomer', 'PutOnHold', 'Resolve', 'Close', 'Reopen', 'Cancel'] as const
export type TicketStatusAction = (typeof ticketStatusActions)[number]

const permissionFor: Readonly<Record<string, Permission>> = {
  Open: Permissions.TicketsWrite, AwaitCustomer: Permissions.TicketsWrite, PutOnHold: Permissions.TicketsWrite,
  Resolve: Permissions.TicketsWrite, Close: Permissions.TicketsWrite, Reopen: Permissions.TicketsWrite,
  Cancel: Permissions.TicketsWrite, Claim: Permissions.TicketsWrite, ChangePriority: Permissions.TicketsWrite,
  ChangeCategory: Permissions.TicketsWrite, Reply: Permissions.TicketsWrite, AddNote: Permissions.TicketsWrite,
  Assign: Permissions.TicketsAssign, Unassign: Permissions.TicketsAssign, Transfer: Permissions.TicketsAssign,
}

/** allowedActions (the aggregate's table) intersected with the caller's permissions. Unknown names are dropped. */
export function offeredActions(
  allowed: ReadonlyArray<string> | null | undefined,
  can: (permission: Permission) => boolean,
): ReadonlySet<string> {
  const offered = new Set<string>()
  for (const action of allowed ?? []) {
    const permission = permissionFor[action]
    if (permission !== undefined && can(permission)) offered.add(action)
  }
  return offered
}

export type BadgeTone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info'

export function ticketStatusTone(status: string | null | undefined): BadgeTone {
  switch (status) {
    case 'New': return 'info'
    case 'Open': return 'primary'
    case 'PendingCustomer': return 'warning'
    case 'Resolved': return 'success'
    default: return 'neutral' // OnHold, Closed, Cancelled
  }
}

export function ticketPriorityTone(priority: string | null | undefined): BadgeTone {
  switch (priority) {
    case 'Urgent': return 'danger'
    case 'High': return 'warning'
    case 'Normal': return 'info'
    default: return 'neutral' // Low
  }
}

/** code -> field for the new ticket drawer (useFormErrors().applyCode). */
export const createTicketFieldForCode: Readonly<Record<string, string>> = {
  'Customers.CustomerNotFound': 'customerId',
  'Organisation.OutOfScope': 'customerId',
  'Tickets.CustomerRequired': 'customerId',
  'Tickets.CustomerInactive': 'customerId',
  'Tickets.ContactNotOfCustomer': 'contactId',
  'Organisation.DepartmentRequired': 'departmentId',
  'Tickets.CategoryNotFound': 'categoryId',
  'Tickets.CategoryInactive': 'categoryId',
  'Tickets.CategoryDepartmentMismatch': 'categoryId',
  'Tickets.SubjectRequired': 'subject',
  'Tickets.SubjectTooLong': 'subject',
  'Tickets.DescriptionRequired': 'description',
  'Tickets.DescriptionTooLong': 'description',
  'Tickets.PriorityUnsupported': 'priority',
}

export const bodyFieldForCode: Readonly<Record<string, string>> = {
  'Tickets.BodyRequired': 'body',
  'Tickets.BodyTooLong': 'body',
}
```

(`Badge.vue`'s `tone` union is the same six names; if the typecheck wants the component's own type, keep this union —
it is structurally identical.)

### F2 — Composables (`app/composables/tickets/`, imported explicitly)

Every read throws the problem inside `useAsyncData` (`useTicket.ts` shape); every command returns `ApiResult` with
`{ toastOnError: false }` (the workspace decides, D6). Types only from `~/api/generated/agent`.

Create file: `types.ts`

```ts
import type { components } from '~/api/generated/agent'
export type TicketDto = components['schemas']['TicketDto']
export type TicketListItemDto = components['schemas']['TicketListItemDto']
export type TicketQueueCountsDto = components['schemas']['TicketQueueCountsDto']
export type TicketAgentDto = components['schemas']['TicketAgentDto']
export type TicketDepartmentRefDto = components['schemas']['TicketDepartmentRefDto']
export type AgentTicketCategoryDto = components['schemas']['TicketCategoryDto']
export type CreateTicketRequest = components['schemas']['CreateTicketRequest']
```

File: `useTicket.ts` — change `export type TicketDto = …` into `export type { TicketDto } from './types'` so existing
imports keep working.

Create file: `useTicketSearch.ts` — copy `useCustomerSearch.ts` with `parseTicketFilters`/`ticketFiltersToQuery`, key
`'agent-tickets'`, and the query

```ts
query: {
  queue: f.view,
  search: f.q === '' ? undefined : f.q,
  status: f.status === '' ? undefined : [f.status],
  priority: f.priority === '' ? undefined : [f.priority],
  categoryId: f.categoryId === '' ? undefined : f.categoryId,
  assigneeId: f.view === 'all' && f.assigneeId !== '' ? f.assigneeId : undefined,   // D3
  departmentId: f.departmentId === '' ? undefined : f.departmentId,
  branchId: f.branchId === '' ? undefined : f.branchId,
  customerId: f.customerId === '' ? undefined : f.customerId,
  sort: f.sort,
  descending: f.descending,
  page: f.page,
  pageSize: ticketsPageSize,
},
```

`update(patch, mode)` identical to the customers one (every key except `page` resets the page).

Create file: `useTicketQueues.ts` — `useAsyncData('agent-ticket-queues', …GET '/api/Agent/Tickets/queues'…)`.

Create file: `useTicketAgents.ts`

```ts
/** GET /api/Agent/Tickets/agents. key: one per consumer ('agent-ticket-agents-inbox', `agent-ticket-agents-${ticketId}`).
 * enabled=false skips the request (the caller may not use the list) and returns an empty list. */
export function useTicketAgents(key: string, departmentId: ComputedRef<string | undefined>, enabled: ComputedRef<boolean>) {
  const { clients, call } = useApi()
  return useAsyncData(key, async (_nuxtApp, { signal }) => {
    if (!enabled.value) return []
    const result = await call(
      clients.agent.GET('/api/Agent/Tickets/agents', { params: { query: { departmentId: departmentId.value } }, signal }),
      { toastOnError: false },
    )
    if (!result.ok) throw result.problem
    return result.data
  }, { watch: [departmentId, enabled] })
}
```

Create file: `useTicketCategoryOptions.ts` — same shape over `GET /api/Agent/TicketCategories` with
`query: { departmentId }` (key per consumer: `'agent-ticket-categories-inbox'`, `` `agent-ticket-categories-${ticketId}` ``).

Create file: `useTransferTargets.ts` — same shape over `GET /api/Agent/Tickets/transfer-targets`, key
`` `agent-ticket-transfer-targets-${ticketId}` ``, with `enabled` only (no department).

Create file: `useTicketCommands.ts`

```ts
import type { CreateTicketRequest } from './types'

export function useTicketCommands() {
  const { clients, call } = useApi()
  const agent = clients.agent
  const quiet = { toastOnError: false }
  return {
    create: (body: CreateTicketRequest) => call(agent.POST('/api/Agent/Tickets', { body }), quiet),
    transition: (id: string, action: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/transitions', { params: { path: { id } }, body: { action, rowVersion } }), quiet),
    claim: (id: string) => call(agent.POST('/api/Agent/Tickets/{id}/claim', { params: { path: { id } } }), quiet),
    assign: (id: string, agentId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/assign', { params: { path: { id } }, body: { agentId, rowVersion } }), quiet),
    unassign: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/unassign', { params: { path: { id } }, body: { rowVersion } }), quiet),
    transfer: (id: string, departmentId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/transfer', { params: { path: { id } }, body: { departmentId, rowVersion } }), quiet),
    changePriority: (id: string, priority: string, rowVersion: string) =>
      call(agent.PUT('/api/Agent/Tickets/{id}/priority', { params: { path: { id } }, body: { priority, rowVersion } }), quiet),
    changeCategory: (id: string, categoryId: string, rowVersion: string) =>
      call(agent.PUT('/api/Agent/Tickets/{id}/category', { params: { path: { id } }, body: { categoryId, rowVersion } }), quiet),
  }
}
```

(If the claim overload demands a `body`, the generated type decides — no cast.)

Create file: `useTicketWorkspace.ts` (D6)

```ts
import type { InjectionKey } from 'vue'
import type { ApiResult } from '~/composables/useApi'
import { problemMessage } from '~/api/problem-message'
import { ProblemTypes } from '~/api/problem'
import { offeredActions } from '~/utils/tickets'
import { useTicket } from './useTicket'
import type { TicketDto } from './types'

export function useTicketWorkspace(id: string, valid: boolean) {
  const { $i18n } = useNuxtApp()
  const translate = { t: (key: string) => $i18n.t(key), te: (key: string) => $i18n.te(key) }
  const api = useApi()
  const toasts = useToasts()
  const { can } = useCan()
  const { data: ticket, status, error, refresh } = useTicket(id).load(valid)

  const version = ref(0)                    // TicketThread loads newer entries when it changes (D15)
  const busy = ref<string | null>(null)     // the action in flight; every action button is disabled meanwhile
  const actionError = ref<string | null>(null)
  const customerLabel = ref<string | null>(null)
  const offered = computed(() => offeredActions(ticket.value?.allowedActions, can))

  function publish(dto: TicketDto): void {
    ticket.value = dto                      // R7: the next command sends dto.rowVersion
    version.value += 1
  }

  async function run(action: string, request: () => Promise<ApiResult<TicketDto>>, successKey = 'tickets.actions.done'): Promise<TicketDto | null> {
    if (busy.value !== null) return null
    busy.value = action
    actionError.value = null
    const result = await request()
    busy.value = null
    if (result.ok) {
      publish(result.data)
      toasts.push({ tone: 'success', title: $i18n.t(successKey) })
      return result.data
    }
    const problem = result.problem
    if (problem.code === 'Persistence.ConcurrencyConflict') {
      api.toast(problem)
    } else if ([ProblemTypes.Conflict, ProblemTypes.DomainRule, ProblemTypes.Validation, ProblemTypes.NotFound].some((type) => type === problem.type)) {
      actionError.value = problemMessage(problem, translate).description ?? problem.title
    } else {
      api.toast(problem)
      return null
    }
    await refresh()
    version.value += 1
    return null
  }

  return { id, ticket, status, error, refresh, version, busy, actionError, customerLabel, offered, publish, run }
}

export type TicketWorkspace = ReturnType<typeof useTicketWorkspace>
export const ticketWorkspaceKey: InjectionKey<TicketWorkspace> = Symbol('ticket-workspace')

/** For the workspace's child components. The page provides it; a missing provider is a programming error. */
export function useTicketWorkspaceContext(): TicketWorkspace {
  const workspace = inject(ticketWorkspaceKey, null)
  if (workspace === null) throw new Error('useTicketWorkspaceContext() outside the ticket page')
  return workspace
}
```

(`ProblemTypes.NotFound` covers transfer to an unknown department — `Organisation.DepartmentNotFound`.)

Create file: `useCustomerTickets.ts` (D14) — `useAsyncData(\`agent-customer-tickets-${customerId}\`, …GET '/api/Agent/Tickets'`
`{ query: { customerId, queue: 'all', sort: 'createdAt', descending: true, page: 1, pageSize: 10 } }…)`, throwing the problem.

File: `app/composables/customers/useCustomerLookup.ts` — add and return `get`, and export a mapper:

```ts
import type { CustomerDto, CustomerLookupDto } from './types'

/** A CustomerDto as the picker's model (Story 20: preselected customer, URL-restored filter). */
export function customerLookupOf(customer: CustomerDto): CustomerLookupDto {
  const primary = (customer.contacts ?? []).find((contact) => contact.isPrimary)
  return {
    id: customer.id, number: customer.number, displayName: customer.displayName,
    primaryEmail: primary?.email ?? null, primaryPhone: primary?.phone ?? null,
  }
}

// inside useCustomerLookup():
function get(id: string, signal?: AbortSignal): Promise<ApiResult<CustomerDto>> {
  return call(clients.agent.GET('/api/Agent/Customers/{id}', { params: { path: { id } }, signal }), { toastOnError: false })
}
return { search, get }
```

### F3 — Copy

**Write tool.** `pnpm lint` runs `check-locales.ts` (same keys both sides, one top-level key per file).

#### F3a — `tickets.json` (both files; keep every existing key)

Add `"tickets"` under the existing `tickets.nav` (`"tickets": "Tickets"` / `"التذاكر"`), then these objects:

| Key | en | ar |
|---|---|---|
| `inbox.title` | Tickets | التذاكر |
| `inbox.caption` | Tickets | التذاكر |
| `inbox.create` | New ticket | تذكرة جديدة |
| `inbox.search` | Search | بحث |
| `inbox.searchHint` | Ticket number or subject | رقم التذكرة أو الموضوع |
| `inbox.results` | {count} tickets | عدد التذاكر: {count} |
| `inbox.loadError` | The tickets could not be loaded | تعذّر تحميل التذاكر |
| `inbox.retry` | Try again | حاول مرة أخرى |
| `inbox.clearFilters` | Clear filters | مسح المرشحات |
| `inbox.open` | Open ticket {number} | فتح التذكرة {number} |
| `inbox.empty` | No tickets match these filters | لا توجد تذاكر مطابقة لهذه المرشحات |
| `inbox.emptyDescription` | Try another search or clear the filters. | جرّب بحثًا آخر أو امسح المرشحات. |
| `inbox.emptyMine` | No tickets assigned to you | لا توجد تذاكر مسندة إليك |
| `inbox.emptyMineDescription` | Pick one from the unassigned tickets. | اختر تذكرة من التذاكر غير المسندة. |
| `inbox.showUnassigned` | Show unassigned tickets | عرض التذاكر غير المسندة |
| `inbox.emptyUnassigned` | No unassigned tickets | لا توجد تذاكر غير مسندة |
| `inbox.emptyUnassignedDescription` | Every open ticket has an owner. | لكل تذكرة مفتوحة مسؤول. |
| `inbox.emptyAll` | No tickets yet | لا توجد تذاكر بعد |
| `inbox.emptyAllDescription` | Create the first ticket to start. | أنشئ أول تذكرة للبدء. |
| `inbox.unknownCustomer` | Customer outside your scope | عميل خارج نطاقك |
| `inbox.unassigned` | Unassigned | غير مسندة |
| `views.label` | Views | طرق العرض |
| `views.mine` | My tickets | تذاكري |
| `views.unassigned` | Unassigned | غير المسندة |
| `views.all` | All in scope | كل التذاكر في نطاقي |
| `filters.label` | Filters | المرشحات |
| `filters.toggle` | Filters ({count}) | المرشحات ({count}) |
| `filters.status` | Status | الحالة |
| `filters.statusWorking` | Open work | العمل المفتوح |
| `filters.statusAny` | Any status | أي حالة |
| `filters.priority` | Priority | الأولوية |
| `filters.category` | Category | التصنيف |
| `filters.assignee` | Assignee | المسؤول |
| `filters.department` | Department | القسم |
| `filters.branch` | Branch | الفرع |
| `filters.customer` | Customer | العميل |
| `filters.all` | All | الكل |
| `filters.anyone` | Anyone | أي شخص |
| `filters.me` | Me | أنا |
| `filters.sort` | Sort by | الترتيب حسب |
| `sort.newest` | Newest first | الأحدث أولًا |
| `sort.oldest` | Oldest first | الأقدم أولًا |
| `sort.updated` | Recently updated | آخر تحديث أولًا |
| `sort.priority` | Highest priority first | الأعلى أولوية أولًا |
| `sort.number` | Ticket number | رقم التذكرة |
| `columns.number` | Number | الرقم |
| `columns.subject` | Subject | الموضوع |
| `columns.status` | Status | الحالة |
| `columns.priority` | Priority | الأولوية |
| `columns.assignee` | Assignee | المسؤول |
| `columns.modifiedAt` | Updated | آخر تحديث |
| `columns.createdAt` | Created | تاريخ الإنشاء |
| `actions.label` | Ticket actions | إجراءات التذكرة |
| `actions.Open` | Open | فتح |
| `actions.AwaitCustomer` | Wait for customer | انتظار العميل |
| `actions.PutOnHold` | Put on hold | تعليق |
| `actions.Resolve` | Resolve | حل |
| `actions.Close` | Close | إغلاق |
| `actions.Reopen` | Reopen | إعادة فتح |
| `actions.Cancel` | Cancel ticket | إلغاء التذكرة |
| `actions.Claim` | Assign to me | إسناد إليّ |
| `actions.Assign` | Assign | إسناد |
| `actions.Unassign` | Unassign | إلغاء الإسناد |
| `actions.Transfer` | Transfer | نقل |
| `actions.done` | Ticket updated | تم تحديث التذكرة |
| `actions.dismiss` | Dismiss | إخفاء |
| `confirm.cancelTitle` | Cancel this ticket? | هل تريد إلغاء هذه التذكرة؟ |
| `confirm.cancelDescription` | A cancelled ticket cannot be reopened. | لا يمكن إعادة فتح التذكرة الملغاة. |
| `confirm.transferTitle` | Transfer this ticket? | هل تريد نقل هذه التذكرة؟ |
| `confirm.transferDescription` | It moves to {department}. The assignee and the category are cleared, and you may lose access to it. | ستنتقل إلى {department}. سيُلغى الإسناد والتصنيف، وقد تفقد الوصول إليها. |
| `confirm.back` | Back | رجوع |
| `confirm.close` | Close | إغلاق |
| `detail.backToTickets` | Back to tickets | العودة إلى التذاكر |
| `detail.assignedTo` | Assigned to {name} | مسندة إلى {name} |
| `detail.unassigned` | Unassigned | غير مسندة |
| `detail.sections` | Ticket sections | أقسام التذكرة |
| `detail.conversation` | Conversation | المحادثة |
| `detail.details` | Details | التفاصيل |
| `detail.customer` | Customer | العميل |
| `detail.transferred` | Ticket transferred to {department} | تم نقل التذكرة إلى {department} |
| `properties.title` | Details | التفاصيل |
| `properties.status` | Status | الحالة |
| `properties.statusActions` | Change status | تغيير الحالة |
| `properties.noStatusActions` | No status change is available. | لا يتوفر تغيير للحالة. |
| `properties.priority` | Priority | الأولوية |
| `properties.category` | Category | التصنيف |
| `properties.noCategory` | No category | بدون تصنيف |
| `properties.inactiveCategory` | {name} (inactive) | {name} (غير نشط) |
| `properties.assignee` | Assignee | المسؤول |
| `properties.assignTo` | Assign to | إسناد إلى |
| `properties.chooseAgent` | Choose an agent | اختر موظفًا |
| `properties.noAgents` | No other agent in this department | لا يوجد موظف آخر في هذا القسم |
| `properties.department` | Department | القسم |
| `properties.transferTo` | Transfer to | نقل إلى |
| `properties.chooseDepartment` | Choose a department | اختر قسمًا |
| `properties.channel` | Channel | القناة |
| `properties.createdAt` | Created | تاريخ الإنشاء |
| `properties.firstResponse` | First response | أول رد |
| `properties.optionsError` | The options could not be loaded | تعذّر تحميل الخيارات |
| `properties.retry` | Try again | حاول مرة أخرى |
| `properties.readOnly` | You can view this ticket but not change it. | يمكنك عرض هذه التذكرة دون تعديلها. |
| `channel.Agent` | Agent | موظف |
| `channel.Portal` | Portal | البوابة |
| `customerPanel.title` | Customer | العميل |
| `customerPanel.open` | Open customer page | فتح صفحة العميل |
| `customerPanel.contact` | Contact | جهة الاتصال |
| `customerPanel.primaryContact` | Primary contact | جهة الاتصال الرئيسية |
| `customerPanel.noContact` | No contact on this ticket | لا توجد جهة اتصال لهذه التذكرة |
| `customerPanel.loadError` | The customer could not be loaded | تعذّر تحميل العميل |
| `customerPanel.retry` | Try again | حاول مرة أخرى |
| `customerPanel.notFound` | The customer was not found | لم يتم العثور على العميل |
| `customerPanel.notFoundDescription` | It may have been removed. | ربما أُزيل. |
| `composer.label` | Write | كتابة |
| `composer.reply` | Reply | رد |
| `composer.note` | Internal note | ملاحظة داخلية |
| `composer.replyLabel` | Reply to the customer | الرد على العميل |
| `composer.replyHint` | The customer sees this reply. | سيرى العميل هذا الرد. |
| `composer.noteLabel` | Internal note | ملاحظة داخلية |
| `composer.noteHint` | Only staff see internal notes. | لا يرى الملاحظات الداخلية إلا الموظفون. |
| `composer.sendReply` | Send reply | إرسال الرد |
| `composer.addNote` | Add note | إضافة الملاحظة |
| `composer.sent` | Reply sent | تم إرسال الرد |
| `composer.noted` | Note added | تمت إضافة الملاحظة |
| `composer.conflict` | Someone updated the ticket at the same moment. Your text is kept, send it again. | حدّث شخص آخر التذكرة في اللحظة نفسها. نصّك محفوظ، أرسله مرة أخرى. |
| `composer.sendAgain` | Send again | إرسال مرة أخرى |
| `create.title` | New ticket | تذكرة جديدة |
| `create.customer` | Customer | العميل |
| `create.customerHint` | Active customers only | العملاء النشطون فقط |
| `create.chooseCustomerFirst` | Choose a customer first | اختر عميلًا أولًا |
| `create.contact` | Contact | جهة الاتصال |
| `create.noContact` | No contact | بدون جهة اتصال |
| `create.contactsError` | The contacts could not be loaded | تعذّر تحميل جهات الاتصال |
| `create.department` | Department | القسم |
| `create.category` | Category | التصنيف |
| `create.noCategory` | No category | بدون تصنيف |
| `create.categoriesError` | The categories could not be loaded | تعذّر تحميل التصنيفات |
| `create.subject` | Subject | الموضوع |
| `create.description` | Description | الوصف |
| `create.priority` | Priority | الأولوية |
| `create.submit` | Create ticket | إنشاء التذكرة |
| `create.cancel` | Cancel | إلغاء |
| `create.close` | Close | إغلاق |
| `create.created` | Ticket {number} created | تم إنشاء التذكرة {number} |

(`tickets.detail.back`, `notFound`, `notFoundDescription`, `loadError` stay; `{…}` are vue-i18n named parameters.)

#### F3b — `customers.json` and `errors.json`

Files `i18n/locales/{en,ar}/customers.json` — `customers.tickets` becomes:

| Key | en | ar |
|---|---|---|
| `title` | Tickets | التذاكر |
| `empty` | No tickets yet | لا توجد تذاكر بعد |
| `emptyDescription` | Create the first ticket for this customer. | أنشئ أول تذكرة لهذا العميل. |
| `create` | New ticket | تذكرة جديدة |
| `viewAll` | View all {count} tickets | عرض كل التذاكر ({count}) |
| `loadError` | The tickets could not be loaded | تعذّر تحميل التذاكر |
| `retry` | Try again | حاول مرة أخرى |

Files `i18n/locales/{en,ar}/errors.json` — add under `errors.code.Organisation`:
`"DepartmentNotFound": "The department was not found."` / `"لم يتم العثور على القسم."`.

`{count}` is always passed pre-formatted (`format.number(n)`).

### F4 — Inbox `/agent/tickets`

Create file: `app/pages/agent/tickets/index.vue` — `definePageMeta({ layout: 'agent' })`. Copy the structure of
`pages/agent/customers/index.vue` (lines 1–145 template, 147–347 script) with these differences:

- Script: `useTicketSearch()`, `useTicketQueues()`, `canWrite = can(Permissions.TicketsWrite)`,
  `agents = useTicketAgents('agent-ticket-agents-inbox', computed(() => undefined), computed(() => filters.value.view === 'all'))`,
  `categories = useTicketCategoryOptions('agent-ticket-categories-inbox', computed(() => filters.value.departmentId || undefined))`.
  The search box (`searchInput`, `lastSent`, `debounce(sendSearch, 300)`, `searchNow`) is copied verbatim.
- **Header row:** `h1` `tickets.inbox.title`; `UiButton v-if="canWrite"` `tickets.inbox.create` → `createOpen = true`.
- **Views:** `<nav :aria-label="t('tickets.views.label')" class="mt-4 flex flex-wrap gap-2">` with one `NuxtLink` per
  `ticketViews` entry to `{ query: ticketFiltersToQuery({ ...filters, view, page: 1 }) }`, `:aria-current="filters.view === view ? 'page' : undefined"`,
  classes `rounded-full border px-3 py-1 text-sm focus-visible:focus-ring` + active
  `border-primary-600 bg-primary-50 text-primary-700` / inactive `border-border text-fg hover:bg-surface-raised`; label
  `t('tickets.views.<view>')` followed by a `UiBadge size="sm"` with `format.number(counts[view])` when the queue counts
  loaded (`mine` / `unassigned` / `all` of `TicketQueueCountsDto`); no badge while pending or on error.
- **Filters:** a "Filters" toggle `UiButton variant="secondary" class="lg:hidden" :aria-expanded="filtersOpen" aria-controls="ticket-filters"`
  with `t('tickets.filters.toggle', { count: format.number(activeFilterCount) })`; the fieldset
  `id="ticket-filters" :class="filtersOpen ? 'grid' : 'hidden lg:grid'" class="mt-4 gap-3 sm:grid-cols-2 lg:grid-cols-4"`
  (legend `sr-only` `tickets.filters.label`) holds:
  status (`''` → `statusWorking` for mine/unassigned, `statusAny` for all; then `ticketStatuses` → `tickets.status.*`),
  priority (`''` → `filters.all`; `ticketPriorities` → `tickets.priority.*`), category (`''` → `filters.all`; the
  category options, `localizedName(name, locale)`), assignee (**only `v-if="filters.view === 'all'"`**: `''` →
  `filters.anyone`, `auth.me?.id` → `filters.me`, then every agent except me), branch and department (copied from the
  customers page lines 247–281, same branch→department narrowing), customer (`CustomerPicker`
  `v-model="customerModel" :label="t('tickets.filters.customer')"`), and the sort select (`class="lg:hidden"`). Each
  select is a `computed({ get, set })` over `filters` calling `update`, validated with the `oneOf`-style guards like the
  customers page. `activeFilterCount` counts non-empty `q/status/priority/categoryId/assigneeId/departmentId/branchId/customerId`;
  `hasFilters = activeFilterCount > 0`; "Clear filters" pushes `{ query: view === 'mine' ? {} : { view } }`.
- **Customer filter (D13):** `customerModel = ref<CustomerLookupDto | null>(null)`;
  `watch(() => filters.value.customerId, async (id) => { if (id === '') { customerModel.value = null; return } if (customerModel.value?.id === id) return; const result = await lookup.get(id); if (result.ok && filters.value.customerId === id) customerModel.value = customerLookupOf(result.data) }, { immediate: true })`;
  `watch(customerModel, (value) => { const id = value?.id ?? ''; if (id !== filters.value.customerId && (value !== null || filters.value.customerId !== '')) void update({ customerId: id }) })`
  — typing in the picker sets the model to `null`, which clears the filter (the picker's own behaviour, Story 14).
- **Sort (D4):** `tableSort` get → `{ key: filters.sort, descending }`; set → `const starting = ['createdAt','modifiedAt','priority'].includes(value.key) ? true : false`,
  and when the key changed use `descending: starting`, else `value.descending`; `update({ sort, descending })`.
  The select offers `createdAt:desc` newest, `createdAt:asc` oldest, `modifiedAt:desc` updated, `priority:desc`
  priority, `number:asc` number.
- **States:** exactly the customers page order: forbidden → `UiErrorState errors.problem.forbidden`; error →
  `tickets.inbox.loadError` + retry; empty (`status === 'success' && rows.length === 0`) → with filters:
  `inbox.empty`/`emptyDescription` + "Clear filters"; `mine`: `emptyMine`/`emptyMineDescription` + `NuxtLink`-button
  "Show unassigned tickets" (`UiButton variant="secondary" @click="update({ view: 'unassigned' })"`); `unassigned`:
  `emptyUnassigned`/`emptyUnassignedDescription` + "New ticket" (`v-if="canWrite"`); `all`: `emptyAll`/`emptyAllDescription`
  + "New ticket" (`v-if="canWrite"`). Results line `inbox.results` with `format.number(total)` (`aria-live="polite"`).
- **Table (`hidden lg:block`):** `UiTable v-model:sort="tableSort" :caption="t('tickets.inbox.caption')"`, columns
  `number` (sortable), `subject`, `status`, `priority` (sortable), `assignee`, `modifiedAt` (sortable), `createdAt`
  (sortable, `align: 'end'`). Cells: number → `NuxtLink :to="\`/agent/tickets/${row.id}\`"` with
  `<bdi dir="ltr" class="font-mono">`; subject → `NuxtLink` `font-medium text-primary-600 underline break-words` and below
  it `text-xs text-fg-muted` the customer (D13: `localizedName({ ar: row.customer.nameAr, en: row.customer.nameEn }, locale)`
  and `<bdi dir="ltr">{{ row.customer.number }}</bdi>`, else `inbox.unknownCustomer`); status →
  `UiBadge size="sm" :tone="ticketStatusTone(row.status)"` `tickets.status.<status>`; priority → same with
  `ticketPriorityTone`/`tickets.priority.*`; assignee → `row.assignedAgentName` in `<bdi>` or `inbox.unassigned` muted;
  dates → `format.dateTime(row.modifiedAtUtc)` / `format.date(row.createdAtUtc)` (empty when null).
- **Cards (`lg:hidden`):** as the customers cards — pending → three skeleton `li`s; each row a block `NuxtLink`
  (`:aria-label="t('tickets.inbox.open', { number: row.number })"`): line 1 `<bdi dir="ltr">` number + created date
  (`text-xs text-fg-muted`), line 2 subject (`font-medium break-words`), line 3 customer, line 4 status + priority
  badges and the assignee.
- **Pagination:** `UiPagination v-if="total > ticketsPageSize"` exactly as the customers page (lines 129–140).
- **Drawer:** `<TicketCreateDrawer v-model:open="createOpen" @created="(ticket) => navigateTo(\`/agent/tickets/${ticket.id}\`)" />`.

File: `app/layouts/agent.vue` — in `agentNav` insert after the Home item (line 101):
`...(can(Permissions.TicketsRead) ? [{ to: '/agent/tickets', label: t('tickets.nav.tickets') }] : []),`.

### F5 — Thread and composer

#### F5a — `app/components/ticket/TicketComposer.vue` (D11)

No props. `const workspace = useTicketWorkspaceContext()`; `const thread = useTicketThread(workspace.id)`;
`const formErrors = useFormErrors()`; `const api = useApi()`.

- `tabs = computed(() => [ ...(offered.has('Reply') ? [{ id: 'ticket-composer-reply', label: t('tickets.composer.reply') }] : []), ...(offered.has('AddNote') ? [{ id: 'ticket-composer-note', label: t('tickets.composer.note') }] : []) ])`;
  `active = ref<string>()`; `watch(tabs, (list) => { if (!list.some((tab) => tab.id === active.value)) active.value = list[0]?.id }, { immediate: true })`.
- Renders nothing when `tabs.length === 0`. Otherwise `<section :aria-labelledby="headingId" class="rounded-md border border-border bg-surface">`
  with an `sr-only` `h2` `composer.label` and `<UiTabs v-model="active" :tabs="tabs" :label="t('tickets.composer.label')">`;
  `#panel="{ id }"` → a `<form @submit.prevent="submit(id)">` with `UiTextarea v-model="drafts[id]" :rows="4" maxlength="10000"`
  (label/hint `replyLabel`/`replyHint` or `noteLabel`/`noteHint`; `:error="formErrors.errorFor('body')"`), the form
  error (`<p role="alert" class="text-sm text-danger-600">`), the conflict alert (`role="alert"`, warning tokens, text
  `composer.conflict` + `UiButton size="sm" variant="secondary"` `composer.sendAgain` → `submit(id)`), and the submit
  button (`composer.sendReply` / `composer.addNote`, `:loading="sending"`, `:disabled="drafts[id].trim() === ''"`). The note
  tab's form sits on `bg-warning-50` so it is visibly internal before sending.
- `drafts = reactive<Record<string, string>>({ 'ticket-composer-reply': '', 'ticket-composer-note': '' })` (reads with
  `?? ''`).
- `submit(id)`: guard `sending`; `formErrors.clear()`; `conflict.value = false`; `const text = (drafts[id] ?? '').trim()`;
  `result = id === 'ticket-composer-reply' ? await thread.reply(text) : await thread.addNote(text)`.
  `ok` → `workspace.publish(result.data)`, `drafts[id] = ''`, success toast `composer.sent`/`composer.noted`.
  Failure → `problem.code === 'Persistence.ConcurrencyConflict'` → `conflict.value = true` (draft kept);
  `formErrors.apply(problem)` (400); `formErrors.applyCode(problem, bodyFieldForCode)` (422 — then
  `await workspace.refresh()` so the tabs follow the real status); else `api.toast(problem)`.

#### F5b — `app/components/ticket/TicketThread.vue` (D15)

- Props (lines 73–76) become `ticketId: string; version?: number; customerLabel?: string` (`withDefaults`: `version: 0`,
  `customerLabel: undefined`).
- `senderOf` (lines 135–137): inbound → `props.customerLabel ?? t('tickets.thread.customer')`.
- Add, after `loadMore` (line 123):

```ts
const lastSequence = computed(() => entries.value[entries.value.length - 1]?.sequence ?? 0)

/** Appends everything after the last loaded entry (R11) - up to 5 pages; beyond that "Show more" stays. */
async function loadNewer(): Promise<void> {
  if (!data.value) {
    await refresh()
    return
  }
  let after = lastSequence.value
  for (let i = 0; i < 5; i += 1) {
    const result = await thread.page(after)
    if (!result.ok) {
      moreFailed.value = true
      return
    }
    morePages.value = [...morePages.value, result.data]
    const next = result.data.nextCursor
    if (next === null || next === undefined) return
    after = next
  }
}

watch(() => props.version, () => void loadNewer())
```

  Entries stay de-duplicated by `sequence` (lines 96–104), so an overlap never shows twice.
- Remove nothing else; the existing states, the "Show more" button and the note/message/activity rendering stay.

### F6 — Workspace components (`app/components/ticket/`)

Every panel: `<section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">`,
`h2 text-base font-semibold`; loading `UiSkeleton`; error `UiErrorState` with retry; forbidden →
`errors.code.Organisation.OutOfScope`. Every action button is `:disabled="workspace.busy.value !== null"` and shows
`:loading="workspace.busy.value === '<action>'"`. The rowVersion for every command: `ticket.rowVersion ?? ''` read **at
click time** from `workspace.ticket.value`.

#### F6a — `TicketWorkspaceHeader.vue` (D8, D9)

`const workspace = useTicketWorkspaceContext()`, `const commands = useTicketCommands()`.
`<header class="sticky top-0 z-10 -mx-4 border-b border-border bg-surface px-4 py-3">` (R8):
- Line 1 (`flex flex-wrap items-center gap-2`): `<bdi dir="ltr" class="font-mono text-sm text-fg-muted">` number;
  status badge (`ticketStatusTone`); priority badge (`ticketPriorityTone`); assignee text
  (`detail.assignedTo` with `{ name: assignedAgentName }` or `detail.unassigned`).
- `h1 class="mt-1 break-words text-xl font-semibold text-fg"` subject.
- Actions (`role="group" :aria-label="t('tickets.actions.label')" class="mt-2 flex flex-wrap gap-2"`): `Claim` → 
  `workspace.run('Claim', () => commands.claim(id))`; `Resolve` / `Reopen` →
  `workspace.run(action, () => commands.transition(id, action, rowVersion))`. Each only when `offered.has(action)`.
- The alert: `<div v-if="workspace.actionError.value" role="alert" class="mt-2 flex items-start justify-between gap-2 rounded-md border border-danger-500 bg-danger-50 p-2 text-sm text-danger-700">`
  with the text and a ghost `UiButton size="sm"` `actions.dismiss` (clears it).

#### F6b — `TicketPropertiesPanel.vue` (D5, D9, D10)

`workspace`, `commands`, `useCan`, `useScopeStore`, `useI18n`, `useFormat`, `useToasts`. Heading `properties.title`.
When `workspace.offered.value.size === 0` show `properties.readOnly` (`text-sm text-fg-muted`) at the top.

A `<dl class="flex flex-col gap-3">` of:
- **Status** — `tickets.status.*`; below it (`properties.statusActions`, `flex flex-wrap gap-2`) one secondary
  `UiButton` per `ticketStatusActions` entry in `offered` (`tickets.actions.<action>`); none → `properties.noStatusActions`.
  `Cancel` opens a `UiModal` (`confirm.cancelTitle`/`cancelDescription`, buttons `confirm.back` and danger
  `actions.Cancel`); the others run immediately: `workspace.run(action, () => commands.transition(id, action, rowVersion))`.
- **Priority** — `UiSelect` (`ticketPriorities` → `tickets.priority.*`), `:disabled="!offered.has('ChangePriority')"`,
  model = a `computed({ get: () => ticket.priority ?? '', set: (value) => { if (value !== ticket.priority) void workspace.run('ChangePriority', () => commands.changePriority(id, value, rowVersion)) } })`.
- **Category** — `useTicketCategoryOptions(\`agent-ticket-categories-${id}\`, computed(() => ticket?.departmentId))`;
  options: `''` → `properties.noCategory` (disabled, shown only while the ticket has none), each category
  (`localizedName`), and the current category when it is not in the list as a disabled
  `properties.inactiveCategory` option; `:disabled="!offered.has('ChangeCategory')"`; set → `changeCategory` (skip `''`
  and the current id). Options error → a small `properties.optionsError` + retry link under the select.
- **Assignee** — current name or `detail.unassigned`; `Claim` button (secondary) when offered; `Unassign` (ghost) when
  offered → `commands.unassign(id, rowVersion)`; when `offered.has('Assign')`: `useTicketAgents(\`agent-ticket-agents-${id}\`, computed(() => ticket?.departmentId), computed(() => offered.has('Assign')))`,
  a `UiSelect` `properties.assignTo` with placeholder option `''` → `properties.chooseAgent` and every agent except the
  current assignee (none → the select is disabled with hint `properties.noAgents`), and an `actions.Assign` button
  (disabled while `''`) → `commands.assign(id, agentId, rowVersion)`; reset the select after a success.
- **Department** — the name from `scope.myScope?.departments` (by `departmentId`) or from the transfer targets, else `—`;
  when `offered.has('Transfer')`: `useTransferTargets(\`agent-ticket-transfer-targets-${id}\`, computed(() => offered.has('Transfer')))`,
  a `UiSelect` `properties.transferTo` (placeholder `properties.chooseDepartment`; every target except the current
  department; label `` `${localizedName({ ar: nameAr, en: nameEn }, locale)} · ${code}` ``) and an `actions.Transfer`
  button → confirm `UiModal` (`confirm.transferTitle`, `confirm.transferDescription` with `{ department }`, buttons
  `confirm.back` and primary `actions.Transfer`) → `const dto = await workspace.run('Transfer', () => commands.transfer(id, target, rowVersion))`;
  when `dto` is not null and its `departmentId` is not in the caller's effective departments
  (`scope.myScope?.activeDepartmentId ? [activeDepartmentId] : (scope.myScope?.departments ?? []).map((d) => d.departmentId)`)
  → `toasts.push({ tone: 'success', title: t('tickets.detail.transferred', { department }) })` and
  `navigateTo('/agent/tickets')`.
- **Channel** (`tickets.channel.*`), **Created** (`format.dateTime`), **First response** (`format.dateTime`, `—` when null).

#### F6c — `TicketCustomerPanel.vue` (D15)

Props `customerId: string`, `contactId?: string | null`. `const { data, status, error, refresh } = useCustomer(props.customerId).load('profile')`.
Heading `customerPanel.title`. Not found → `UiEmptyState :heading-level="3"` `customerPanel.notFound`/`notFoundDescription`;
forbidden → `UiErrorState` `errors.problem.forbidden` + `errors.code.Organisation.OutOfScope` (no retry); error →
`customerPanel.loadError` + retry. Data: the localized name (`font-medium`), `<bdi dir="ltr">` number, a `NuxtLink`
`customerPanel.open` → `/agent/customers/${customerId}` (`text-sm text-primary-600 underline`); then the contact
(`contactId` match in `data.contacts`, label `customerPanel.contact`; no `contactId` → the primary contact labelled
`customerPanel.primaryContact`; none → `customerPanel.noContact`): name, `mailto:` email and `tel:` phone in
`<bdi dir="ltr">` like `CustomerContactsPanel.vue`. `watch([data, locale], …, { immediate: true })` sets
`workspace.customerLabel.value` to the contact's name, else the localized customer name.

#### F6d — `TicketCreateDrawer.vue` (D12)

Props `customer?: CustomerLookupDto | null`; `v-model:open`; emits `created: [TicketDto]`. `UiDrawer size="lg"`
title `create.title`, close `create.close`. `form = reactive({ customer: null as CustomerLookupDto | null, contactId: '', departmentId: '', categoryId: '', subject: '', description: '', priority: 'Normal' })`
(type the `customer` field with an interface instead of the `as` — no cast). `watch(open, (value) => { if (value) reset() })` —
reset: `form.customer = props.customer ?? null`, everything else cleared, `formErrors.clear()`.

- Fields in order: `CustomerPicker v-model="form.customer" :label="t('tickets.create.customer')" :hint="t('tickets.create.customerHint')" :error="formErrors.errorFor('customerId')" required`;
  contact `UiSelect` (`''` → `create.noContact`, then contacts `name` + ` · ` + email or phone) — loaded by
  `watch(() => form.customer?.id, …)` through `useCustomerLookup().get(id)` (an `AbortController` per change; the
  default is the primary contact; failure → `create.contactsError` under the select and only "No contact");
  department `UiSelect v-if="showDepartment"` (`showDepartment` exactly `CustomerFormDrawer.vue` lines 222–223; options
  via `organisation.scope.option` like that file); category `UiSelect` (`''` → `create.noCategory`; categories of
  `targetDepartmentId`, loaded by a `watch` on it with `GET /api/Agent/TicketCategories?departmentId=` through a direct
  `call` — failure → `create.categoriesError`); `UiInput` subject `maxlength="200" required`; `UiTextarea` description
  `:rows="5" maxlength="4000" required`; priority `UiSelect` (`ticketPriorities`).
- `targetDepartmentId = computed(() => showDepartment.value ? form.departmentId : (scope.myScope?.activeDepartmentId ?? scope.activeDepartments[0]?.departmentId ?? ''))`;
  clear `form.categoryId` when it changes.
- `submit()`: guard `submitting`; `formErrors.clear()`; no customer → `formErrors.fieldErrors.value = { customerId: [t('tickets.create.chooseCustomerFirst')] }`
  and return. Body `{ customerId, contactId: form.contactId || null, subject: form.subject.trim(), description: form.description.trim(), priority: form.priority, categoryId: form.categoryId || null, departmentId: targetDepartmentId.value || null }`
  → `useTicketCommands().create(body)`. `ok` → toast `create.created` with `{ number }`, close, `emit('created', data)`.
  Failure → `if (formErrors.applyCode(problem, createTicketFieldForCode)) return` → `if (formErrors.apply(problem)) return`
  → `useApi().toast(problem)`. `formErrors.formError` renders as `<p role="alert">`.
- Footer: `create.cancel` (close) and primary `create.submit` `:loading="submitting"`; a hidden submit button inside
  the form so Enter submits (the users/customers pattern).

### F7 — Ticket detail page `/agent/tickets/[id]`

Replace file: `app/pages/agent/tickets/[id].vue`

```ts
import { ProblemTypes, problemOf } from '~/api/problem'
import { isGuid } from '~/utils/customers'
import { ticketWorkspaceKey, useTicketWorkspace } from '~/composables/tickets/useTicketWorkspace'

definePageMeta({ layout: 'agent', key: (route) => route.path })
const { t } = useI18n()
const route = useRoute()
const id = String(route.params.id)
const valid = isGuid(id)
const workspace = useTicketWorkspace(id, valid)
provide(ticketWorkspaceKey, workspace)
const { ticket, status, error, refresh, version, customerLabel } = workspace
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)
type Region = 'conversation' | 'details' | 'customer'
const region = ref<Region>('conversation')
const regions = computed(() => [
  { id: 'conversation' as const, label: t('tickets.detail.conversation') },
  { id: 'details' as const, label: t('tickets.detail.details') },
  { id: 'customer' as const, label: t('tickets.detail.customer') },
])
const shown = (value: Region): string => (region.value === value ? '' : 'hidden lg:block')
```

Template:
- `NuxtLink to="/agent/tickets" class="text-sm text-primary-600 underline"` `tickets.detail.backToTickets`.
- `!valid || notFound` → `UiEmptyState` `detail.notFound`/`notFoundDescription` with an action link back to the inbox.
- pending without data → `UiSkeleton :lines="3"` then `UiSkeleton :lines="6"`; forbidden → `UiErrorState`
  `errors.problem.forbidden` + `errors.code.Organisation.OutOfScope`; other error without data → `UiErrorState`
  `detail.loadError` + retry.
- With `ticket`:

```vue
<div class="mt-2 flex flex-col gap-4">
  <TicketWorkspaceHeader />
  <div role="group" :aria-label="t('tickets.detail.sections')" class="flex gap-1 rounded-md border border-border p-1 lg:hidden">
    <button v-for="item in regions" :key="item.id" type="button" :aria-pressed="region === item.id"
      class="flex-1 rounded-sm px-3 py-2 text-sm font-medium focus-visible:focus-ring"
      :class="region === item.id ? 'bg-primary-50 text-primary-700' : 'text-fg-muted hover:bg-surface-raised'"
      @click="region = item.id">{{ item.label }}</button>
  </div>
  <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
    <div class="flex min-w-0 flex-col gap-4" :class="shown('conversation')">
      <TicketThread :ticket-id="id" :version="version" :customer-label="customerLabel ?? undefined" />
      <TicketComposer />
    </div>
    <div class="flex min-w-0 flex-col gap-4">
      <div :class="shown('details')"><TicketPropertiesPanel /></div>
      <div :class="shown('customer')">
        <TicketCustomerPanel :customer-id="ticket.customerId ?? ''" :contact-id="ticket.contactId" />
      </div>
    </div>
  </div>
</div>
```

(`ticket.customerId` is optional in the generated type; render `TicketCustomerPanel` only `v-if="ticket.customerId"`.)
The Story 18 comment at the top of the old template is removed.

### F8 — Customer 360 Tickets panel (D14)

File: `app/components/customer/CustomerTicketsPanel.vue` — keep the `<section>` shell and heading; props
`customerId: string`; `useCustomerTickets(customerId)`; `canWrite = can(TicketsWrite)`;
`profile = useNuxtData<CustomerDto>(customerPanelKey(customerId, 'profile')).data` (read-only — the header panel loads
it; no second fetch) → `preselected = computed(() => profile.value ? customerLookupOf(profile.value) : null)`.
States: loading `UiSkeleton :lines="3"`; forbidden / error `UiErrorState` `customers.tickets.loadError` + retry;
empty → `UiEmptyState :heading-level="3"` `customers.tickets.empty`/`emptyDescription` with `#actions` "New ticket"
(`v-if="canWrite"`) → `createOpen = true`. Data: `<ul class="divide-y divide-border">`, each `li` a `NuxtLink`
(`block py-2 hover:bg-surface-raised focus-visible:focus-ring`) with `<bdi dir="ltr">` number, subject
(`break-words`), status + priority badges, `format.date(createdAtUtc)`; under the list, when `total > 10`, a
`NuxtLink :to="{ path: '/agent/tickets', query: { view: 'all', customer: customerId } }"`
`customers.tickets.viewAll` with `format.number(total)`; next to the heading a "New ticket" `UiButton size="sm"`
(`v-if="canWrite && items.length > 0"`). `<TicketCreateDrawer v-model:open="createOpen" :customer="preselected" @created="(t) => navigateTo(\`/agent/tickets/${t.id}\`)" />`
(name the callback parameter `ticket`, not `t`, to avoid shadowing `t()`).

File: `app/pages/agent/customers/[id].vue` line 17 → `<CustomerTicketsPanel v-if="can(Permissions.TicketsRead)" :customer-id="id" />`
(add `useCan()` and the `Permissions` import to the script).

---

## Edge Cases & Failure Modes

- **Stale actions** (another agent changed the status after this page loaded) → the button is still shown; the handler
  asks the transition table first → 422 `Tickets.TransitionNotAllowed` with the localised detail naming the current
  status (`TicketRefusals.cs` lines 14–24) → inline alert (D6, D7) → refresh → the offered set follows the real status.
- **Claim race** → one 200; the loser gets 409 `Tickets.AlreadyClaimed` naming the winner (pre-check lines 35–38 or the
  race path lines 53–63 of `ClaimTicketCommandHandler.cs`) → inline alert with that detail → refresh shows the winner
  (R9). Claiming a ticket the caller already holds is the same 409 (Story 15 rule) — the button is not offered then.
- **Stale rowVersion** on priority/category/assign/unassign/transfer/transition → 409 `Persistence.ConcurrencyConflict`
  → toast + refresh; the agent retries on fresh data. Replies/notes take no rowVersion; two simultaneous appends can
  still 409 → the composer keeps the draft (D11).
- **Reply on Closed/Cancelled** → the Reply tab is not offered; if the ticket closed meanwhile → 422 → form error +
  refresh → the Reply tab disappears, the draft of the note tab survives.
- **Reply on Resolved** → reopens (Story 18 D6): the returned `TicketDto` updates the header badge and actions; the
  thread shows the message then "reopened" (two entries from one `loadNewer`).
- **Transfer out of scope** → the page leaves to the inbox with a toast (D9); a later visit to the URL shows the
  forbidden state. Transfer clears assignee and category (Story 15) — the panel shows "Unassigned" / "No category".
- **Supervisor with two roles** — permissions follow the active scope (`scope.setActive` reloads `me`): in scope
  RYD-BILL (Agent there) the assign/transfer controls disappear; the API would answer 403 anyway.
- **No `tickets.write`/`tickets.assign`** (no seeded role lacks them, as in Story 14 R7) → verify by editing
  `auth.me.permissions` in Vue DevTools: all write controls and the composer vanish, "You can view this ticket but not
  change it" shows.
- **Customer outside the caller's scope** (transferred ticket) → inbox row "Customer outside your scope" (R4); the
  detail's customer panel shows the forbidden state; the thread's inbound label falls back to "Customer".
- **Lookup failures** (Identity/Organisation/Customers read fails) → search still 200 with names missing
  (`TicketDtoAssembler`); the agents endpoint returns the failure (the picker shows its options error + retry); the
  inbox assignee filter then offers "Anyone"/"Me" only.
- **More than 200 members** in the scope → the agents list names the first 200 (Identity limit); recorded in the handler comment.
- **Hand-edited URL** (`?view=foo&status=bar&page=-2&customer=x`) → `parseTicketFilters` drops them; the API never sees
  them. `?view=mine&assignee=<id>` → the assignee is ignored (D3).
- **Filter ids outside scope** (a pasted department) → empty page (Story 15 rule), shown as "No tickets match".
- **Scope switch** → `refreshNuxtData()` refetches the inbox, queue counts, agents, categories, and the open ticket; a
  ticket now out of scope → forbidden state.
- **Unknown / non-uuid ticket id** → not-found state; a non-uuid never calls the API (`isGuid`).
- **Inactive category on a ticket** → shown as a disabled "(inactive)" option; choosing another category works; the
  inactive one cannot be re-selected (422 `Tickets.CategoryInactive` would follow).
- **Create without customer / subject / description** → client error for the customer; 400 field errors for subject
  and description in the active locale (FluentValidation follows `Accept-Language`); inactive customer → 422
  `Tickets.CustomerInactive` under the customer field; a category of another department cannot be chosen (the list is
  the target department's).
- **Long subjects, Arabic subject in `en` UI** → `break-words`, `min-w-0` on flex/grid children; subject `h1` wraps;
  table cells use `break-words`, not `truncate`, for the subject.
- **Bodies with HTML** → shown as text (Story 18: interpolation only, never `v-html`).
- **Offline** → inbox, counts, thread, panels each show their own error state; commands toast `errors.problem.network`.
- **375 px** → cards, collapsed filters, the three-way switch; drawers use `w-full max-w-[28rem]` (`Drawer.vue` line 8).

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the `CRM0001` rules, the startup guard,
`backend/requests/ticket-workspace.http`, the linter and the browser walk replace the suite. Record each row's result
and any R-fallback in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001` |
| 2 | Risks R1–R11 | as stated in Prerequisites | each proven or its fallback recorded |
| 3 | New endpoints | `ticket-workspace.http` 1–11 | every row's expected status/body |
| 4 | Regression (backend) | `tickets.http` 12, 21, 42, 43, 52–54, 57–64; `ticket-thread.http` 3, 6, 9 | unchanged (list rows gain only `customer`) |
| 5 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0 each |
| 6 | Inbox server-side (AC) | agent A: switch views, sort each sortable column, each filter, search `T-00000`, page with `?page=2` after creating 21+ tickets or by reading the request | every change is a new `/api/Agent/Tickets?…` request; URL mirrors it; reload restores the exact view |
| 7 | Legal actions only (AC) | T-000001 (New) vs a Resolved ticket vs a Closed one | only the table's actions show; Reply tab absent on Closed; the header never shows Reply/AddNote as status buttons |
| 8 | Refusal reason (AC) | R9 second procedure, `en` and `ar` | localised reason inline, ticket refreshed |
| 9 | Claim lost (AC) | R9 first procedure | "already claimed by <name>" in the active language; the ticket shows the winner |
| 10 | Append without reload (AC) | R11 | reply and note appear at the end; note visibly internal |
| 11 | Create (AC) | drawer: submit empty → field errors; pick Ahmed, contact, category, subject, description → create | lands on `/agent/tickets/<new id>`; errors under the fields in the active locale |
| 12 | Customer 360 panel (AC) | Ahmed's page; a new customer with no tickets | list with links (+ "View all" when > 10); empty state with "New ticket" preselecting the customer |
| 13 | Assign / unassign / transfer | supervisor on T-000001: assign agent A, unassign, transfer to JED-CARE (confirm) | toasts; activity entries append; after transfer the page returns to the inbox |
| 14 | Priority / category | agent A on a RYD-CARE ticket | R7; inactive category shown disabled after `tickets.http` 65a |
| 15 | States (AC) | per view: Slow 3G skeletons; empty views (agent B "My tickets"); Offline error + retry | present everywhere, empty states carry an action |
| 16 | Responsive + RTL (AC) | 375 / 768 / 1280 in `en` and `ar`: inbox, detail, drawer, customer page | cards < 1024, table ≥ 1024; switch < 1024, two columns ≥ 1024 mirrored in `ar`; sticky header (R8); no horizontal scroll |
| 17 | Permission hiding | Vue DevTools: remove `tickets.write`, then `tickets.assign` from `auth.me.permissions` | matching controls vanish; read-only note shows |
| 18 | Regression (frontend) | `/agent/customers` list and detail, `/admin/ticket-categories`, `/_dev/ui` Tabs section | unchanged |

---

## Verification Steps

1. **Backend builds:** `dotnet tool restore`; `dotnet build backend/Crm.sln` → Test Plan row 1.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` (Development, :5187, seeded, EF SQL logging) →
   R2, R3, R6 (routing half); run `tickets.http` requests 1–54 once (request 52 transfers `@t2`), then `ticket-workspace.http` 1–11 (R4, rows 3–4).
   `git diff --stat -- backend/src/BuildingBlocks` must show only `AREAS.md`; no new migration folder.
3. **Frontend types:** R1, then `pnpm --dir frontend gen:api` → R5; only `openapi/agent.json` and
   `app/api/generated/agent.ts` change.
4. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` → row 5.
   Then (Git Bash, new files staged with `git add -N` first):

   ```bash
   git diff -- frontend/app | grep -nE '(^|[[:space:]":])-?(ml|mr|pl|pr|left|right|text-left|text-right|border-l|border-r|rounded-l|rounded-r)-'
   ```

   must print nothing (an `inset-x-0` is fine, a `left-2` is not).
5. **Frontend runs:** `pnpm --dir frontend dev`; sign in as `agent.a@crm.local` / `Crm_Dev_2026!`
   (`backend/requests/auth.http`) → R6 (serialisation half), R7, R8, R10, R11 → Test Plan rows 6–12, 14–17; then
   `supervisor@crm.local` for rows 9 and 13, `agent.b@crm.local` for R4 in the UI.
6. **Regression:** rows 4 and 18.

---

## Done Criteria

- [ ] The inbox pages, sorts and filters server-side; the URL restores the exact view on refresh and back/forward.
- [ ] Only legal, permitted actions are offered (`allowedActions` ∩ permissions, `Reply`/`AddNote` never in the status
      bar); an illegal action invoked anyway shows the aggregate's localised reason inline.
- [ ] A claim lost to a colleague shows "already claimed by <name>" and refreshes the ticket.
- [ ] Reply and internal note append to the thread without a full reload; notes are visibly internal.
- [ ] Creating a ticket from the drawer lands on its detail page; validation errors land on the fields.
- [ ] The customer 360 Tickets panel lists that customer's tickets with links.
- [ ] Walked in `en` and `ar` at 375, 768 and 1280 px; the detail layout mirrors in RTL; no physical-direction utility.
- [ ] Every view has a loading skeleton, an empty state with a next action, and an error state with retry.
- [ ] Inbox rows show customer names from `ICustomersModuleApi.GetCustomerNamesAsync`; `ticket-workspace.http`
      exercises `…/agents` and `…/transfer-targets`.
- [ ] `dotnet build backend/Crm.sln` clean with 0 warnings and no `CRM0001`; no migration; no building-block change
      besides `AREAS.md`; `pnpm typecheck` / `lint` / `build` clean; `gen:api` output committed; no new package.
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/ticket-management/00-overview.md` and `.squad/MVP-STORIES.md` list this plan; MVP-STORIES row 12
      marked done by the implementation session.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 47.**
