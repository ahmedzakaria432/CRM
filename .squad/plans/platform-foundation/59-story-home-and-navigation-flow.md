# Story 59 — Home and navigation flow

## Prerequisites

- **Stories 01–07, 11, 14, 15, 18, 20, 47 and 58 are implemented and committed** (latest `e41d7e6` "Add Docker stack
  and root README", after `a013113` "Implement Story 58"). This story was added after the MVP was complete (owner
  feedback 2026-10-01, `.squad/MVP-STORIES.md` row 15). It builds on:
  - Story 15 — [`../ticket-management/15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md`](../ticket-management/15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md):
    `GET /api/Agent/Tickets/queues` (`TicketQueueCountsDto { mine, unassigned, all }`) and `GET /api/Agent/Tickets`
    (`queue`, `sort`, `descending`, `page`, `pageSize` 1–100).
  - Story 20 — [`../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md`](../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md):
    the inbox views (`?view=mine|unassigned|all`, the URL is the state — D2), `useTicketQueues`, `TicketCreateDrawer`.
  - Story 47 — [`../customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md`](../customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md):
    D17 (the SSR server never holds a session) and **R7** (the portal guard runs on the client after hydration; never
    redirect during hydration).
  - Story 58 — [`../integrations-platform/58-story-custom-branding-theming-and-tenant-configuration.md`](../integrations-platform/58-story-custom-branding-theming-and-tenant-configuration.md):
    `plugins/branding.ts` loads public branding server-side and never throws (D10); `ShellBrandMark`.
  - Story 07 — [`../identity-access/07-story-authentication-users-roles-and-permissions.md`](../identity-access/07-story-authentication-users-roles-and-permissions.md):
    `useCan()` and the fixed role map (`CrmPermissions.cs`: Agent = tickets.read/write + customers.read/write;
    Supervisor adds tickets.assign; Administrator = every staff permission incl. `admin.users`, `admin.branding`).
- **Modules:** **`Organisation`, `Identity`, `Customers`, `Tickets` — Development seed data only** (owner addition
  2026-10-01, intake "Demo seed users for every case"). Each module's own `…DevSeed` class (Contracts) and
  `…Seeder` (Infrastructure) changes; nothing else in any module. **No domain, application, controller, DTO or EF
  configuration change, no migration, no `gen:api` change, no new project reference** (the seeders already reference
  exactly the Contracts they need — `IdentitySeeder` lines 1–7, `CustomersSeeder` 1–7, `TicketsSeeder` 1–9). The
  application screens read `Tickets` (queue counts, ticket search), `Identity` (`/api/Agent/Me`, `/api/Portal/Me`) and
  `Organisation` (public branding, `/api/Agent/MyScope`) through endpoints that already exist. Verified during planning:
  `SearchTicketsQuery` (`backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/Queries/SearchTickets/SearchTicketsQuery.cs`
  lines 9–13) already accepts `Queue`, `Sort`, `Descending`, `PageSize` (validator line 12: `InclusiveBetween(1, 100)`),
  so "my newest 5" needs no backend change. One new request file, `backend/requests/demo-accounts.http` (B6).
- **Checked during planning (read from `frontend/package.json`, not run):** `nuxt` 4.5.2, `vue` 3.5.43,
  `@nuxtjs/i18n` 10.6.0, `pinia` 4.0.3, `typescript` 5.9.3, `pnpm` 12.6.0 (`packageManager`). No package is added.

---

## Story Goal

Every entry point leads somewhere obvious:

1. **`/` is a branded, server-rendered landing** (portal layout): the organisation's logo/name and colours (Story 58),
   a short welcome, and two entry cards — **"Staff sign-in"** → `/login`, and **"Customer portal"** → `/portal/login`
   (with a secondary "Create an account" → `/portal/register`). Without branding it shows the product defaults.
2. **A signed-in user never sees the landing:** staff go to `/agent`, a portal customer to `/portal`.
3. **`/agent` is a start page:** greeting with the user's name and active scope; three count cards (My tickets,
   Unassigned, All in scope) linking to `/agent/tickets?view=…`; **My recent tickets** (newest 5 assigned to me) with
   an empty state pointing at Unassigned; quick actions (New ticket, Find customer, and Branding / Users for those
   who hold the permission).
4. **`/portal` greets the signed-in customer** next to the existing "New request" action and requests list (the empty
   state with its action already exists — Story 47).
5. **Navigation is consistent:** the brand mark links to its layout's home; sign-in and sign-out land where listed; the
   staff login links to the customer portal and the portal login links back to `/`.
6. **Every role and scope case has a seeded account** (Development only, shared dev password, idempotent, well-known
   ids): an administrator, a supervisor across two branches, agents in different departments and branches, an agent
   with no tickets, a deactivated user (refused), a portal account admin, a non-admin portal contact, a customer with
   no requests, a revoked portal contact (refused), and tickets spread across statuses, priorities, departments,
   assigned and unassigned. The seed table is in the root `README.md` "Demo accounts" and in `backend/README.md`.

**Not in scope** (intake): SLA widgets, charts, team views, personalisation (story 31), notifications, global search,
onboarding tours, any backend change other than Development seed data, any test suite.

---

## Context — Read These Files First

1. `frontend/app/pages/index.vue` — lines 1–15: the Story 04 placeholder (portal layout, `shell.home.tagline`). Replaced.
2. `frontend/app/pages/agent/index.vue` — lines 1–12: the empty state (`shell.agentHome.*`). Replaced.
3. `frontend/app/middleware/auth.global.ts` — lines 1–36. Line 8 returns early on the server; lines 10–25 the portal
   branch (line 13 skips during hydration — R7); lines 27–35 the staff branch (line 35: `/login` while signed in →
   `/agent`). The `/` redirect goes **before** line 8.
4. `frontend/app/plugins/portalGuard.client.ts` — lines 1–17: the R7 fallback; unchanged, but read it to see why the
   middleware must not redirect while `isHydrating`.
5. `frontend/app/stores/auth.ts` — lines 26–30 `clear()`, 32–38 `resetScope()` (the `nuxtApp.runWithContext` +
   `useCookie` pattern to copy), 49–71 `login()`, 89–108 `restore()`, 110–119 `logout()` (already navigates to
   `/login` — keep).
6. `frontend/app/stores/portalAuth.ts` — lines 28–32 `clear()`, 49–66 `login()`, 102–121 `restore()`, 128–136
   `logout()` (already navigates to `/portal/login` — keep).
7. `frontend/app/pages/login.vue` — lines 43–47 `safeRedirect` (already defaults to `/agent` — keep); lines 1–27 the
   template (add the "Customer portal" link after the form).
8. `frontend/app/pages/portal/login.vue` — lines 48–52 `safeRedirect` (already `/portal` — keep); lines 26–30 the
   "noAccount" link (add the "back home" link next to it).
9. `frontend/app/pages/portal/index.vue` — lines 1–6 the header (title + "New request"), lines 26–35 the existing empty
   state with `portal.tickets.emptyAction`, line 102 the "idle counts as pending" rule (hydration).
10. `frontend/app/layouts/agent.vue` — line 12 (sidebar `ShellBrandMark`), line 39 (header `ShellBrandMark md:hidden`),
    lines 108–122 `agentNav` (the permission-gated nav — the quick actions use the same permission constants).
11. `frontend/app/layouts/portal.vue` — lines 13–16: the brand links to `/portal` only on portal routes, and is
    **unlinked** on `/` and `/login`. Lines 77–84 `isPortalRoute`.
12. `frontend/app/components/shell/BrandMark.vue` — lines 1–14: logo `<img alt=name>` or the name; used inside a link.
13. `frontend/app/composables/tickets/useTicketQueues.ts` — lines 1–9: `useAsyncData('agent-ticket-queues', …)`; reuse
    as is (same key as the inbox, so the home and the inbox show the same numbers).
14. `frontend/app/composables/tickets/useTicketSearch.ts` — lines 19–46: the `GET /api/Agent/Tickets` call shape to
    mirror in the new recent-tickets composable. **Do not reuse `useTicketSearch` itself**: it parses `route.query`
    and shares the `'agent-tickets'` key with the inbox (page size 20).
15. `frontend/app/utils/tickets.ts` — lines 5–8 `ticketViews`, `ticketStatuses`; lines 105–113 `ticketStatusTone`.
16. `frontend/app/pages/agent/tickets/index.vue` — lines 8–24 the view tabs and counts, lines 116–181 the row markup
    (number in `<bdi dir="ltr" class="font-mono">`, customer name via `localizedName`), lines 197 the
    `TicketCreateDrawer` usage (`v-model:open`, `@created` → navigate to the ticket), lines 460–465 `statusLabel`.
17. `frontend/app/components/shell/ScopeSwitcher.vue` — lines 21–38: how the active scope is labelled
    (`organisation.scope.option` / `organisation.scope.all`, `scope.myScope?.activeDepartmentId`).
18. `frontend/app/stores/scope.ts` — lines 86 `activeDepartments`, 88–95 `setActive` (calls `refreshNuxtData()`, so the
    home panels refetch on a scope switch with no extra code).
19. `frontend/app/composables/useCan.ts` and `frontend/app/utils/permissions.ts` lines 9–20 (`Permissions`).
20. `frontend/app/components/ui/EmptyState.vue` (props `title`, `description`, `headingLevel`; slot `actions`),
    `ErrorState.vue` (props `title`, `description`, `retryLabel`; emit `retry`), `Skeleton.vue` (props `variant`,
    `lines`), `Badge.vue` (props `tone`, `size`).
21. `frontend/app/components/ui/Button.vue` — lines 36–60: the primary/secondary classes. **Pages and non-kit
    components use `hover:` and `focus-visible:focus-ring`, never `ui-hover:`/`ui-focus:`** (CLAUDE.md).
22. `frontend/i18n/locales/{en,ar}/shell.json` — `shell.home` and `shell.agentHome` are used only by the two pages being
    replaced (grep during planning: no other reference). Remove them.
23. `backend/src/Modules/Identity/Crm.Modules.Identity.Presentation/RefreshCookie.cs` and `PortalRefreshCookie.cs` —
    the refresh cookies are `HttpOnly` and scoped to `/api/Agent/Auth` / `/api/Portal/Auth`: neither the browser nor
    the Nuxt SSR server can see them on a request to `/`. This is why D2 exists. **Do not change them.**
