# Story 47 — Customer portal accounts ticket submission tracking and history

## Prerequisites

- **Stories 01–07, 11, 14, 15, 18 and 20 are implemented and committed** (latest `863b51b` "Implement Story 20").
  This story builds on:
  - Story 07's token stack — [`../identity-access/07-story-authentication-users-roles-and-permissions.md`](../identity-access/07-story-authentication-users-roles-and-permissions.md)
    lines 108–110 (coordination: "Story 47 adds a second JWT bearer scheme with its own audience for portal customers
    next to `CrmAuthenticationSchemes.Staff`, and a Portal-Area convention; it reuses `IAccessTokenIssuer`'s shape, the
    `RefreshTokenFamily` aggregate, and the refresh-cookie pattern" — this plan keeps the scheme, convention, issuer
    shape and cookie pattern but **does not reuse `RefreshTokenFamily`**, decision **D7**).
  - Story 11 — [`../customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md`](../customer-management/11-story-customer-aggregate-profiles-contact-details-and-notes.md)
    lines 97–101: "Story 47 must add its own, security-reviewed path … **do not** loosen `FindContactsByEmailQuery`".
  - Story 15 — [`../ticket-management/15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md`](../ticket-management/15-story-ticket-aggregate-lifecycle-taxonomy-and-assignment.md)
    lines 122–125: portal tickets use `TicketChannel.Portal` through **its own command**, only `IsCustomerVisible`
    categories (`CategoriesSpecification.customerVisibleOnly`), and a reviewed customer check.
  - Story 18 — [`../ticket-management/18-story-ticket-conversation-thread-messages-internal-notes-and-history.md`](../ticket-management/18-story-ticket-conversation-thread-messages-internal-notes-and-history.md)
    lines 97–100: the portal reads the thread **only through `ITicketThreadReadModel.ListPublicAsync`** and replies
    through `Ticket.ReceiveCustomerReply(body, contactId, actorId, now)`.
  - Story 20 — [`../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md`](../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md)
    line 96: the portal must not call `/agents` or `/transfer-targets`.
- **Modules.** The `Portal` Area is a persona, not a module (CLAUDE.md, module catalogue). This story touches:
  - **`Identity`** — new `PortalAccount` and `PortalSession` aggregates, portal sign-in/registration/refresh/sign-out,
    portal "me" and profile, the portal JWT scheme, one Agent endpoint to set a contact's initial portal password,
    one migration (`PortalAccounts`). New project reference `Identity.Application → Customers.Contracts`.
  - **`Customers`** — two new in-process, anonymous, **scope-lifted** sign-in lookups behind `ICustomersModuleApi`
    (`FindPortalContactsByEmailAsync`, `GetPortalContactAsync`). No command, no schema change.
  - **`Tickets`** — `Ticket` gains the `IPortalCustomerOwned` marker and two customer-facing lifecycle methods; a
    portal read model, four portal queries and four portal commands, two Portal controllers. **No schema change,
    no migration** (the marker is over the existing `CustomerId` column, already indexed).
  - **Building blocks** (security-relevant, reviewed here explicitly — see "Security design"): the portal principal
    (`PortalCustomerGrant` on `OrganisationScope`, `IPortalCustomerOwned` in the shared kernel, the scope filter and
    write guard extended for it), `CrmAuthenticationSchemes.Portal`, claim types, the `PortalCustomer` pseudo-role and
    two portal permissions, the Portal policy + convention, the Staff policy made audience-aware, the Portal rate
    limits, and one **tightening** of `AuthorizationBehavior`.
  - **Host** — `Program.cs` (convention, Swagger text), `appsettings*.json`.
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md` row 13).** Absorbs **48** (submission and tracking) and **49**
  (request history). **Deferred:** email/SMS verification, password reset, CAPTCHA, invitations by email, attachments,
  KB deflection and help centre (37–40), realtime, public tracking by code (27), announcements, exports, surveys (50),
  an explicit "account admin" flag on contacts (D4), portal account deactivation/listing for agents, audit-log entries
  (Story 10 is deferred), **any test suite**.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`);
  `Directory.Packages.props`: EF Core 10.0.11, MediatR **12.4.1** (licence pin — do not bump), FluentValidation
  12.1.1, Swashbuckle 10.2.3, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11. `frontend/package.json`: Nuxt
  **4.5.2**, Vue 3.5.43, `@nuxtjs/i18n` 10.6.0, `openapi-fetch` 0.17.0, `openapi-typescript` 7.13.0, TypeScript 5.9.3,
  pnpm **12.6.0**. **No new NuGet package. No new npm package. No new UI kit component** (so no new `/_dev/ui`
  section). This machine has **no Python** and **no `pnpm` on PATH** until `corepack enable pnpm --install-directory
  "$APPDATA/npm"`; Arabic text and `$` break Bash heredocs — use the **Write tool** for every file with Arabic in it
  (locale JSON, `.resx`, `.http`).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — `pnpm` is available.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, `pnpm
    --version` prints `12.6.0`, `pnpm --dir frontend install --frozen-lockfile` exits 0.
  - **R2 — cross-audience answers are 403, not 401.** The design (D1) relies on two facts that only a run proves:
    (a) `JwtBearerHandler` stamps `ClaimsIdentity.AuthenticationType` with `TokenValidationParameters.AuthenticationType`
    (set to `"Staff"` / `"Portal"`), and (b) a policy with **both** schemes merges the one successful principal and,
    when the assertion fails, **forbids** (403) instead of challenging. **Prove (right after B1 + B5 + B6, before any
    Tickets work):** `portal.http` 3a–3c (portal token → `GET /api/Agent/Me`, `GET /api/Agent/Tickets`,
    `GET /api/Admin/Users`) all **403 `urn:crm:problem:forbidden`**; 4a–4b (staff token → `GET /api/Portal/Me`,
    `GET /api/Portal/Tickets`) **403**; no token → **401**; `auth.http` 1–3 unchanged. **Fallback** (if (a) fails —
    `AuthenticationType` stays `"AuthenticationTypes.Federation"`): both issuers add a claim
    `CrmClaimTypes.PrincipalKind` (`"staff"` / `"portal"`), both assertions check that claim instead, and record that
    staff tokens issued before the deploy get 403 until they expire (≤ `AccessTokenLifetime`). Record which one shipped.
  - **R3 — the portal scope filter translates and staff SQL is unchanged.** **Prove:** EF SQL log for
    `portal.http` 6 shows `[t].[TenantId] = @… AND … [t].[CustomerId] = @…` on `[tickets].[Tickets]`; for
    `tickets.http` 1 (agent A inbox) the same filter shape plus `[t].[CustomerId] = @…` with the parameter
    `00000000-0000-0000-0000-000000000000` (never matches) and the same rows as before this story.
  - **R4 — `RefreshToken` as an owned type of two owners.** `PortalSession` owns `RefreshToken` (the class
    `RefreshTokenFamily` already owns) in its own table. **Prove:** `ef.ps1 -Module Identity migrations add
    PortalAccounts` succeeds and the migration creates `identity.PortalRefreshTokens` with its own unique
    `TokenHash` index and **does not touch** `identity.RefreshTokens`; `auth.http` 1–8 still pass. **Fallback:** copy
    `RefreshToken` to `PortalRefreshToken` (same members) and own that instead; record it.
  - **R5 — startup guard and DI.** Every new MediatR request carries exactly one authorisation attribute naming a
    known permission (`RequestAuthorizationGuard`). **Prove:** the API starts after B8.
  - **R6 — the portal refresh cookie reaches the API through the Nuxt dev proxy.** `crm_portal_refresh`, path
    `/api/Portal/Auth`, `Secure`, `SameSite=Strict`, like `crm_refresh` (Story 07, R6). **Prove:** portal sign-in in the
    browser → reload `/portal` → DevTools shows one `POST /api/Portal/Auth/refresh` with the cookie sent → 200.
  - **R7 — the client guard runs on SSR portal pages.** `middleware/auth.global.ts` returns early on the server; on
    the first (server-rendered) load of `/portal/…` the middleware must also run on the client during hydration.
    **Prove:** signed out, open `http://localhost:3000/portal` directly → lands on `/portal/login?redirect=/portal`;
    signed in, reload `/portal/tickets/<id>` → stays, no hydration-mismatch warning in the console. **Fallback:** a
    `usePortalGuard()` composable called in `onMounted` of every protected portal page doing the same restore/redirect.
  - **R8 — the Portal-Area global limiter sees the authenticated user.** `UseRateLimiter()` runs after
    `UseAuthorization()` (`Program.cs` lines 135–139), which replaces `HttpContext.User` with the portal principal.
    **Prove:** Development log for a portal request shows no limiter rejection at 60 calls from one IP by two different
    accounts; `portal.http` 30 (11 bad logins) → the 11th is **429** with `Retry-After`.
  - **R9 — `JwtSettings` binds the new optional `PortalAudience`.** **Prove:** the API starts; removing
    `Identity:Jwt:PortalAudience` from `appsettings.json` makes it refuse to start with the `Identity:Jwt:PortalAudience`
    message (restore it).
  - **R10 — the Portal Swagger document generates.** Two `AuthController`/`MeController`/`TicketsController` classes
    exist (Agent and Portal namespaces). **Prove:** `GET /swagger/portal/swagger.json` → 200 with 13 paths; `GET
    /swagger/agent/swagger.json` → 200 and now contains `/api/Agent/PortalAccounts`.

---

## Story Goal

A customer contact with portal access can sign in to the branded portal, raise a ticket, follow it, talk to support,
close it and reopen it — and can never see anything that is not theirs.

When this story is done:

1. `POST /api/Portal/Auth/register` binds a new portal account to the one portal-enabled contact with that email;
   any other case (unknown email, email already registered, ambiguous) answers the **identical 204** and creates
   nothing. `login`, `refresh`, `logout`, `GET/PUT /api/Portal/Me`, `POST /api/Portal/Me/change-password` complete the
   account. Login, refresh and registration are rate limited per IP (429 + `Retry-After`); failed logins lock the
   account (per-account limit).
2. An agent with `customers.write` sets a portal-enabled contact's initial password (`POST /api/Agent/PortalAccounts`)
   from the customer page.
3. A portal token gets **403** on every Agent/Admin endpoint; a staff token gets **403** on every Portal endpoint.
4. `GET /api/Portal/TicketCategories` lists the active, customer-visible categories of the customer's department.
   `POST /api/Portal/Tickets` creates a ticket with channel **Portal** in the customer's owning department; it appears
   in the agent inbox.
5. `GET /api/Portal/Tickets` lists only the caller's tickets (all of the customer's tickets for the customer's
   primary contact — the account admin, D4), with customer-friendly statuses, Open/Closed tabs, a status filter and
   search. Another customer's ticket id is **404** on every portal endpoint.
6. `GET /api/Portal/Tickets/{id}` and `…/thread` show the ticket and its **public messages only**; `…/replies`,
   `…/request-closure` (resolves) and `…/reopen` (Resolved only; 422 on Closed) act on it.
7. SSR pages on the `portal` layout: `/portal/login`, `/portal/register`, `/portal` (my tickets), `/portal/tickets/new`,
   `/portal/tickets/[id]`, `/portal/account` — bilingual, RTL, mobile first.
8. `backend/requests/portal.http` exercises every endpoint, including cross-customer attempts and the leak checks.

**Not in scope:** see Prerequisites, "MVP scope".

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modules talk only through Contracts", "Scoping" (the one permitted lift, the write guard, Identity's
   hand-written tenant filter), "API surface" (Areas, `StaffAreaAuthorizationConvention`, `[AllowAnonymous]` rule),
   "Pipeline behaviours" (the three authorisation attributes), "Localization keys", "Time", "Frontend". B11 edits it.
2. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Abstractions/OrganisationScope.cs` — the constructor
   (lines 23–65: `Departments`/`Branches`/`EffectiveRoles` derivation), `Allows` (94–114, "the one place the
   three-column rule is written in C#"), `ResolveWriteTarget` (119–148). B1 adds the portal grant here.
3. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.OrganisationScope.cs` —
   the whole file (122 lines): scope properties read per query (20–28, "never cache these in fields"),
   `ApplyOrganisationScopeFilters` (42–58), `FilterOrganisationScoped` (60–65), `EnsureWritesInScope` (74–98),
   `WasInScopeBeforeChange` (102–113). B1 adds one filter method and nothing else.
4. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs` — `Resolve` (lines
   50–97): tenant claim, membership claims, `X-Crm-Scope`. The per-principal cache (11–14, 38–46) is why the
   principal swap done by the authorisation middleware is safe.
5. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Authorization/CrmPermissions.cs` (lines 10–36) and
   `…/Abstractions/CrmRoles.cs`, `…/Abstractions/CrmClaimTypes.cs`, `…/Abstractions/CrmAuthenticationSchemes.cs`.
6. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/AuthorizationBehavior.cs` — lines 22–46 (the
   decision order: anonymous/unrestricted → 401 → permission → next).
7. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/Authorization/CrmPolicies.cs` (whole, 18 lines) and
   `StaffAreaAuthorizationConvention.cs` (whole, 30 lines).
8. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/RateLimiting/RateLimitingServiceCollectionExtensions.cs`
   — `CrmRateLimitPolicies` (line 17), settings reads (31–36), `GlobalLimiter` (43–53), `AddPolicy` (54–56), `OnRejected`.
9. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Specifications/Specification.cs` — lines 1–10 and 55–58
   (the lift is a security review); `…/Infrastructure/Persistence/Repositories/Repository.cs` — **`GetByIdAsync` /
   `FirstOrDefaultAsync` throw `OrganisationScopeViolationException` (→ 403) on a miss when the row exists out of
   scope** (`ThrowIfOutOfScopeAsync`); `ListAsync`/`CountAsync` never throw. This is why D11 exists.
10. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Application/Specifications/MembershipsOfUserSpecification.cs`
    and `Queries/GetMembershipsOfUser/GetMembershipsOfUserQuery.cs` — the precedent for B2 (anonymous in-process
    sign-in lookup + `IgnoreOrganisationScope()`).
11. `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/ICustomersModuleApi.cs` (whole; line 21 is the
    `FindContactByEmailAsync` the portal must **not** use), `Crm.Modules.Customers.Application/CustomersModuleApi.cs`,
    `Queries/FindContactsByEmail/*` (the in-memory contact mapping to copy), `Specifications/CustomersWithContactEmailSpecification.cs`,
    `Crm.Modules.Customers.Domain/Contact.cs` (`HasPortalAccess`, `IsPrimary`; portal access requires an email, line
    62–65 of the file), `Customer.cs` (`IsActive`, `PreferredLanguage`, scope columns).
12. `backend/src/Modules/Identity/` — `Crm.Modules.Identity.Domain/User.cs` (the "not ITenantScoped on purpose"
    remark, lockout, password methods), `RefreshTokenFamily.cs` (`Start`, `Rotate` line 61, `Revoke` line 97),
    `RefreshToken.cs`, `EmailAddress.cs`, `DisplayName.cs`, `PreferredLocale.cs`, `UserEvents.cs`;
    `Crm.Modules.Identity.Application/Commands/SignIn/SignInCommandHandler.cs` (the timing-safe refusal order, lines
    25–97), `Commands/RefreshSession/*`, `Commands/SignOut/*`, `Commands/ChangePassword/*`, `Queries/GetMe/*`,
    `DomainEventHandlers/*` (three handlers), `Abstractions/IAccessTokenIssuer.cs`, `AccessTokenSubjectFactory.cs`,
    `Dtos.cs`, `IdentityErrors.cs`, `Validation/IdentityRules.cs`, `Localization/IdentityMessageKeys.cs`;
    `Crm.Modules.Identity.Infrastructure/IdentityModule.cs` (repositories lines 31–32, issuer line 57, staff scheme
    60–79), `Security/JwtAccessTokenIssuer.cs` (`Issue` line 20), `Security/JwtSettings.cs`,
    `Persistence/Configurations/*.cs`, `Persistence/IdentityDbContext.cs`, `Persistence/IdentitySeeder.cs` (lines
    33–39); `Crm.Modules.Identity.Presentation/Controllers/Agent/AuthController.cs` (whole — `ToSessionResponse` line
    70), `MeController.cs`, `RefreshCookie.cs`.
13. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/Ticket.cs` — class line 12, `Create` 117, `AcceptsReplies`
    155, the transition table (25–35), `Resolve` 185, `Reopen` 197 (allowed from Resolved **and Closed**),
    `ReceiveCustomerReply` 343, `EnsureAllowed` 391; `TicketDomainErrors.cs` line 37 (`TransitionNotAllowed`).
14. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/` — `Abstractions/ITicketThreadReadModel.cs`
    (`ListPublicAsync`: "the ONLY method a customer-facing query may call"), `Abstractions/ITicketReadModel.cs`,
    `Commands/CreateTicket/CreateTicketCommandHandler.cs` (event publishing after the assembler), `Commands/ReceiveCustomerReply/*`,
    `Commands/TransitionTicket/*`, `Queries/GetTicketThread/GetTicketThreadQueryHandler.cs` (note it calls
    `GetByIdAsync` to get a 403 — the portal must not), `Queries/SearchTickets/SearchTicketsQueryHandler.cs` (page +
    count shape), `Specifications/CategoriesSpecification.cs`, `Specifications/TicketSearchSpecification.cs` (one
    combined `Where`), `TicketRefusals.cs`, `TicketsErrors.cs`, `CurrentActor.cs`, `Dtos.cs`,
    `Localization/TicketsMessageKeys.cs`, `Validation/TicketsRules.cs`.
15. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Infrastructure/Persistence/TicketThreadReadModel.cs`
    (`ListPublicAsync` lines 45–61), `TicketReadModel.cs`, `TicketsModule.cs` (line 32);
    `Crm.Modules.Tickets.Presentation/Controllers/Agent/TicketsController.cs`, `Controllers/Agent/TicketCategoriesController.cs`.
16. `backend/src/Crm.Api/Program.cs` — lines 33–36 (default scheme, authorisation), 53 (convention), 78–88 (Swagger
    security text), 104–139 (middleware order). `appsettings.json` / `appsettings.Development.json` (`Identity`,
    `RateLimiting`).
17. `backend/requests/auth.http` (style, seeded users), `backend/requests/ticket-thread.http` (login blocks, chaining),
    `backend/requests/customers.http` (contact update payloads, rowVersion chaining).
18. Frontend: `frontend/app/api/crm-fetch.ts` (lines 4–8 options, 45–74 the retry contract — **do not change the
    retry logic**), `app/composables/useAuthSession.ts` (whole), `app/plugins/api.ts` (lines 12–24), `app/plugins/auth.client.ts`
    (lines 7–12), `app/stores/auth.ts` (the template for the portal store: `restoring` single-flight, `runWithContext`
    after `await`), `app/middleware/auth.global.ts`, `app/layouts/portal.vue`, `app/pages/login.vue` (safe redirect,
    inline 401/429), `app/pages/agent/account.vue` (password form), `app/composables/tickets/useTicketThread.ts`
    (`useAsyncData` + cursor), `app/components/ticket/TicketThread.vue` (lines 1–59 template, 158–178 author helpers),
    `app/utils/ticketThread.ts` (`personName`), `app/utils/tickets.ts` (`ticketStatusTone`, code→field maps),
    `app/utils/permissions.ts`, `app/components/customer/CustomerContactsPanel.vue` (lines 1–80),
    `app/components/customer/CustomerContactModal.vue`, `app/components/ui/{Tabs,Select,Pagination,Textarea,Input,Modal,Badge,EmptyState,ErrorState,Skeleton}.vue`
    (props), `nuxt.config.ts` (`routeRules`: `/portal/**` stays SSR), `eslint.config.mjs` lines 37–41 (bare strings),
    `scripts/check-locales.ts`, `i18n/locales/en/{tickets,customers,errors,identity}.json`.

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **D1 — Portal principal = its own audience and scheme.** Portal access tokens are issued by Identity with the same
  issuer (`crm`) and signing key as staff tokens but audience **`crm-portal`** (`Identity:Jwt:PortalAudience`), and are
  validated by a second JWT bearer scheme `CrmAuthenticationSchemes.Portal` whose `TokenValidationParameters.AuthenticationType`
  is `"Portal"` (the staff scheme's becomes `"Staff"`). The **Staff** policy and the new **Portal** policy each
  authenticate with **both** schemes and assert the identity's `AuthenticationType`, so a token of the other audience
  is *authenticated but forbidden* → **403**; no token → 401. Portal tokens carry **no** `membership`/`role` claims.
- **D2 — Customer-principal scope, narrow and explicit.** A portal principal's `OrganisationScope` has the tenant,
  **no memberships, no departments, no branches**, `EffectiveRoles = ["PortalCustomer"]`, and a `PortalCustomerGrant`
  (customer id, contact id, account-admin flag, the customer's home branch/department). The scope filter and write
  guard gain exactly one extra clause: a row implementing the new shared-kernel marker **`IPortalCustomerOwned`** is
  visible and writable when its tenant is the grant's tenant **and** its `CustomerId` equals the grant's customer id.
  **Only `Ticket` implements the marker.** Every other `IOrganisationScoped` row (customers, …) stays invisible to a
  portal principal; `ITenantScoped` rows (categories) are tenant-visible, as for any principal of the tenant.
  Adding an implementer of `IPortalCustomerOwned` is a security review (same rule as `ITransferableOrganisationScoped`).
- **D3 — Two new sign-in lookups lift the scope, in Customers only.** `FindPortalContactsByEmailQuery` and
  `GetPortalContactQuery` (`[AllowAnonymousRequest]`, in-process only, no HTTP endpoint) use a new
  `PortalContactsSpecification` that calls `IgnoreOrganisationScope()` — callers 2 of the lift. They return **only**
  contacts with portal access of **active** customers, and only the fields sign-in needs. `FindContactsByEmailQuery`
  (Story 11) is untouched and unused by the portal.
- **D4 — Account admin = the customer's primary contact.** No new flag: `Contact.IsPrimary` at sign-in/refresh time
  decides it. The account admin sees every ticket of the customer; any other contact sees only tickets whose
  `ContactId` is theirs. An explicit "portal account admin" flag is a later story.
- **D5 — Registration never reveals anything.** `register` answers **204 No Content** in every non-400/429 case. It
  creates an account only when (a) no portal account uses the email, (b) exactly **one** portal-enabled contact of an
  active customer has the email (across tenants), and (c) that contact has no portal account yet. The password is
  hashed on every path (equal cost). The customer then signs in; there is no verification step (no channels).
- **D6 — `PortalAccount` lives in Identity and is not `ITenantScoped`** (like `User`: sign-in reads and writes it
  before a scope exists). Its email is **globally unique**; one account per contact (unique `ContactId`). The agent
  endpoint compares `TenantId` explicitly (the Customers summary lookup already enforces the agent's scope).
- **D7 — Portal sessions are a separate aggregate `PortalSession`** (tables `PortalSessions` + `PortalRefreshTokens`),
  not `RefreshTokenFamily` rows: a portal refresh token presented to the staff refresh endpoint (or vice versa) is
  simply unknown there, and `RefreshTokenFamily.UserId` keeps meaning a staff user. It mirrors
  `RefreshTokenFamily.Start/Rotate/Revoke` exactly (rotation, reuse detection revokes the session).
- **D8 — Actor ids.** In Tickets, a portal action's actor (activity `ActorId`, message `AuthorId`/`RecordedById`) is
  the **contact id** from the grant — "the customer acted". The portal account id (`sub`) lands in the audit columns
  (`CreatedBy`/`ModifiedBy`) and logs. The agent UI names that contact (F8).
- **D9 — Customer-friendly statuses and tabs.** Portal status = `Received` (New), `InProgress` (Open, OnHold),
  `AwaitingYourReply` (PendingCustomer), `Resolved` (Resolved), `Closed` (Closed, Cancelled). Tab **open** = New,
  Open, PendingCustomer, OnHold; tab **closed** = Resolved, Closed, Cancelled. Search matches ticket number or subject.
  Newest activity first (`ModifiedAtUtc` desc, then number).
- **D10 — Customer actions.** `Reply` while the ticket accepts replies (not Closed/Cancelled); `RequestClosure` in the
  working statuses → **Resolved** (`Ticket.RequestClosure`); `Reopen` **only from Resolved** →
  Open (`Ticket.ReopenAtCustomerRequest`; the agent's `Reopen` from Closed is unchanged). A customer reply to a Resolved
  or PendingCustomer ticket reopens it (Story 18 rule, unchanged). Portal commands take **no rowVersion**; a
  simultaneous agent edit is a 409 `Persistence.ConcurrencyConflict` the portal shows as "try again".
- **D11 — Not yours = 404, everywhere.** Portal handlers load tickets **only** through `ListAsync`/`CountAsync` or the
  portal read model with a `PortalTicketsSpecification` (customer + contact criteria); **never `GetByIdAsync` or
  `FirstOrDefaultAsync` on `Ticket`** (they turn a miss on an existing row into 403, which would confirm another
  customer's ticket exists). Unknown, other-customer and other-contact (non-admin) ids are all 404 `Tickets.TicketNotFound`.
- **D12 — Internal data is structurally unreachable.** Portal DTOs are separate records with no internal field
  (no assignee, department, branch, priority, rowVersion, notes, activities, staff ids). The portal thread query calls
  `ITicketThreadReadModel.ListPublicAsync` only; outbound messages show "Support team", inbound "You"/"Your
  colleague" — no staff names or ids.
- **D13 — `[RequireAuthenticatedUser]` means staff.** `AuthorizationBehavior` answers 403 `Authorization.StaffOnly` to a
  portal principal on a `[RequireAuthenticatedUser]` request (the attribute's doc already says "Any signed-in staff
  user"). Portal requests declare `[RequirePermission(PortalTickets | PortalAccount)]`. Staff roles never hold portal
  permissions (the Administrator map changes from `All` to the staff list).
- **D14 — Rate limits.** Global limiter extended to the **Portal Area**: fixed window per account (`sub`) when signed
  in, per IP otherwise (`RateLimiting:Portal`). Named policy **`portal-sign-in`** per IP on register, login and refresh
  (`RateLimiting:PortalSignIn`). Per account: the staff `LockoutPolicy` (5 failures → locked for the window).
- **D15 — Grant freshness.** Portal access revoked on the contact, a deactivated customer, or a changed primary
  contact take effect at the next login/refresh (the session is revoked on refresh), i.e. within `AccessTokenLifetime`.
- **D16 — Profile.** Display name and preferred language live on `PortalAccount` (initialised from the contact name and
  the customer's preferred language). They are **not** written back to the contact.
- **D17 — SSR + client data.** `/portal/**` pages are server-rendered with the portal layout; the session and every
  data call run in the browser (`useAsyncData(..., { server: false })`). The SSR server never holds a session.
- **D18 — One frontend session per audience.** `createCrmFetch` picks the portal session for `/api/Portal/*` requests
  and the staff session otherwise; a portal 401 that cannot refresh goes to `/portal/login`. The retry contract is
  unchanged. `X-Crm-Scope` is never sent on a portal request (the backend ignores it for portal principals).
- **D19 — Agent provisioning.** `POST /api/Agent/PortalAccounts` (`customers.write`) creates the portal account of a
  portal-enabled contact of a customer **in the agent's scope** (checked through `ICustomersModuleApi.GetCustomerSummaryAsync`,
  so 403/404 come from Customers), or — when the contact already has one — sets its password, clears its lockout and
  revokes its sessions. The agent tells the customer the password out of band (no channels in the MVP).
- **Error codes (frontend `errors.code.*` contract; never rename):** `Identity.PortalContactNotFound` (404),
  `Identity.PortalAccessNotEnabled` (422), `Identity.PortalCustomerInactive` (422), `Identity.PortalEmailInUse` (409),
  `Authorization.StaffOnly` (403), `Authorization.PortalOnly` (401, a request without a grant),
  `Tickets.TicketNotFound`/`Tickets.CategoryNotFound`/`Tickets.TransitionNotAllowed` (existing).

---

## Security design (named security risks — the reviewer reads this section first)

- **S1 — A customer-principal scope in the shared building blocks** (`OrganisationScope`, `ModuleDbContext` filter,
  write guard). *Narrowing:* the grant exists only when the authenticated identity came from the **Portal** scheme
  (`AuthenticationType == "Portal"`) and every portal claim parses; otherwise `OrganisationScope.None`. The extra filter
  clause applies only to `IPortalCustomerOwned` types (one: `Ticket`), compares `CustomerId` to a context property that
  is `Guid.Empty` for every staff/anonymous/system caller (no ticket has `Guid.Empty` — `Ticket.Create` refuses it),
  and keeps the tenant condition. The write guard reuses `Allows` — one C# rule, one SQL rule, kept equivalent in the
  same two files as today. `IsUnrestricted` is never true for a portal principal. *Residual risk:* a future aggregate
  that implements `IPortalCustomerOwned` without review; the marker's XML doc and CLAUDE.md say it is a security review.
- **S2 — A second scope lift, cross-tenant, anonymous** (Customers' `PortalContactsSpecification`). *Narrowing:*
  in-process only (no controller), `[AllowAnonymousRequest]`, soft-delete filter kept, hard criteria `HasPortalAccess &&
  customer Active`, at most 20 rows, returns ids + name + email + primary flag + language + scope ids only; Identity binds
  an account only on exactly one match (D5). *Residual risk:* the same email on two portal-enabled contacts blocks
  registration for both (logged as `ambiguous`), by design.
- **S3 — Shared signing key.** Audience separation relies on `ValidateAudience = true` (on in both schemes) plus the
  `AuthenticationType` assertion. A leaked key compromises both audiences equally — no worse than today. A separate
  `PortalSigningKey` is a deliberate non-goal (config friction); revisit when the portal is exposed on its own host.
- **S4 — Tenant-scoped rows are visible to a portal principal of that tenant** (e.g. `Category`, Organisation's
  tenant-scoped rows). *Narrowing:* only Portal endpoints are reachable with a portal token (policy), every staff
  request is refused by permission (portal role grants none) or by D13. Portal handlers read categories through
  `customerVisibleOnly: true` of the home department only.
- **S5 — Stale grant inside a live access token** (D15): ≤ `AccessTokenLifetime` (15 min; 2 min in Development).
- **S6 — Enumeration.** Registration: identical 204, equal hashing cost, per-IP limit. Login: the staff refusal order
  (dummy hash on unknown/locked), generic 401 `Identity.InvalidCredentials`. Ticket ids: 404 for all not-yours (D11).
- **S7 — Leaks by reuse.** Portal DTOs and read-model rows are separate types with no internal fields (D12); the
  `TicketDtoAssembler` (which carries assignee, people, activities, rowVersion) is never used by a portal handler.

---

## Backend Tasks

Order: **B1 → B2 → B3 → B4 → B5 → B6 → prove R2/R4/R5/R9 → B7 → B8 → B9 → prove R3/R10 → B10 → B11 → B12**.

### B1 — Building blocks: the portal principal

**File: `backend/src/BuildingBlocks/Crm.SharedKernel/Scoping/ITenantScoped.cs`** — append:

```csharp
/// <summary>Story 47: an organisation-scoped row that belongs to exactly one customer. A portal customer principal
/// (OrganisationScope.PortalCustomer) sees and may write such a row of its own tenant whose CustomerId equals its
/// grant's customer id - nothing else of the module. Implemented by Ticket only. CustomerId must be set once, never
/// Guid.Empty, and never change. Adding an implementer is a security review.</summary>
public interface IPortalCustomerOwned : IOrganisationScoped
{
    Guid CustomerId { get; }
}
```

**File: `…/Crm.BuildingBlocks.Application/Abstractions/CrmAuthenticationSchemes.cs`** — add
`public const string Portal = "Portal";` (doc: "Portal customer JWT bearer (Portal Area). Story 47.") and fix the Staff
doc comment ("Story 47 adds Portal" → "Agent + Admin Areas").

**File: `…/Abstractions/CrmClaimTypes.cs`** — add, with a doc line "Portal tokens only (Story 47); a staff token never
carries them":

```csharp
public const string PortalCustomerId = "customer_id";
public const string PortalContactId = "contact_id";
public const string PortalAccountAdmin = "portal_admin";      // "true" | "false"
public const string PortalBranchId = "portal_branch";         // the customer's home branch
public const string PortalDepartmentId = "portal_department"; // the customer's home (owning) department
```

**File: `…/Abstractions/CrmRoles.cs`** — add `public const string PortalCustomer = "PortalCustomer";` with doc "Pseudo-role
of every portal principal (Story 47). Never a membership role, never in a staff token."

**File: `…/Authorization/CrmPermissions.cs`** — replace lines 10–36 content so that:

```csharp
public const string PortalTickets = "portal.tickets";
public const string PortalAccount = "portal.account";

public static readonly IReadOnlyList<string> Staff =
    [TicketsRead, TicketsWrite, TicketsAssign, CustomersRead, CustomersWrite, AdminOrganisation, AdminUsers, AdminBranding];

public static readonly IReadOnlyList<string> All = [.. Staff, PortalAccount, PortalTickets];

// ByRole: Administrator = Staff (NOT All - a staff token must never satisfy a portal request, D13);
//         PortalCustomer = [PortalAccount, PortalTickets].
```

Keep `ForRoles` and `IsKnown` unchanged. Grep `CrmPermissions.All` (only line 28 uses it today) after the change.

**File: `…/Abstractions/OrganisationScope.cs`** — add, above the class:

```csharp
/// <summary>Story 47: what a portal customer principal may reach. Built only by CurrentUserService from a Portal-scheme
/// token. HomeBranchId/HomeDepartmentId = the customer's owning branch/department (where its new tickets go).</summary>
public sealed record PortalCustomerGrant(
    Guid CustomerId, Guid ContactId, bool IsAccountAdmin, BranchId HomeBranchId, DepartmentId HomeDepartmentId);
```

Inside the class:
- `public PortalCustomerGrant? PortalCustomer { get; }` and `public bool IsPortalCustomer => PortalCustomer is not null;`
  (the public constructor sets `PortalCustomer = null`).
- `public static OrganisationScope ForPortalCustomer(TenantId tenant, PortalCustomerGrant grant)` calling a new
  **private** constructor that sets `IsUnrestricted = false`, `Tenant = tenant`, `Memberships = []`,
  `ActiveDepartment = null`, `ActiveScopeRejected = false`, `Departments = []`, `Branches = []`,
  `Roles = EffectiveRoles = [CrmRoles.PortalCustomer]`, `PortalCustomer = grant`. Throw `ArgumentException` when
  `tenant == TenantId.None` or `grant.CustomerId == Guid.Empty` or `grant.ContactId == Guid.Empty`.
- `Allows` (line 94): after the tenant check (line 103–106) and **before** `if (row is not IOrganisationScoped scoped)`,
  insert:

```csharp
if (row is IPortalCustomerOwned owned && PortalCustomer is { } grant)
{
    return owned.CustomerId == grant.CustomerId; // Story 47 (D2): a portal principal reaches its own customer's rows only
}
```

  Update the method comment: "the C# twin of both SQL rules in ModuleDbContext.OrganisationScope.cs".
- `ResolveWriteTarget` is unchanged (a portal principal has no departments → `null`; portal handlers never call it).

**File: `…/Crm.BuildingBlocks.Infrastructure/Persistence/ModuleDbContext.OrganisationScope.cs`**:
- Add next to line 28: `private Guid ScopePortalCustomerId => Scope.PortalCustomer?.CustomerId ?? Guid.Empty;`
- In `ApplyOrganisationScopeFilters` (lines 46–48) choose `nameof(FilterPortalCustomerOwned)` first when
  `typeof(IPortalCustomerOwned).IsAssignableFrom(entityType.ClrType)`.
- Add (after line 65):

```csharp
// Story 47 (D2): the organisation rule OR "the portal principal's own customer". ScopePortalCustomerId is Guid.Empty
// for every non-portal caller, and no IPortalCustomerOwned row has Guid.Empty, so staff reads are unchanged.
private void FilterPortalCustomerOwned<T>(ModelBuilder modelBuilder) where T : class, IPortalCustomerOwned =>
    modelBuilder.Entity<T>().HasQueryFilter(ScopeFilterName, (T e) =>
        ScopeIsUnrestricted
        || (e.TenantId.Equals(ScopeTenantId)
            && ((ScopeBranchIds.Contains(e.BranchId) && ScopeDepartmentIds.Contains(e.DepartmentId))
                || e.CustomerId == ScopePortalCustomerId)));
```

- `EnsureWritesInScope` needs no change (it calls `Scope.Allows`). Prove R3.

**File: `…/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs`** — in `Resolve` (line 50), after the
tenant parse (line 64):

```csharp
bool portal = user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == CrmAuthenticationSchemes.Portal);
if (portal)
{
    // Never mixed with staff claims; X-Crm-Scope is ignored (a portal principal has no departments to narrow).
    return user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType != CrmAuthenticationSchemes.Portal)
        ? OrganisationScope.None
        : ResolvePortal(user, tenant);
}
```

and a private static `ResolvePortal(ClaimsPrincipal user, TenantId tenant)` that parses the five `CrmClaimTypes.Portal*`
claims (`Guid.TryParse` ×4, `bool.TryParse`); **any** missing/malformed claim → `OrganisationScope.None` (never throws);
otherwise `OrganisationScope.ForPortalCustomer(tenant, new PortalCustomerGrant(...))`. The staff path (lines 66–96) is
unchanged. Update the class comment ("Story 47: a Portal-scheme identity resolves to a customer grant").

**File: `…/Crm.BuildingBlocks.Application/Behaviors/AuthorizationBehavior.cs`** — between the 401 check (line 32–36)
and the permission check (line 38), insert (D13):

```csharp
if (Requirement.Permission is null && scope.IsPortalCustomer)
{
    LogStaffOnly(typeof(TRequest).Name, currentUser.UserIdOrSystem);
    return CreateFailureResponse(Error.Forbidden("Authorization.StaffOnly", "This operation is for staff users."));
}
```

plus `[LoggerMessage(Level = LogLevel.Warning, Message = "Portal principal {UserId} refused staff-only {RequestName}")]
private partial void LogStaffOnly(string requestName, string userId);`. Update the summary comment.

**File: `…/Crm.BuildingBlocks.Application/Authorization/RequestAuthorizationAttributes.cs`** — `RequireAuthenticatedUser`
doc: "Any signed-in staff user; no permission. A portal principal is refused (403 Authorization.StaffOnly, Story 47)."
`AllowAnonymousRequest` doc: add "Story 47: the portal sign-in lookups in Customers".

**File: `…/Crm.BuildingBlocks.Application/Specifications/Specification.cs`** — lines 9 and 55–57: "used only by the
sign-in lookups: Organisation's `MembershipsOfUserSpecification` (Story 06) and Customers' `PortalContactsSpecification`
(Story 47)". Same edit in `ModuleDbContext.OrganisationScope.cs` line 16 and in `MembershipsOfUserSpecification.cs`
line 6 ("THE ONLY CALLER" → "One of the two callers").

**File: `…/Crm.BuildingBlocks.Presentation/Authorization/CrmPolicies.cs`** — replace the body:

```csharp
public const string Staff = "Staff";
public const string Portal = "Portal";

public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
    services.AddAuthorizationBuilder()
        // Both schemes authenticate, so a token of the OTHER audience is authenticated-but-forbidden (403, D1).
        .AddPolicy(Staff, policy => policy
            .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => HasIdentity(context.User, CrmAuthenticationSchemes.Staff)))
        .AddPolicy(Portal, policy => policy
            .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => HasIdentity(context.User, CrmAuthenticationSchemes.Portal)))
        .Services;

private static bool HasIdentity(ClaimsPrincipal user, string scheme) =>
    user.Identities.Any(i => i.IsAuthenticated && string.Equals(i.AuthenticationType, scheme, StringComparison.Ordinal));
```

**Create file: `…/Crm.BuildingBlocks.Presentation/Authorization/PortalAreaAuthorizationConvention.cs`** — a copy of
`StaffAreaAuthorizationConvention` for `CrmAreas.Portal` that adds `new AuthorizeAttribute(CrmPolicies.Portal)`;
summary "Every Portal endpoint requires a signed-in portal customer; [AllowAnonymous] on an action (register, login,
refresh, logout) wins (Story 47)." Update `StaffAreaAuthorizationConvention`'s summary ("Portal: PortalAreaAuthorizationConvention").

**File: `…/Crm.BuildingBlocks.Presentation/RateLimiting/RateLimitingServiceCollectionExtensions.cs`**:
- `CrmRateLimitPolicies` (line 17): add `public const string PortalSignIn = "portal-sign-in";`.
- Read two more sections (next to lines 31–36), same exception style: `RateLimiting:Portal` and `RateLimiting:PortalSignIn`.
- `GlobalLimiter` (lines 43–53): switch on the endpoint's area — Public as today; **Portal** →
  `RateLimitPartition.GetFixedWindowLimiter($"portal:{context.User.FindFirst(CrmClaimTypes.UserId)?.Value ?? context.Connection.RemoteIpAddress?.ToString()}", …portal settings…)`;
  otherwise no limiter. Keep `QueueLimit = 0`.
- `AddPolicy(CrmRateLimitPolicies.PortalSignIn, …)` per IP with the `PortalSignIn` settings, key `portal-sign-in:{ip}`.
- Update the summary: "Public and Portal Areas are limited globally; Portal per account when signed in (Story 47)."

### B2 — Customers: the portal sign-in lookups (D3)

**File: `backend/src/Modules/Customers/Crm.Modules.Customers.Contracts/ICustomersModuleApi.cs`** — add to the interface
(after line 21) and a new record:

```csharp
/// <summary>Story 47, in-process sign-in lookup (no HTTP endpoint, anonymous, scope lifted - reviewed): every contact
/// WITH PORTAL ACCESS of an ACTIVE customer, in any tenant, whose normalised email equals <paramref name="email"/>. A
/// malformed email is an empty list. At most 20.</summary>
Task<Result<IReadOnlyList<PortalContactMatch>>> FindPortalContactsByEmailAsync(string email, CancellationToken cancellationToken = default);

/// <summary>Story 47, in-process sign-in/refresh lookup: the contact if it still has portal access and its customer is
/// active; otherwise a failed Result (404 Customers.PortalContactNotFound).</summary>
Task<Result<PortalContactMatch>> GetPortalContactAsync(Guid contactId, CancellationToken cancellationToken = default);

public sealed record PortalContactMatch(
    Guid CustomerId, Guid ContactId, string ContactName, string Email, bool IsPrimary, string PreferredLanguage,
    Guid TenantId, Guid BranchId, Guid DepartmentId);
```

Also amend `FindContactByEmailAsync`'s doc (line 20): "Staff only (customers.read, caller's scope). The portal uses
FindPortalContactsByEmailAsync."

**Create file: `…/Crm.Modules.Customers.Application/Specifications/PortalContactsSpecification.cs`**:

```csharp
/// <summary>ONE OF THE TWO CALLERS of Specification.IgnoreOrganisationScope() (Story 47, S2): portal sign-in runs before
/// a scope exists. Only ACTIVE customers with a PORTAL-ENABLED contact matching the email or the contact id; soft
/// delete stays. At most 20, read-only.</summary>
public sealed class PortalContactsSpecification : Specification<Customer>
{
    public PortalContactsSpecification(string? email, Guid? contactId)
    {
        Where(c => c.Status == CustomerStatus.Active
            && c.Contacts.Any(ct => ct.HasPortalAccess
                && ((email != null && ct.Email != null && ct.Email.Value == email) || (contactId != null && ct.Id == contactId))));
        ApplyOrderBy(c => c.Number.Value);
        ApplyPaging(0, 20);
        AsReadOnly();
        IgnoreOrganisationScope();
    }
}
```

**Create folder `…/Queries/FindPortalContactsByEmail/`**: `FindPortalContactsByEmailQuery.cs`
(`[AllowAnonymousRequest] public sealed record FindPortalContactsByEmailQuery(string Email) : IRequest<Result<IReadOnlyList<PortalContactMatch>>>;`
doc "In-process portal registration lookup (Identity, through ICustomersModuleApi); no HTTP endpoint exposes it."),
`…QueryHandler.cs` (copy `FindContactsByEmailQueryHandler`: `EmailAddress.TryCreate` → empty on failure; list with
`new PortalContactsSpecification(email.Value, null)`; map in memory only contacts with `HasPortalAccess && Email == email`),
`…QueryValidator.cs` (`NotEmpty().MaximumLength(EmailAddress.MaxLength)`).

**Create folder `…/Queries/GetPortalContact/`**: `GetPortalContactQuery(Guid ContactId)` `[AllowAnonymousRequest]`
→ `Result<PortalContactMatch>`; handler lists with `new PortalContactsSpecification(null, request.ContactId)`, picks the
contact with that id and `HasPortalAccess`, else `CustomersErrors.PortalContactNotFound(contactId)`; validator
`ContactId NotEmpty`.

Shared mapping (put a `private static PortalContactMatch ToMatch(Customer customer, Contact contact)` in each handler, or
one `internal static class PortalContactMapper` in `Queries/`): `new PortalContactMatch(customer.Id.Value, contact.Id,
contact.Name, contact.Email!.Value, contact.IsPrimary, customer.PreferredLanguage.Value, customer.TenantId.Value,
customer.BranchId.Value, customer.DepartmentId.Value)` — `Email` is non-null because portal access requires it
(`Contact.Change`), but guard with a `where` rather than `!` (`contact.Email is { } e`) — **no `!`** (CLAUDE.md).

**File: `…/CustomersErrors.cs`** — add `PortalContactNotFound(Guid id) => Error.NotFound("Customers.PortalContactNotFound", …)`.

**File: `…/CustomersModuleApi.cs`** — implement both methods with `sender.Send(new …Query(…), cancellationToken)`.

### B3 — Identity domain: `PortalAccount` and `PortalSession`

**Create file: `backend/src/Modules/Identity/Crm.Modules.Identity.Domain/PortalAccountId.cs`** and **`PortalSessionId.cs`**
— copies of `UserId.cs` / `RefreshTokenFamilyId.cs`.

**Create file: `…/Domain/PortalAccount.cs`**:

```csharp
/// <summary>A customer contact's portal sign-in (Story 47). Bound to exactly one contact of one customer, for life.
/// NOT ITenantScoped on purpose (like User): registration and sign-in run before a scope exists. Email is unique
/// globally; one account per contact.</summary>
public sealed class PortalAccount : AuditableEntity<PortalAccountId>, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;
    public Guid CustomerId { get; private set; }
    public Guid ContactId { get; private set; }
    public EmailAddress Email { get; private set; }
    public DisplayName DisplayName { get; private set; }
    public PreferredLocale PreferredLocale { get; private set; }
    public string PasswordHash { get; private set; }
    public int FailedSignInCount { get; private set; }
    public DateTimeOffset? LockedOutUntilUtc { get; private set; }
    public DateTimeOffset? LastSignInAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // private parameterless ctor for EF (null! for the value objects, string.Empty for the hash) + private full ctor

    /// <summary>Self-registration (D5) and agent provisioning (D19) both create the account through this factory.</summary>
    public static PortalAccount Create(
        TenantId tenantId, Guid customerId, Guid contactId, EmailAddress email, DisplayName displayName,
        PreferredLocale locale, string passwordHash, PortalAccountId? id = null)
    // throws DomainException: Identity.TenantRequired (TenantId.None), Identity.PortalContactRequired
    // (customerId or contactId Guid.Empty), Identity.PasswordHashRequired (blank hash)

    public bool IsLockedOut(DateTimeOffset nowUtc)                               // == User.IsLockedOut
    public void RecordFailedSignIn(DateTimeOffset nowUtc, LockoutPolicy policy)  // == User.RecordFailedSignIn
    public void RecordSuccessfulSignIn(DateTimeOffset nowUtc)                    // == User.RecordSuccessfulSignIn
    public void UpdateProfile(DisplayName displayName, PreferredLocale locale)
    /// <summary>Self-service: every portal session except keepSessionId is revoked (domain event).</summary>
    public void ChangePassword(string newPasswordHash, Guid? keepSessionId, DateTimeOffset nowUtc)
        // raises PortalPasswordChanged(Id, keepSessionId, nowUtc)
    /// <summary>An agent sets the password (D19): clears the lockout, revokes every session.</summary>
    public void SetPasswordByAgent(string newPasswordHash, DateTimeOffset nowUtc)
        // FailedSignInCount = 0; LockedOutUntilUtc = null; raises PortalPasswordChanged(Id, null, nowUtc)
}
```

Copy the method bodies from `User.cs` (same rules, same `DomainError` codes for the shared ones). Inline `DomainError`
like `User.cs` does.

**Create file: `…/Domain/PortalSession.cs`** — mirrors `RefreshTokenFamily.cs` line for line with `PortalAccountId
PortalAccountId` instead of `UserId`, raising `PortalSessionStarted(PortalAccountId, PortalSessionId, now)` in
`Start`, and reusing `RefreshToken` (internal constructor — same assembly), `RefreshTokenRotation` and
`SessionRevocationReason`. Summary: "One portal sign-in (Story 47, D7). Mirrors RefreshTokenFamily; its id is the
portal token's sid."

**Create file: `…/Domain/PortalAccountEvents.cs`**:

```csharp
public sealed record PortalPasswordChanged(PortalAccountId AccountId, Guid? KeepSessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
public sealed record PortalSessionStarted(PortalAccountId AccountId, PortalSessionId SessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
```

Add `SessionRevocationReason.PortalAccessRevoked` (append at the end of the enum — the column is a string, so the
order does not matter, but append anyway).

### B4 — Identity application

**File: `…/Crm.Modules.Identity.Application/Crm.Modules.Identity.Application.csproj`** — add
`<ProjectReference Include="..\..\Customers\Crm.Modules.Customers.Contracts\Crm.Modules.Customers.Contracts.csproj" />`
with the comment "Portal registration/sign-in read contacts through ICustomersModuleApi (Contracts only, CRM0001)."

**File: `…/Abstractions/IAccessTokenIssuer.cs`** — add
`IssuedAccessToken IssuePortal(PortalAccessTokenSubject subject, DateTimeOffset nowUtc);` and:

```csharp
public sealed record PortalAccessTokenSubject(
    Guid AccountId, string DisplayName, Guid TenantId, Guid SessionId, Guid CustomerId, Guid ContactId,
    bool IsAccountAdmin, Guid BranchId, Guid DepartmentId);
```

**Create file: `…/PortalAccessTokenSubjectFactory.cs`** (`internal static`): `Build(PortalAccount account,
PortalContactMatch contact, PortalSessionId sessionId)` → subject with `IsAccountAdmin = contact.IsPrimary` (D4) and
the contact's branch/department. Returns `null` (caller refuses) when `contact.TenantId != account.TenantId.Value` or
`contact.CustomerId != account.CustomerId` (defensive, like `AccessTokenSubjectFactory`'s tenant check).

**File: `…/Dtos.cs`** — add:

```csharp
public sealed record PortalMeDto(
    Guid Id, string Email, string DisplayName, string PreferredLocale, Guid CustomerId, Guid ContactId,
    bool IsAccountAdmin, IReadOnlyList<string> Permissions);

public sealed record PortalAccountDto(Guid Id, Guid CustomerId, Guid ContactId, string Email, DateTimeOffset? LastSignInAtUtc);
```

`SignInOutcome` / `SessionTokens` are reused by the portal commands.

**File: `…/IdentityErrors.cs`** — add `InvalidPortalCredentials = "Identity.InvalidCredentials"` (same code: the
frontend already maps it), and:

```csharp
public static Error PortalContactNotFound(Guid contactId) => Error.NotFound("Identity.PortalContactNotFound", $"Contact '{contactId}' was not found on this customer.");
public static Error PortalAccessNotEnabled() => Error.Domain("Identity.PortalAccessNotEnabled", "Enable portal access on the contact first.");
public static Error PortalCustomerInactive() => Error.Domain("Identity.PortalCustomerInactive", "The customer is inactive.");
public static Error PortalEmailInUse() => Error.Conflict("Identity.PortalEmailInUse", "Another portal account already uses this email.");
public static Error PortalOnly() => Error.Unauthorized("Authorization.PortalOnly", "Sign in to the customer portal to continue.");
```

**Create specifications** (`…/Specifications/`): `PortalAccountByEmailSpecification(EmailAddress)` (tracked),
`PortalAccountByContactSpecification(Guid contactId)` (tracked), `PortalSessionByTokenHashSpecification(string)` (tracked,
copy of `RefreshTokenFamilyByTokenHashSpecification`), `ActivePortalSessionsOfAccountSpecification(PortalAccountId)`
(tracked, copy of `ActiveRefreshTokenFamiliesOfUserSpecification`).

**Create commands** (each folder: request record, handler, validator; every request exactly one attribute — R5):

| Folder | Request | Attribute | Returns |
|---|---|---|---|
| `Commands/RegisterPortalAccount/` | `RegisterPortalAccountCommand(string Email, string Password, string? PreferredLocale)` | `[AllowAnonymousRequest]` | `Result` |
| `Commands/PortalSignIn/` | `PortalSignInCommand(string Email, string Password)` | `[AllowAnonymousRequest]` | `Result<SignInOutcome>` |
| `Commands/RefreshPortalSession/` | `RefreshPortalSessionCommand(string RefreshToken)` | `[AllowAnonymousRequest]` | `Result<SignInOutcome>` |
| `Commands/PortalSignOut/` | `PortalSignOutCommand(string? RefreshToken)` | `[AllowAnonymousRequest]` | `Result` |
| `Commands/ChangePortalPassword/` | `ChangePortalPasswordCommand(string CurrentPassword, string NewPassword)` | `[RequirePermission(CrmPermissions.PortalAccount)]` | `Result` |
| `Commands/UpdatePortalProfile/` | `UpdatePortalProfileCommand(string DisplayName, string PreferredLocale)` | `[RequirePermission(CrmPermissions.PortalAccount)]` | `Result<PortalMeDto>` |
| `Commands/ProvisionPortalAccount/` | `ProvisionPortalAccountCommand(Guid CustomerId, Guid ContactId, string Password)` | `[RequirePermission(CrmPermissions.CustomersWrite)]` | `Result<PortalAccountDto>` |

and one query: `Queries/GetPortalMe/GetPortalMeQuery` `[RequirePermission(CrmPermissions.PortalAccount)]` → `Result<PortalMeDto>`.

Handler rules:

- **`RegisterPortalAccountCommandHandler`** (D5; inject `IRepository<PortalAccount, PortalAccountId>`,
  `ICustomersModuleApi`, `IPasswordHasher`, `IDateTime`, `IIdentityUnitOfWork`, `ILogger<>`):
  1. `string hash = hasher.Hash(request.Password);` **first, on every path** (equal cost).
  2. `EmailAddress.TryCreate` fails → log `malformed`, `Result.Success()`.
  3. An account with the email exists (`PortalAccountByEmailSpecification`, `ListAsync(...).Count > 0`) → log `taken`, success.
  4. `customers.FindPortalContactsByEmailAsync(email.Value)`; failure → **return the failure** (a 5xx-class problem is
     not an oracle); `Count != 1` → log `unknown` (0) or `ambiguous` (>1), success.
  5. An account for that contact exists (`PortalAccountByContactSpecification`) → log `contact-taken`, success.
  6. `PortalAccount.Create(new TenantId(m.TenantId), m.CustomerId, m.ContactId, email, DisplayName.Create(m.ContactName),
     PreferredLocale.Create(request.PreferredLocale ?? m.PreferredLanguage), hash)`; `Add`; `SaveChangesAsync`; log
     `registered` with the account id. **Never log the email or password** (LoggerMessage with reason + ids only).
  7. A unique-index race (two registrations for the same email at once) surfaces as a `DbUpdateException` → 500. Accept
     (documented in Edge Cases); do not catch.
  Validator: `Email NotEmpty MaximumLength(254)`; `Password` → `.StaffPassword(localization)` (the one password policy);
  `PreferredLocale` → `.SupportedLocale(localization)` `.When(c => c.PreferredLocale is not null)`.
- **`PortalSignInCommandHandler`** — copy `SignInCommandHandler` (refusal order, dummy verification, lockout, logs)
  over `PortalAccount` / `PortalAccountByEmailSpecification`. After the password verifies: `customers.GetPortalContactAsync(account.ContactId)`
  (**before** anything is published); failure (404) → `hasher` not needed (already verified), log `portal-access-revoked`,
  return `Failed()` (the generic 401 — D15); `PortalAccessTokenSubjectFactory.Build(...)` null → log `binding-mismatch`,
  `Failed()`. Then `PortalSession.Start(account.Id, …)`, `issuer.IssuePortal(subject, now)`, `SaveChangesAsync`,
  success with `SessionTokens`. `PortalSessionStarted` → `RecordPortalSignInWhenSessionStarted` records the sign-in
  (one transaction, one aggregate — Story 07 precedent).
- **`RefreshPortalSessionCommandHandler`** — copy `RefreshSessionCommandHandler`; the account lookup is
  `accounts.GetByIdAsync(session.PortalAccountId)`; after a successful rotation call `GetPortalContactAsync`; on failure
  `session.Revoke(SessionRevocationReason.PortalAccessRevoked, now)`, save, `Failed()`. Issue with `IssuePortal`.
- **`PortalSignOutCommandHandler`** — copy `SignOutCommandHandler` over `PortalSession`.
- **`ChangePortalPasswordCommandHandler`** — copy `ChangePasswordCommandHandler`: account id from `currentUser.UserId`;
  also require `currentUser.Scope.PortalCustomer is { } grant && grant.ContactId == account.ContactId` else
  `IdentityErrors.PortalOnly()`; `account.ChangePassword(hash, keepSession, now)`. Validator = `ChangePasswordCommandValidator`.
- **`UpdatePortalProfileCommandHandler`** — load the account like above, `UpdateProfile(DisplayName.Create(…),
  PreferredLocale.Create(…))`, save, return the `PortalMeDto` (build it with the same private helper as `GetPortalMe`).
  Validator: `DisplayName NotEmpty MaximumLength(DisplayName.MaxLength)`, `PreferredLocale SupportedLocale`.
- **`GetPortalMeQueryHandler`** — account by `currentUser.UserId`; null, or grant missing, or grant/contact/tenant
  mismatch → `PortalOnly()`; else `PortalMeDto(..., grant.IsAccountAdmin, CrmPermissions.ForRoles(scope.EffectiveRoles))`.
- **`ProvisionPortalAccountCommandHandler`** (D19): `customers.GetCustomerSummaryAsync(request.CustomerId)` (scoped,
  `customers.read` — agent has it; 403/404 pass through); contact not in `summary.Contacts` → `PortalContactNotFound`;
  `!summary.IsActive` → `PortalCustomerInactive`; `!contact.HasPortalAccess || contact.Email is null` →
  `PortalAccessNotEnabled`. Existing account by contact → **tenant check** `account.TenantId.Value == summary.TenantId`
  (else `PortalContactNotFound`), `account.SetPasswordByAgent(hash, now)`. None → an account with the contact's email
  exists → `PortalEmailInUse`; else `PortalAccount.Create(..., DisplayName.Create(contact.Name),
  PreferredLocale.Create(summary.PreferredLanguage), hash)`. Save; return `PortalAccountDto`. Validator: both ids
  `NotEmpty`, `Password StaffPassword`.

**Create domain event handlers** (`…/DomainEventHandlers/`): `RecordPortalSignInWhenSessionStarted` (copy of
`RecordSignInWhenSessionStarted`) and `RevokePortalSessionsWhenPasswordChanged` (copy of
`RevokeSessionsWhenPasswordChanged` over `ActivePortalSessionsOfAccountSpecification`).

### B5 — Identity infrastructure

**File: `…/Crm.Modules.Identity.Infrastructure/Security/JwtSettings.cs`** — append an optional last parameter:
`JwtSettings(string Issuer, string Audience, TimeSpan AccessTokenLifetime, TimeSpan ClockSkew, string SigningKey = "",
string PortalAudience = "")`. `Validate()`: `Require(!string.IsNullOrWhiteSpace(PortalAudience) &&
!string.Equals(PortalAudience, Audience, StringComparison.Ordinal), "PortalAudience")` (the two audiences must differ —
D1 depends on it). Prove R9.

**File: `…/Security/JwtAccessTokenIssuer.cs`** — add `IssuePortal` next to `Issue` (line 20): the same claims
dictionary shape with `sub` = account id, `name`, `tenant_id`, `sid`, `customer_id`, `contact_id`, `portal_admin`
(`"true"`/`"false"`), `portal_branch`, `portal_department` (all Guids `"D"`), `jti`, `iat`, `nbf`, `exp`; **no
`membership`, no `role`**; `Audience = settings.PortalAudience`. Update the class summary.

**Create file: `…/Persistence/Configurations/PortalAccountConfiguration.cs`** — table `PortalAccounts`; copy
`UserConfiguration` for Id (`PortalAccountId` conversion, `ValueGeneratedNever`), `Email` (owned, column `Email`,
**unique**), `DisplayName` (owned), `PreferredLocale` (conversion, max 2), `PasswordHash` (500), `RowVersion`; plus
`builder.HasIndex(x => x.ContactId).IsUnique();`, `builder.HasIndex(x => x.CustomerId);`, `builder.HasIndex(x => x.TenantId);`.
TenantId is converted by the `ModuleDbContext` convention — do not configure it.

**Create file: `…/Persistence/Configurations/PortalSessionConfiguration.cs`** — copy `RefreshTokenFamilyConfiguration`:
table `PortalSessions`, `PortalAccountId` conversion, tokens `OwnsMany` → table **`PortalRefreshTokens`**, FK
`"SessionId"`, key `Id` `ValueGeneratedNever`, `TokenHash` max 64 required **unique**, field access mode, rowversion,
index on `PortalAccountId`. Prove R4.

**File: `…/Persistence/IdentityDbContext.cs`** — add `DbSet<Domain.PortalAccount> PortalAccounts` and
`DbSet<Domain.PortalSession> PortalSessions`.

**Migration:** `backend/scripts/ef.ps1 -Module Identity migrations add PortalAccounts` → review: creates
`identity.PortalAccounts` (unique `Email`, unique `ContactId`), `identity.PortalSessions`, `identity.PortalRefreshTokens`;
nothing else changes.

**File: `…/IdentityModule.cs`**:
- After line 32: `services.AddRepository<PortalAccount, PortalAccountId, IdentityDbContext>();` and
  `services.AddRepository<PortalSession, PortalSessionId, IdentityDbContext>();`.
- Staff scheme (lines 60–79): add `AuthenticationType = CrmAuthenticationSchemes.Staff` to its
  `TokenValidationParameters`; chain `.AddJwtBearer(CrmAuthenticationSchemes.Portal, …)` with the same parameters except
  `ValidAudience = jwt.PortalAudience`, `AuthenticationType = CrmAuthenticationSchemes.Portal`. Fix the comment on line 59
  ("Story 47 adds the portal scheme here" → "Staff and Portal schemes; the Staff/Portal policies authenticate with both, D1").

**File: `…/Persistence/IdentitySeeder.cs`** — after line 37: `await EnsurePortalAccountAsync(tenant, password, cancellationToken);`
seeding **Ahmed** (idempotent on `IdentityDevSeed.AhmedPortalAccountId`): `PortalAccount.Create(tenant,
CustomersDevSeed.AhmedCustomerId, CustomersDevSeed.AhmedContactId, EmailAddress.Create(CustomersDevSeed.AhmedEmail),
DisplayName.Create("أحمد القحطاني"), PreferredLocale.Arabic, hasher.Hash(password), new PortalAccountId(...))`.
`CustomersDevSeed` reaches Infrastructure transitively through Application's new Customers.Contracts reference.

**File: `…/Crm.Modules.Identity.Contracts/DevSeed/IdentityDevSeed.cs`** — add
`public static readonly Guid AhmedPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000001");` with doc
"Portal account of CustomersDevSeed.AhmedContactId (primary contact → account admin); password = Identity:DevSeed:Password."

### B6 — Identity presentation

**Create file: `…/Crm.Modules.Identity.Presentation/PortalRefreshCookie.cs`** — copy `RefreshCookie.cs`: name
`crm_portal_refresh`, path `/api/Portal/Auth`.

**Create file: `…/Controllers/Portal/AuthController.cs`** (namespace `…Controllers.Portal`, `[Area(CrmAreas.Portal)]`):
copy the Agent `AuthController` (login/refresh/logout + `ToSessionResponse`, `Secure()`, `DeleteCookie()`) sending
`PortalSignInCommand` / `RefreshPortalSessionCommand` / `PortalSignOutCommand`, using `PortalRefreshCookie`, and
`[EnableRateLimiting(CrmRateLimitPolicies.PortalSignIn)]` on login and refresh. Add:

```csharp
/// <summary>Always 204 (unless 400/429): the response never says whether the email belongs to a customer (D5).</summary>
[HttpPost("register")]
[AllowAnonymous]
[EnableRateLimiting(CrmRateLimitPolicies.PortalSignIn)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public async Task<ActionResult> Register([FromBody] PortalRegisterRequest request, CancellationToken cancellationToken) =>
    ToActionResult(await Sender.Send(new RegisterPortalAccountCommand(request.Email, request.Password, request.PreferredLocale), cancellationToken));
```

Records (Portal-prefixed to keep Swagger schema ids distinct — R10): `PortalRegisterRequest(string Email, string
Password, string? PreferredLocale = null)`, `PortalLoginRequest(string Email, string Password)`,
`PortalRefreshRequest(string? RefreshToken)`, `PortalSignInResponse(...)` (same members as `SignInResponse`).

**Create file: `…/Controllers/Portal/MeController.cs`** — `GET` → `GetPortalMeQuery`; `PUT` (body
`PortalProfileRequest(string DisplayName, string PreferredLocale)`) → `UpdatePortalProfileCommand`;
`POST change-password` (body `PortalChangePasswordRequest(string CurrentPassword, string NewPassword)`) →
`ChangePortalPasswordCommand`. Class-level `[ProducesResponseType(401)]`, `[ProducesResponseType(403)]`.

**Create file: `…/Controllers/Agent/PortalAccountsController.cs`** — `[Area(CrmAreas.Agent)]`; `POST` body
`ProvisionPortalAccountRequest(Guid CustomerId, Guid ContactId, string Password)` → `ProvisionPortalAccountCommand`;
responses 200 `PortalAccountDto`, 400, 403, 404, 409, 422. Summary "An agent sets (or resets) a portal-enabled contact's
password (customers.write). Creates the portal account on first use."

### B7 — Tickets domain

**File: `backend/src/Modules/Tickets/Crm.Modules.Tickets.Domain/Ticket.cs`**:
- Line 12: implement `IPortalCustomerOwned` as well: `…, ITransferableOrganisationScoped, IPortalCustomerOwned`
  (`CustomerId` already exists, private setter, set once in the constructor). Add to the `<remarks>`: "IPortalCustomerOwned
  (Story 47): the portal customer of CustomerId reaches this ticket; CustomerId never changes."
- Add after `AllowedActions()` (line 174):

```csharp
/// <summary>What the customer may do in this status (Story 47, D10). One source for the portal DTO and the two
/// customer methods below.</summary>
public static IReadOnlyList<CustomerTicketAction> CustomerActionsFor(TicketStatus status)
{
    List<CustomerTicketAction> actions = [];
    if (status is not (TicketStatus.Closed or TicketStatus.Cancelled)) actions.Add(CustomerTicketAction.Reply);
    if (StatusTransitions[TicketAction.Resolve].From.Contains(status)) actions.Add(CustomerTicketAction.RequestClosure);
    if (status == TicketStatus.Resolved) actions.Add(CustomerTicketAction.Reopen);
    return actions;
}

public bool CustomerCan(CustomerTicketAction action) => CustomerActionsFor(Status).Contains(action);

/// <summary>The customer says the problem is solved: the ticket is Resolved.</summary>
public void RequestClosure(Guid actorId, DateTimeOffset nowUtc)
{
    EnsureCustomerAllowed(CustomerTicketAction.RequestClosure, TicketAction.Resolve);
    Resolve(actorId, nowUtc);
}

/// <summary>The customer reopens a Resolved ticket. A Closed ticket cannot be reopened by the customer.</summary>
public void ReopenAtCustomerRequest(Guid actorId, DateTimeOffset nowUtc)
{
    EnsureCustomerAllowed(CustomerTicketAction.Reopen, TicketAction.Reopen);
    Reopen(actorId, nowUtc);
}

private void EnsureCustomerAllowed(CustomerTicketAction action, TicketAction asTicketAction)
{
    if (!CustomerCan(action))
    {
        throw new DomainException(TicketDomainErrors.TransitionNotAllowed(asTicketAction, Status));
    }
}
```

  (Keep the `if` bodies braced to match the file style.)
- **Create file: `…/Domain/CustomerTicketAction.cs`**: `public enum CustomerTicketAction { Reply, RequestClosure, Reopen }`.

### B8 — Tickets application

**Create file: `…/Crm.Modules.Tickets.Application/PortalCaller.cs`**:

```csharp
/// <summary>The portal customer behind a portal request (Story 47). Null for anyone else - the handler answers
/// 401 Authorization.PortalOnly. ActorId = the contact (D8).</summary>
internal static class PortalCaller
{
    public static PortalCustomerGrant? Grant(ICurrentUserService user) => user.Scope.PortalCustomer;
}
```

**File: `…/TicketsErrors.cs`** — add `PortalOnly()` (same code/text as Identity's) — `Error.Unauthorized("Authorization.PortalOnly", "Sign in to the customer portal to continue.")`.

**File: `…/TicketRefusals.cs`** — add `CustomerNotAllowed(Ticket ticket, CustomerTicketAction action,
ILocalizationService localization)` returning `null` when `ticket.CustomerCan(action)`, else the same
`TicketsErrors.TransitionNotAllowed(...)` with `TicketsMessageKeys.Refusal.TransitionNotAllowed` and the localised
**portal** status name (add `PortalStatusName` using new `TicketsMessageKeys.PortalStatus.*`).

**Create file: `…/Specifications/PortalTicketsSpecification.cs`**:

```csharp
/// <summary>The portal customer's tickets (Story 47, D4/D9/D11). The scope filter already limits to the grant's tenant
/// and customer; this adds the contact rule (non-admin sees only their own) and the list criteria. Tracked when a
/// command loads the aggregate; read-only otherwise.</summary>
public sealed class PortalTicketsSpecification : Specification<Ticket>
{
    public PortalTicketsSpecification(
        PortalCustomerGrant grant, TicketId? id, IReadOnlyList<TicketStatus> statuses, string? search,
        int? page, int? pageSize, bool tracked)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(statuses);
        Guid customerId = grant.CustomerId;
        Guid? contactId = grant.IsAccountAdmin ? null : grant.ContactId;
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        bool anyStatus = statuses.Count == 0;

        Where(t => t.CustomerId == customerId
            && (contactId == null || t.ContactId == contactId)
            && (id == null || t.Id == id)
            && (anyStatus || statuses.Contains(t.Status))
            && (term == null || t.Number.Value.Contains(term) || t.Subject.Contains(term)));

        ApplyOrderByDescending(t => t.ModifiedAtUtc ?? t.CreatedAtUtc ?? DateTimeOffset.MinValue);
        ApplyThenBy(t => t.Number.Value);
        if (page is { } p && pageSize is { } size) ApplyPaging((p - 1) * size, size);
        if (!tracked) AsReadOnly();
    }
}
```

(Braced `if`s in the real file. `ModifiedAtUtc ?? CreatedAtUtc` inside `OrderByDescending` must translate — the
`TicketSearchSpecification` precedent uses `?? DateTimeOffset.MinValue` on one column; if the chained coalesce does not
translate, use `t => t.ModifiedAtUtc ?? DateTimeOffset.MinValue` and record it.)

**Create file: `…/PortalTicketStatuses.cs`** (`internal static`): `Name(TicketStatus)` per D9 (`"Received"`,
`"InProgress"`, `"AwaitingYourReply"`, `"Resolved"`, `"Closed"`), `OpenTab` / `ClosedTab` status lists, and
`Statuses(string portalName)` → the domain statuses of one portal status. `public static readonly IReadOnlyList<string> All`.

**Create file: `…/Abstractions/IPortalTicketReadModel.cs`**:

```csharp
/// <summary>Customer-safe projections (Story 47, D12/S7): the rows carry no assignee, department, branch, priority,
/// rowVersion, note or activity. The Story 06 filters (incl. the portal customer clause) apply.</summary>
public interface IPortalTicketReadModel
{
    Task<IReadOnlyList<PortalTicketRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default);
}

public sealed record PortalTicketRow(
    Guid Id, string Number, string Subject, string Description, TicketStatus Status, Guid? CategoryId, Guid? ContactId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc, DateTimeOffset? ResolvedAtUtc, DateTimeOffset? ClosedAtUtc);
```

**File: `…/Dtos.cs`** — append the portal DTOs (separate records, S7):

```csharp
public sealed record PortalTicketCategoryDto(Guid Id, string NameAr, string NameEn);

/// <summary>Status is the customer-friendly name (Received | InProgress | AwaitingYourReply | Resolved | Closed).</summary>
public sealed record PortalTicketSummaryDto(
    Guid Id, string Number, string Subject, string Status, PortalTicketCategoryDto? Category,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

/// <summary>Actions: what the customer may do now (Reply | RequestClosure | Reopen).</summary>
public sealed record PortalTicketDto(
    Guid Id, string Number, string Subject, string Description, string Status, PortalTicketCategoryDto? Category,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, DateTimeOffset? ResolvedAtUtc, DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<string> Actions);

/// <summary>A public message. Sender: Customer | Support. IsYou: the caller's own contact wrote it. No staff id or name.</summary>
public sealed record PortalThreadMessageDto(int Sequence, Guid Id, string Sender, bool IsYou, string Body, DateTimeOffset OccurredAtUtc);

public sealed record PortalThreadPageDto(IReadOnlyList<PortalThreadMessageDto> Items, int? NextCursor);
```

**Create file: `…/PortalTicketMapper.cs`** (`internal sealed class`, registered scoped): `ToSummariesAsync(rows)` and
`ToDtoAsync(row)` resolving category names with `categories.ListAsync(new CategoriesByIdsSpecification(...))`
(`Category` is tenant-scoped — visible), `ToDto(Ticket ticket, Category? category)` for command results (reads only
`Id, Number, Subject, Description, Status, CategoryId, CreatedAtUtc, ModifiedAtUtc, ResolvedAtUtc, ClosedAtUtc` —
never notes/activities), `Actions = [.. Ticket.CustomerActionsFor(status).Select(a => a.ToString())]`.

**Create queries** (all `[RequirePermission(CrmPermissions.PortalTickets)]`; handlers start with
`if (PortalCaller.Grant(currentUser) is not { } grant) return Failure(TicketsErrors.PortalOnly());`):

- `Queries/ListPortalTicketCategories/ListPortalTicketCategoriesQuery()` → `Result<IReadOnlyList<PortalTicketCategoryDto>>`:
  `categories.ListAsync(new CategoriesSpecification([grant.HomeDepartmentId], activeOnly: true, customerVisibleOnly: true))`.
- `Queries/ListPortalTickets/ListPortalTicketsQuery(string? Tab, string? Status, string? Search, int Page, int PageSize)`
  → `Result<PagedResult<PortalTicketSummaryDto>>`. Statuses: `Status` given → `PortalTicketStatuses.Statuses(Status)`
  intersected with the tab's list; else the tab's list (`Tab` null → `open`). Page with `readModel.ListAsync(spec with
  paging)`, total with `tickets.CountAsync(spec without paging)` (the `SearchTicketsQueryHandler` shape). Validator:
  `Tab` in `open|closed` when not null (`TicketsRules.SupportedPortalTab`), `Status` in `PortalTicketStatuses.All`
  when not null (`SupportedPortalStatus`), `Search MaximumLength(200)`, `Page >= 1`, `PageSize 1..50`.
- `Queries/GetPortalTicket/GetPortalTicketQuery(Guid Id)` → `Result<PortalTicketDto>`: `readModel.ListAsync(new
  PortalTicketsSpecification(grant, new TicketId(id), [], null, null, null, tracked: false))` → empty → **404
  `TicketsErrors.TicketNotFound`** (D11). Validator `Id NotEmpty`.
- `Queries/GetPortalTicketThread/GetPortalTicketThreadQuery(Guid Id, int? After, int Limit)` →
  `Result<PortalThreadPageDto>`: first the same ownership check through `readModel.ListAsync(...)` (404 when empty —
  this is what enforces the non-admin contact rule, since `ListPublicAsync` only sees the scope filter); then
  `threads.ListPublicAsync(id, after ?? 0, limit + 1)` — **the only thread method this handler names**; `null` → 404.
  Map rows: `Sender = row.Direction == Inbound ? "Customer" : "Support"`, `IsYou = row.Direction == Inbound &&
  row.AuthorId == grant.ContactId`, `Body = row.Body ?? string.Empty`; `NextCursor` as in `ToThreadPageAsync` (take
  `limit`, cursor = last sequence when there were `limit + 1`). Validator: `Id NotEmpty`, `After >= 0` when given,
  `Limit 1..100`.

**Create commands** (all `[RequirePermission(CrmPermissions.PortalTickets)]`, `ICommand<PortalTicketDto>`; each loads
with `tickets.ListAsync(new PortalTicketsSpecification(grant, id, [], null, null, null, tracked: true))` →
`FirstOrDefault()` → 404 when null; **never** `GetByIdAsync`; actor = `grant.ContactId` (D8); publish **after** the
mapper's nested reads, `Publish` last (CreateTicket precedent):

- `Commands/SubmitPortalTicket/SubmitPortalTicketCommand(string Subject, string Description, Guid? CategoryId)`:
  category (when given) = `categories.ListAsync(new CategoriesSpecification([grant.HomeDepartmentId], true, true))`
  containing that id, else **404 `TicketsErrors.CategoryNotFound(id)`** (never `GetByIdAsync` — a foreign-tenant id would
  be 403); `number = await numbers.NextAsync()`; `Ticket.Create(number, subject, description, grant.CustomerId,
  grant.ContactId, TicketChannel.Portal, category, TicketPriority.Normal, currentUser.Scope.Tenant, grant.HomeBranchId,
  grant.HomeDepartmentId, grant.ContactId, now)`; `Add`; save; map; publish `TicketCreatedIntegrationEvent(...,
  Channel "Portal", ..., ActorId: grant.ContactId, …)`. Validator: `Subject NotEmpty MaximumLength(Ticket.SubjectMaxLength)`,
  `Description NotEmpty MaximumLength(Ticket.DescriptionMaxLength)`.
- `Commands/ReplyFromPortal/ReplyFromPortalCommand(Guid Id, string Body)`: `TicketRefusals.CustomerNotAllowed(ticket,
  CustomerTicketAction.Reply, …)` → 422; `ticket.ReceiveCustomerReply(body, grant.ContactId, grant.ContactId, now)`;
  save; publish `TicketMessageAddedIntegrationEvent` (Direction "Inbound", AuthorId = contact) and, when the status
  changed, `TicketStatusChangedIntegrationEvent` — copy `ReceiveCustomerReplyCommandHandler` lines for both. Validator
  like `ReceiveCustomerReplyCommandValidator`.
- `Commands/RequestTicketClosure/RequestTicketClosureCommand(Guid Id)`: refusal check `RequestClosure`;
  `ticket.RequestClosure(grant.ContactId, now)`; publish `TicketStatusChangedIntegrationEvent`.
- `Commands/ReopenPortalTicket/ReopenPortalTicketCommand(Guid Id)`: refusal check `Reopen` (Closed → **422
  `Tickets.TransitionNotAllowed`**); `ticket.ReopenAtCustomerRequest(grant.ContactId, now)`; publish
  `TicketStatusChangedIntegrationEvent`.

**File: `…/Localization/TicketsMessageKeys.cs`** — add `Validation.PortalTabUnsupported`, `Validation.PortalStatusUnsupported`,
and `public static class PortalStatus { Received, InProgress, AwaitingYourReply, Resolved, Closed }` (values
`"Tickets.PortalStatus.<Name>"`). **Files: `…/Localization/Resources/TicketsMessages.resx` / `.ar.resx`** — add
(Write tool for the Arabic file):

| Key | en | ar |
|---|---|---|
| `Tickets.Validation.PortalTabUnsupported` | Tab must be open or closed. | يجب أن يكون التبويب مفتوحة أو مغلقة. |
| `Tickets.Validation.PortalStatusUnsupported` | Status must be Received, InProgress, AwaitingYourReply, Resolved or Closed. | يجب أن تكون الحالة إحدى: مستلمة، قيد المعالجة، بانتظار ردك، تم الحل، مغلقة. |
| `Tickets.PortalStatus.Received` | received | مستلمة |
| `Tickets.PortalStatus.InProgress` | in progress | قيد المعالجة |
| `Tickets.PortalStatus.AwaitingYourReply` | awaiting your reply | بانتظار ردك |
| `Tickets.PortalStatus.Resolved` | resolved | تم الحل |
| `Tickets.PortalStatus.Closed` | closed | مغلقة |

**File: `…/Validation/TicketsRules.cs`** — add `SupportedPortalTab` and `SupportedPortalStatus` in the shape of the
existing `SupportedStatus`.

### B9 — Tickets infrastructure and presentation

**Create file: `…/Crm.Modules.Tickets.Infrastructure/Persistence/PortalTicketReadModel.cs`** — copy `TicketReadModel`
projecting `new PortalTicketRow(t.Id.Value, t.Number.Value, t.Subject, t.Description, t.Status, t.CategoryId == null ?
null : t.CategoryId.Value, t.ContactId, t.CreatedAtUtc, t.ModifiedAtUtc, t.ResolvedAtUtc, t.ClosedAtUtc)`.

**File: `…/TicketsModule.cs`** — after line 32: `services.AddScoped<IPortalTicketReadModel, PortalTicketReadModel>();`
and `services.AddScoped<PortalTicketMapper>();`. Update the class summary ("… and the portal customer's view, Story 47").

**Create file: `…/Crm.Modules.Tickets.Presentation/Controllers/Portal/TicketsController.cs`** — `[Area(CrmAreas.Portal)]`,
class-level 401/403:

| Verb + route | Request | Response |
|---|---|---|
| `GET api/Portal/Tickets?tab=&status=&search=&page=1&pageSize=20` | `ListPortalTicketsQuery` | 200 `PagedResult<PortalTicketSummaryDto>`, 400 |
| `POST api/Portal/Tickets` body `PortalSubmitTicketRequest(string Subject, string Description, Guid? CategoryId = null)` | `SubmitPortalTicketCommand` | 200 `PortalTicketDto`, 400, 404, 422 |
| `GET api/Portal/Tickets/{id:guid}` | `GetPortalTicketQuery` | 200, 404 |
| `GET api/Portal/Tickets/{id:guid}/thread?after=&limit=50` | `GetPortalTicketThreadQuery` | 200 `PortalThreadPageDto`, 400, 404 |
| `POST api/Portal/Tickets/{id:guid}/replies` body `PortalReplyRequest(string Body)` | `ReplyFromPortalCommand` | 200 `PortalTicketDto`, 400, 404, 409, 422 |
| `POST api/Portal/Tickets/{id:guid}/request-closure` | `RequestTicketClosureCommand` | 200, 404, 409, 422 |
| `POST api/Portal/Tickets/{id:guid}/reopen` | `ReopenPortalTicketCommand` | 200, 404, 409, 422 |

**Create file: `…/Controllers/Portal/TicketCategoriesController.cs`** — `GET api/Portal/TicketCategories` →
`ListPortalTicketCategoriesQuery` → 200 `IReadOnlyList<PortalTicketCategoryDto>`.

### B10 — Host

**File: `backend/src/Crm.Api/Program.cs`**:
- Line 53: `options.Conventions.Add(new PortalAreaAuthorizationConvention());` next to the staff one.
- Lines 33–35 comment: "Staff JWT (Agent/Admin) and portal JWT (Portal) are both registered by Identity; Staff stays
  the default scheme so a bare challenge is a 401 problem."
- Line 83: `Description = "Access token: staff from POST /api/Agent/Auth/login, customer from POST /api/Portal/Auth/login"`.
- Middleware order: unchanged (comment 6: "authentication (staff by default; the Staff/Portal policies authenticate
  both schemes)").

**File: `backend/src/Crm.Api/appsettings.json`** — `"Identity": { "Jwt": { …, "PortalAudience": "crm-portal" } }`;
`"RateLimiting": { …, "Portal": { "PermitLimit": 120, "Window": "00:01:00" }, "PortalSignIn": { "PermitLimit": 10,
"Window": "00:01:00" } }`. **`appsettings.Development.json`** — `"Portal": { "PermitLimit": 120, "Window": "00:01:00" }`,
`"PortalSignIn": { "PermitLimit": 10, "Window": "00:01:00" }` (so `portal.http` 30 can hit 429 with 11 calls).

### B11 — Documentation

- **`CLAUDE.md`** — "API surface": replace "Portal and Public are anonymous until stories 47/55." with "Portal controllers
  require the portal customer JWT by convention (`PortalAreaAuthorizationConvention`); a portal token is 403 on
  Agent/Admin and a staff token 403 on Portal (both policies authenticate both schemes). Public is anonymous until
  story 55." and extend the `[AllowAnonymous]` sentence with "and the Portal auth actions (register, login, refresh,
  logout)". "Pipeline behaviours": after the three attributes add "`[RequireAuthenticatedUser]` means a signed-in
  **staff** user — a portal principal gets 403 `Authorization.StaffOnly`; portal requests declare `portal.tickets` /
  `portal.account`." "Scoping": replace "The one permitted lift is `Specification.IgnoreOrganisationScope()`, used only
  by the sign-in membership lookup (`Organisation`'s `MembershipsOfUserSpecification`)" with "The only permitted lift is
  `Specification.IgnoreOrganisationScope()`, used only by the two sign-in lookups (`Organisation`'s
  `MembershipsOfUserSpecification`, `Customers`' `PortalContactsSpecification`)"; add "A portal customer (Story 47) has a
  tenant and a `PortalCustomerGrant` but no departments: it reaches `ITenantScoped` rows of its tenant and
  `IPortalCustomerOwned` rows (tickets) of its own customer only; adding an `IPortalCustomerOwned` implementer is a
  security review."; "Identity's `User` is deliberately not `ITenantScoped`" → "Identity's `User` and `PortalAccount` are
  deliberately not `ITenantScoped`".
- **`backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md`** — Portal Area (lines 14–19): "Story 47:
  Identity's `AuthController` (register/login/refresh/logout, anonymous, `portal-sign-in` limit) and `MeController`
  (`portal.account`); Tickets' `TicketsController` and `TicketCategoriesController` (`portal.tickets`). Every controller
  requires the portal JWT (`PortalAreaAuthorizationConvention`). Rate limited per account (signed in) or per IP by the
  global limiter (`RateLimiting:Portal`)." Replace the "Anonymous until story 47" comment. Agent Area: "Story 47:
  Identity's `PortalAccountsController` (`customers.write`)." Public Area line: "Story 47 added `portal-sign-in`".

### B12 — `backend/requests/portal.http` (Write tool — Arabic bodies)

Header comment like `auth.http`: run top to bottom against a Development API; seeded portal account **Ahmed**
(`ahmed@example.com`, `Crm_Dev_2026!`, primary contact of Ahmed Al-Qahtani, RYD-CARE; ticket T-000001); access tokens
live 2 minutes (re-run the login block); **leak check**: search every portal response for `SECRET-NOTE-47`,
`assignedAgent`, `departmentId`, `rowVersion`, `activities` — none may appear. Variables: `@host`, `@ahmedTicket =
15000000-0000-0000-0000-000000000001`, `@najdTicket = 15000000-0000-0000-0000-000000000002`, `@najd =
11000000-0000-0000-0000-000000000002`, `@redSea = 11000000-0000-0000-0000-000000000003`, `@fahad =
11000000-0000-0000-0001-000000000004`, category ids from `TicketsDevSeed`.

Requests (number them; each with the expected status in the `###` title):
1. Portal login Ahmed → 200, `Set-Cookie: crm_portal_refresh=…; path=/api/Portal/Auth`; decode the token: `aud`
   `crm-portal`, `customer_id`, `contact_id`, `portal_admin` `"true"`, no `membership`/`role`. `# @name portal`.
2. `GET /api/Portal/Me` → 200 (`isAccountAdmin: true`, permissions `["portal.account","portal.tickets"]`). 2b no token → 401.
3. Cross-audience (portal token): 3a `GET /api/Agent/Me`, 3b `GET /api/Agent/Tickets`, 3c `GET /api/Admin/Users` → **403**.
4. Staff login agent A (`# @name agentA`); 4a `GET /api/Portal/Me`, 4b `GET /api/Portal/Tickets` with the staff token → **403**.
   4c staff login admin; `GET /api/Portal/Tickets` with the admin token → **403** (D13/D14 — admin has no portal permission).
5. `GET /api/Portal/TicketCategories` → 200: RY-CARE General, Technical, Complaint only (not Internal, not Legacy, no RY-BILL/JED).
6. `GET /api/Portal/Tickets` → 200: T-000001 only, status `Received`; 6b `?tab=closed` → empty; 6c `?search=T-000001`; 6d `?status=Nope` → 400.
7. Cross-customer: `GET /api/Portal/Tickets/{{najdTicket}}` → **404**; 7b `…/thread` → 404; 7c `POST …/replies` → 404;
   7d `POST …/request-closure` → 404; 7e `POST …/reopen` → 404; 7f unknown guid → 404 (identical body shape).
8. Submit: `POST /api/Portal/Tickets` `{ "subject": "لا أستطيع تتبع شحنتي", "description": "رقم الشحنة لا يظهر في التطبيق.", "categoryId": "<General>" }`
   → 200, status `Received`, actions `["Reply","RequestClosure"]` (`# @name submitted`). 8b non-visible category
   (Internal) → 404 `Tickets.CategoryNotFound`; 8c empty subject → 400.
9. Agent A: `GET /api/Agent/Tickets?queue=all` → the new ticket, channel `Portal`, department RYD-CARE, customer Ahmed.
10. Agent A: `POST /api/Agent/Tickets/{{submitted}}/notes` `{ "body": "SECRET-NOTE-47 do not show the customer" }` → 200;
    `POST …/replies` `{ "body": "مرحباً أحمد، نعمل على ذلك." }` → 200.
11. Portal: `GET …/{{submitted}}/thread` → 200: the agent reply (`sender: "Support"`), **no** `SECRET-NOTE-47`, no
    activity; `GET …/{{submitted}}` → no note text anywhere.
12. Portal reply `POST …/replies` `{ "body": "شكراً، بانتظاركم." }` → 200; agent `GET /api/Agent/Tickets/{{submitted}}/thread`
    → the inbound message (`authorId` = Ahmed's contact id).
13. Portal `POST …/request-closure` → 200, status `Resolved`, actions `["Reply","Reopen"]`;
    13b `POST …/reopen` → 200 `InProgress`; 13c request-closure again → `Resolved`; 13d agent A transition `Close` (rowVersion
    from `GET /api/Agent/Tickets/{id}`) → 200; 13e portal `POST …/reopen` → **422 `Tickets.TransitionNotAllowed`**;
    13f portal reply to the closed ticket → 422.
14. Registration: 14a unknown email `nobody@example.com` → **204**; 14b Ahmed's email (already registered) → **204**
    (nothing created — 14c: login with the 14b password → 401); 14d `fahad@redsea.example.com` (portal access off) → 204,
    login → 401.
15. Agent B (JED-CARE) enables portal access on Fahad (`PUT /api/Agent/Customers/{{redSea}}/contacts/{{fahad}}` with
    `hasPortalAccess: true`, rowVersion chained from a `GET`) → 200; 15b register Fahad → 204; 15c login Fahad → 200;
    15d Fahad lists → T-000003 only; 15e Fahad `GET {{ahmedTicket}}` → 404.
16. Agent provisioning: agent A `POST /api/Agent/PortalAccounts` for Red Sea/Fahad → **403** (out of scope); agent A adds a
    contact to Najd (`POST /api/Agent/Customers/{{najd}}/contacts` `{ "name": "Layla Hassan", "email":
    "layla@najd.example.com", "isPrimary": false, "hasPortalAccess": true, "rowVersion": … }`) → 200; `POST
    /api/Agent/PortalAccounts` `{ customerId: najd, contactId: <layla>, password: "Layla_Portal_2026" }` → 200;
    16b same without portal access (Omar) → 422 `Identity.PortalAccessNotEnabled`; 16c weak password → 400.
17. Layla (non-admin, D4): login → `portal_admin` `"false"`; list → empty (T-000002 belongs to Sara); `GET {{najdTicket}}`
    → **404**; submit a ticket → 200.
18. Agent A enables portal on Sara (primary) and provisions her → Sara login → list shows T-000002 **and** Layla's
    ticket (account admin).
19. Refresh: `POST /api/Portal/Auth/refresh` with the body token → 200 new pair; the old token again → 401 (reuse revokes);
    the **portal** refresh token at `POST /api/Agent/Auth/refresh` → 401 (unknown there, D7).
20. Profile: `PUT /api/Portal/Me` `{ "displayName": "Ahmed Q.", "preferredLocale": "en" }` → 200; `POST
    /api/Portal/Me/change-password` wrong current → 400 on `currentPassword`; right → 204; old password login → 401.
21. Revocation (D15): agent A turns Ahmed's portal access off → Ahmed refresh → 401; login → 401; turn it back on.
22. Logout → 204 and the cookie cleared.
30. Rate limit: 11 × `POST /api/Portal/Auth/login` with a wrong password inside a minute → the 11th **429** with
    `Retry-After` and `urn:crm:problem:rate-limited`; 31: 11 × `register` → 429.

---

## Frontend Tasks

Order: **F0 → F1 → F2 → F3 → F4 → F5 → F6 → F7 → F8**. Every file with Arabic: Write tool.

### F0 — Regenerate the API types

With the API running (Development): `pnpm --dir frontend gen:api`. Expect `openapi/portal.json` with the 13 portal
paths, `openapi/agent.json` with `/api/Agent/PortalAccounts`, and `app/api/generated/{portal,agent}.ts` updated. Commit
both. Never hand-write a DTO type.

### F1 — Two sessions (D18)

**File: `frontend/app/api/crm-fetch.ts`** — `CrmFetchOptions` (lines 4–8): `session: (request: Request) => AuthSession`
and `onUnauthenticated: (request: Request) => void | Promise<void>`. In `crmFetch` (45–74) resolve
`const current = session(input)` once and use it where `session()` is called today (token, `isRefreshRequest`,
`refresh` via `refreshOnce(current)`, `clear`), and call `onUnauthenticated(input)`. Make the single-flight guard
**per session object** (`const inflight = new WeakMap<AuthSession, Promise<boolean>>()`) so a portal refresh never joins a
staff one. **The 401 → one refresh → one retry → login contract is unchanged.** `pages/_dev/api.vue` (lines 242–271)
passes `() => session` — still type-correct; confirm its scenarios A–E still pass in the browser.

**File: `frontend/app/composables/useAuthSession.ts`** — hold two module-level refs: `session` (staff) and
`portalSession`, both `anonymousSession` by default; `setAuthSession(next)` unchanged, add `setPortalSession(next)`.
Keep the SSR warning comment (both stay anonymous on the server).

**File: `frontend/app/utils/permissions.ts`** — add `PortalTickets: 'portal.tickets'`, `PortalAccount: 'portal.account'`
to `Permissions`; add `export const portalAuthPathPrefix = '/api/Portal/Auth/'` and `export const portalApiPrefix =
'/api/Portal/'`; `export function isPortalApiRequest(request: Request): boolean` (pathname of `new URL(request.url,
'http://local')` starts with `portalApiPrefix`).

**File: `frontend/app/plugins/api.ts`** — `baseFetch`: skip `X-Crm-Scope` when `isPortalApiRequest(request)`;
`session: (request) => isPortalApiRequest(request) ? useAuthSession().portalSession.value : useAuthSession().session.value`;
`onUnauthenticated: (request) => navigateTo({ path: isPortalApiRequest(request) ? '/portal/login' : '/login', query: {
redirect: useRoute().fullPath } })`.

**Create file: `frontend/app/stores/portalAuth.ts`** — `usePortalAuthStore` mirroring `stores/auth.ts` exactly
(module-level `restoring`, `nuxtApp.runWithContext` after awaits) over `clients.portal`:
`POST /api/Portal/Auth/login|refresh|logout`, `GET /api/Portal/Me`; `me` typed `components['schemas']['PortalMeDto']`
from `~/api/generated/portal`; `login` applies `preferredLocale`; `logout` navigates to `/portal/login`; no scope reset.
Add `register(email, password)` → `POST /api/Portal/Auth/register` with `preferredLocale: $i18n.locale.value`, and
`applyMe(me)` (used after a profile update).

**File: `frontend/app/plugins/auth.client.ts`** — also `setPortalSession({ accessToken: () => portal.accessToken,
refresh: () => portal.refresh(), clear: () => portal.clear(), isRefreshRequest: (r) => new URL(r.url,
window.location.origin).pathname.startsWith(portalAuthPathPrefix) })`.

### F2 — Guard (R7)

**File: `frontend/app/middleware/auth.global.ts`** — keep the staff branch; add: `const portalPublic = ['/portal/login',
'/portal/register']`; when `to.path.startsWith('/portal')` and not public and `!portal.isAuthenticated` → `await
portal.restore()` → on failure `navigateTo({ path: '/portal/login', query: { redirect: to.fullPath } })`; a signed-in
customer opening `/portal/login` or `/portal/register` → `/portal`. The early `if (import.meta.server) return` stays
(D17). Update the file comment ("/portal/** is SSR; its guard runs on the client — R7").

### F3 — Portal layout

**File: `frontend/app/layouts/portal.vue`** — inside the header's right-hand group, before the locale switcher, a
`<nav :aria-label="t('portal.nav.label')">` rendered only when `route.path.startsWith('/portal') &&
portal.isAuthenticated`, with `NuxtLink`s `/portal` (`portal.nav.tickets`), `/portal/tickets/new` (`portal.nav.new`),
`/portal/account` (`portal.nav.account`) and a sign-out `UiButton variant="ghost" size="sm"`. Logical utilities only
(`gap-*`, `ms-*`); wraps under 375 px (`flex-wrap`). The app name links to `/portal` on portal routes. No other change
(the staff `/login` keeps using this layout).

### F4 — Composables (`app/composables/portal/`)

- `types.ts` — `PortalTicketSummaryDto`, `PortalTicketDto`, `PortalThreadPageDto`, `PortalThreadMessageDto`,
  `PortalTicketCategoryDto`, `PortalMeDto` from `~/api/generated/portal` `components['schemas']`.
- `usePortalTicketList.ts` — `(filters: Ref<PortalTicketFilters>)` → `useAsyncData('portal-tickets', …, { server: false,
  watch: [filters] })` calling `GET /api/Portal/Tickets` with `tab/status/search/page/pageSize`; throws the problem on
  failure (like `useTicketThread.load`).
- `usePortalTicket.ts` — `(id)` → `useAsyncData(\`portal-ticket-${id}\`, …, { server: false })`.
- `usePortalTicketThread.ts` — copy `useTicketThread.ts` over `/api/Portal/Tickets/{id}/thread` (key
  `portal-ticket-thread-${id}`, `server: false`), plus `reply(text)`.
- `usePortalTicketCommands.ts` — `(id)` → `requestClosure()`, `reopen()`; `submit(body)` for new tickets; all
  `toastOnError: false` (the page decides).
- `usePortalCategories.ts` — `GET /api/Portal/TicketCategories`, `server: false`.
- `usePortalProfile.ts` — `update(displayName, preferredLocale)` (`PUT /api/Portal/Me`), `changePassword(...)`.

### F5 — `frontend/app/utils/portal.ts`

`portalTabs = ['open','closed'] as const`; `portalStatuses = ['Received','InProgress','AwaitingYourReply','Resolved','Closed'] as const`;
`openStatuses`/`closedStatuses` per D9; `PortalTicketFilters { tab, status, q, page }`; `parsePortalFilters(query)` /
`portalFiltersToQuery(f)` (copy the `utils/tickets.ts` helpers' shape; only non-defaults reach the URL; `status` must
belong to the tab, else `''`); `portalStatusTone(status)` → `Received` info, `InProgress` primary, `AwaitingYourReply`
warning, `Resolved` success, `Closed` neutral; `submitFieldForCode = { 'Tickets.CategoryNotFound': 'categoryId',
'Tickets.SubjectRequired': 'subject', 'Tickets.SubjectTooLong': 'subject', 'Tickets.DescriptionRequired': 'description',
'Tickets.DescriptionTooLong': 'description' }`; reuse `bodyFieldForCode` from `utils/tickets.ts` for replies.

### F6 — Copy (`i18n/locales/{en,ar}/portal.json`, top-level key `portal`; plus additions)

`portal.json` keys (en → ar), grouped: `nav` (label "Portal", tickets "My requests" / "طلباتي", new "New request" /
"طلب جديد", account "My account" / "حسابي", signOut "Sign out" / "تسجيل الخروج"); `login` (title "Sign in to your
support portal" / "تسجيل الدخول إلى بوابة الدعم", email, password, submit, invalid "Wrong email or password." / "البريد
الإلكتروني أو كلمة المرور غير صحيحة.", rateLimited, failed, noAccount "New here? Create your account" / "جديد هنا؟ أنشئ
حسابك"); `register` (title "Create your portal account" / "أنشئ حساب البوابة", intro "Use the email your support team
has on file." / "استخدم البريد الإلكتروني المسجل لدى فريق الدعم.", password hint, confirm, mismatch, submit, done
"If this email belongs to a customer with portal access, your account is ready. Sign in to continue." / "إذا كان هذا
البريد يخص عميلاً لديه صلاحية الوصول إلى البوابة فحسابك جاهز. سجّل الدخول للمتابعة.", signIn); `tickets` (title, tabs.open
"Open" / "مفتوحة", tabs.closed "Closed" / "مغلقة", statusFilter, allStatuses, search, empty "You have no requests here
yet." / "لا توجد لديك طلبات هنا بعد.", emptyAction "Raise a request" / "أنشئ طلباً", loadError, retry, number, updated,
pagination labels); `status` (Received "Received" / "مستلمة", InProgress "In progress" / "قيد المعالجة",
AwaitingYourReply "Awaiting your reply" / "بانتظار ردك", Resolved "Resolved" / "تم الحل", Closed "Closed" / "مغلقة");
`new` (title, subject, description, category, noCategory "No category" / "بدون تصنيف", submit, submitted "Your request
was sent." / "تم إرسال طلبك."); `detail` (backToList, created, updated, description, thread "Conversation" / "المحادثة",
threadEmpty, you "You" / "أنت", colleague "Your colleague" / "زميلك", support "Support team" / "فريق الدعم", loadMore,
reply "Reply" / "رد", replyLabel, send, requestClosure "My issue is solved" / "تم حل مشكلتي", requestClosureConfirm,
reopen "Reopen request" / "إعادة فتح الطلب", closedNote "This request is closed. Raise a new request if you need more
help." / "هذا الطلب مغلق. أنشئ طلباً جديداً إذا احتجت إلى مساعدة إضافية.", notFound "We could not find this request." /
"تعذر العثور على هذا الطلب.", conflict "The request changed. Reload and try again." / "تغيّر الطلب. أعد التحميل وحاول
مجدداً."); `account` (title, profile, displayName, language, save, saved, changePassword, currentPassword, newPassword,
confirmPassword, passwordRules, mismatch, changed).

**Also:** `i18n/locales/{en,ar}/errors.json` → `errors.code.Identity.PortalContactNotFound`, `PortalAccessNotEnabled`,
`PortalCustomerInactive`, `PortalEmailInUse`, `errors.code.Authorization.StaffOnly`, `Authorization.PortalOnly`;
`customers.json` → `customers.contacts.setPortalPassword` ("Set portal password" / "تعيين كلمة مرور البوابة"),
`customers.portal.title`, `.password`, `.hint`, `.save`, `.saved` ("The contact can now sign in to the portal." /
"يمكن لجهة الاتصال الآن تسجيل الدخول إلى البوابة."), `.close`; `tickets.json` → `tickets.thread.viaPortal` ("via the
portal" / "عبر البوابة"). `pnpm lint` checks both key sets match.

### F7 — Portal pages (`app/pages/portal/`, all `definePageMeta({ layout: 'portal' })`, SSR)

Every data-backed view renders **loading** (`UiSkeleton` while `status` is `'idle'` or `'pending'` and no data — the
server renders the skeleton, D17; hydration must match), **empty** (`UiEmptyState`) and **error** (`UiErrorState` with
retry; 404 → `portal.detail.notFound`). 400s go to `useFormErrors()`, never a toast. Mobile first; check 375/768/1280.
Identifiers (ticket numbers, emails) in `<bdi dir="ltr">`. Dates via `useFormat().dateTime`.

- **`login.vue`** — copy `pages/login.vue` over `usePortalAuthStore().login`; safe redirect default `/portal`; link to
  `/portal/register`.
- **`register.vue`** — email, password (`hint` = `portal.register.passwordHint`, rules: 10+ chars, a letter and a
  digit), confirm; on 204 replace the form with the `portal.register.done` message and a link to `/portal/login` (never
  sign in automatically — the response carries nothing, D5); 429 inline; 400 via `useFormErrors`.
- **`index.vue`** (my tickets) — `UiTabs` (open/closed) bound to the URL (`parsePortalFilters`/`router.replace`), status
  `UiSelect` (statuses of the active tab + "all"), search `UiInput` (debounced with `utils/debounce.ts`), a card list
  (`<ul>` of `NuxtLink` to `/portal/tickets/<id>`: number, subject, `UiBadge` with `portalStatusTone`, category name by
  locale, updated time), `UiPagination` (page size 20). Empty state with a "Raise a request" action → `/portal/tickets/new`.
- **`tickets/new.vue`** — category `UiSelect` (from `usePortalCategories`, "No category" option, names by locale),
  subject `UiInput` (maxlength 200), description `UiTextarea` (maxlength 4000); submit → toast `portal.new.submitted`,
  `navigateTo('/portal/tickets/<id>')`; problem codes via `submitFieldForCode` + `useFormErrors().applyCode` (as
  `TicketCreateDrawer.vue` does).
- **`tickets/[id].vue`** — header (number, subject, status badge, created/updated), description, the thread (a list like
  `TicketThread.vue`'s message bubbles: `IsYou` → "You" aligned to the end, other inbound → "Your colleague", `Support`
  → "Support team" at the start; `dir="auto"`, `whitespace-pre-wrap`, body as **text**, never `v-html`; "Show more"
  with the cursor), then actions from `ticket.actions`: reply `UiTextarea` + send (on 200: write the returned
  `PortalTicketDto` into the page data and load newer thread entries with the cursor), "My issue is solved" (confirm
  `UiModal`) → `requestClosure`, "Reopen request" → `reopen`; when neither `Reply` nor `Reopen` is offered show
  `portal.detail.closedNote` with a link to `/portal/tickets/new`. 409 → inline `portal.detail.conflict` + reload; 422 →
  the problem message inline.
- **`account.vue`** — copy `pages/agent/account.vue`: profile form (display name `UiInput`, language `UiSelect` en/ar)
  → `usePortalProfile().update` → `portal.applyMe(dto)` and `setLocale` when the language changed; change-password form
  (current/new/confirm) → 204 → success toast; sign-out button.

### F8 — Agent side

- **Create `frontend/app/composables/customers/useCustomerPortalAccount.ts`** — `setPassword(customerId, contactId,
  password)` → `POST /api/Agent/PortalAccounts` (`clients.agent`, `toastOnError: false`).
- **Create `frontend/app/components/customer/CustomerPortalPasswordModal.vue`** — `UiModal` with one password `UiInput`
  (`autocomplete="new-password"`, `dir="ltr"`, hint = the password rules), save → success toast
  `customers.portal.saved`, close; 400 → `useFormErrors`; 403/404/409/422 → inline problem message.
- **`frontend/app/components/customer/CustomerContactsPanel.vue`** — in the per-contact action group (the
  `v-if="canWrite"` `role="group"` block), add `UiButton size="sm" variant="ghost"` `customers.contacts.setPortalPassword`
  when `contact.hasPortalAccess && contact.email`, opening the modal for that contact.
- **`frontend/app/components/ticket/TicketThread.vue`** — new optional prop `contactId?: string`; `lookups` computed:
  merge `contactId → (props.customerLabel ?? t('tickets.thread.customer'))` into `people` so portal activities (actor =
  contact, D8) read "Ahmed Al-Qahtani resolved the ticket" instead of "a colleague"; in the inbound footer show
  `tickets.thread.viaPortal` instead of `recordedBy` when `entry.recordedById === entry.authorId` (the customer recorded
  it themselves). **File: `app/pages/agent/tickets/[id].vue`** line 52 — pass `:contact-id="ticket.contactId ?? undefined"`.

---

## Edge Cases & Failure Modes

- **Portal token on any Agent/Admin endpoint** → 403 `urn:crm:problem:forbidden` (Staff policy assertion, B1
  `CrmPolicies`). **Staff token on any Portal endpoint** → 403 (Portal policy). No token → 401. Expired portal token →
  401 → the frontend refreshes through `crm_portal_refresh` (F1). R2.
- **A staff principal sends a portal MediatR request in-process** → 403 "permission 'portal.tickets' is required"
  (Administrator no longer holds `All`, B1 `CrmPermissions`).
- **A portal principal reaches a `[RequireAuthenticatedUser]` request** (staff `GET Me`, change-password, my-scope,
  Organisation lookups) → 403 `Authorization.StaffOnly` (B1 `AuthorizationBehavior`, D13) — even if a future route
  exposes it by mistake.
- **Forged/malformed portal claims** (a token signed by us can't be forged; a malformed claim would only come from a
  bug) → `OrganisationScope.None` → every scoped read empty, every portal request 403 (no role) (B1 `CurrentUserService`).
- **Portal token with an `X-Crm-Scope` header** → ignored; no 403 from `OrganisationScopeMiddleware` (scope never
  "rejected" for a portal principal).
- **Another customer's ticket id, unknown id, soft-deleted ticket** → 404 `Tickets.TicketNotFound` on all seven ticket
  endpoints (D11: `ListAsync` with `PortalTicketsSpecification`; the portal handlers never call `GetByIdAsync`).
  Verified by `portal.http` 7a–7f.
- **Non-admin contact asks for a colleague's ticket of the same customer** → 404 (the spec's contact criterion; the
  thread query checks ownership *before* `ListPublicAsync`, B8).
- **Ticket transferred to another department** → still the customer's: the portal filter matches on `CustomerId`, not
  department (B1 filter); the agent side is unaffected.
- **Internal notes and activities** → never in a portal payload: the thread handler names only `ListPublicAsync`
  (`TicketThreadReadModel.cs` lines 45–61 selects `t.Messages` only); DTOs have no such fields (D12). `portal.http` 11.
- **Customer replies to a Closed/Cancelled ticket** → 422 `Tickets.TransitionNotAllowed` (pre-check
  `TicketRefusals.CustomerNotAllowed`, backstop `Ticket.ReceiveCustomerReply` → `EnsureAllowed`).
- **Reopen a Closed ticket from the portal** → 422 (`Ticket.ReopenAtCustomerRequest`, B7); the agent's `Reopen` of a
  Closed ticket still works (unchanged transition table).
- **Request closure on Resolved/Closed** → 422 (not in `CustomerActionsFor`).
- **Agent and customer act at the same instant** → the ticket rowversion → `ConcurrencyConflictException` → 409
  `Persistence.ConcurrencyConflict` (`TransactionBehavior`); the portal shows `portal.detail.conflict` (F7).
- **Portal submit with a category of another department, an inactive or non-visible category, or another tenant's id**
  → 404 `Tickets.CategoryNotFound` (B8: resolved from the customer-visible list, never `GetByIdAsync`).
- **The customer's owning department was deactivated** → the ticket is still created there (no check, like the agent
  path); agents with that department still see it. Accepted for the MVP; noted.
- **Registration: unknown email / already registered / contact already has an account / ambiguous email / portal access
  off / customer inactive** → identical 204, nothing created, a reason-only log line (B4). The password is hashed first
  on every path (equal cost). Malformed email or weak password → 400 (says nothing about existence).
- **Two registrations for the same email at the same instant** → the unique `Email` index makes the second `INSERT`
  fail → 500 `urn:crm:problem:internal` (no exception text). Rare, not an oracle (the first succeeded); accepted.
- **Email changed on the contact after registration** → the account keeps its own email (it is the login name); the
  binding is by `ContactId`. Portal access still required at every login/refresh.
- **Contact removed, portal access turned off, customer deactivated** → next login/refresh fails with the generic 401
  (`GetPortalContactAsync` → 404, B4); a live access token keeps working until it expires (S5, D15).
- **Primary contact changes** → the new account-admin flag reaches the token at the next refresh (D4, D15).
- **Refresh-token reuse** (stolen token replayed) → the whole portal session is revoked (`PortalSession.Rotate`, B3).
- **Portal refresh token sent to the staff refresh endpoint (or vice versa)** → 401 (unknown in that table, D7).
- **Lockout** → after 5 wrong passwords the account is locked for `LockoutWindow`; attempts while locked are the same 401
  and never extend it (`PortalAccount.RecordFailedSignIn`, copied from `User`).
- **429** on register/login/refresh beyond `RateLimiting:PortalSignIn` per IP; on any portal endpoint beyond
  `RateLimiting:Portal` per account/IP — `Retry-After` + `urn:crm:problem:rate-limited` (B1). The login page shows
  `portal.login.rateLimited` inline (F7).
- **Two browser tabs refresh the portal token at the same instant** → reuse detection revokes the session, both tabs go
  to `/portal/login` (Story 07 accepted the same race; noted).
- **Staff and portal signed in in the same browser** → independent stores, cookies (different paths) and sessions (F1);
  signing out of one does not touch the other.
- **Hydration**: the server renders portal pages with the skeleton; `useAsyncData(..., { server: false })` starts
  `'idle'` on the client, so the template must treat `'idle'` like `'pending'` or Vue warns about a mismatch (F7, R7).
- **Arabic text and ticket numbers** → `dir="auto"` on bodies, `<bdi dir="ltr">` on numbers/emails (F7).
- **Agent thread of a portal ticket** → activities by the contact read "<contact name> …" and inbound messages "via the
  portal" (F8); if the customer panel has not loaded yet, "the customer".

---

## Test Plan

This project ships **without automated tests** (CLAUDE.md, "Testing policy"). Do not add a test project, a `*.spec.ts`,
or a test framework. In their place:

1. **Compiler and build:** `dotnet build backend/Crm.sln` — zero warnings, no `CRM0001` (the new
   `Identity.Application → Customers.Contracts` reference is allowed by rule 1 of `backend/Directory.Build.targets`).
   `pnpm --dir frontend typecheck` / `lint` / `build` clean.
2. **Startup guard (R5):** the API starts — every new request declares exactly one attribute and a known permission.
3. **Request samples (smoke, manual):** `backend/requests/portal.http` 1–31 top to bottom, each response matching the
   `###` title; the leak check (search for `SECRET-NOTE-47`, `assignedAgent`, `departmentId`, `rowVersion`,
   `activities` in every portal response body) finds nothing. Then re-run `auth.http`, `tickets.http`,
   `ticket-thread.http`, `ticket-workspace.http`, `customers.http` top to bottom — unchanged results (regression for
   the policy, permission-map and filter changes).
4. **EF SQL log (R3):** portal list and an agent inbox request, as described in R3.
5. **Browser walkthrough (manual, en + ar, 375/768/1280):** register (unknown email → the same message), sign in as Ahmed,
   my tickets (tabs, status filter, search, pagination, empty tab), new ticket (validation errors on fields), detail
   (thread, reply, "My issue is solved", reopen, closed note), account (name, language switch, change password), sign
   out; agent side: set a portal password from the customer page, see the portal ticket in the inbox and its thread with
   the contact's name and "via the portal". `view-source:` of `/portal` shows the server-rendered portal layout.

---

## Migration / Rollback

- **One migration:** `Identity` `PortalAccounts` (three new tables in `identity`). No other module's schema changes.
  Apply: Development applies it on startup (`MigrateAndSeedModulesAsync`); elsewhere `backend/scripts/ef.ps1 -Module
  Identity database update`.
- **Rollback:** `ef.ps1 -Module Identity database update Initial` (the previous migration, `20260930013707_Initial`)
  drops the three tables; revert the commit. Nothing else holds portal data (tickets created from the portal stay valid
  tickets with channel `Portal`).
- **Half-applied state:** if the migration applied but the code is reverted, the extra tables are inert. If the code
  ships without the migration, portal endpoints fail with 500 (missing table) while staff endpoints keep working.
- **Config:** a deployment must add `Identity:Jwt:PortalAudience`, `RateLimiting:Portal`, `RateLimiting:PortalSignIn`
  — the app refuses to start without them (fail fast).
- **Behavioural change for staff:** the Staff policy now also runs the Portal scheme (one extra failed validation per
  request, logged below `Warning`); Administrator's permission list no longer contains `portal.*` (it never had them
  before this story).

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/Crm.sln` — clean, zero warnings, no `CRM0001`.
2. **Migration:** `backend/scripts/ef.ps1 -Module Identity migrations add PortalAccounts`; review the generated file (R4).
3. **Backend runs:** `docker compose up -d`, then `dotnet run --project backend/src/Crm.Api --launch-profile http`;
   Swagger `/swagger` shows the Portal document with 13 paths (R10).
4. **Prove R2, R9 first**, then run `backend/requests/portal.http` 1–31, then the regression files (Test Plan 3).
5. **Frontend:** `corepack enable pnpm --install-directory "$APPDATA/npm"` (R1); `pnpm --dir frontend install
   --frozen-lockfile`; `pnpm --dir frontend gen:api`; `pnpm --dir frontend typecheck`; `pnpm --dir frontend lint`;
   `pnpm --dir frontend build`.
6. **Frontend runs:** `pnpm --dir frontend dev`; walk every portal page in `en` and `ar` at 375, 768 and 1280 px; prove
   R6 and R7; walk the agent-side changes (F8).
7. **Regression:** staff sign-in, inbox, ticket detail, customer page, admin pages still work in the browser.

---

## Done Criteria

- [ ] A portal token is rejected with **403** on every Agent/Admin endpoint and a staff token on every Portal endpoint
      (`portal.http` 3–4); no token is 401.
- [ ] Registering with the email of a portal-enabled contact creates an account bound to that contact; an unknown email
      (and every other refusal) gets the identical 204 and creates nothing (`portal.http` 14–15).
- [ ] An agent can set a portal-enabled contact's initial password from the customer page (`portal.http` 16, browser).
- [ ] A customer sees only their own tickets (non-admin contacts only their own; the primary contact all of the
      customer's); another customer's ticket id returns **404** on every portal endpoint (`portal.http` 7, 15e, 17).
- [ ] Internal notes (and activities, assignee, department, rowVersion) never appear in any portal response
      (`portal.http` 11 leak check).
- [ ] Submitting a ticket creates it with channel **Portal** in the customer's department and it appears in the agent
      inbox (`portal.http` 8–9, browser).
- [ ] A customer reply appears in the agent's thread; an agent reply appears in the portal after refresh (`portal.http`
      10–12, browser).
- [ ] Request closure resolves the ticket; reopen works on a Resolved ticket and is rejected (422) on a Closed one
      (`portal.http` 13).
- [ ] Portal login and registration are rate limited (429 + `Retry-After`) and failed logins lock the account
      (`portal.http` 30–31).
- [ ] Every portal page walked in `en` and `ar` at 375, 768 and 1280 px, server-rendered with the portal layout, with
      loading, empty and error states.
- [ ] `dotnet build backend/Crm.sln` clean (zero warnings, no `CRM0001`); the `Identity` migration is included;
      `pnpm typecheck` / `lint` / `build` clean; `openapi/*.json` and `app/api/generated/*.ts` regenerated and committed.
- [ ] `CLAUDE.md` and `AREAS.md` updated (B11); the plan's named risks R1–R10 recorded as proven (or their fallback).
- [ ] No test project, spec file or test framework added.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 58.**