24. `backend/src/Modules/Identity/Crm.Modules.Identity.Presentation/Controllers/Agent/AuthController.cs` lines 34–40 and
    `Controllers/Portal/AuthController.cs` lines 49–55: **refresh is rate-limited under the sign-in policy**
    (`CrmRateLimitPolicies.SignIn` / `PortalSignIn`). Blind refresh calls from `/` would spend the sign-in budget (D2).

**Seed (backend, B1–B7):**

25. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Modules/IModuleSeeder.cs` lines 3–5: seeders run after
    every migration, in Development only (`Crm.Api/Program.cs` lines 135–137), and "may rely on its own schema only —
    never on another module's seed having run first". Ids into other modules are primitives from their `…DevSeed`.
26. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/DevSeed/OrganisationDevSeed.cs` lines 6–37 —
    the id families: users `06000000-0000-0000-0003-…`, memberships `06000000-0000-0000-0004-…` (last used `…0007`).
27. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Infrastructure/Persistence/OrganisationSeeder.cs` lines
    113–121 (memberships through `EnsureMembershipAsync`, lines 169–180, soft-delete filter lifted — copy the call).
28. `backend/src/Modules/Identity/Crm.Modules.Identity.Contracts/DevSeed/IdentityDevSeed.cs` lines 8–15 (emails; portal
    account ids `47000000-0000-0000-0000-…`, last used `…0001`).
29. `backend/src/Modules/Identity/Crm.Modules.Identity.Infrastructure/Persistence/IdentitySeeder.cs` lines 51–56
    (`EnsureAsync` per user), 62–80 (`EnsurePortalAccountAsync` for Ahmed — generalise), 82–94 (`EnsureAsync`).
30. `backend/src/Modules/Identity/Crm.Modules.Identity.Domain/User.cs` lines 132–141 `Deactivate(DateTimeOffset)` (throws
    if already inactive); `Application/Commands/SignIn/SignInCommandHandler.cs` lines 45–49: an inactive user gets the
    generic 401 (`LogRefused("inactive", …)`).
31. `backend/src/Modules/Identity/Crm.Modules.Identity.Application/Commands/PortalSignIn/PortalSignInCommandHandler.cs`
    lines 69–77 and `backend/src/Modules/Customers/Crm.Modules.Customers.Application/Queries/GetPortalContact/GetPortalContactQueryHandler.cs`:
    a portal account whose **contact has `HasPortalAccess == false`** is refused with the generic 401
    (`portal-access-revoked`). That is what "revoked" means — `PortalAccount` itself has no revoke method; do not add one.
32. `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/DevSeed/CustomersDevSeed.cs` lines 7–21 (customers
    `11000000-0000-0000-0000-…` last `…0003`, contacts `11000000-0000-0000-0001-…` last `…0004`) and
    `Infrastructure/Persistence/CustomersSeeder.cs` lines 45–58 (one `if (!await ExistsAsync(…))` block per customer,
    `AddContact(…, isPrimary, hasPortalAccess, id)`, `SaveAsync`).
33. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Contracts/DevSeed/TicketsDevSeed.cs` lines 7–26 (tickets
    `15000000-0000-0000-0000-…` last `…0003`) and `Infrastructure/Persistence/TicketsSeeder.cs` lines 53–102 (categories
    via `EnsureCategoryAsync`, one block per ticket: `Ticket.Create(…)`, then status/assignment methods, `SaveAsync`).
34. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/Ticket.cs` lines 26–36 (`StatusTransitions`: the legal
    order of `Open`/`AwaitCustomer`/`PutOnHold`/`Resolve`/`Close`/`Cancel`), 59–62 (`Create` signature), 248 `AssignTo`,
    362 `ReplyAsAgent`. A transition out of order throws `DomainException` — in a seeder that stops the API from starting.
35. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/Specifications/PortalTicketsSpecification.cs` line 21: a
    non-admin portal contact sees only tickets whose `ContactId` is theirs; the account admin (primary contact) sees all
    of the customer's tickets.
36. Request files whose comments assume today's seed counts (update the comments only, B7): `backend/requests/customers.http`
    lines 7, 60, 88; `tickets.http` lines 8, 182; `users.http` lines 31, 35; `portal.http` lines 7–11; `ticket-workspace.http`
    line 45. `auth.http` and `organisation.http` are **unaffected** because no existing user, membership, customer or
    ticket changes.
37. `README.md` lines 69–79 ("Demo accounts") and `backend/README.md` lines 315–325 (the seeded-user table).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **D1 — No new endpoint; the backend change is Development seed data only.** Every read exists (Context 13–14, 17).
  The full dashboard (story 31) will add its own endpoints; this story does not pre-build them.
- **D13 — Seed additions are new rows only.** No existing seeded user, membership, customer, contact or ticket is
  changed: an existing seeder skips an aggregate that already exists, so a change to one would never reach an existing
  database, and `auth.http`/`organisation.http`/`portal.http` expectations stay true. Every case gets **new** accounts
  and aggregates, at the next free id in each existing family.
- **D14 — "Revoked portal access" = the contact's `HasPortalAccess` is `false` while its `PortalAccount` exists**
  (Context 31). "Deactivated" = `User.Deactivate` applied once, when the user is first created. Neither is re-applied on
  restart: an administrator who reactivates the user (or re-enables access) is not overridden (same rule as the Story 58
  branding seed, `OrganisationSeeder.cs` lines 138–139).
- **D15 — Seeded states come from the aggregate methods**, in legal order, never by setting fields: the seed proves the
  invariants as the real flow would. All seeded timestamps are `dateTime.UtcNow` (audit `CreatedAtUtc` comes from the
  interceptor anyway), so "newest" is insertion order.
- **D2 — Session hint cookies decide the `/` redirect.** The session lives in memory and the refresh cookies are
  HttpOnly and path-scoped (Context 23), so nothing can tell on `/` whether a session exists without calling
  refresh — which is rate-limited with sign-in (Context 24) and would cost two calls per anonymous visit. Instead the
  stores keep two **non-secret, non-HttpOnly hint cookies**, `crm-staff-session` and `crm-portal-session` (value
  `true`, `path=/`, `SameSite=Lax`, 14 days = `Identity:Sessions:RefreshTokenLifetime`): set on a successful
  `login()`/`restore()`, removed in `clear()` (which every sign-out and every failed refresh goes through). The hint
  only chooses where to **navigate**; it grants nothing — the API and the existing guards still decide. A stale hint
  costs one redirect to the sign-in page, after which `clear()` has removed it.
- **D3 — The `/` redirect is decided on the server when the page is server-rendered.** `auth.global.ts` handles
  `to.path === '/'` before its server early-return: on the server it reads the hint cookies and answers a redirect
  (no HTML, so no hydration mismatch — the Story 47 R7 lesson); on the client it does nothing while `isHydrating` (the
  server already decided) and otherwise uses the stores or the hints. **Staff wins** when both sessions exist
  (`/agent`), then portal (`/portal`).
- **D4 — The existing guards are unchanged.** `/agent/**`, `/admin/**` and `/portal/**` still restore through refresh
  exactly as before; the hints are **not** used to skip a restore (a user signed in before this story ships has no
  hint and must keep working).
- **D5 — The landing's customer card goes straight to `/portal/login`** (plus "Create an account" → `/portal/register`),
  not to `/portal`: `/portal` would cost a refresh call and a redirect for a visitor who is, by definition, signed out.
- **D6 — Recent tickets = `GET /api/Agent/Tickets?queue=mine&sort=createdAt&descending=true&page=1&pageSize=5`** in a
  new `useMyRecentTickets` composable with its own key (`'agent-home-recent-tickets'`). "Newest" means created most
  recently. Like the inbox's My tickets view, no status filter is sent, so the backend's default for the mine queue
  applies — the list and the My tickets count agree.
- **D7 — Counts reuse `useTicketQueues()` as is** (same key as the inbox tabs), so the cards cannot disagree with the
  tabs. The card for My tickets links to `/agent/tickets?view=mine` (the inbox parses it; `ticketFiltersToQuery`
  drops it on the next filter change — harmless).
- **D8 — New ticket opens `TicketCreateDrawer` on the home page** (no new query flag on the inbox); `@created` navigates
  to the new ticket, like the inbox (Context 16, line 197).
- **D9 — Each panel is its own component** (`HomeQueueCounts`, `HomeRecentTickets`) with its own loading, empty and
  error-with-retry states, rendered only when the caller `can(Permissions.TicketsRead)`. A failing panel never breaks
  the rest of the page and never toasts (`toastOnError: false`).
- **D10 — Quick actions are permission-gated with the nav's constants:** New ticket `tickets.write`; Find customer
  `customers.read`; Branding `admin.branding`; Users `admin.users`. An Agent sees New ticket and Find customer only.
- **D11 — Copy lives in a new `home` locale domain** (`frontend/i18n/locales/{en,ar}/home.json`); the obsolete
  `shell.home` and `shell.agentHome` keys are removed from both `shell.json` files. Labels that already exist are
  reused (`tickets.views.*`, `tickets.status.*`, `tickets.inbox.unknownCustomer`, `branding.nav.branding`,
  `identity.nav.users`, `organisation.scope.*`).
- **D12 — Portal greeting renders from `portal.me` only after the session is restored** (`v-if`), so the server HTML and
  the hydration render are identical (both have no `me`).

## Security notes

- The hint cookies hold the literal `true` — no token, no user id, no email. They are readable by script by design.
  Nothing on the server trusts them; forging one only causes a redirect to a page that then asks for sign-in.
- The `/` redirect targets are two fixed paths; no query value is followed (no open redirect). The existing
  `safeRedirect` guards on both login pages stay as they are.
- `RefreshCookie`/`PortalRefreshCookie`, the rate-limit policies, `createCrmFetch` and the guards are not modified.
- The new seed accounts exist only where seeders run: `MigrateAndSeedModulesAsync` is called only in Development
  (`Program.cs` lines 135–137), and `IdentitySeeder` seeds no account at all without `Identity:DevSeed:Password`
  (lines 41–47). No new password, secret or configuration key is introduced; every account uses that one password.
- The deactivated user and the revoked contact are refused by the **existing** sign-in checks (Context 30–31) with the
  same generic 401 as a wrong password — the seed adds no bypass and no new refusal path.

---

## Seeded accounts and data (the README tables are generated from this section)

Password for every account: `Identity:DevSeed:Password` (`Crm_Dev_2026!` in `appsettings.Development.json`). Rows marked
**new** are added by this story; the others exist and are **unchanged** (D13). Ids are the full well-known Guids.

### Staff (sign in at `/login`)

| Id | Email | Name | Role — department (branch) | Demonstrates |
|---|---|---|---|---|
| `06000000-0000-0000-0003-000000000001` | `admin@crm.local` | System Administrator | Administrator — RYD-CARE, RYD-BILL (Riyadh), JED-CARE (Jeddah) | admin nav and home quick links (Branding, Users), every department |
| `06000000-0000-0000-0003-000000000002` | `supervisor@crm.local` | سارة القحطاني | Supervisor — RYD-CARE; Agent — RYD-BILL | two roles in two departments: assign in Care, not in Billing; scope switcher |
| `06000000-0000-0000-0003-000000000003` | `agent.a@crm.local` | Omar Haddad | Agent — RYD-CARE (Riyadh) | a busy agent: My tickets, recent list; no admin links; no assign |
| `06000000-0000-0000-0003-000000000004` | `agent.b@crm.local` | نورة الشمري | Agent — JED-CARE (Jeddah) | branch isolation: sees none of Riyadh's customers or tickets |
| `06000000-0000-0000-0003-000000000005` **new** | `agent.c@crm.local` | Hessa Al-Mutairi | Agent — RYD-BILL (Riyadh) | department isolation in the same branch; **no tickets assigned** → home and inbox empty states |
| `06000000-0000-0000-0003-000000000006` **new** | `supervisor.b@crm.local` | خالد الحربي | Supervisor — RYD-CARE (Riyadh) and JED-CARE (Jeddah) | supervisor across two branches: scope switcher, assign and transfer in both |
| `06000000-0000-0000-0003-000000000007` **new** | `agent.inactive@crm.local` | Majed Al-Zahrani | Agent — RYD-CARE (Riyadh), **deactivated** | sign-in refused (401); shown inactive in Admin → Users |

New memberships: `06000000-0000-0000-0004-000000000008` agent C → RYD-BILL (Agent); `…0004-000000000009` supervisor B
→ RYD-CARE (Supervisor); `…0004-000000000010` supervisor B → JED-CARE (Supervisor); `…0004-000000000011` inactive agent →
RYD-CARE (Agent).

### Customers and portal accounts (sign in at `/portal/login`)

| Customer (id, department) | Contact (id) | Portal account (id) — email | Demonstrates |
|---|---|---|---|
| Ahmed Al-Qahtani `11000000-0000-0000-0000-000000000001`, RYD-CARE | Ahmed, primary `…0001-000000000001` | `47000000-0000-0000-0000-000000000001` — `ahmed@example.com` | existing account (Story 47), unchanged |
| **new** Al-Noor Clinics / مجمع النور الطبي `11000000-0000-0000-0000-000000000004`, Organisation, RYD-CARE | Mona Al-Saleh, **primary** `…0001-000000000005` | `47000000-0000-0000-0000-000000000002` — `mona@alnoor.example.com` | account admin: sees **all** Al-Noor requests, including Yousef's |
| (Al-Noor) | Yousef Al-Amri, non-primary `…0001-000000000006` | `47000000-0000-0000-0000-000000000003` — `yousef@alnoor.example.com` | sees **only his own** requests |
| (Al-Noor) | Huda Al-Shehri, non-primary, **`hasPortalAccess: false`** `…0001-000000000007` | `47000000-0000-0000-0000-000000000004` — `huda@alnoor.example.com` | portal access revoked: sign-in refused (401) |
| **new** Faisal Al-Dosari / فيصل الدوسري `11000000-0000-0000-0000-000000000005`, Individual, RYD-CARE | Faisal, primary `…0001-000000000008` | `47000000-0000-0000-0000-000000000005` — `faisal@example.com` | **no requests** → portal empty state with "New request" |
| **new** Gulf Retail / الخليج للتجزئة `11000000-0000-0000-0000-000000000006`, Organisation, RYD-BILL | Nasser Al-Qahtani, primary, no portal `…0001-000000000009` (`nasser@gulfretail.example.com`) | — | a Billing customer, visible to agent C and the supervisor, not to agent A |

Najd Logistics and Red Sea Trading (existing) are unchanged.

### Tickets (new `15000000-0000-0000-0000-0000000000NN`; numbers on a fresh database)

| NN | Number | Customer / contact | Dept | Category | Priority | Status (methods, in order) | Assignee | Channel |
|---|---|---|---|---|---|---|---|---|
| 04 | T-000004 | Al-Noor / Mona | RYD-CARE | Technical | High | Open (`Open`) | agent A | Agent |
| 05 | T-000005 | Al-Noor / Mona | RYD-CARE | General | Normal | PendingCustomer (`Open`, `ReplyAsAgent`, `AwaitCustomer`) | agent A | Portal |
| 06 | T-000006 | Al-Noor / Yousef | RYD-CARE | General | Low | New | — | Portal |
| 07 | T-000007 | Al-Noor / Yousef | RYD-CARE | Complaint | Normal | Resolved (`Open`, `AssignTo`, `Resolve`) | supervisor B | Portal |
| 08 | T-000008 | Al-Noor / Mona | RYD-CARE | General | Low | Closed (`Open`, `AssignTo`, `Resolve`, `Close`) | agent A | Agent |
| 09 | T-000009 | Al-Noor / Mona | RYD-CARE | Technical | Urgent | OnHold (`Open`, `AssignTo`, `PutOnHold`) | supervisor B | Agent |
| 10 | T-000010 | Al-Noor / Mona | RYD-CARE | General | Normal | Cancelled (`Cancel`) | — | Agent |
| 11 | T-000011 | Gulf Retail / Nasser | RYD-BILL | Invoice question | Normal | New | — | Agent |
| 12 | T-000012 | Gulf Retail / Nasser | RYD-BILL | Invoice question | High | Open | supervisor | Agent |
| 13 | T-000013 | Red Sea / Fahad | JED-CARE | JED General | Normal | Open | agent B | Agent |
| 14 | T-000014 | Red Sea / Fahad | JED-CARE | JED General | Low | Resolved (`Open`, `AssignTo`, `Resolve`) | agent B | Agent |
| 15 | T-000015 | Red Sea / Fahad | JED-CARE | JED General | High | New | — | Agent |

Subjects: give 05, 09 and 13 Arabic subjects/descriptions, the rest English (both scripts on screen).

### Expected queue counts on a fresh database (scope "all"; `{mine, unassigned, all}`)

Mine and Unassigned count working statuses only (New, Open, PendingCustomer, OnHold —
`TicketSearchSpecification.cs` lines 12–13, 24); All counts every status.

| User | mine | unassigned | all |
|---|---|---|---|
| admin | 0 | 5 (T1, 06, 11, T3, 15) | 15 |
| supervisor | 1 (12) | 3 (T1, 06, 11) | 11 |
| agent A | 3 (T2, 04, 05) | 2 (T1, 06) | 9 |
| agent B | 1 (13) | 2 (T3, 15) | 4 |
| agent C | 0 | 1 (11) | 2 |
| supervisor B | 1 (09) | 4 (T1, 06, T3, 15) | 13 |

Portal: Mona sees 04–10 (7); Yousef sees 06 and 07 only; Faisal sees none; Ahmed still sees T-000001 only.

---

## Backend Tasks

Development seed only (D1, D13). Order of work: B1 → B2 → B3 → B4, then build and start the API once (R7) before B5–B7.
After every task: `dotnet build backend/Crm.sln` stays at zero warnings (XML doc comments on new public members, as in
the existing `…DevSeed` classes).

### B1 — Organisation: users' memberships

File: `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/DevSeed/OrganisationDevSeed.cs`

- After `AgentBUserId` (line 29) add, each with a `<summary>` matching the staff table:
  `AgentCUserId = 06000000-0000-0000-0003-000000000005` ("Agent in RYD-BILL; no tickets assigned (Story 59)"),
  `SupervisorBUserId = …0003-000000000006` ("Supervisor in RYD-CARE and JED-CARE — two branches"),
  `InactiveAgentUserId = …0003-000000000007` ("Agent in RYD-CARE, deactivated by the Identity seed").
- After line 37 add `AgentCBillingMembershipId = …0004-000000000008`, `SupervisorBCareMembershipId = …0004-000000000009`,
  `SupervisorBJeddahCareMembershipId = …0004-000000000010`, `InactiveAgentCareMembershipId = …0004-000000000011`.
- Update the class summary (line 3): "… Story 59 adds the demo users for every role and scope case."

File: `backend/src/Modules/Organisation/Crm.Modules.Organisation.Infrastructure/Persistence/OrganisationSeeder.cs`

- After line 119 add four `EnsureMembershipAsync` calls (same shape):
  `(riyadhBilling, AgentCUserId, SystemRole.Agent, AgentCBillingMembershipId)`,
  `(riyadhCare, SupervisorBUserId, SystemRole.Supervisor, SupervisorBCareMembershipId)`,
  `(jeddahCare, SupervisorBUserId, SystemRole.Supervisor, SupervisorBJeddahCareMembershipId)`,
  `(riyadhCare, InactiveAgentUserId, SystemRole.Agent, InactiveAgentCareMembershipId)`.
- Update the class summary (lines 49–51): "memberships for the seven well-known user ids".

### B2 — Customers: three customers, five contacts

File: `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/DevSeed/CustomersDevSeed.cs` — after line 21 add
(with summaries from the customers table):

```csharp
    /// <summary>Organisation, Riyadh Customer Care (Story 59): Mona is primary (portal account admin), Yousef is not,
    /// Huda's portal access is off although her portal account exists (revoked).</summary>
    public static readonly Guid AlNoorClinicsCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000004");
    public static readonly Guid AlNoorMonaContactId = Guid.Parse("11000000-0000-0000-0001-000000000005");
    public static readonly Guid AlNoorYousefContactId = Guid.Parse("11000000-0000-0000-0001-000000000006");
    public static readonly Guid AlNoorHudaContactId = Guid.Parse("11000000-0000-0000-0001-000000000007");
    public const string AlNoorMonaEmail = "mona@alnoor.example.com";
    public const string AlNoorYousefEmail = "yousef@alnoor.example.com";
    public const string AlNoorHudaEmail = "huda@alnoor.example.com";

    /// <summary>Individual, Riyadh Customer Care, a portal account and no tickets (the portal empty state).</summary>
    public static readonly Guid FaisalCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000005");
    public static readonly Guid FaisalContactId = Guid.Parse("11000000-0000-0000-0001-000000000008");
    public const string FaisalEmail = "faisal@example.com";

    /// <summary>Organisation, Riyadh Billing - visible to agent C and the supervisor, not to agent A.</summary>
    public static readonly Guid GulfRetailCustomerId = Guid.Parse("11000000-0000-0000-0000-000000000006");
    public static readonly Guid GulfRetailNasserContactId = Guid.Parse("11000000-0000-0000-0001-000000000009");
```

File: `backend/src/Modules/Customers/Crm.Modules.Customers.Infrastructure/Persistence/CustomersSeeder.cs`

- Add `var riyadhBilling = new DepartmentId(OrganisationDevSeed.RiyadhBillingDepartmentId);` next to line 41.
- After the Red Sea block (line 96) add three blocks in the same shape (`if (!await ExistsAsync(id))` → `Customer.Create(
  await numbers.NextAsync(…), LocalizedText.Create(ar, en), type, legalName, language, tenant, branch, department,
  new CustomerId(id))` → `AddContact` calls → `SaveAsync`):
  - Al-Noor: `("مجمع النور الطبي", "Al-Noor Clinics")`, `CustomerType.Organisation`, `"Al-Noor Medical Clinics Co."`,
    `PreferredLanguage.Arabic`, Riyadh / RYD-CARE. Contacts in this order: Mona (`"Mona Al-Saleh"`, `"Operations
    Manager"`, Mona email, null phone, `isPrimary: true`, `hasPortalAccess: true`), Yousef (`"Yousef Al-Amri"`,
    `"Front desk"`, Yousef email, `PhoneNumber.Create("+966503334455")`, `isPrimary: false`, `hasPortalAccess: true`),
    Huda (`"Huda Al-Shehri"`, `"Accounts"`, Huda email, null, `isPrimary: false`, **`hasPortalAccess: false`**).
  - Faisal: `("فيصل الدوسري", "Faisal Al-Dosari")`, `CustomerType.Individual`, null legal name,
    `PreferredLanguage.English`, Riyadh / RYD-CARE; one contact `"Faisal Al-Dosari"`, Faisal email,
    `PhoneNumber.Create("+966505556677")`, primary, portal access on.
  - Gulf Retail: `("الخليج للتجزئة", "Gulf Retail")`, `CustomerType.Organisation`, `"Gulf Retail Trading Co."`,
    `PreferredLanguage.English`, Riyadh / **RYD-BILL**; one contact `"Nasser Al-Qahtani"`, `"Finance"`,
    `EmailAddress.Create("nasser@gulfretail.example.com")`, null, primary, portal access off.
- Check the `Customer.Create` and `AddContact` parameter order against lines 47–55 (the Ahmed block) before writing.

### B3 — Identity: three users, four portal accounts

File: `backend/src/Modules/Identity/Crm.Modules.Identity.Contracts/DevSeed/IdentityDevSeed.cs`

- After line 11 add `AgentCEmail = "agent.c@crm.local"`, `SupervisorBEmail = "supervisor.b@crm.local"`,
  `InactiveAgentEmail = "agent.inactive@crm.local"`.
- After line 15 add `MonaPortalAccountId = 47000000-0000-0000-0000-000000000002`, `YousefPortalAccountId = …0003`,
  `HudaPortalAccountId = …0004` (summary: "contact's portal access is off - sign-in is refused"), `FaisalPortalAccountId
  = …0005`.

File: `backend/src/Modules/Identity/Crm.Modules.Identity.Infrastructure/Persistence/IdentitySeeder.cs`

- After line 54:

```csharp
        await EnsureAsync(tenant, OrganisationDevSeed.AgentCUserId, IdentityDevSeed.AgentCEmail, "Hessa Al-Mutairi", PreferredLocale.English, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.SupervisorBUserId, IdentityDevSeed.SupervisorBEmail, "خالد الحربي", PreferredLocale.Arabic, password, deactivate: false, cancellationToken);
        await EnsureAsync(tenant, OrganisationDevSeed.InactiveAgentUserId, IdentityDevSeed.InactiveAgentEmail, "Majed Al-Zahrani", PreferredLocale.English, password, deactivate: true, cancellationToken);
```

- `EnsureAsync` (lines 82–94) gains a **required** `bool deactivate` parameter placed **before** `cancellationToken`
  (CA1068 wants the token last; so the third new call above reads `…, password, deactivate: true, cancellationToken)`
  and every other call passes `deactivate: false`). Inject `IDateTime dateTime`
  (`Crm.BuildingBlocks.Application.Abstractions`, as `CustomersSeeder` line 35 does) into the primary constructor.
  When the user does **not** exist: create it, and if `deactivate` call `user.Deactivate(dateTime.UtcNow)` before
  `context.Users.Add(user)`. When it exists: return unchanged (D14).
- Replace `EnsurePortalAccountAsync(tenant, password, ct)` (lines 61–80) with a general
  `EnsurePortalAccountAsync(TenantId tenant, Guid accountId, Guid customerId, Guid contactId, string email,
  string displayName, PreferredLocale locale, string password, CancellationToken ct)` with the same body, and call it
  five times at line 56: Ahmed (unchanged values: `AhmedPortalAccountId`, `AhmedCustomerId`, `AhmedContactId`,
  `AhmedEmail`, `"أحمد القحطاني"`, Arabic), Mona (`"منى الصالح"`, Arabic), Yousef (`"Yousef Al-Amri"`, English), Huda
  (`"Huda Al-Shehri"`, English), Faisal (`"Faisal Al-Dosari"`, English) — ids from `IdentityDevSeed`/`CustomersDevSeed`.
- Update the class summary (lines 30–32): "the seven users … and five portal accounts (Story 59: one of them, Huda,
  belongs to a contact whose portal access is off, so her sign-in is refused)".

### B4 — Tickets: twelve tickets

File: `backend/src/Modules/Tickets/Crm.Modules.Tickets.Contracts/DevSeed/TicketsDevSeed.cs`

- Update the class summary (line 4): "The seed takes T-000001..T-000015 on a fresh database."
- After line 26 add one `public static readonly Guid` per row of the tickets table, named after its case, each with a
  one-line summary copied from the table: `AlNoorOpenTicketId` (04), `AlNoorPendingTicketId` (05),
  `AlNoorYousefNewTicketId` (06), `AlNoorYousefResolvedTicketId` (07), `AlNoorClosedTicketId` (08),
  `AlNoorOnHoldTicketId` (09), `AlNoorCancelledTicketId` (10), `GulfRetailNewTicketId` (11), `GulfRetailOpenTicketId`
  (12), `RedSeaOpenTicketId` (13), `RedSeaResolvedTicketId` (14), `RedSeaHighNewTicketId` (15) =
  `15000000-0000-0000-0000-0000000000NN`.

File: `backend/src/Modules/Tickets/Crm.Modules.Tickets.Infrastructure/Persistence/TicketsSeeder.cs`

- Capture the complaint and invoice categories (lines 57–58 and 63–64 currently discard them):
  `Category complaint = await EnsureCategoryAsync(…)`, `Category invoice = await EnsureCategoryAsync(…)`.
- After the Red Sea block (line 102) add a private helper and twelve calls instead of twelve copies:

```csharp
    private async Task EnsureTicketAsync(
        Guid id, string subject, string description, Guid customerId, Guid contactId, TicketChannel channel, Category category,
        TicketPriority priority, TenantId tenant, BranchId branch, DepartmentId department, Action<Ticket, Guid, DateTimeOffset> progress,
        CancellationToken cancellationToken)
    {
        if (await TicketExistsAsync(id, cancellationToken))
        {
            return;
        }

        Guid admin = OrganisationDevSeed.AdminUserId;
        DateTimeOffset now = dateTime.UtcNow;
        Ticket ticket = Ticket.Create(
            await numbers.NextAsync(cancellationToken), subject, description, customerId, contactId, channel, category, priority,
            tenant, branch, department, admin, now, new TicketId(id));
        progress(ticket, admin, now);
        await SaveAsync(ticket, cancellationToken);
    }
```

  Each call passes a lambda with the table's methods in order, e.g. row 08:
  `(t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentAUserId, by, at); t.Resolve(by, at); t.Close(by, at); }`,
  row 05: `(t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentAUserId, by, at); t.ReplyAsAgent("…", OrganisationDevSeed.AgentAUserId, at); t.AwaitCustomer(by, at); }`,
  rows with status New and no assignee: `(_, _, _) => { }`. Assign **before** `Resolve`/`Close`/`PutOnHold` (assignment
  is a working-status action, Context 34). Portal-channel tickets (05, 06, 07) pass the contact as on the portal. Tickets
  for Gulf Retail use `riyadhBranch`, `riyadhBilling`, `invoice`; Red Sea uses `jeddahBranch`, `jeddahCare`, `jeddahGeneral`.
- If `Action<…>` trips an analyzer (none expected — `CA1062` does not apply to private methods), inline the twelve blocks
  in the existing style instead.

### B5 — Prove the seed (R7, R8)

Start the API on a **fresh** database (`docker compose -f backend/docker-compose.yml down -v`, then `up -d`, then
`dotnet run --project backend/src/Crm.Api --launch-profile http`) and once on the **existing** database; both must start.
Restart a second time: no duplicate row (SQL checks in Test Plan 8).

### B6 — `backend/requests/demo-accounts.http`

Create file: `backend/requests/demo-accounts.http`, header like `portal.http` lines 1–11 (`@host = http://localhost:5187`,
run order, "counts assume a fresh database", the 10/min sign-in limit — wait a minute on 429). One block per account,
each named `### N. <email> - <expected>`:

1. Staff, per account: `POST {{host}}/api/Agent/Auth/login` (`# @name <alias>`) → 200; then `GET /api/Agent/Me` (name,
   roles, memberships as in the staff table), `GET /api/Agent/MyScope` (departments), `GET /api/Agent/Tickets/queues`
   (the counts table). For `agent.a` add `GET /api/Agent/Tickets?queue=mine&sort=createdAt&descending=true&pageSize=5`
   (T-000005, T-000004, T-000002). For `agent.c` the same call → `items: []`. For `agent.b`
   `GET /api/Agent/Customers/{{AlNoorClinicsCustomerId}}` → **403** `urn:crm:problem:forbidden` (branch isolation).
   For `supervisor.b` `GET /api/Agent/Tickets/queues` with `X-Crm-Scope: 06000000-0000-0000-0002-000000000003` → only
   JED-CARE counts `{1, 2, 4}`.
2. `agent.inactive@crm.local` → **401** with the same body as a wrong password (`auth.http` request 13/14).
3. Portal, per account: `POST {{host}}/api/Portal/Auth/login` → 200; `GET /api/Portal/Me` (`isAccountAdmin` true for Mona
   and Faisal, false for Yousef); `GET /api/Portal/Tickets` (Mona 7, Yousef 2, Faisal 0).
4. `huda@alnoor.example.com` → **401** (generic; the log line says `portal-access-revoked`).
5. Copy the exact list/query parameter names from `tickets.http` and `portal.http` (e.g. the portal list's tab/status
   parameters) rather than inventing them.

### B7 — Request-file comments and README tables

Comments only — no request in these files changes:

- `backend/requests/customers.http` line 7: "the seed takes C-000001..C-000006, so request 6 is C-000007"; line 88 the
  same number; line 60's expected result: re-run and write what it returns.
- `backend/requests/tickets.http` line 8 and line 182: "T-000001..T-000015 … request 12 is T-000016".
- `backend/requests/users.http` line 31: "the seven seeded users"; line 35: re-run and write the new total for
  `search=agent` (agent A, agent B, agent C, agent.inactive → expected total 4).
- `backend/requests/portal.http` lines 7–11: mention the four new portal accounts and point to `demo-accounts.http`;
  "Counts … (T-000001 only …)" stays true for Ahmed.
- `backend/requests/ticket-workspace.http` line 45: agent A's list now also has the Al-Noor rows (T-000004…T-000010).
- `README.md` "Demo accounts" (lines 69–79): replace the table with **Email | Role — department (branch) | Demonstrates**
  for all seven staff and five portal accounts from "Seeded accounts and data", marking the two refused accounts
  "sign-in refused", and one sentence: "All passwords: `Crm_Dev_2026!` (Development seed only; re-running the API never
  duplicates a row)". Add the `/` row to the URL table (F11).
- `backend/README.md` (lines 315–325): "The Development seed creates seven users and five portal accounts"; extend the
  user table with the three new rows (id suffix, email, memberships), and add a portal table (account id suffix, email,
  customer, contact, case). Link to `requests/demo-accounts.http`.

## Frontend Tasks

### F1 — Session hint utility

Create file: `frontend/app/utils/sessionHint.ts`

```ts
/** Story 59 (D2): non-secret hints that a session probably exists, so `/` can route a returning user without calling
 * refresh (HttpOnly, path-scoped and rate-limited). They grant nothing - the API and the guards still decide. */
export const staffSessionHintCookie = 'crm-staff-session'
export const portalSessionHintCookie = 'crm-portal-session'

/** 14 days = Identity:Sessions:RefreshTokenLifetime. Rewritten on every successful restore. */
export const sessionHintCookieOptions = { sameSite: 'lax', path: '/', maxAge: 60 * 60 * 24 * 14 } as const
```

### F2 — Stores write and clear the hints

File: `frontend/app/stores/auth.ts`

- Import `{ sessionHintCookieOptions, staffSessionHintCookie } from '~/utils/sessionHint'`.
- Add next to `resetScope()` (lines 32–38), same pattern:

```ts
  function setSessionHint(signedIn: boolean): void {
    nuxtApp.runWithContext(() => {
      const cookie = useCookie<boolean | null>(staffSessionHintCookie, sessionHintCookieOptions)
      cookie.value = signedIn ? true : null
    })
  }
```

- `clear()` (lines 26–30): add `setSessionHint(false)` as its last line.
- `login()`: after `const loaded = await loadMe()` succeeds (after the `if (!loaded.ok)` block, line 60), call
  `setSessionHint(true)` before `resetScope()`.
- `restore()`: after the `if (!loaded.ok) { … }` block (line 100), call `setSessionHint(true)` before `return true`.
- Do **not** export `setSessionHint`.

File: `frontend/app/stores/portalAuth.ts` — the same three edits with `portalSessionHintCookie`: `clear()` (lines
28–32), `login()` after line 60, `restore()` after line 113.

### F3 — `/` redirect in the global middleware

File: `frontend/app/middleware/auth.global.ts`

- Import `{ portalSessionHintCookie, sessionHintCookieOptions, staffSessionHintCookie } from '~/utils/sessionHint'`.
- Update the header comment (lines 1–4): add "- `/` (Story 59): a returning user goes to their home; decided on the
  server from the session hint cookies (D2/D3), so the landing never renders and then redirects."
- Add above `export default`:

```ts
/** Where a signed-in user belongs. Staff first (D3). On the server only the hints exist (no session there - D17). */
function signedInHome(): '/agent' | '/portal' | null {
  const staffHint = useCookie<boolean | null>(staffSessionHintCookie, sessionHintCookieOptions).value === true
  const portalHint = useCookie<boolean | null>(portalSessionHintCookie, sessionHintCookieOptions).value === true
  if (import.meta.client) {
    if (useAuthStore().isAuthenticated || staffHint) return '/agent'
    if (usePortalAuthStore().isAuthenticated || portalHint) return '/portal'
    return null
  }
  if (staffHint) return '/agent'
  if (portalHint) return '/portal'
  return null
}
```

- First statement inside the middleware, **before** `if (import.meta.server) return` (line 8):

```ts
  if (to.path === '/') {
    // The server already decided on the first load; redirecting during hydration would mismatch its HTML (R7).
    if (import.meta.client && useNuxtApp().isHydrating) return
    const home = signedInHome()
    if (home !== null) return navigateTo(home)
    return
  }
```

- Leave everything else in the file unchanged (D4).

### F4 — Landing page `/`

File: `frontend/app/pages/index.vue` — replace the whole file.

- Keep `definePageMeta({ layout: 'portal' })`. No API call of its own (branding comes from `plugins/branding.ts`).
- Script: `const { t, locale } = useI18n()`, `const branding = useBrandingStore()`,
  `const brandName = computed(() => branding.displayName(locale.value) ?? t('shell.appName'))`.
- Template (Tailwind tokens and logical utilities only):
  - `<section class="flex flex-col items-center gap-4 py-6 text-center sm:py-10">` containing `<ShellBrandMark />`
    (it renders the logo or the name; it already falls back), an `<h1 class="text-2xl font-bold text-fg sm:text-3xl">`
    with `t('home.landing.title', { name: brandName })`, and `<p class="max-w-prose text-fg-muted">` with
    `t('home.landing.intro')`.
  - `<h2 class="sr-only">{{ t('home.landing.entryLabel') }}</h2>` then
    `<ul class="mt-6 grid gap-4 sm:grid-cols-2">` with two `<li>` cards, each
    `class="flex h-full flex-col gap-3 rounded-lg border border-border bg-surface p-6"`:
    - **Staff:** `<h3 class="text-lg font-semibold text-fg">` `home.landing.staff.title`; `<p class="flex-1 text-sm
      text-fg-muted">` `home.landing.staff.description`; one `NuxtLink to="/login"` styled as a primary button:
      `class="inline-flex items-center justify-center rounded-md bg-primary-600 px-4 py-2.5 text-base font-medium
      text-fg-inverse hover:bg-primary-700 focus-visible:focus-ring"` with `home.landing.staff.action`.
    - **Customer:** `home.landing.portal.title` / `.description`; a `<div class="flex flex-wrap gap-2">` with
      `NuxtLink to="/portal/login"` (primary classes as above, `home.landing.portal.signIn`) and
      `NuxtLink to="/portal/register"` (secondary: `inline-flex items-center justify-center rounded-md border
      border-border bg-surface-raised px-4 py-2.5 text-base font-medium text-fg hover:bg-surface-sunken
      focus-visible:focus-ring`, `home.landing.portal.register`).
- No loading/empty/error panel: the page makes no call; branding failure already yields the defaults (Story 58 D10).

### F5 — Agent home panels

Create file: `frontend/app/composables/tickets/useMyRecentTickets.ts`

```ts
/** Agent home (Story 59, D6): the newest tickets assigned to me. Same endpoint and parameter names as the inbox
 * (useTicketSearch); its own key so it never overwrites the inbox list. */
export const myRecentTicketsCount = 5

export function useMyRecentTickets() {
  const { clients, call } = useApi()
  return useAsyncData('agent-home-recent-tickets', async (_nuxtApp, { signal }) => {
    const result = await call(
      clients.agent.GET('/api/Agent/Tickets', {
        params: {
          query: { queue: 'mine', sort: 'createdAt', descending: true, page: 1, pageSize: myRecentTicketsCount },
        },
        signal,
      }),
      { toastOnError: false },
    )
    if (!result.ok) throw result.problem
    return result.data.items ?? []
  })
}
```

Create file: `frontend/app/components/home/HomeQueueCounts.vue` (auto-imported as `<HomeQueueCounts>`)

- Script: `useTicketQueues()` → `{ data, status, error, refresh }`; `useFormat()`; `ticketViews` from `~/utils/tickets`.
  `const loading = computed(() => (status.value === 'idle' || status.value === 'pending') && !data.value)`.
  `const allZero = computed(() => data.value != null && ticketViews.every((view) => (data.value?.[view] ?? 0) === 0))`.
- Template: `<section aria-labelledby="home-queues-title">` with `<h2 id="home-queues-title" class="text-lg
  font-semibold text-fg">` `home.agent.queues.title`, then:
  - `loading` → `<div class="mt-3 grid gap-3 sm:grid-cols-3">` with three `<UiSkeleton variant="rect" class="h-24" />`.
  - `error && !data` → `<UiErrorState class="mt-3" :title="t('home.agent.queues.loadError')"
    :retry-label="t('home.agent.queues.retry')" @retry="refresh()" />`.
  - otherwise `<ul class="mt-3 grid gap-3 sm:grid-cols-3">`, one `<li>` per `ticketViews` entry:
    `<NuxtLink :to="`/agent/tickets?view=${view}`" class="flex h-full flex-col gap-1 rounded-lg border border-border
    bg-surface p-4 hover:bg-surface-raised focus-visible:focus-ring">` with `<span class="text-sm text-fg-muted">`
    `t(`tickets.views.${view}`)` and `<span class="text-3xl font-bold text-fg">` `format.number(data?.[view] ?? 0)`.
    The link text is the label plus the number, so no `aria-label` is needed.
  - **Empty:** when `allZero`, below the cards `<p class="mt-2 text-sm text-fg-muted">` `home.agent.queues.empty`
    (the cards stay, showing 0 — the numbers are the content).

Create file: `frontend/app/components/home/HomeRecentTickets.vue` (`<HomeRecentTickets>`)

- Script: `useMyRecentTickets()`; `useFormat()`; `const { t, locale } = useI18n()`; `ticketStatuses`,
  `ticketStatusTone` from `~/utils/tickets`; `localizedName` from `~/utils/scope`; `TicketCustomerRefDto` from
  `~/composables/tickets/types`. Copy `isStatus` / `statusLabel` / `customerName` from
  `pages/agent/tickets/index.vue` lines 307–309, 460–462, 260–262. Same `loading` rule as `HomeQueueCounts`.
- Template: `<section aria-labelledby="home-recent-title">`, header row `flex flex-wrap items-center justify-between
  gap-2` with `<h2 id="home-recent-title">` `home.agent.recent.title` and a `NuxtLink to="/agent/tickets?view=mine"`
  (`text-sm text-primary-600 underline focus-visible:focus-ring`) `home.agent.recent.viewAll`, then:
  - `loading` → `<ul class="mt-3 flex flex-col gap-2">` with 3 `<li class="rounded-md border border-border p-3"><UiSkeleton :lines="2" /></li>`.
  - `error && !data` → `UiErrorState` (`home.agent.recent.loadError`, `home.agent.recent.retry`, `@retry="refresh()"`).
  - `data.length === 0` → `<UiEmptyState :heading-level="3" :title="t('home.agent.recent.empty')"
    :description="t('home.agent.recent.emptyDescription')">` with `#actions` a `NuxtLink
    to="/agent/tickets?view=unassigned"` styled as a secondary button (classes as F4) `home.agent.recent.emptyAction`.
  - otherwise `<ul class="mt-3 flex flex-col gap-2">`, per row (mirror the inbox mobile card, Context 16 lines
    155–178): `NuxtLink :to="`/agent/tickets/${row.id}`" class="block rounded-md border border-border bg-surface p-3
    hover:bg-surface-raised focus-visible:focus-ring"`; first line `flex items-start justify-between gap-2 text-xs
    text-fg-muted`: `<bdi dir="ltr" class="font-mono">{{ row.number }}</bdi>` and
    `row.createdAtUtc ? format.date(row.createdAtUtc) : ''`; `<div dir="auto" class="mt-1 break-words font-medium
    text-fg">{{ row.subject }}</div>`; then `flex flex-wrap items-center gap-2` with
    `<UiBadge size="sm" :tone="ticketStatusTone(row.status)">` and the customer name + `<bdi dir="ltr">` number (or
    `tickets.inbox.unknownCustomer`).

### F6 — Agent home page

File: `frontend/app/pages/agent/index.vue` — replace the whole file.

- Script:

```ts
import { Permissions } from '~/utils/permissions'
import { allDepartments, localizedName } from '~/utils/scope'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const auth = useAuthStore()
const scope = useScopeStore() // loaded by the layout's useMyScope()
const { can } = useCan()
const createOpen = ref(false)

const greeting = computed(() => {
  const name = auth.me?.displayName
  return name ? t('home.agent.greeting', { name }) : t('home.agent.greetingNoName')
})

// Same labels as the scope switcher (ScopeSwitcher.vue lines 21-31). One department and no explicit choice -> that one.
const scopeLabel = computed<string | null>(() => {
  if (scope.status !== 'ready') return null
  const active = scope.myScope?.activeDepartmentId ?? allDepartments
  const departments = scope.activeDepartments
  const chosen =
    active === allDepartments
      ? departments.length === 1 ? departments[0] : undefined
      : departments.find((d) => d.departmentId === active)
  if (chosen === undefined) return t('organisation.scope.all')
  return t('organisation.scope.option', {
    department: localizedName(chosen.departmentName, locale.value),
    branch: localizedName(chosen.branchName, locale.value),
  })
})

interface QuickAction { to: string; label: string }
const links = computed<ReadonlyArray<QuickAction>>(() => [
  ...(can(Permissions.CustomersRead) ? [{ to: '/agent/customers', label: t('home.agent.actions.findCustomer') }] : []),
  ...(can(Permissions.AdminBranding) ? [{ to: '/admin/branding', label: t('branding.nav.branding') }] : []),
  ...(can(Permissions.AdminUsers) ? [{ to: '/admin/users', label: t('identity.nav.users') }] : []),
])
```

- Template:
  - `<h1 class="text-2xl font-bold text-fg">{{ greeting }}</h1>`; below it `<p v-if="scopeLabel" class="mt-1 text-sm
    text-fg-muted">{{ t('home.agent.scope', { scope: scopeLabel }) }}</p>`, and `<UiSkeleton v-else-if="scope.status
    === 'loading'" class="mt-2 w-48" />` (scope errors are retried by the header `ScopeSwitcher`; the page shows
    nothing then).
  - Quick actions `<section aria-labelledby="home-actions-title" class="mt-6">`, `<h2 id="home-actions-title"
    class="sr-only">` `home.agent.actions.title`, `<div class="flex flex-wrap gap-2">`: `<UiButton
    v-if="can(Permissions.TicketsWrite)" @click="createOpen = true">` `home.agent.actions.newTicket`, then each
    `links` entry as a `NuxtLink` with the secondary-button classes from F4 (size `px-3 py-2 text-sm`).
  - `<div v-if="can(Permissions.TicketsRead)" class="mt-8 flex flex-col gap-8"><HomeQueueCounts /><HomeRecentTickets /></div>`
    — stacked: the counts span the full width and the list sits below them (no two-column grid).
  - `<TicketCreateDrawer v-if="can(Permissions.TicketsWrite)" v-model:open="createOpen"
    @created="(ticket) => navigateTo(`/agent/tickets/${ticket.id}`)" />`.

### F7 — Portal home greeting

File: `frontend/app/pages/portal/index.vue`

- Script: add `const portal = usePortalAuthStore()` and
  `const greeting = computed(() => (portal.me?.displayName ? t('portal.tickets.greeting', { name: portal.me.displayName }) : null))`.
- Template: above the header row (line 3) add `<p v-if="greeting" class="mb-1 text-sm text-fg-muted">{{ greeting }}</p>`
  (D12). Make the header "New request" button `size="lg"` (line 5) so it is the prominent action. Nothing else
  changes — the empty state with `portal.tickets.emptyAction` (lines 26–35) already satisfies the empty-list criterion.

### F8 — Brand mark links to its layout's home

File: `frontend/app/layouts/agent.vue`

- Line 12: `<div class="flex min-w-0 px-7 pt-4"><NuxtLink to="/agent" class="flex min-w-0 rounded-md
  focus-visible:focus-ring"><ShellBrandMark /></NuxtLink></div>`.
- Line 39: replace `<ShellBrandMark class="md:hidden" />` with `<NuxtLink to="/agent" class="flex min-w-0 rounded-md
  focus-visible:focus-ring md:hidden"><ShellBrandMark /></NuxtLink>`.

File: `frontend/app/layouts/portal.vue`

- Line 16: replace `<ShellBrandMark v-else />` with `<NuxtLink v-else to="/" class="text-lg font-semibold text-fg
  focus-visible:focus-ring"><ShellBrandMark /></NuxtLink>` (same classes as line 13). Update the comment at line 83:
  "The brand links to /portal on portal routes and to / on the landing and the staff /login page (Story 59)."

### F9 — Login cross-links

File: `frontend/app/pages/login.vue` — after `</form>` (line 25) add:

```vue
    <p class="mt-4 text-sm">
      <NuxtLink to="/portal/login" class="text-primary-600 underline focus-visible:focus-ring">
        {{ t('identity.login.customerPortal') }}
      </NuxtLink>
    </p>
```

File: `frontend/app/pages/portal/login.vue` — change the `<p class="mt-4 text-sm">` at lines 26–30 to
`<p class="mt-4 flex flex-col gap-2 text-sm">` and add a second `NuxtLink to="/"` (same classes) with
`t('portal.login.backHome')`.

`safeRedirect` (both pages) and both `logout()` targets already match the intake — **no change**; verify only.

### F10 — Copy

Create file: `frontend/i18n/locales/en/home.json`

```json
{
  "home": {
    "landing": {
      "title": "Welcome to {name}",
      "intro": "Get help from our support team, or sign in to work on customer requests.",
      "entryLabel": "Choose how to continue",
      "staff": {
        "title": "Staff sign-in",
        "description": "For agents, supervisors and administrators: the ticket inbox, customers and settings.",
        "action": "Sign in as staff"
      },
      "portal": {
        "title": "Customer portal",
        "description": "Submit a request and follow its progress.",
        "signIn": "Sign in",
        "register": "Create an account"
      }
    },
    "agent": {
      "greeting": "Welcome back, {name}",
      "greetingNoName": "Welcome back",
      "scope": "Working in {scope}",
      "actions": {
        "title": "Quick actions",
        "newTicket": "New ticket",
        "findCustomer": "Find a customer"
      },
      "queues": {
        "title": "Your queues",
        "loadError": "The ticket counts could not be loaded",
        "retry": "Try again",
        "empty": "There are no tickets in your scope yet."
      },
      "recent": {
        "title": "My recent tickets",
        "viewAll": "View all my tickets",
        "empty": "No tickets are assigned to you",
        "emptyDescription": "Pick one up from the unassigned queue.",
        "emptyAction": "Show unassigned",
        "loadError": "Your tickets could not be loaded",
        "retry": "Try again"
      }
    }
  }
}
```

Create file: `frontend/i18n/locales/ar/home.json` — the same keys:

```json
{
  "home": {
    "landing": {
      "title": "مرحبًا بك في {name}",
      "intro": "احصل على المساعدة من فريق الدعم، أو سجّل الدخول للعمل على طلبات العملاء.",
      "entryLabel": "اختر طريقة المتابعة",
      "staff": {
        "title": "دخول الموظفين",
        "description": "للوكلاء والمشرفين والمسؤولين: صندوق التذاكر والعملاء والإعدادات.",
        "action": "تسجيل دخول الموظفين"
      },
      "portal": {
        "title": "بوابة العملاء",
        "description": "قدّم طلبًا وتابع تقدّمه.",
        "signIn": "تسجيل الدخول",
        "register": "إنشاء حساب"
      }
    },
    "agent": {
      "greeting": "مرحبًا بعودتك، {name}",
      "greetingNoName": "مرحبًا بعودتك",
      "scope": "تعمل ضمن {scope}",
      "actions": {
        "title": "إجراءات سريعة",
        "newTicket": "تذكرة جديدة",
        "findCustomer": "البحث عن عميل"
      },
      "queues": {
        "title": "قوائم التذاكر",
        "loadError": "تعذّر تحميل أعداد التذاكر",
        "retry": "حاول مرة أخرى",
        "empty": "لا توجد تذاكر ضمن نطاقك بعد."
      },
      "recent": {
        "title": "أحدث تذاكري",
        "viewAll": "عرض كل تذاكري",
        "empty": "لا توجد تذاكر مسندة إليك",
        "emptyDescription": "اختر تذكرة من قائمة غير المسندة.",
        "emptyAction": "عرض غير المسندة",
        "loadError": "تعذّر تحميل تذاكرك",
        "retry": "حاول مرة أخرى"
      }
    }
  }
}
```

Edit existing files (keep key sets identical in `en` and `ar`):

- `frontend/i18n/locales/{en,ar}/shell.json` — **remove** `shell.home` and `shell.agentHome` (both objects).
- `frontend/i18n/locales/en/identity.json` — under `identity.login` add
  `"customerPortal": "Are you a customer? Go to the customer portal"`; `ar`: `"هل أنت عميل؟ انتقل إلى بوابة العملاء"`.
- `frontend/i18n/locales/en/portal.json` — under `portal.login` add `"backHome": "Back to the home page"`
  (`ar`: `"العودة إلى الصفحة الرئيسية"`); under `portal.tickets` add `"greeting": "Hello, {name}"`
  (`ar`: `"مرحبًا، {name}"`).
- `nuxt.config.ts` needs **no** change: it loads every `en/*.json` (lines 5–9).

### F11 — README entry point

File: `README.md` — the URL table at lines 22–26: add a first row `| `/` | Landing: choose staff sign-in or the
customer portal |`. Keep the other rows.

---

## Edge Cases & Failure Modes

- **Anonymous visitor on `/`** → no hint cookie → landing renders server-side; no refresh call (network tab: only the
  branding call from Story 58, made on the server). Enforced in F3 `signedInHome()`.
- **Staff with a valid session reloads `/`** → hint `true` → the server answers a redirect to `/agent`; the SPA guard
  restores through refresh (Context 3 lines 30–33). F2/F3.
- **Stale staff hint** (refresh cookie expired or revoked) → `/` → `/agent` → restore fails → `auth.refresh()` calls
  `clear()` which removes the hint → `/login?redirect=/agent`. The next visit to `/` shows the landing. F2.
- **Hint missing but a valid session** (signed in before this story shipped, or cookies partially cleared) → `/` shows
  the landing; `/agent` and `/portal` still work because their guards restore without looking at hints (D4). The next
  successful restore writes the hint.
- **Staff and portal both signed in** → `/` goes to `/agent` (D3). The portal is still one click away (`/portal`).
- **Client-side navigation to `/`** (brand link on `/login`, error page "Go home" → `clearError({ redirect: '/' })`,
  `frontend/app/error.vue` line 29) → middleware runs on the client, not hydrating → store state or hint decides. A
  signed-in staff user clicking the error page's home button lands on `/agent`.
- **Hydration on `/`** → the server either redirected (no HTML) or rendered the landing with no session-dependent
  content; the client skips the `/` branch while `isHydrating`. No mismatch possible (R2).
- **Portal greeting on SSR `/portal`** → `portal.me` is `null` on the server and during hydration (session restored in
  `portalGuard.client.ts` after mount), so the `v-if` renders nothing in both; it appears after restore (D12, R4).
- **Branding unavailable** → `branding.displayName()` is `null` → `t('shell.appName')`; `ShellBrandMark` shows the name;
  tokens.css colours (Story 58 D10). F4.
- **Queue counts fail (500, network) or 403** → `HomeQueueCounts` shows its error with retry; the recent list and the
  quick actions still render (D9). Same the other way round.
- **Scope switch on the home page** → `scope.setActive()` calls `refreshNuxtData()` (Context 18), so both panels refetch
  under the new `X-Crm-Scope`; the greeting's scope line follows `scope.myScope`.
- **User with no `tickets.read`** (not reachable with today's role map, but permissions are per active scope) → neither
  panel mounts, so no 403 call is made; quick actions show what remains (D9, D10).
- **Agent (no admin permissions)** → quick actions: New ticket, Find a customer. **Administrator** → also Branding and
  Users. F6 `links`.
- **Zero tickets in scope** → three cards showing `0` plus `home.agent.queues.empty`; recent list empty state with the
  "Show unassigned" link. F5.
- **Long Arabic subject or customer name** → `break-words` + `dir="auto"` on the subject; ticket and customer numbers in
  `<bdi dir="ltr">`. F5.
- **A ticket created from the home drawer** → navigates to `/agent/tickets/<id>`; returning to `/agent` refetches the
  counts (client navigation re-runs `useAsyncData`).
- **`useCookie` value decoding** → Nuxt's default decoder parses JSON, so the hint is typed `boolean | null` and
  compared with `=== true`; any other value (manual edit) counts as no hint (F3). See R1.

**Seed (B1–B7):**

- **API restarted** → every `Ensure…`/`ExistsAsync` check is keyed on a well-known id; nothing is inserted twice (Test
  Plan 8). Enforced by the existing guard pattern in each seeder (Context 27, 29, 32, 33).
- **Existing development database** (created before this story) → the new rows are added; existing rows are untouched
  (D13). Customer and ticket **numbers** of the new rows are the next free ones, not `C-000004`/`T-000004` — the README
  and `.http` comments say "on a fresh database".
- **A seeded account was changed by hand** (reactivated user, Huda's access switched on, a seeded ticket resolved) →
  the seeder never re-applies its state (D14); the README table describes the fresh state.
- **Seeding order between modules** is not guaranteed (Context 25). Every cross-module id is a primitive constant, and
  no seeder reads another module's tables, so any order works; until the Identity seeder has run, the new memberships
  point at users that do not exist yet — harmless, the same as today.
- **A domain rule rejects a seeded transition** (e.g. `AssignTo` on a Resolved ticket, `AwaitCustomer` from New) →
  `DomainException` at startup and the API does not start. B4's orders follow `StatusTransitions` (Context 34); R7.
- **`Identity:DevSeed:Password` missing** → no user or portal account is seeded (existing warning); the customer and
  ticket rows still are — the same as today.
- **Production / any non-Development environment** → `MigrateAndSeedModulesAsync` never runs; no demo account exists.
- **Lockout** → `auth.http` request 16 locks `agent.b` (5 wrong passwords). The new accounts are not used there; the
  `demo-accounts.http` refusal requests send the *right* password, so they never lock an account.
- **Huda tries to register** with her email → the contact has no portal access, so registration does nothing (204, as
  for any unknown email — Story 47 D5).
- **Agent B opens an Al-Noor ticket or customer by URL** → 403 `urn:crm:problem:forbidden` (`Organisation.OutOfScope`),
  never 404 — the branch-isolation demonstration (B6).

---

## Test Plan

This project ships **without automated tests** (CLAUDE.md, "Testing policy"). Do not add a test project, a
`*.spec.ts`, `*.test.ts`, or a test framework. In their place:

1. **Compiler and lint:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint` (includes
   `scripts/check-locales.ts` — the new `home.json` pair and the removed `shell` keys must match across `en`/`ar`; no
   bare template strings; no physical-direction or literal-colour utilities), `pnpm --dir frontend build`.
2. **Backend build:** `dotnet build backend/Crm.sln` — zero warnings, no `CRM0001` (no project reference changes).
3. **Manual smoke — redirects (dev, `pnpm --dir frontend dev` + API):**
   1. Signed out, private window: `/` → landing, view-source contains the welcome heading and both cards (SSR).
   2. Sign in as `agent.a@crm.local` from the landing's staff card → `/agent`. Reload `/` → `/agent` (network: the
      document request for `/` answers a redirect). Sign out → `/login`; `/` → landing.
   3. Sign in on `/portal/login` as `ahmed@example.com` → `/portal`; reload `/` → `/portal`; sign out → `/portal/login`.
   4. `/agent/tickets` signed out → `/login?redirect=/agent/tickets` → sign in → back on the inbox (redirect query).
   5. Delete the `crm_refresh` cookie (DevTools) but keep `crm-staff-session` → `/` → `/agent` → `/login?redirect=/agent`,
      and `crm-staff-session` is gone afterwards (stale-hint path).
4. **Manual smoke — agent home:** as `agent.a` the three numbers equal the inbox tab badges; each card opens the inbox
   on that tab; recent list shows ≤ 5 tickets, newest first, each opening its ticket; quick actions show only New ticket
   and Find a customer. As `admin@crm.local` Branding and Users appear. Stop the API (or block
   `/api/Agent/Tickets/queues` in DevTools) → that panel's error state, retry recovers, the other panel is unaffected.
   `agent.c@crm.local` (no assigned tickets) → Mine 0 and the recent-list empty state with "Show unassigned" (→ T-000011).
   Every staff account's cards equal the "Expected queue counts" table on a fresh database.
5. **Manual smoke — portal home:** `faisal@example.com` sees the greeting, the "New request" button, and the empty
   state with its action; `mona@alnoor.example.com` sees all seven Al-Noor requests across the open and closed tabs;
   `yousef@alnoor.example.com` sees only T-000006 and T-000007.
6. **Walkthrough:** every screen above in `en` and `ar` at 375, 768 and 1280 px; brand mark links (`/` and `/login` →
   `/`, portal → `/portal`, agent sidebar and mobile header → `/agent`); login cross-links.
7. **Docker:** `docker compose up -d --build`, then http://localhost:8080 — repeat 3.1, 3.2 and 5 (R5).
8. **Seed (manual, B5–B6):** on a fresh database and again after a restart, `backend/requests/demo-accounts.http` top to
   bottom — every staff and portal account signs in and shows its case; `agent.inactive@crm.local` and
   `huda@alnoor.example.com` get the generic **401**. Idempotency, after the second start (SQL, `sqlcmd` or Azure Data
   Studio): `SELECT Id, COUNT(*) FROM identity.Users GROUP BY Id HAVING COUNT(*) > 1` and the same for
   `identity.PortalAccounts`, `organisation.Memberships`, `customers.Customers`, `tickets.Tickets` return no rows;
   `SELECT COUNT(*) FROM tickets.Tickets WHERE Id LIKE '15000000-0000-0000-0000-%'` = 15 (check the real table names in
   each module's `…DbContext`/configuration first). Then re-run `auth.http`, `organisation.http`, `portal.http` 1–17,
   `users.http` 1–2, `customers.http` 1–6 and `tickets.http` 1–12 — each matches its (updated) comment.
9. **Seed in the UI:** sign in as `supervisor.b@crm.local` → the scope switcher lists RYD-CARE and JED-CARE; in each,
   assign a ticket and open Transfer (targets listed); as `agent.b` the Al-Noor customer is absent from the customer list;
   Admin → Users shows `agent.inactive@crm.local` as inactive.

---

## Named risks (the implementation session proves each before relying on it)

- **R1 — `useCookie` round-trip and timing.** Prove: after staff sign-in DevTools → Application → Cookies shows
  `crm-staff-session=true` with `Path=/`, `SameSite=Lax` and a 14-day expiry; after sign-out it is gone; the same for
  `crm-portal-session`. Writes made inside `nuxtApp.runWithContext` after an `await` must reach `document.cookie`
  (Nuxt writes client cookies from a watcher). **Fallback:** call `refreshCookie(name)` after the write, or write the
  hint with `document.cookie` behind `import.meta.client` in the store.
- **R2 — Server-side redirect from a global middleware.** Prove: `curl -s -o /dev/null -w "%{http_code} %{redirect_url}"
  --cookie "crm-staff-session=true" http://localhost:3000/` prints a 302 to `/agent`; without the cookie, 200 and the
  landing HTML; no hydration warning in the browser console on `/` (both locales). **Fallback:** if the server
  redirect misbehaves, drop the server branch and redirect on the client in `onMounted` of `pages/index.vue`
  (accepting a brief landing flash), keeping the hydration skip.
- **R3 — Hint and SameSite on the redirect.** `SameSite=Lax` cookies are sent on top-level navigations, so a link to the
  app from another site still sees the hint. Prove by opening `http://localhost:3000/` from an external page or the
  address bar while signed in.
- **R4 — Portal greeting hydration.** Prove: reload `/portal` signed in → no "Hydration ... mismatch" warning; the
  greeting appears after the session restores.
- **R5 — Docker stack.** The production build (`node .output/server/index.mjs` behind nginx on 8080) serves `/` server-
  side with the redirect and the landing, and the new pages render. Cookies are not `Secure` (the stack is http), as
  with `crm-scope`.
- **R6 — Sign-in rate limit during the walkthrough.** Every restore spends one `SignIn`/`PortalSignIn` permit
  (Context 24). Repeated reloads during the walkthrough may hit 429 in Development; wait out the window rather than
  changing the limits. This story reduces refresh calls (the landing makes none). `demo-accounts.http` signs in twelve
  times — split it into staff and portal halves with a one-minute pause, as `portal.http` does.
- **R7 — Seeded transitions and startup.** A seeder runs outside the MediatR pipeline, and an exception in it stops the
  API from starting. Prove first: build, start on a fresh database, and read the startup log; then start again. Check
  in particular `ReplyAsAgent` on an Open ticket (row 05 — it may append an activity or change the status itself; if it
  changes the status, drop the explicit call that becomes illegal) and `AssignTo` before `Resolve` (rows 07, 08, 14).
  **Fallback:** remove the failing method from that row and record the seeded status as it really is in the tables.
- **R8 — Seeding against an existing database.** Prove on the current development database (do not drop it first): the
  API starts, the new rows appear, no existing row changes (Ahmed still has one ticket on the portal; `auth.http` 1–8
  unchanged). The number generators continue from the existing maximum (customers, tickets) — record the actual numbers
  only in the walkthrough, never in the README (which says "fresh database").
- **R9 — Queue counts table.** The expected counts assume the mine/unassigned queues count working statuses only and
  "all" counts every status (Context 34, `TicketSearchSpecification.cs` line 24) and that "all" covers every department
  of the caller. Prove with `demo-accounts.http` on a fresh database; if a count differs, fix the table (README,
  `.http` comments), not the seed.
- **R10 — Seeding through the scope write guard.** The new rows are scoped (`IOrganisationScoped`); the seeders run with
  no HttpContext (`OrganisationScope.Unrestricted`, `CustomersSeeder.cs` lines 99–100). Prove the new RYD-BILL and
  JED-CARE rows save without `Organisation.OutOfScope`. No fallback that weakens the guard is acceptable — stop and
  report instead.
- **R11 — Portal visibility of seeded tickets.** Prove that Mona (primary contact) sees all seven Al-Noor tickets and
  Yousef only his own (Context 35), and that tickets with a customer-visible category show in the portal list. If the
  portal list hides a status (e.g. Cancelled), record what it shows in the table instead of changing the code.

---

## Verification Steps

1. **Frontend tooling:** `corepack enable pnpm --install-directory "$APPDATA/npm"` (pnpm is not on PATH on this machine),
   then `pnpm --dir frontend install --frozen-lockfile` (no package change; the lockfile must not change).
2. **Frontend builds:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` — all
   clean.
3. **Backend builds (regression):** `dotnet build backend/Crm.sln` — zero warnings, no `CRM0001`.
4. **Frontend runs:** `docker compose -f backend/docker-compose.yml up -d` (SQL Server, Redis), `dotnet run --project
   backend/src/Crm.Api --launch-profile http`, `pnpm --dir frontend dev`. Prove R1–R4, then Test Plan 3–6.
5. **Diff hygiene (AC):** from the repo root in Git Bash, over the changed files only:
   `git diff -U0 -- frontend/app | grep -nE "\b(ml|mr|pl|pr|left|right|text-left|text-right|border-l|border-r|rounded-l|rounded-r)-|#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\("`
   prints nothing; `git diff --stat` shows backend changes **only** in the eight seed files (`…DevSeed.cs` and
   `…Seeder.cs` in Organisation, Identity, Customers, Tickets) and under `backend/requests/` and `backend/README.md` —
   no migration, controller, DTO, domain or configuration file.
5b. **Seed:** prove R7, R8, R10, then Test Plan 8–9 (R9, R11).
6. **Docker:** `docker compose up -d --build`; http://localhost:8080 — Test Plan 7 (R5).
7. **Regression:** inbox tabs and counts, ticket detail, create drawer from the inbox, customer pages, admin pages,
   portal tickets list/new/detail, both sign-in pages' error messages (wrong password, 429).

---

## Done Criteria

- [ ] `/` shows the branded landing with "Staff sign-in" and "Customer portal" entry points, server-rendered
      (view-source), in `en` and `ar`; without branding it shows the product defaults.
- [ ] Visiting `/` signed in as staff lands on `/agent`; as a portal customer on `/portal`; signed out shows the landing
      (with no refresh call).
- [ ] `/agent` greets the user by name with the active scope, shows My tickets / Unassigned / All in scope counts equal
      to the inbox tab badges, and each card opens `/agent/tickets?view=…` on that view.
- [ ] `/agent` lists my newest 5 assigned tickets (each opening the ticket), or an empty state linking to Unassigned.
- [ ] Quick actions respect permissions: Agent A sees New ticket and Find a customer only; an admin also sees Branding
      and Users. New ticket opens the create drawer.
- [ ] Each count/list panel has a loading skeleton, an empty state, and an error state with retry that leaves the rest of
      the page working.
- [ ] Portal home greets the customer, shows the "New request" action, and an empty state for a customer with no
      requests.
- [ ] Staff sign-in → `redirect` or `/agent`; customer → `redirect` or `/portal`; staff sign-out → `/login`; customer
      sign-out → `/portal/login`. Staff login links to the customer portal; portal login links back to `/`. The brand
      mark links to `/`, `/agent` or `/portal` by layout.
- [ ] Walked in `en` and `ar` at 375, 768 and 1280 px; Verification step 5 grep prints nothing; the Docker stack on
      http://localhost:8080 serves the new pages.
- [ ] Every seeded account signs in and shows its case, or is refused (401) for `agent.inactive@crm.local` and
      `huda@alnoor.example.com` (`demo-accounts.http`); restarting the API duplicates no seed row (Test Plan 8); seeding
      runs only in Development.
- [ ] The root `README.md` "Demo accounts" and `backend/README.md` tables list all seven staff and five portal accounts
      (email, role, department/branch, what each demonstrates); the request-file comments match the new seed counts.
- [ ] `pnpm typecheck` / `lint` / `build` clean; `dotnet build` clean with no `CRM0001`; backend changes limited to the
      seed files, request files and `backend/README.md`; `home.json` pair added and the obsolete `shell` keys removed in
      both locales.
- [ ] Named risks R1–R11 recorded as proven (or their fallback) in the implementation report.
- [ ] No test project, spec file or test framework added.

**STOP HERE. Report to the user and wait for confirmation.**
