# Story 07 — Authentication users roles and permissions

## Prerequisites

- **Stories 01–06 are implemented and committed** (latest `cb2724a`). Read
  [`../platform-foundation/00-overview.md`](../platform-foundation/00-overview.md) "decisions carried forward" for
  Stories 02–05, and [`../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md`](../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md)
  "Product rules" (claim shape, the one permitted scope lift, `Unrestricted` vs `None`) and its "Coordination"
  bullet for Story 07 (delete the dev login, issue exactly the dev handler's claims, seed users with the
  `OrganisationDevSeed` ids).
- **Modules:** creates the **`Identity`** module (five projects, schema `identity`). Changes the **BuildingBlocks**
  (`Crm.SharedKernel`, `Crm.BuildingBlocks.Application`, `.Infrastructure`, `.Presentation`) and the host. Touches
  **`Organisation`** (permission declarations on its requests, dev-login seam removed, controller attributes) and
  **`Sample`** (permission declarations only). Cross-module work: Identity reads memberships through
  `IOrganisationModuleApi.GetMembershipsAsync` (Contracts) and its seeder reads `OrganisationDevSeed` ids
  (Contracts). **No other cross-module reference.**
- **MVP scope (2026-09-29, `.squad/MVP-STORIES.md`).** This story absorbs 08 (user and role management) and 09
  (permission model), reduced to: three **fixed** roles (`Administrator`, `Supervisor`, `Agent` — the existing
  `SystemRole`/`CrmRoles`), a **code-defined** permission catalogue and role → permission map, and **basic user
  admin** (list/search/page, create with initial password, edit display name and preferred language,
  deactivate/reactivate, admin password reset). **Deferred:** forgot/reset password by email, 2FA, invite-by-email,
  custom roles, the permission-matrix screen, availability/load, avatars, signatures, audit entries (story 10),
  portal accounts (story 47), SSO, per-row (per-department) permission checks, a "change my language" endpoint.
- **Checked during planning (2026-09-30, from files, no runs):** .NET SDK `10.0.301` (`backend/global.json`,
  rollForward latestFeature); EF Core **10.0.11**, `dotnet-ef` **10.0.11** (`.config/dotnet-tools.json`);
  MediatR **12.4.1** (licence pin — do not bump); FluentValidation **12.1.1**; Swashbuckle **10.2.3**
  (`backend/Directory.Packages.props`). **One new NuGet package:** `Microsoft.AspNetCore.Authentication.JwtBearer`
  **10.0.11** (matches the repo's 10.0.11 ASP.NET/EF line; the local cache at `~/.nuget/packages` holds 10.0.0,
  10.0.1, 10.0.3, 10.0.6, 10.0.12 but not 10.0.11 — see R1). It brings `Microsoft.IdentityModel.JsonWebTokens`
  (`JsonWebTokenHandler`) transitively. `PasswordHasher<T>` ships in the `Microsoft.AspNetCore.App` shared
  framework (`Microsoft.Extensions.Identity.Core`) — **no ASP.NET Core Identity stores, UI, or EF package.**
  **No new npm package.** Frontend pins unchanged (`frontend/package.json`: Nuxt 4.5.2, `@nuxtjs/i18n` 10.6.0,
  Pinia 4.0.3, `@pinia/nuxt` 1.0.2, openapi-fetch 0.17.0, pnpm 12.6.0).
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — package restore.** `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 restores from the configured
    feed. **Prove:** `dotnet restore backend/Crm.sln` after B1. **Fallback (no other):** pin **10.0.12** (cached
    locally) and record it. If `PasswordHasher<T>` does not resolve in `Crm.Modules.Identity.Infrastructure`, add
    `<PackageVersion Include="Microsoft.Extensions.Identity.Core" Version="10.0.11" />` and a `PackageReference`
    to that one project; record it.
  - **R2 — the JWT is built without `System.DateTime` and round-trips the claim shape.** `DateTime` is banned
    (`backend/build/BannedSymbols.Clock.txt` line 1), so `SecurityTokenDescriptor.Expires/IssuedAt/NotBefore`
    (all `DateTime?`) are not used; `exp`/`iat`/`nbf` go into `SecurityTokenDescriptor.Claims` as Unix seconds with
    `JsonWebTokenHandler.SetDefaultTimesOnTokenCreation = false` (Task B9). The multi-valued `membership` and
    `role` claims are `string[]` values and must arrive as **several** claims with `MapInboundClaims = false`.
    **Prove:** `auth.http` request 1 → paste the `accessToken` payload into a base64url decoder: `exp`, `iat`,
    `nbf` are integers, `membership` is an array; then `auth.http` request 6 (`GET /api/Agent/MyScope` as the
    supervisor) returns **two** departments. **Fallback (no other):** build the payload with
    `System.Text.Json.Nodes.JsonObject` and call `JsonWebTokenHandler.CreateToken(string payload,
    SigningCredentials)`; record which path shipped.
  - **R3 — the Area authorisation convention.** `StaffAreaAuthorizationConvention` (Task B6) adds an
    `AuthorizeAttribute(CrmPolicies.Staff)` to every Agent/Admin action's `SelectorModel.EndpointMetadata`; the
    authorisation middleware must enforce it, and `[AllowAnonymous]` on an action must win. **Prove:** `auth.http`
    requests 2 (`GET /api/Agent/Me` with no token → 401) and 1 (login with no token → 200), and
    `GET /api/Public/Diagnostics/ping` with no token → 200. **Fallback:** in the convention, instead call
    `controller.Filters.Add(new AuthorizeFilter(CrmPolicies.Staff))` (`Microsoft.AspNetCore.Mvc.Authorization`);
    record it.
  - **R4 — a JWT challenge is an RFC 9457 problem.** The bearer handler's 401 has an empty body;
    `UseStatusCodePages()` (`backend/src/Crm.Api/Program.cs` line 106) must turn it into
    `urn:crm:problem:unauthorized`, as it did for Story 06's dev scheme. **Prove:** `auth.http` request 2 body.
    **Fallback:** in `AddJwtBearer`, set `options.Events = new JwtBearerEvents { OnChallenge = async c => {
    c.HandleResponse(); c.Response.StatusCode = 401; await c.HttpContext.RequestServices
    .GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext { HttpContext = c.HttpContext,
    ProblemDetails = new ProblemDetails { Status = 401 } }); } }` (the `CustomizeProblemDetails` hook fills type and
    title).
  - **R5 — the startup guard refuses to start.** `RequestAuthorizationGuard` (Task B3) throws from
    `AddBuildingBlocksApplication`. **Prove:** temporarily delete the attribute from
    `ListBranchesQuery`, run the API: the process exits and the log's `Host terminated unexpectedly` Fatal line names
    `ListBranchesQuery`; put the attribute back. Do the same with `[RequirePermission("tickets.raed")]` (unknown
    permission) — it must also refuse to start.
  - **R6 — the HttpOnly refresh cookie through the Nuxt dev proxy.** `crm_refresh` is set by the API with
    `Path=/api/Agent/Auth`, `HttpOnly`, `SameSite=Strict`, `Secure`; `nitro.devProxy` (`frontend/nuxt.config.ts`
    line 35) must pass `Set-Cookie` through, and the browser must accept a `Secure` cookie on `http://localhost`.
    **Prove:** sign in at `http://localhost:3000/login`; DevTools → Application → Cookies shows `crm_refresh` for
    `localhost` with that path; reload `/agent` and the page stays signed in (silent refresh, one
    `POST /api/Agent/Auth/refresh` 200). **Fallback:** set `Secure = Request.IsHttps` in `RefreshCookie.Options`
    (Task B12) and record it.
  - **R7 — the first domain-event handlers in the solution.** `INotificationHandler<DomainEventNotification<T>>`
    handlers in `Crm.Modules.Identity.Application` are discovered by the host's single `AddMediatR` scan and run
    **after** commit with `SaveChangesAsync` outside a transaction. **Prove:** `users.http` requests 12–14 (deactivate
    agent B, then refreshing agent B's token → 401); `SELECT RevokedAtUtc, RevocationReason FROM
    identity.RefreshTokenFamilies WHERE UserId = '06000000-0000-0000-0003-000000000004'` shows every row revoked with
    `UserDeactivated`.
  - **R8 — the owned refresh-token collection.** `RefreshTokenFamily.Tokens` is `OwnsMany` (table
    `RefreshTokens`, unique index on `TokenHash`); `f.Tokens.Any(t => t.TokenHash == hash)` must translate.
    **Prove:** `auth.http` request 8 (refresh) → 200, and the EF log shows an `EXISTS (… [RefreshTokens] …)`
    subquery.
  - **R9 — a nested read across modules inside the sign-in transaction.** `SignInCommand` (Identity transaction
    open) sends `GetMembershipsOfUserQuery` (Organisation DbContext, its own connection) through
    `IOrganisationModuleApi`. **Prove:** `auth.http` request 1 → 200; the log shows
    `Handling GetMembershipsOfUserQuery` between `Handling SignInCommand` and `Handled SignInCommand`, with no
    lock timeout.
  - **R10 — named rate-limit policy alongside the global limiter.** `[EnableRateLimiting(CrmRateLimitPolicies.SignIn)]`
    on the login action (Task B11). **Prove:** `auth.http` request 20 (loop the bad login past
    `RateLimiting:SignIn:PermitLimit` in Development) → 429 `urn:crm:problem:rate-limited` with `Retry-After`.
  - **R11 — Swashbuckle 10 security scheme.** The Bearer security definition uses the Microsoft.OpenApi v2 types
    (Task B13). **Prove:** `dotnet build`, then `/swagger` shows an "Authorize" button on the Agent and Admin
    documents. **Fallback:** drop the security definition and requirement entirely (the `.http` files remain the
    verification path) and record it.
  - **R12 — Nuxt context inside Pinia actions after `await`.** `useCookie`/`useNuxtApp` called after an `await`
    inside a store action can lose the Nuxt instance. The auth store captures `const nuxtApp = useNuxtApp()` in
    its setup function and wraps post-`await` composable calls in `nuxtApp.runWithContext(() => …)` (Task F3).
    **Prove:** sign in, sign out, reload `/agent` — no "nuxt instance unavailable" error in the console.
- **Coordination:**
  - **Story 11** (Customers) declares `[RequirePermission(CrmPermissions.CustomersRead/CustomersWrite)]` on its
    requests (Sample already uses them — copy the shape) and deletes `Sample`.
  - **Story 15** (Tickets) uses `TicketsRead/TicketsWrite/TicketsAssign`, and adds per-department role checks only
    if it needs them (this story checks permissions against the **effective scope**, not per row).
  - **Story 47** (Portal) adds a second JWT bearer scheme with its own audience for portal customers next to
    `CrmAuthenticationSchemes.Staff`, and a Portal-Area convention; it reuses `IAccessTokenIssuer`'s shape, the
    `RefreshTokenFamily` aggregate, and the refresh-cookie pattern.
  - **Story 58** (Branding) declares `CrmPermissions.AdminBranding` on its requests.

---

## Story Goal

Staff sign in with an email and a password; every endpoint knows who is calling and what they may do.

When this story is done:

1. `POST /api/Agent/Auth/login` returns a short-lived JWT access token (claims `sub`, `name`, `tenant_id`, `sid`, one
   `membership` per membership, one `role` per distinct role) and a rotating refresh token (JSON body **and** an
   HttpOnly `crm_refresh` cookie). Invalid credentials, an unknown email, a deactivated user, and a locked account
   all return the same **401** `Identity.InvalidCredentials`.
2. `POST /api/Agent/Auth/refresh` rotates the refresh token; presenting an already-rotated token **revokes the whole
   family** and returns 401. `POST /api/Agent/Auth/logout` revokes the presented token's family.
3. `GET /api/Agent/Me` returns profile, preferred language, memberships, active department, roles, and **effective
   permissions** (the permissions of the roles held in the departments of the active scope).
   `POST /api/Agent/Me/change-password` changes the password and signs out every other session.
4. Every MediatR request in every module declares exactly one of `[RequirePermission(…)]`,
   `[RequireAuthenticatedUser]`, `[AllowAnonymousRequest]`; the app **refuses to start** otherwise.
   `AuthorizationBehavior` returns **401** without a user and **403 naming the permission** without it; the handler
   never runs.
5. JWT bearer (scheme `Staff`) protects every Agent and Admin controller by convention; Portal and Public stay
   anonymous (stories 47, 55). Story 06's `X-Dev-User` handler, `IDevUserDirectory`, `/_dev/login`, and the
   `crm-dev-user` cookie are deleted.
6. Five failed sign-ins lock the account for `Identity:Sessions:LockoutWindow`.
7. Admin (`admin.users`) lists users with search and paging, creates a user with an initial password, edits display
   name and preferred language, deactivates (revoking every refresh token) and reactivates, and resets a password.
   The user screen manages memberships through Story 06's membership endpoints.
8. Frontend: a real login page, an auth Pinia store holding the access token in memory, a global route middleware
   (authenticated `/agent/**` and `/admin/**`, guest `/login`), the Story 04 `AuthSession` wired to the refresh
   endpoint, the profile language applied at sign-in, `useCan()` hiding actions, an account page (change password,
   sign out), and the users admin screens — in `en` and `ar`, 375/768/1280, keyboard-navigable.

**Not in scope:** everything listed as deferred in Prerequisites, and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith" (module catalogue: `Identity` owns stories 07–09 and 47), "Application, API, and
   persistence" bullets **Pipeline behaviours**, **Results**, **Time**, **Scoping** (the one permitted lift),
   **Localization keys**; "Testing policy".
2. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/AuthorizationBehavior.cs` lines 1–17 — the
   pass-through placeholder this story replaces. `…/Behaviors/OrganisationScopeBehavior.cs` lines 37–52 — the
   reflection that builds a failed `TResponse`; copy it (do not share it).
3. `…/Crm.BuildingBlocks.Application/DependencyInjection.cs` lines 19–36 — `AddBuildingBlocksApplication` (the guard
   call goes after line 34, before `AddValidatorsFromAssemblies`); behaviour order lines 26–33 (unchanged).
4. `…/Crm.BuildingBlocks.Application/Abstractions/CrmClaimTypes.cs` lines 8–16, `CrmRoles.cs` lines 6–11,
   `ICurrentUserService.cs` lines 3–21, `OrganisationScope.cs` lines 23–58 (constructor; `EffectiveRoles` is
   computed next to `Roles` at line 57) and `IDevUserDirectory.cs` (deleted).
5. `…/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs` lines 8–109 (claims → scope; comment line 8
   and 11–13 change), `SystemCurrentUser.cs` lines 7–26, `DevUserAuthenticationHandler.cs` lines 1–86 (deleted —
   its claim building at lines 58–70 is what `JwtAccessTokenIssuer` reproduces).
6. `…/Crm.BuildingBlocks.Presentation/Authorization/CrmPolicies.cs` lines 6–15 (the `Administrator` policy is
   replaced by `Staff`), `ErrorHandling/ErrorTypeMapping.cs` lines 9–27, `ErrorHandling/ProblemTypes.cs` line 19
   (`Unauthorized` already exists), `RateLimiting/RateLimitingServiceCollectionExtensions.cs` lines 24–71,
   `ApiControllerBase.cs` lines 26–68, `AREAS.md`.
7. `backend/src/BuildingBlocks/Crm.SharedKernel/Results/Error.cs` lines 3–18 and `ErrorType.cs` lines 3–11.
8. `backend/src/Crm.Api/Program.cs` lines 34–37 (dev scheme → `Staff` scheme), 54 (`AddControllers` gains the
   convention), 62–84 (Swagger), 93–128 (**ORDER MATTERS** block — unchanged order); `Modules/ModuleCatalog.cs`
   lines 12–20; `Crm.Api.csproj` lines 7–14; `appsettings.json`, `appsettings.Development.json`;
   `Diagnostics/Admin/DiagnosticsController.cs` lines 12–17 (gains `[AllowAnonymous]`).
9. The whole `backend/src/Modules/Organisation/` module — the template for every Identity file: `Branch.cs`
   (aggregate shape, EF constructor lines 18–22, factory with optional id), `OrganisationCode.cs` (value object),
   `Membership.cs`, `Dtos.cs`, `Commands/RenameBranch/*` (handler with `SetOriginalRowVersion`),
   `Queries/GetMyScope/GetMyScopeQueryHandler.cs`, `Specifications/*`, `OrganisationModuleApi.cs`,
   `Contracts/IOrganisationModuleApi.cs` + `DevSeed/OrganisationDevSeed.cs`, `Infrastructure/OrganisationModule.cs`,
   `Persistence/OrganisationDbContext.cs`, `OrganisationDbContextFactory.cs`, `OrganisationSeeder.cs`,
   `Configurations/BranchConfiguration.cs` (OwnsOne + HasConversion), `Presentation/Controllers/Admin/*.cs`, all five
   `.csproj`.
10. `backend/src/Modules/Sample/Crm.Modules.Sample.Infrastructure/Persistence/Configurations/SampleAggregateConfiguration.cs`
    lines 59–71 — the `OwnsMany` + backing-field pattern `RefreshTokenFamilyConfiguration` copies;
    `Crm.Modules.Sample.Domain/SampleLine.cs` — the child-entity shape `RefreshToken` copies.
11. `backend/Directory.Build.targets` rules 1–7 (Identity.Application and Identity.Infrastructure may reference
    `Crm.Modules.Organisation.Contracts`; the host references only Identity.Infrastructure and .Presentation);
    `backend/build/BannedSymbols.*.txt` (no `DateTime`, no `DateTimeOffset.UtcNow`, no `TimeProvider`, no `Console`).
12. `backend/requests/organisation.http`, `persistence.http`, `diagnostics.http`, `localization.http` — every
    `X-Dev-User` line becomes a bearer token (Task B15). REST Client request variables (`# @name x` /
    `{{x.response.body.id}}`) are the established pattern (`organisation.http` lines 183–190).
13. Frontend: `frontend/app/api/auth-session.ts` lines 1–18, `app/api/crm-fetch.ts` lines 19–75 (the Story 04
    contract — **do not change it**), `app/composables/useAuthSession.ts` lines 1–13, `app/plugins/api.ts`
    lines 1–36, `app/composables/useApi.ts`, `app/api/problem.ts`, `app/api/problem-message.ts`,
    `app/composables/useFormErrors.ts`, `app/stores/scope.ts`, `app/composables/useMyScope.ts`,
    `app/components/shell/ScopeSwitcher.vue`, `app/layouts/agent.vue` lines 37–42 and 81–105,
    `app/pages/login.vue`, `app/pages/_dev/login.vue` (deleted), `app/pages/admin/branches.vue` (the admin-screen
    template: states, modal form, `useFormErrors`, toasts), `app/composables/useBranches.ts` /
    `useDepartments.ts` (composable shape, `useAsyncData` keys), `app/composables/useFormat.ts` lines 14–35,
    `app/components/ui/{Input,Select,Table,Modal,Button,Badge,Pagination,Avatar}.vue` props, `app/utils/scope.ts`,
    `frontend/nuxt.config.ts` lines 28–52, `frontend/i18n/locales/{en,ar}/{shell,errors,organisation}.json`,
    `frontend/eslint.config.mjs` (physical-direction and bare-string rules).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **Three fixed roles, one code-defined map** (`Crm.BuildingBlocks.Application/Authorization/CrmPermissions.cs`):

  | Permission | Agent | Supervisor | Administrator |
  |---|---|---|---|
  | `tickets.read`, `tickets.write`, `customers.read`, `customers.write` | ✓ | ✓ | ✓ |
  | `tickets.assign` | | ✓ | ✓ |
  | `admin.organisation`, `admin.users`, `admin.branding` | | | ✓ |

- **Effective permissions follow the active scope.** They are the union of the permissions of the roles of the
  memberships whose department is in `OrganisationScope.Departments` (`all` → every membership; `X-Crm-Scope: <id>`
  → that department's membership only). The supervisor seed therefore has `tickets.assign` with `all` or `RYD-CARE`
  active, and not with `RYD-BILL` active. A permission is checked against the effective scope, **not per row** —
  a later story that needs "Supervisor in *this* ticket's department" adds that check itself.
- **Every request declares.** Exactly one of `[RequirePermission(CrmPermissions.X)]`, `[RequireAuthenticatedUser]`
  (any signed-in staff user — `GetMe`, `ChangePassword`, `GetMyScope`), `[AllowAnonymousRequest]` (sign-in,
  refresh, sign-out, and Organisation's in-process `GetMembershipsOfUserQuery`). No request inherits one. The
  startup guard also rejects an unknown permission string and a request whose response is not `Result`/`Result<T>`.
- **No `HttpContext` = system.** A request sent with no `HttpContext` (`OrganisationScope.IsUnrestricted`) passes
  `AuthorizationBehavior` — seeders and background code are trusted in-process callers (the same rule as Story 06's
  scope). An HTTP request with no user fails with 401 before any handler runs.
- **The pipeline order is unchanged** (validation → authorisation → organisation scope → logging → transaction →
  caching): a malformed body answers 400 before a permission check. The `.http` files send valid bodies to the 403
  cases.
- **Tokens.** Access token: JWT HS256, issuer `Identity:Jwt:Issuer`, audience `Identity:Jwt:Audience`, lifetime
  `Identity:Jwt:AccessTokenLifetime`, clock skew `Identity:Jwt:ClockSkew`, signing key `Identity:Jwt:SigningKey`
  (≥ 32 UTF-8 bytes; **never** in `appsettings.json` — environment/secret store only; the app refuses to start
  without it). Refresh token: 32 random bytes, base64url, stored **only** as its SHA-256 hash; lifetime
  `Identity:Sessions:RefreshTokenLifetime`, sliding (each rotation gets a fresh expiry; no absolute cap in the MVP).
  One `RefreshTokenFamily` per sign-in; its id is the `sid` claim.
- **Memberships are read at sign-in and at every refresh** and carried on the token. A membership change reaches the
  user at the next refresh (≤ `AccessTokenLifetime`), not instantly. Deactivation revokes the refresh tokens, so a
  deactivated user's access token dies at its expiry and `GET /api/Agent/Me` already answers 401 for them.
- **One generic sign-in failure.** Unknown email, wrong password, deactivated, locked out: all 401
  `Identity.InvalidCredentials`, "Invalid email or password.", same timing (a dummy hash is verified for an unknown
  email). The real reason is logged (Information; Warning when the attempt locks the account) with the user id when
  known — **never** the email or the password.
- **Lockout.** `Identity:Sessions:MaxFailedSignInAttempts` (5) consecutive failures lock for
  `Identity:Sessions:LockoutWindow`; attempts while locked do not extend it; a success resets the count; an admin
  password reset and a reactivation clear the lock.
- **`User` is not tenant-scoped by marker.** Sign-in runs before a scope exists and must read and write the `User`
  (failed-attempt counter), which the Story 06 scope filter and write guard would refuse. `User` therefore carries a
  plain `TenantId` without `ITenantScoped`, and the admin user queries/commands compare it to
  `currentUser.Scope.Tenant` **explicitly** (403 `Organisation.OutOfScope` on a mismatch). This is the one
  hand-written tenant filter in the codebase; the one permitted scope-filter lift stays
  `MembershipsOfUserSpecification`. `RefreshTokenFamily` is unscoped (it is found only by a secret hash or by user id).
- **Email** is unique **globally** (one staff account per address across tenants), normalised trimmed + lower-case
  invariant, ≤ 254, `local@domain.tld` shape (`EmailAddress` in `Crm.Modules.Identity.Domain` — Customers defines
  its own in Story 11).
- **Passwords:** 10–128 characters, at least one letter and one digit (validators); hashed with
  `PasswordHasher<T>` (PBKDF2, ASP.NET Core Identity v3 format). `SuccessRehashNeeded` is treated as success (no
  rehash-on-login in the MVP).
- **Refresh token transport.** Browser: HttpOnly cookie `crm_refresh`, `Path=/api/Agent/Auth`, `SameSite=Strict`,
  `Secure`, expiring with the token; the frontend never reads it. API clients and `.http` files: the JSON body.
  `refresh`/`logout` take `{ "refreshToken": "…" }` and fall back to the cookie when the body omits it. The access
  token lives only in memory in the browser; a reload restores the session with one silent refresh.
- **Sign-in endpoints never trigger a refresh.** `AuthSession.isRefreshRequest` is true for every
  `/api/Agent/Auth/*` path (login, refresh, logout), so a wrong password is a 401 the login form shows, not a
  redirect. `change-password` is therefore on `/api/Agent/Me`, where an expired token does refresh.
- **Self-protection.** An administrator cannot deactivate themself (422 `Identity.CannotDeactivateSelf`).
- **Preferred language** (`en`/`ar`) is applied with `setLocale()` **at sign-in only**; the locale switcher changes
  the `crm-locale` cookie for the session and does not write the profile (no endpoint in the MVP).
- **Domain rule: one transaction, one aggregate.** Sign-in reads `User`, creates a `RefreshTokenFamily`, and the
  family's `SessionStarted` domain event records the successful sign-in on `User` after commit. Deactivation and
  password changes revoke families through `UserDeactivated` / `UserPasswordChanged` domain events. A failed sign-in
  changes `User` only.
- **Failed sign-in is a committed fact, not a failed Result.** `SignInCommand` and `RefreshSessionCommand` return
  `Result<SignInOutcome>` **success** with `Succeeded = false` so the failed-attempt counter (or the family
  revocation on reuse) commits; `TransactionBehavior` rolls back every failed `Result`
  (`TransactionBehavior.cs` lines 53–58). The controller maps `Succeeded = false` to 401.

---

## Backend Tasks

All paths are under `backend/src/` unless they start with `backend/`. Use the **Write tool** for every new file
(Arabic text and `$` break Bash heredocs on this machine — memory note: no Python, no `pnpm` on PATH until
corepack). File-scoped namespaces, `sealed` classes, `[LoggerMessage]` for every log line, `IDateTime` for every
clock read. EF-only private constructors may assign `null!` to value-object properties exactly as `Branch.cs`
lines 18–22 do; **no other `!`** in new code.

### B1 — Package and solution

**File: `backend/Directory.Packages.props`** — after line 33 add:

```xml
    <!-- Story 07 - staff sign-in. Brings Microsoft.IdentityModel.JsonWebTokens (JsonWebTokenHandler). -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
```

Create the five Identity projects (B7) by copying the Organisation `.csproj` files, then
`dotnet sln backend/Crm.sln add` each one (they land under `src/Modules/Identity` like Organisation). Prove R1.

### B2 — Shared kernel: an `Unauthorized` error type

**File: `BuildingBlocks/Crm.SharedKernel/Results/ErrorType.cs`** — append `Unauthorized,` after `Domain,` (line 10).
Appending keeps existing numeric values.

**File: `…/Results/Error.cs`** — after line 17 add
`public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);`

**File: `BuildingBlocks/Crm.BuildingBlocks.Presentation/ErrorHandling/ErrorTypeMapping.cs`** — add
`ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,` to `ToStatusCode` and
`ErrorType.Unauthorized => ProblemTypes.Unauthorized,` to `ToProblemType` (before the `_` arms).

### B3 — Permission catalogue, declarations, startup guard (`Crm.BuildingBlocks.Application/Authorization/`)

**Create file: `…/Authorization/CrmPermissions.cs`**

```csharp
using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Application.Authorization;

/// <summary>The code-defined permission catalogue and the fixed role -> permission map (Story 07). Lives in the
/// building blocks, like CrmRoles, because AuthorizationBehavior needs it and a building block cannot reference a
/// module. Adding a permission is a code change here plus the requests that declare it.</summary>
public static class CrmPermissions
{
    public const string TicketsRead = "tickets.read";
    public const string TicketsWrite = "tickets.write";
    public const string TicketsAssign = "tickets.assign";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string AdminOrganisation = "admin.organisation";
    public const string AdminUsers = "admin.users";
    public const string AdminBranding = "admin.branding";

    public static readonly IReadOnlyList<string> All =
        [TicketsRead, TicketsWrite, TicketsAssign, CustomersRead, CustomersWrite, AdminOrganisation, AdminUsers, AdminBranding];

    private static readonly IReadOnlyList<string> AgentPermissions = [TicketsRead, TicketsWrite, CustomersRead, CustomersWrite];

    private static readonly Dictionary<string, IReadOnlyList<string>> ByRole = new(StringComparer.Ordinal)
    {
        [CrmRoles.Agent] = AgentPermissions,
        [CrmRoles.Supervisor] = [.. AgentPermissions, TicketsAssign],
        [CrmRoles.Administrator] = All,
    };

    /// <summary>Union of the roles' permissions, ordinal-sorted. An unknown role grants nothing.</summary>
    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        [.. roles.SelectMany(r => ByRole.TryGetValue(r, out IReadOnlyList<string>? p) ? p : [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static bool IsKnown(string permission) => All.Contains(permission, StringComparer.Ordinal);
}
```

**Create file: `…/Authorization/RequestAuthorizationAttributes.cs`** — three `sealed` attributes,
`[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`:

```csharp
/// <summary>The caller needs this permission in the active scope (CrmPermissions).</summary>
public sealed class RequirePermissionAttribute(string permission) : Attribute { public string Permission { get; } = permission; }

/// <summary>Any signed-in staff user; no permission.</summary>
public sealed class RequireAuthenticatedUserAttribute : Attribute;

/// <summary>No user needed: sign-in, refresh, sign-out, and in-process sign-in lookups. Never on a request an HTTP
/// endpoint exposes for data that belongs to someone.</summary>
public sealed class AllowAnonymousRequestAttribute : Attribute;
```

**Create file: `…/Authorization/RequestAuthorizationGuard.cs`**

- `public static class RequestAuthorizationGuard` with
  `public static void EnsureEveryRequestDeclares(IEnumerable<Assembly> assemblies)`.
- For every exported **and** internal concrete type (`!IsAbstract && !IsInterface && !IsGenericTypeDefinition`)
  assignable to `MediatR.IBaseRequest` in each assembly, collect problems:
  - count of the three attributes (`GetCustomAttribute<…>(inherit: false)`) ≠ 1 → `"{FullName}: declare exactly one of [RequirePermission], [RequireAuthenticatedUser], [AllowAnonymousRequest]"`;
  - `RequirePermission` with `!CrmPermissions.IsKnown(p)` → `"{FullName}: unknown permission '{p}'"`;
  - the `IRequest<TResponse>` interface's `TResponse` is not `Result` and not `Result<>` →
    `"{FullName}: must return Result or Result<T>"` (AuthorizationBehavior can only answer 401/403 through a Result).
- Any problem → `throw new InvalidOperationException("Every MediatR request must declare its authorisation (CLAUDE.md, Authorisation):" + Environment.NewLine + string.Join(Environment.NewLine, problems))`.
- Also expose `public static AuthorizationRequirement For(Type requestType)` returning a small
  `public sealed record AuthorizationRequirement(bool AllowsAnonymous, string? Permission)` (anonymous → `(true,
  null)`; authenticated → `(false, null)`; permission → `(false, p)`), used by the behaviour.

**File: `…/DependencyInjection.cs`** — after the `AddMediatR` block (line 34) add:

```csharp
        // Story 07: the app refuses to start when any request in any module lacks an authorisation declaration.
        RequestAuthorizationGuard.EnsureEveryRequestDeclares(moduleApplicationAssemblies);
```

### B4 — Claims, scope roles, schemes, paging (`Crm.BuildingBlocks.Application`)

**File: `…/Abstractions/CrmClaimTypes.cs`** — add `public const string DisplayName = "name";` and
`/// <summary>The refresh-token family (session) id. Story 07.</summary> public const string SessionId = "sid";`;
update the summary (lines 3–7): "The claims Story 07's staff JWT carries: sub, name, tenant_id, sid, one
membership per membership, one role per distinct role."

**Create file: `…/Abstractions/CrmAuthenticationSchemes.cs`** —
`public static class CrmAuthenticationSchemes { /// <summary>Staff JWT bearer (Agent + Admin Areas). Story 47 adds Portal.</summary> public const string Staff = "Staff"; }`.

**File: `…/Abstractions/ICurrentUserService.cs`** — add after line 8:

```csharp
    /// <summary>The caller's session (refresh-token family) id from the `sid` claim, or null. Story 07.</summary>
    string? SessionId { get; }
```

**File: `…/Abstractions/OrganisationScope.cs`** — after line 57 compute
`EffectiveRoles = memberships.Where(m => Departments.Contains(m.DepartmentId)).Select(m => m.Role).Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();`
and add after line 79:

```csharp
    /// <summary>Roles of the memberships inside <see cref="Departments"/> - what effective permissions follow
    /// (Story 07). Empty when the active scope was rejected.</summary>
    public IReadOnlyList<string> EffectiveRoles { get; }
```

**Create file: `…/Paging/PagedResult.cs`** —
`public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);` (namespace
`Crm.BuildingBlocks.Application.Paging`). Story 11 reuses it.

**Delete file: `…/Abstractions/IDevUserDirectory.cs`.**

### B5 — `AuthorizationBehavior` (replaces the placeholder)

**File: `…/Behaviors/AuthorizationBehavior.cs`** — rewrite:

```csharp
public sealed partial class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Resolved once per closed type; the startup guard already proved every request declares exactly one.
    private static readonly AuthorizationRequirement Requirement = RequestAuthorizationGuard.For(typeof(TRequest));

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        OrganisationScope scope = currentUser.Scope;

        if (Requirement.AllowsAnonymous || scope.IsUnrestricted)
        {
            return await next();
        }

        if (currentUser.UserId is null)
        {
            LogUnauthenticated(typeof(TRequest).Name);
            return CreateFailureResponse(Error.Unauthorized("Authorization.Unauthenticated", "Sign in to continue."));
        }

        if (Requirement.Permission is { } permission
            && !CrmPermissions.ForRoles(scope.EffectiveRoles).Contains(permission, StringComparer.Ordinal))
        {
            LogDenied(permission, typeof(TRequest).Name, currentUser.UserIdOrSystem, string.Join(',', scope.EffectiveRoles));
            return CreateFailureResponse(Error.Forbidden("Authorization.PermissionDenied", $"The permission '{permission}' is required."));
        }

        return await next();
    }
    // CreateFailureResponse: copy OrganisationScopeBehavior.cs lines 37-52.
    // [LoggerMessage(Information) "Unauthenticated call to {RequestName} refused"]
    // [LoggerMessage(Warning) "Permission {Permission} denied for {RequestName} to user {UserId} (roles {Roles})"]
}
```

The comment block at the top of the file becomes "Story 07: enforces the request's authorisation declaration
(CLAUDE.md, Authorisation); the handler never runs on a 401/403".

### B6 — Authentication plumbing (`Crm.BuildingBlocks.Presentation` / `.Infrastructure`)

**File: `BuildingBlocks/Crm.BuildingBlocks.Presentation/Authorization/CrmPolicies.cs`** — replace the
`Administrator` constant and policy:

```csharp
public static class CrmPolicies
{
    /// <summary>A signed-in staff user (Staff JWT scheme). Applied to every Agent and Admin controller by
    /// StaffAreaAuthorizationConvention; what the user may do is decided per request by AuthorizationBehavior.</summary>
    public const string Staff = "Staff";

    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Staff, policy => policy
                .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff)
                .RequireAuthenticatedUser())
            .Services;
}
```

**Create file: `…/Authorization/StaffAreaAuthorizationConvention.cs`** —
`public sealed class StaffAreaAuthorizationConvention : IControllerModelConvention`; in `Apply(ControllerModel
controller)`: read `controller.RouteValues.TryGetValue("area", out string? area)`; when `area` is `CrmAreas.Agent`
or `CrmAreas.Admin` (ordinal), for every `action` in `controller.Actions` and every `selector` in
`action.Selectors`, `selector.EndpointMetadata.Add(new AuthorizeAttribute(CrmPolicies.Staff));`. Summary: "Every
Agent/Admin endpoint requires a signed-in staff user, with no attribute to forget; `[AllowAnonymous]` on an action
(sign-in, refresh, sign-out) wins. Portal and Public are untouched (stories 47, 55)." Prove R3.

**File: `…/Crm.BuildingBlocks.Presentation/AREAS.md`** — Agent and Admin sections: replace the "Story 09 … No
[Authorize] yet" comments with "Story 07: every controller here requires the Staff JWT
(`StaffAreaAuthorizationConvention`); each MediatR request declares its permission (`[RequirePermission]`).
`[AllowAnonymous]` only on the Identity sign-in actions and the Development-only diagnostics." Admin section
line 26: replace "which carry `[Authorize(Policy = CrmPolicies.Administrator)]`" with "whose requests require
`admin.organisation`". Portal/Public comments: "Anonymous until story 47 (portal JWT) / 55 (API keys)."

**File: `BuildingBlocks/Crm.BuildingBlocks.Infrastructure/Identity/CurrentUserService.cs`** — add
`public string? SessionId => Claim(CrmClaimTypes.SessionId);`; line 8 comment → "Story 06: scope from membership
claims + X-Crm-Scope. Story 07: the claims come from the staff JWT (Identity module)."; lines 11–13 comment → keep
the per-principal cache and say why it stays safe ("an authentication handler that resolves this service before
`HttpContext.User` is set would otherwise pin `None`").

**File: `…/Identity/SystemCurrentUser.cs`** — add `public string? SessionId => null;`.

**Delete file: `…/Identity/DevUserAuthenticationHandler.cs`.**

### B7 — Identity module: projects

| Project | References |
|---|---|
| `Modules/Identity/Crm.Modules.Identity.Domain` | `Crm.SharedKernel` |
| `…Contracts` | `Crm.SharedKernel` |
| `…Application` | `Crm.BuildingBlocks.Application`, `…Identity.Domain`, `…Identity.Contracts`, `..\..\Organisation\Crm.Modules.Organisation.Contracts` |
| `…Infrastructure` | `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `PackageReference Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore.Design` exactly as Organisation's; `Crm.BuildingBlocks.Infrastructure`, `…Identity.Application`, `..\..\Organisation\Crm.Modules.Organisation.Contracts` (seed ids) |
| `…Presentation` | `DisableTransitiveProjectReferences` + `Crm.SharedKernel`, `Crm.BuildingBlocks.Application`, `Crm.BuildingBlocks.Presentation`, `…Identity.Application` (copy Organisation's comment block) |

`AssemblyReference.cs` in Application and Presentation, copied from Organisation. Every reference above is allowed
by `Directory.Build.targets` rules 1–5.

### B8 — Identity module: Domain (`Crm.Modules.Identity.Domain`)

Invariants throw `DomainException(new DomainError(code, message))` (English). Aggregates are `sealed`,
`AuditableEntity<TId>`, `IHasRowVersion` (`public byte[] RowVersion { get; private set; } = [];`), private EF
constructor, factory with an optional well-known id (as `Branch.Create`).

- **`UserId.cs`** — `public sealed record UserId(Guid Value) : StronglyTypedId(Value) { public static UserId New() => new(Guid.NewGuid()); }`
- **`EmailAddress.cs`** — `sealed partial class EmailAddress : ValueObject`, `Value`; private ctor parameter named
  `value` (EF owned-type rule, `SampleName.cs` lines 8–13). `public static EmailAddress Create(string value)`:
  `ArgumentNullException.ThrowIfNull`; trim; `ToLowerInvariant()`; length ≤ 254 and
  `[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]` else `Identity.EmailInvalid` "The email address is not
  valid.". `public static bool TryCreate(string? value, [NotNullWhen(true)] out EmailAddress? email)` (never
  throws; used by sign-in so a malformed address is a generic 401, not a 422).
- **`DisplayName.cs`** — value object `Value`, ctor parameter `value`; trimmed, 1–200 else `Identity.DisplayNameRequired`
  / `Identity.DisplayNameTooLong`.
- **`PreferredLocale.cs`** — value object `Value` ∈ {`en`, `ar`} (ordinal after `ToLowerInvariant`) else
  `Identity.LocaleUnsupported`; `public static readonly PreferredLocale English = new("en"), Arabic = new("ar");`.
- **`LockoutPolicy.cs`** — `public sealed record LockoutPolicy(int MaxFailedAttempts, TimeSpan Window)` (guards:
  `MaxFailedAttempts >= 1`, `Window > TimeSpan.Zero`, else `ArgumentOutOfRangeException` — configuration error).
- **Domain events** (`sealed record … : IDomainEvent`): `UserDeactivated(UserId UserId, DateTimeOffset OccurredOnUtc)`,
  `UserPasswordChanged(UserId UserId, Guid? KeepSessionId, DateTimeOffset OccurredOnUtc)`,
  `SessionStarted(UserId UserId, RefreshTokenFamilyId SessionId, DateTimeOffset OccurredOnUtc)`.
- **`User.cs`** — `User : AuditableEntity<UserId>, IHasRowVersion` (**not** `ITenantScoped` — Product rules):

```csharp
    public TenantId TenantId { get; private set; } = TenantId.None;   // plain column, convention-converted
    public EmailAddress Email { get; private set; }
    public DisplayName DisplayName { get; private set; }
    public PreferredLocale PreferredLocale { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int FailedSignInCount { get; private set; }
    public DateTimeOffset? LockedOutUntilUtc { get; private set; }
    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    public static User Create(TenantId tenantId, EmailAddress email, DisplayName displayName, PreferredLocale locale,
        string passwordHash, UserId? id = null);   // tenant != None (Identity.TenantRequired); hash not blank (Identity.PasswordHashRequired)
    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedOutUntilUtc is { } until && until > nowUtc;
    public void RecordFailedSignIn(DateTimeOffset nowUtc, LockoutPolicy policy); // ignored while locked; ++count; at max: LockedOutUntilUtc = now + Window, count = 0
    public void RecordSuccessfulSignIn(DateTimeOffset nowUtc);                   // count = 0, LockedOutUntilUtc = null, LastSignInAtUtc = now
    public void Rename(DisplayName displayName);
    public void ChangePreferredLocale(PreferredLocale locale);
    public void ChangePassword(string newPasswordHash, Guid? keepSessionId, DateTimeOffset nowUtc); // raises UserPasswordChanged(Id, keepSessionId, now)
    public void ResetPassword(string newPasswordHash, DateTimeOffset nowUtc);   // clears lockout; raises UserPasswordChanged(Id, null, now)
    public void Deactivate(DateTimeOffset nowUtc);   // already inactive -> Identity.UserAlreadyInactive; raises UserDeactivated
    public void Reactivate();                         // already active -> Identity.UserAlreadyActive; clears lockout
```

  `RecordFailedSignIn` returns nothing; the handler reads `IsLockedOut(now)` afterwards to log "locked".
- **`RefreshTokenFamilyId.cs`** — typed id with `New()`.
- **`RefreshToken.cs`** — child entity `sealed class RefreshToken : Entity<Guid>` (shape of `SampleLine.cs`):
  `TokenHash` (string), `IssuedAtUtc`, `ExpiresAtUtc`, `RotatedAtUtc?`; `internal RefreshToken(string tokenHash,
  DateTimeOffset issuedAtUtc, DateTimeOffset expiresAtUtc) : base(Guid.NewGuid())`; `internal void
  MarkRotated(DateTimeOffset nowUtc)`.
- **`RefreshTokenRotation.cs`** — `public enum RefreshTokenRotation { Rotated, ReuseDetected, Expired, Revoked }`.
- **`SessionRevocationReason.cs`** — `public enum SessionRevocationReason { SignedOut, ReuseDetected, UserDeactivated, PasswordChanged, PasswordReset }`.
- **`RefreshTokenFamily.cs`** — `RefreshTokenFamily : AuditableEntity<RefreshTokenFamilyId>, IHasRowVersion`:
  `UserId UserId`, `DateTimeOffset? RevokedAtUtc`, `SessionRevocationReason? RevocationReason`, private
  `List<RefreshToken> _tokens`, `IReadOnlyCollection<RefreshToken> Tokens => _tokens.AsReadOnly()`.

```csharp
    public static RefreshTokenFamily Start(UserId userId, string tokenHash, DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc, RefreshTokenFamilyId? id = null);   // hash not blank; expires > now; raises SessionStarted

    /// Rotated: the presented token is the family's current one and unexpired -> marked rotated, newHash appended.
    /// ReuseDetected: the presented token was ALREADY rotated -> the family is revoked (ReuseDetected).
    /// Expired / Revoked: nothing changes.
    public RefreshTokenRotation Rotate(string presentedHash, string newHash, DateTimeOffset nowUtc, DateTimeOffset newExpiresAtUtc);

    /// Idempotent: a revoked family stays revoked with its first reason.
    public void Revoke(SessionRevocationReason reason, DateTimeOffset nowUtc);
    public bool IsRevoked => RevokedAtUtc is not null;
```

  Order inside `Rotate`: revoked → `Revoked`; token not in `_tokens` → `Revoked` (defensive; the spec loads by hash);
  `token.RotatedAtUtc is not null` → `Revoke(ReuseDetected, now)`, `ReuseDetected`; `token.ExpiresAtUtc <= now` →
  `Expired`; else rotate. Hash comparison `string.Equals(…, StringComparison.Ordinal)`.

### B9 — Identity module: Application (`Crm.Modules.Identity.Application`)

**Root files**

- `IIdentityUnitOfWork.cs` — `public interface IIdentityUnitOfWork : IUnitOfWork;`
- `Abstractions/IPasswordHasher.cs` — `string Hash(string password); bool Verify(string passwordHash, string
  password); void VerifyAgainstDummy(string password);` (the last keeps unknown-email timing equal).
- `Abstractions/IRefreshTokenGenerator.cs` — `NewRefreshToken Create(); string Hash(string refreshToken);` with
  `public sealed record NewRefreshToken(string Token, string TokenHash);`.
- `Abstractions/IAccessTokenIssuer.cs` — `IssuedAccessToken Issue(AccessTokenSubject subject, DateTimeOffset nowUtc);`
  with `public sealed record AccessTokenSubject(Guid UserId, string DisplayName, Guid TenantId, Guid SessionId,
  IReadOnlyList<TokenMembership> Memberships);`, `public sealed record TokenMembership(Guid DepartmentId, Guid
  BranchId, string Role);`, `public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);`.
- `SessionSettings.cs` — `public sealed record SessionSettings(TimeSpan RefreshTokenLifetime, int
  MaxFailedSignInAttempts, TimeSpan LockoutWindow) { public LockoutPolicy Lockout => new(MaxFailedSignInAttempts, LockoutWindow); }`
  (bound in B10 from `Identity:Sessions`).
- `Dtos.cs` (primitives only, `FromDomain` where an aggregate maps — `Organisation/…/Dtos.cs` style):

```csharp
public sealed record SessionTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);

/// <summary>Succeeded = false still COMMITS (failed-attempt counter, reuse revocation) - see Product rules.</summary>
public sealed record SignInOutcome(SessionTokens? Tokens, string? FailureCode)
{
    public bool Succeeded => Tokens is not null;
    public static SignInOutcome Success(SessionTokens tokens) => new(tokens, null);
    public static SignInOutcome Failed(string code) => new(null, code);
}

public sealed record UserDto(Guid Id, string Email, string DisplayName, string PreferredLocale, bool IsActive,
    bool IsLockedOut, DateTimeOffset? LastSignInAtUtc, DateTimeOffset? CreatedAtUtc, byte[] RowVersion)
{
    public static UserDto FromDomain(User user, DateTimeOffset nowUtc) => …;   // IsLockedOut = user.IsLockedOut(nowUtc)
}

public sealed record MeMembershipDto(Guid DepartmentId, Guid BranchId, string Role);
public sealed record MeDto(Guid Id, string Email, string DisplayName, string PreferredLocale, Guid TenantId,
    Guid? ActiveDepartmentId, IReadOnlyList<MeMembershipDto> Memberships, IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
```

- `IdentityErrors.cs` — `internal static class IdentityErrors` with the codes used below as `const string` and
  factory helpers (`UserNotFound(Guid id)` → `Error.NotFound("Identity.UserNotFound", …)`, `OutOfScope()` →
  `Error.Forbidden("Organisation.OutOfScope", "The user belongs to another organisation.")`,
  `InvalidCredentials = "Identity.InvalidCredentials"`, `InvalidRefreshToken = "Identity.InvalidRefreshToken"`).
- `AccessTokenSubjectFactory.cs` — `internal static AccessTokenSubject Build(User user, UserMemberships memberships,
  RefreshTokenFamilyId sessionId)`: keeps only memberships when `memberships.TenantId == user.TenantId.Value` or the
  list is empty (a mismatch drops them all — defensive); `TenantId = user.TenantId.Value`.

**Specifications/** (`Specification<T>` subclasses; no scope lift anywhere in this module)

| Spec | Criteria |
|---|---|
| `UserByEmailSpecification(EmailAddress email)` | `u.Email.Value == email.Value` |
| `UsersSpecification(TenantId tenant, string? search, int? page, int? pageSize)` | `u.TenantId.Equals(tenant) && (search == null \|\| u.Email.Value.Contains(search) \|\| u.DisplayName.Value.Contains(search))`, `ApplyOrderBy(u => u.DisplayName.Value)`, paging when both given, `AsReadOnly()`; search trimmed, empty → null (constructor) |
| `RefreshTokenFamilyByTokenHashSpecification(string tokenHash)` | `f.Tokens.Any(t => t.TokenHash == tokenHash)` (tracked) |
| `ActiveRefreshTokenFamiliesOfUserSpecification(UserId userId)` | `f.UserId.Equals(userId) && f.RevokedAtUtc == null` (tracked) |

`UsersSpecification` is the hand-written tenant filter of Product rules — its summary says so.

**Commands** (folder per use case: record + handler + validator; handlers inject `IIdentityUnitOfWork`, the
repositories, `IDateTime`, `ICurrentUserService` as needed; every record carries its authorisation attribute)

| Use case | Record | Attribute |
|---|---|---|
| `SignIn` | `SignInCommand(string Email, string Password) : ICommand<SignInOutcome>` | `[AllowAnonymousRequest]` |
| `RefreshSession` | `RefreshSessionCommand(string RefreshToken) : ICommand<SignInOutcome>` | `[AllowAnonymousRequest]` |
| `SignOut` | `SignOutCommand(string? RefreshToken) : ICommand` | `[AllowAnonymousRequest]` |
| `ChangePassword` | `ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand` | `[RequireAuthenticatedUser]` |
| `CreateUser` | `CreateUserCommand(string Email, string DisplayName, string PreferredLocale, string InitialPassword) : ICommand<UserDto>` | `[RequirePermission(CrmPermissions.AdminUsers)]` |
| `UpdateUserProfile` | `UpdateUserProfileCommand(Guid Id, string DisplayName, string PreferredLocale, byte[] RowVersion) : ICommand<UserDto>` | same |
| `DeactivateUser` | `DeactivateUserCommand(Guid Id, byte[] RowVersion) : ICommand<UserDto>` | same |
| `ReactivateUser` | `ReactivateUserCommand(Guid Id, byte[] RowVersion) : ICommand<UserDto>` | same |
| `ResetUserPassword` | `ResetUserPasswordCommand(Guid Id, string NewPassword, byte[] RowVersion) : ICommand<UserDto>` | same |

Handler rules:

- **`SignInCommandHandler`** (`IRepository<User, UserId>`, `IRepository<RefreshTokenFamily, RefreshTokenFamilyId>`,
  `IPasswordHasher`, `IRefreshTokenGenerator`, `IAccessTokenIssuer`, `IOrganisationModuleApi`, `SessionSettings`,
  `IDateTime`, `IIdentityUnitOfWork`, `ILogger<…>`):
  1. `now = dateTime.UtcNow`. `EmailAddress.TryCreate(request.Email, out email)` false → `VerifyAgainstDummy`,
     log `malformed`, return `Success(SignInOutcome.Failed(InvalidCredentials))`.
  2. `user = FirstOrDefaultAsync(new UserByEmailSpecification(email))`; null → `VerifyAgainstDummy`, log
     `unknown`, Failed.
  3. `!user.IsActive` → `VerifyAgainstDummy`, log `inactive` (user id), Failed. `user.IsLockedOut(now)` →
     `VerifyAgainstDummy`, log `locked`, Failed.
  4. `!hasher.Verify(user.PasswordHash, request.Password)` → `user.RecordFailedSignIn(now, settings.Lockout)`;
     `SaveChangesAsync`; log `wrong-password` (Warning + "account locked until {Until}" when
     `user.IsLockedOut(now)` is now true); Failed.
  5. `memberships = await organisation.GetMembershipsAsync(user.Id.Value, ct)`; failure → return
     `Result.Failure<SignInOutcome>(memberships.Error)` (rolls back; logged by LoggingBehavior). **No integration
     event may be published before this call** (a nested query's `TransactionBehavior` would throw on a pending
     event — `TransactionBehavior.cs` lines 34–39, 109–117).
  6. `refresh = generator.Create()`; `family = RefreshTokenFamily.Start(user.Id, refresh.TokenHash, now, now +
     settings.RefreshTokenLifetime)`; `familyRepository.Add(family)`.
  7. `access = issuer.Issue(AccessTokenSubjectFactory.Build(user, memberships.Value, family.Id), now)`;
     `SaveChangesAsync`; log `signed-in` (Information, user id, session id);
     return `Success(SignInOutcome.Success(new SessionTokens(access.Token, access.ExpiresAtUtc, refresh.Token,
     family.Tokens.Single().ExpiresAtUtc)))`.

  Logging: one `[LoggerMessage(Information)] "Sign-in refused ({Reason}) for user {UserId}"` with `UserId` "unknown"
  when there is none, one Warning for the lockout, one Information for success. **Never** log `request` (the
  `LoggingBehavior` Debug payload is redacted by name — `Password` matches `SensitiveData.Fragments`).
  Validator: `Email` `NotEmpty().MaximumLength(254)`; `Password` `NotEmpty().MaximumLength(128)` (no format rules —
  a format rule would reveal nothing but would make a typo a 400 instead of the generic 401; keep it 401).
- **`RefreshSessionCommandHandler`**: `hash = generator.Hash(request.RefreshToken)`; family by
  `RefreshTokenFamilyByTokenHashSpecification`; null → log `unknown-token`, Failed(`InvalidRefreshToken`).
  `user = GetByIdAsync(family.UserId)`; null or `!IsActive` → `family.Revoke(UserDeactivated, now)`, save, Failed.
  `next = generator.Create()`; `rotation = family.Rotate(hash, next.TokenHash, now, now + lifetime)`:
  `ReuseDetected` → save, **Warning** `"Refresh token reuse detected; session {SessionId} of user {UserId} revoked"`,
  Failed; `Expired`/`Revoked` → Failed. `Rotated` → memberships through `IOrganisationModuleApi` (as sign-in step 5),
  issue the access token with `family.Id`, save, Success with the new refresh token and its expiry.
  Validator: `RefreshToken` `NotEmpty().MaximumLength(200)`.
- **`SignOutCommandHandler`**: null/blank token → `Result.Success()`; otherwise family by hash; when found
  `Revoke(SignedOut, now)` and save; always `Result.Success()` (idempotent, no oracle). No validator.
- **`ChangePasswordCommandHandler`**: user by `Guid.Parse(currentUser.UserId)` (the attribute guarantees it — use
  `Guid.TryParse`; failure → `Error.Unauthorized("Authorization.Unauthenticated", …)`); null → 401 same;
  `!hasher.Verify(user.PasswordHash, CurrentPassword)` → `Error.Validation("Identity.CurrentPasswordInvalid",
  "CurrentPassword: The current password is incorrect.")` (the `Field: message` shape `ValidationErrorParser`
  reads); `user.ChangePassword(hasher.Hash(NewPassword), Guid.TryParse(currentUser.SessionId, out var sid) ? sid :
  null, now)`; save. Validator: `CurrentPassword` `NotEmpty()`; `NewPassword` password rule (below) and
  `NotEqual(c => c.CurrentPassword)`.
- **`CreateUserCommandHandler`**: `tenant = currentUser.Scope.Tenant`; `TenantId.None` →
  `Error.Forbidden("Identity.TenantRequired", …)`; `EmailAddress.Create` (domain guard; validator already checked);
  `CountAsync(new UserByEmailSpecification(email)) > 0` → `Error.Conflict("Identity.EmailTaken", "A user with this
  email already exists.")`; `User.Create(…, hasher.Hash(InitialPassword))`; add; save; `UserDto.FromDomain(user, now)`.
  Validator: `Email` `NotEmpty().MaximumLength(254).EmailAddress()`; `DisplayName` `NotEmpty().MaximumLength(200)`;
  `PreferredLocale` `Must(l => l is "en" or "ar")` "Preferred language must be en or ar."; `InitialPassword` password rule.
- **`UpdateUserProfileCommandHandler` / `DeactivateUserCommandHandler` / `ReactivateUserCommandHandler` /
  `ResetUserPasswordCommandHandler`**: `GetByIdAsync(new UserId(Id))`; null → `IdentityErrors.UserNotFound`;
  `user.TenantId != currentUser.Scope.Tenant` → `IdentityErrors.OutOfScope()`; `SetOriginalRowVersion(user,
  RowVersion)`; then `Rename` + `ChangePreferredLocale` / `Deactivate(now)` (first:
  `request.Id.ToString() == currentUser.UserId` → `Error.Domain("Identity.CannotDeactivateSelf", "You cannot
  deactivate your own account.")`) / `Reactivate()` / `ResetPassword(hasher.Hash(NewPassword), now)`; save;
  `UserDto`. Validators: `Id` `NotEmpty()`, `RowVersion` `NotEmpty()`, profile fields as `CreateUser`,
  `NewPassword` password rule.
- **Password rule** (one extension in `Validation/PasswordRules.cs`: `public static IRuleBuilderOptions<T, string>
  StaffPassword<T>(this IRuleBuilder<T, string> rule) => rule.NotEmpty().MinimumLength(10).MaximumLength(128)
  .Matches("[A-Za-z]").WithMessage("The password needs at least one letter.").Matches("[0-9]").WithMessage("The
  password needs at least one digit.");`).

**Queries** (`IRequest<Result<…>>`; not cached — they depend on the caller and change on every sign-in)

| Use case | Returns | Attribute | Rules |
|---|---|---|---|
| `GetMe()` | `MeDto` | `[RequireAuthenticatedUser]` | user by current id; null or `!IsActive` → `Error.Unauthorized("Authorization.Unauthenticated", …)`; memberships from `currentUser.Scope.Memberships`; `Roles = scope.EffectiveRoles`; `Permissions = CrmPermissions.ForRoles(scope.EffectiveRoles)`; `ActiveDepartmentId = scope.ActiveDepartment?.Value` |
| `ListUsers(string? Search, int Page = 1, int PageSize = 20)` | `PagedResult<UserDto>` | `[RequirePermission(CrmPermissions.AdminUsers)]` | `ListAsync` + `CountAsync` over `UsersSpecification(currentUser.Scope.Tenant, …)`; validator `Page >= 1`, `PageSize` 1–100, `Search` ≤ 200 |
| `GetUser(Guid Id)` | `UserDto` | same | not found → 404; other tenant → 403 `Organisation.OutOfScope` |

**DomainEventHandlers/** (the first `INotificationHandler`s in the solution — R7; each `sealed`, injects the
repositories, `IIdentityUnitOfWork`, `IDateTime`; each ends with `SaveChangesAsync`; none publishes an integration
event):

- `RecordSignInWhenSessionStarted : INotificationHandler<DomainEventNotification<SessionStarted>>` —
  `user.RecordSuccessfulSignIn(notification.DomainEvent.OccurredOnUtc)`.
- `RevokeSessionsWhenUserDeactivated : INotificationHandler<DomainEventNotification<UserDeactivated>>` — every
  family of `ActiveRefreshTokenFamiliesOfUserSpecification` → `Revoke(UserDeactivated, now)`.
- `RevokeSessionsWhenPasswordChanged : INotificationHandler<DomainEventNotification<UserPasswordChanged>>` — same,
  skipping `KeepSessionId`; reason `PasswordChanged` when `KeepSessionId` is set, else `PasswordReset`.

### B10 — Identity module: Contracts, Infrastructure

**Contracts** — `DevSeed/IdentityDevSeed.cs`: `public static class IdentityDevSeed` with the four seed emails as
`const string` (`AdminEmail = "admin@crm.local"`, `SupervisorEmail = "supervisor@crm.local"`,
`AgentAEmail = "agent.a@crm.local"`, `AgentBEmail = "agent.b@crm.local"`). No module API in this story — no other
module needs one yet (Story 15 adds `IIdentityModuleApi` when it shows agent names).

**Infrastructure (`Crm.Modules.Identity.Infrastructure`)**

- **`Security/JwtSettings.cs`** — `public sealed record JwtSettings(string Issuer, string Audience, string SigningKey,
  TimeSpan AccessTokenLifetime, TimeSpan ClockSkew)` with `internal void Validate()`: issuer/audience non-blank,
  `Encoding.UTF8.GetByteCount(SigningKey) >= 32`, lifetime > 0 — else `InvalidOperationException("Identity:Jwt:…
  is not configured. Set it through the environment (Identity__Jwt__SigningKey), never appsettings.json.")`.
- **`Security/JwtAccessTokenIssuer.cs`** — `IAccessTokenIssuer`; `JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }`;
  `SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)`:

```csharp
        DateTimeOffset expires = nowUtc + settings.AccessTokenLifetime;
        var claims = new Dictionary<string, object>
        {
            [CrmClaimTypes.UserId] = subject.UserId.ToString("D"),
            [CrmClaimTypes.DisplayName] = subject.DisplayName,
            [CrmClaimTypes.TenantId] = subject.TenantId.ToString("D"),
            [CrmClaimTypes.SessionId] = subject.SessionId.ToString("D"),
            // exactly Story 06's shape (DevUserAuthenticationHandler lines 64-68, deleted in B6)
            [CrmClaimTypes.Membership] = subject.Memberships.Select(m => $"{m.DepartmentId:D}|{m.BranchId:D}|{m.Role}").ToArray(),
            [CrmClaimTypes.Role] = subject.Memberships.Select(m => m.Role).Distinct(StringComparer.Ordinal).ToArray(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [JwtRegisteredClaimNames.Iat] = nowUtc.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Nbf] = nowUtc.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Exp] = expires.ToUnixTimeSeconds(),
        };
        string token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer, Audience = settings.Audience, Claims = claims, SigningCredentials = credentials,
        });
        return new IssuedAccessToken(token, expires);
```

  Prove R2 (fallback: string payload via `JsonObject`).
- **`Security/AspNetPasswordHasher.cs`** — `IPasswordHasher` over
  `Microsoft.AspNetCore.Identity.PasswordHasher<HashSubject>` (`private sealed class HashSubject;`, one static
  instance). `Verify` → `result is PasswordVerificationResult.Success or SuccessRehashNeeded`. `VerifyAgainstDummy`
  → `Verify(DummyHash, password)` where `private static readonly string DummyHash = Hasher.HashPassword(Subject,
  "dummy-password-for-timing-0")`.
- **`Security/RefreshTokenGenerator.cs`** — `Create()`: `RandomNumberGenerator.GetBytes(32)` →
  `Base64Url.EncodeToString` (`System.Buffers.Text.Base64Url`, .NET 9+); `Hash()`: `SHA256.HashData(UTF8 bytes)` →
  `Convert.ToHexString`.
- **`Persistence/IdentityDbContext.cs`** — `SchemaName = "identity"`; ctor
  `(DbContextOptions<IdentityDbContext> options, ICurrentUserService currentUser) : ModuleDbContext(options,
  currentUser), IIdentityUnitOfWork`; DbSets `Users`, `RefreshTokenFamilies`. (No clash: ASP.NET Core Identity's
  EF package is not referenced.)
- **`Persistence/IdentityDbContextFactory.cs`** — as Organisation's; `new(options, SystemCurrentUser.Instance)`.
- **`Persistence/Configurations/UserConfiguration.cs`** — table `Users`; `Id` `HasConversion(id => id.Value, v =>
  new UserId(v)).ValueGeneratedNever()` (UserId is not a scope type — like `MembershipId`); **no** conversion for
  `TenantId` (the `ModuleDbContext` convention); `OwnsOne(Email, e => { e.Property(p => p.Value).HasColumnName("Email").HasMaxLength(254).IsRequired(); e.HasIndex(p => p.Value).IsUnique(); })`;
  `OwnsOne(DisplayName, … "DisplayName", 200)`; `PreferredLocale` `HasConversion(l => l.Value, v =>
  PreferredLocale.Create(v)).HasColumnName("PreferredLocale").HasMaxLength(2)`; `PasswordHash` max 500 required;
  `RowVersion` `IsRowVersion()`; `HasIndex(x => x.TenantId)`. Email is `OwnsOne` (not `HasConversion`) so
  `u.Email.Value.Contains(search)` translates.
- **`Persistence/Configurations/RefreshTokenFamilyConfiguration.cs`** — table `RefreshTokenFamilies`; `Id`
  conversion; `UserId` `HasConversion(id => id.Value, v => new UserId(v))`; `RevocationReason`
  `HasConversion<string>().HasMaxLength(30)`; `OwnsMany(Tokens, t => { t.ToTable("RefreshTokens");
  t.WithOwner().HasForeignKey("FamilyId"); t.HasKey(x => x.Id); t.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
  t.HasIndex(x => x.TokenHash).IsUnique(); })`; `Navigation(Tokens).UsePropertyAccessMode(PropertyAccessMode.Field)`
  (`SampleAggregateConfiguration.cs` lines 62–71); `RowVersion`; `HasIndex(x => x.UserId)`.
- **Migration:** `pwsh backend/scripts/ef.ps1 -Module Identity migrations add Initial`. Read the generated file:
  schema `identity` only (plus its `outbox_messages`/`outbox_message_consumers`), unique `Email` and `TokenHash`
  indexes, no `dbo` object, no FK outside the schema.
- **`Persistence/IdentitySeeder.cs`** — `IModuleSeeder`, idempotent per well-known id (`AnyAsync(u => u.Id == id)`),
  Development only by construction (seeders run from `MigrateAndSeedModulesAsync`, Program.cs line 121). Reads
  `Identity:DevSeed:Password` from `IConfiguration`; **missing → log a Warning and seed nothing**. Creates, with
  `TenantId = new TenantId(OrganisationDevSeed.TenantId)` and `hasher.Hash(password)`:

  | Id (`OrganisationDevSeed`) | Email (`IdentityDevSeed`) | Display name | Locale |
  |---|---|---|---|
  | `AdminUserId` | `admin@crm.local` | "System Administrator" | `en` |
  | `SupervisorUserId` | `supervisor@crm.local` | "سارة القحطاني" | `ar` |
  | `AgentAUserId` | `agent.a@crm.local` | "Omar Haddad" | `en` |
  | `AgentBUserId` | `agent.b@crm.local` | "نورة الشمري" | `ar` |

- **`IdentityModule.cs`** — `Name => "Identity"`; in `AddServices`:

```csharp
        services.AddModuleDbContext<IdentityDbContext, IIdentityUnitOfWork>(configuration, Name, IdentityDbContext.SchemaName, ApplicationAssembly);
        services.AddRepository<User, UserId, IdentityDbContext>();
        services.AddRepository<RefreshTokenFamily, RefreshTokenFamilyId, IdentityDbContext>();
        services.AddScoped<IModuleSeeder, IdentitySeeder>();

        JwtSettings jwt = configuration.GetSection("Identity:Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("Identity:Jwt is not configured.");
        jwt.Validate();
        SessionSettings sessions = configuration.GetSection("Identity:Sessions").Get<SessionSettings>()
            ?? throw new InvalidOperationException("Identity:Sessions is not configured.");
        services.AddSingleton(jwt);
        services.AddSingleton(sessions);

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // The staff scheme. The host set it as the default (Program.cs); Story 47 adds the portal scheme here.
        services.AddAuthentication().AddJwtBearer(CrmAuthenticationSchemes.Staff, options =>
        {
            options.MapInboundClaims = false; // keep "sub", "role", "membership" as issued (R2)
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
                ValidateLifetime = true, RequireExpirationTime = true,
                ClockSkew = jwt.ClockSkew,
                NameClaimType = CrmClaimTypes.UserId,
                RoleClaimType = CrmClaimTypes.Role,
            };
        });
```

  Missing configuration throws inside `AddServices` → the host logs Fatal and exits (it is inside Program.cs's
  `try`, line 26) — the refuse-to-start behaviour for a missing signing key.

### B11 — Rate limit on sign-in (`Crm.BuildingBlocks.Presentation/RateLimiting`)

**File: `RateLimitingServiceCollectionExtensions.cs`** — add `public static class CrmRateLimitPolicies { public const
string SignIn = "sign-in"; }` and, inside `AddRateLimiter` (after line 44), read
`configuration.GetSection("RateLimiting:SignIn").Get<PublicRateLimitSettings>()` (throw like line 26–27 when
missing) and add:

```csharp
            options.AddPolicy(CrmRateLimitPolicies.SignIn, context => RateLimitPartition.GetFixedWindowLimiter(
                $"sign-in:{context.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = signIn.PermitLimit, Window = signIn.Window, QueueLimit = 0 }));
```

`OnRejected` (lines 46–67) already writes the 429 problem for every policy. Prove R10.

### B12 — Identity module: Presentation (`Crm.Modules.Identity.Presentation`)

**Create file: `RefreshCookie.cs`** — `internal static class RefreshCookie { public const string Name = "crm_refresh";
public const string Path = "/api/Agent/Auth"; public static CookieOptions Options(DateTimeOffset expiresAtUtc) =>
new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = Path, Expires = expiresAtUtc,
IsEssential = true }; }` (R6 fallback: `Secure = request.IsHttps`).

**Controllers** (`ApiControllerBase`, `[Area(…)]`, `ProducesResponseType` per status like `BranchesController`):

| Controller (Area) | Action | Request → command | Result |
|---|---|---|---|
| `Controllers/Agent/AuthController` | `[HttpPost("login")] [AllowAnonymous] [EnableRateLimiting(CrmRateLimitPolicies.SignIn)]` | `LoginRequest(string Email, string Password)` → `SignInCommand` | success: append cookie, 200 `SignInResponse`; `Succeeded = false` → `Problem(Error.Unauthorized(outcome.FailureCode, "Invalid email or password."))`; other failures → `Problem(error)` |
| | `[HttpPost("refresh")] [AllowAnonymous] [EnableRateLimiting(SignIn)]` | `RefreshRequest(string? RefreshToken)`; token = body value ?? `Request.Cookies[RefreshCookie.Name]`; none → 401 `Identity.InvalidRefreshToken` | success: cookie + 200 `SignInResponse`; not succeeded → **delete the cookie** (`Response.Cookies.Delete(Name, new CookieOptions { Path = Path })`) + 401 "Your session has ended. Sign in again." |
| | `[HttpPost("logout")] [AllowAnonymous]` | `RefreshRequest` (same fallback) → `SignOutCommand` | delete cookie; 204 |
| `Controllers/Agent/MeController` | `[HttpGet]` | `GetMeQuery` | 200 `MeDto` |
| | `[HttpPost("change-password")]` | `ChangePasswordRequest(string CurrentPassword, string NewPassword)` → `ChangePasswordCommand` | 204 |
| `Controllers/Admin/UsersController` | `[HttpGet]` | `[FromQuery] string? search, int page = 1, int pageSize = 20` → `ListUsersQuery` | 200 `PagedResult<UserDto>` |
| | `[HttpGet("{id:guid}")]` | `GetUserQuery` | 200 / 403 / 404 |
| | `[HttpPost]` | `CreateUserRequest(string Email, string DisplayName, string PreferredLocale, string InitialPassword)` | 200 / 400 / 409 |
| | `[HttpPut("{id:guid}/profile")]` | `UpdateUserProfileRequest(string DisplayName, string PreferredLocale, byte[] RowVersion)` | 200 / 400 / 404 / 409 |
| | `[HttpPost("{id:guid}/deactivate")]`, `[HttpPost("{id:guid}/reactivate")]` | `UserRowVersionRequest(byte[] RowVersion)` | 200 / 404 / 409 / 422 |
| | `[HttpPut("{id:guid}/password")]` | `ResetPasswordRequest(string NewPassword, byte[] RowVersion)` | 200 / 400 / 404 / 409 |

`public sealed record SignInResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken,
DateTimeOffset RefreshTokenExpiresAtUtc, string TokenType = "Bearer");` next to `AuthController`. Request records
next to their controllers. Controller names `Auth`, `Me`, `Users` are unique in their Areas (Agent already has
`MyScope`, `Sample`; Admin has `Branches`, `Departments`, `Memberships`, `Samples`, `Diagnostics`). No other
`[AllowAnonymous]` in the module.

### B13 — Host wiring

**File: `backend/src/Crm.Api/Program.cs`**

- Lines 34–37 become:

```csharp
    // Story 07: staff sign-in is JWT bearer (scheme registered by the Identity module). It is the default scheme,
    // so an [Authorize] challenge is always a 401 problem, never "no authenticationScheme was specified".
    builder.Services.AddAuthentication(CrmAuthenticationSchemes.Staff);
    builder.Services.AddCrmAuthorization();
```

- Line 54: `IMvcBuilder mvc = builder.Services.AddControllers(options => options.Conventions.Add(new StaffAreaAuthorizationConvention()));`
- Inside `AddSwaggerGen` (after line 83) add the Bearer security definition + requirement with the Microsoft.OpenApi
  v2 types: `options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http,
  Scheme = "bearer", BearerFormat = "JWT", Description = "Staff access token from POST /api/Agent/Auth/login" });`
  and `options.AddSecurityRequirement(document => new OpenApiSecurityRequirement { [new
  OpenApiSecuritySchemeReference("Bearer", document)] = [] });` (R11; fallback: omit both).
- Usings: remove `Crm.BuildingBlocks.Infrastructure.Identity` if no longer used; add
  `Crm.BuildingBlocks.Application.Abstractions`; keep `Microsoft.AspNetCore.Authentication` only if used.
- ORDER MATTERS block (lines 93–128): order unchanged; comment 6 becomes "authentication (staff JWT), then the
  active-scope check (403 on a forged X-Crm-Scope), then authorisation".

**File: `Modules/ModuleCatalog.cs`** — after the Organisation entry (line 16) add
`new(new Crm.Modules.Identity.Infrastructure.IdentityModule(), Crm.Modules.Identity.Presentation.AssemblyReference.Assembly),`.

**File: `Crm.Api.csproj`** — ProjectReferences to `..\Modules\Identity\Crm.Modules.Identity.Infrastructure\…csproj`
and `…Presentation\…csproj`.

**File: `Diagnostics/Admin/DiagnosticsController.cs`** — add `[AllowAnonymous]` (using
`Microsoft.AspNetCore.Authorization`) under `[DevelopmentOnly]` with the comment "Development-only failure-mode
endpoints; 404 elsewhere. Anonymous so diagnostics.http needs no token for them (Story 07)."

**File: `appsettings.json`** — add:

```json
  "Identity": {
    "Jwt": { "Issuer": "crm", "Audience": "crm-staff", "AccessTokenLifetime": "00:15:00", "ClockSkew": "00:00:30" },
    "Sessions": { "RefreshTokenLifetime": "14.00:00:00", "MaxFailedSignInAttempts": 5, "LockoutWindow": "00:15:00" }
  },
```

and under `RateLimiting`: `"SignIn": { "PermitLimit": 10, "Window": "00:01:00" }`. **No `SigningKey` here.**

**File: `appsettings.Development.json`** — add:

```json
  "Identity": {
    "Jwt": { "SigningKey": "crm-development-signing-key-not-for-production-2026", "AccessTokenLifetime": "00:02:00", "ClockSkew": "00:00:05" },
    "Sessions": { "LockoutWindow": "00:02:00" },
    "DevSeed": { "Password": "Crm_Dev_2026!" }
  },
```

and `RateLimiting:SignIn` `{ "PermitLimit": 30, "Window": "00:01:00" }` (the negative-path walk sends many logins).
The two-minute access token and lockout make the expiry and lockout criteria walkable by hand.

### B14 — Organisation and Sample: declarations; dev-login seam removed

**Organisation (`Crm.Modules.Organisation.Application`)** — add the attribute (and
`using Crm.BuildingBlocks.Application.Authorization;`) to every request record:

| Request | Attribute |
|---|---|
| `AddMembershipCommand`, `ChangeMembershipRoleCommand`, `RemoveMembershipCommand`, `CreateBranchCommand`, `RenameBranchCommand`, `DeactivateBranchCommand`, `CreateDepartmentCommand`, `RenameDepartmentCommand`, `DeactivateDepartmentCommand`, `ListBranchesQuery`, `ListDepartmentsQuery`, `ListMembershipsQuery` | `[RequirePermission(CrmPermissions.AdminOrganisation)]` |
| `GetMyScopeQuery` | `[RequireAuthenticatedUser]` |
| `GetMembershipsOfUserQuery` | `[AllowAnonymousRequest]` — comment: "In-process sign-in lookup (Identity's sign-in and refresh, through IOrganisationModuleApi); no HTTP endpoint exposes it." |

- **Delete file: `Crm.Modules.Organisation.Application/DevUserDirectory.cs`.**
- `Crm.Modules.Organisation.Infrastructure/OrganisationModule.cs` — delete lines 35–37 and the now-unused
  `using Crm.BuildingBlocks.Application.Abstractions;`.
- `…/Queries/GetMembershipsOfUser/GetMembershipsOfUserQueryHandler.cs` lines 10–11 comment → "the sign-in membership
  lookup, run while the caller has no scope yet (Identity's sign-in and refresh)". Same wording in
  `MembershipsOfUserSpecification.cs` lines 6–8 and `Contracts/IOrganisationModuleApi.cs` line 5–6.
- `Crm.Modules.Organisation.Presentation/Controllers/Admin/{Branches,Departments,Memberships}Controller.cs` — delete
  `[Authorize(Policy = CrmPolicies.Administrator)]` (line 15) and the `Crm.BuildingBlocks.Presentation.Authorization`
  and `Microsoft.AspNetCore.Authorization` usings; `Controllers/Agent/MyScopeController.cs` — delete `[Authorize]`
  (line 11) and its using. Add `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` and `…403…` at class
  level on each.

**Sample (`Crm.Modules.Sample.Application`)** — `GetSampleQuery`, `GetSampleByIdQuery`, `ListSamplesQuery` →
`[RequirePermission(CrmPermissions.CustomersRead)]`; `CreateSampleCommand`, `RenameSampleCommand`,
`DeleteSampleCommand` → `[RequirePermission(CrmPermissions.CustomersWrite)]` (Story 11 copies this onto Customer).

After B14: `grep -rn "IRequest<\|ICommand" backend/src --include=*.cs` lists 32 request records (14 Organisation + 6
Sample + 12 Identity); the guard (R5) proves each declares.

### B15 — Request samples

**Create file: `backend/requests/auth.http`** (`@host = http://localhost:5187`; header comment like
`organisation.http` lines 1–6 naming the dev users and that the password is `Identity:DevSeed:Password` in
`appsettings.Development.json`). Each request has a `###` line stating the expected status and body:

1. `# @name loginAdmin` `POST /api/Agent/Auth/login` `{ "email": "admin@crm.local", "password": "Crm_Dev_2026!" }` →
   200, `accessToken`, `refreshToken`, both expiries, `Set-Cookie: crm_refresh=…; path=/api/Agent/Auth; secure;
   samesite=strict; httponly` (R2: decode the payload).
2. `GET /api/Agent/Me` with no `Authorization` → 401 `urn:crm:problem:unauthorized` (R3, R4).
3. `GET /api/Agent/Me` as admin → 200: display name, `preferredLocale: "en"`, three memberships, `roles:
   ["Administrator"]`, all eight permissions.
4. `POST login` `{ "email": "ADMIN@crm.local ", … }` → 200 (normalised email).
5. `# @name loginSupervisor` login as `supervisor@crm.local` → 200.
6. `GET /api/Agent/MyScope` as supervisor → two departments (R2 — two `membership` claims).
7. `GET /api/Agent/Me` as supervisor, no `X-Crm-Scope` → roles `["Agent","Supervisor"]`, permissions include
   `tickets.assign`; with `X-Crm-Scope: 06000000-0000-0000-0002-000000000002` (RYD-BILL) → roles `["Agent"]`, no
   `tickets.assign`; with RYD-CARE → `["Supervisor"]`, has it (AC: effective permissions follow the active scope).
8. `# @name refresh1` `POST /api/Agent/Auth/refresh` `{ "refreshToken": "{{loginSupervisor.response.body.refreshToken}}" }`
   → 200, a **new** refresh token (R8).
9. Refresh again with the **old** token (`loginSupervisor…refreshToken`) → 401 `Identity.InvalidRefreshToken`; the
   API log has the Warning "Refresh token reuse detected".
10. Refresh with `{{refresh1.response.body.refreshToken}}` → 401 (the whole family was revoked — AC).
11. `# @name loginAgentA` login agent A; `POST /api/Agent/Auth/logout` with its refresh token → 204; refresh with it →
    401 (AC: logout revokes).
12. `POST logout` with `{ "refreshToken": "not-a-token" }` → 204 (idempotent, no oracle).
13. Login `{ "email": "admin@crm.local", "password": "wrong-password-1" }` → 401 `Identity.InvalidCredentials`,
    detail "Invalid email or password.".
14. Login with `nobody@crm.local` → 401 with the **same** body (AC: never reveals whether the account exists).
15. Login `{ "email": "not-an-email", "password": "x" }` → 401 same body; `{ "email": "", "password": "" }` → 400
    `urn:crm:problem:validation` (`errors.Email`, `errors.Password`).
16. **Lockout** — login as `agent.b@crm.local` with a wrong password **five** times → five 401s; the fifth writes the
    Warning "account locked until …"; then the **correct** password → 401 (locked); wait
    `Identity:Sessions:LockoutWindow` (2 min in Development) → correct password → 200 (AC).
17. `# @name loginAgentB2` login agent B; `POST /api/Agent/Me/change-password` `{ "currentPassword": "wrong", "newPassword": "Another_Pass_42" }`
    → 400 `errors.CurrentPassword`; `{ "currentPassword": "Crm_Dev_2026!", "newPassword": "Crm_Dev_2026!" }` →
    400 (`NewPassword` must differ); `{ "currentPassword": "Crm_Dev_2026!", "newPassword": "short" }` → 400.
18. Change agent B's password to `Another_Pass_42` → 204; an older agent B session's refresh (from request 16) → 401;
    this session's refresh → 200; login with the new password → 200; change it back to `Crm_Dev_2026!` → 204.
19. Access-token expiry: request 3 with an admin token older than two minutes → 401 (`WWW-Authenticate: Bearer
    error="invalid_token"`); refresh → 200; retry with the new token → 200.
20. Rate limit: run request 13 repeatedly — the 31st within a minute → 429 `urn:crm:problem:rate-limited` with
    `Retry-After` (R10). Wait a minute before continuing.

**Create file: `backend/requests/users.http`** (admin, agent A, supervisor tokens from login requests at the top):

1. `GET /api/Admin/Users` as admin → 200, `items` (4 seed users, ordered by display name), `total: 4`, `page: 1`,
   `pageSize: 20`.
2. `GET /api/Admin/Users?search=agent&page=1&pageSize=1` → 1 item, `total: 2`.
3. `GET /api/Admin/Users` as agent A → **403** `Authorization.PermissionDenied`, detail names `admin.users`
   (AC: API rejects what `useCan()` hides).
4. `GET /api/Admin/Users` with no token → 401.
5. `# @name created` `POST /api/Admin/Users` `{ "email": "new.agent@crm.local", "displayName": "New Agent",
   "preferredLocale": "ar", "initialPassword": "Welcome_2026" }` → 200.
6. Same again → 409 `Identity.EmailTaken`.
7. `{ "email": "broken@", "displayName": "", "preferredLocale": "fr", "initialPassword": "short" }` with
   `Accept-Language: ar` → 400 with Arabic messages for `Email`, `DisplayName`, `PreferredLocale`,
   `InitialPassword` (AC: `EmailAddress` rejects a malformed address — the domain guard is the second line; its
   422 `Identity.EmailInvalid` is reached only by a caller that bypasses the validator, noted in Edge Cases).
8. Login as `new.agent@crm.local` → 200; `GET /api/Agent/Me` → memberships `[]`, permissions `[]`;
   `GET /api/Agent/MyScope` → 403 `Organisation.NoMemberships`.
9. As admin `POST /api/Admin/Memberships` `{ "userId": "{{created.response.body.id}}", "departmentId": "{{ryCare}}", "role": "Agent" }`
   → 200; refresh the new agent's session → `GET /api/Agent/Me` now lists RYD-CARE and the Agent permissions.
10. `PUT /api/Admin/Users/{{createdId}}/profile` `{ "displayName": "وكيل جديد", "preferredLocale": "en", "rowVersion": "{{created.response.body.rowVersion}}" }`
    → 200; same stale `rowVersion` again → 409 `Persistence.ConcurrencyConflict`.
11. `PUT /api/Admin/Users/{{createdId}}/password` `{ "newPassword": "Reset_Pass_77", "rowVersion": … }` → 200; the new
    agent's previous refresh token → 401; login with `Reset_Pass_77` → 200.
12. `# @name loginAgentB` login agent B (keep its refresh token).
13. `POST /api/Admin/Users/{{agentB}}/deactivate` `{ "rowVersion": … }` (from `GET /api/Admin/Users/{{agentB}}`) →
    200 `isActive: false`; again → 422 `Identity.UserAlreadyInactive`.
14. Refresh with agent B's token from 12 → 401 (AC: deactivation revokes tokens — R7); login as agent B → 401
    `Identity.InvalidCredentials` (AC: blocks sign-in); `GET /api/Agent/Me` with agent B's still-unexpired access token
    → 401.
15. `POST /api/Admin/Users/{{admin}}/deactivate` as admin → 422 `Identity.CannotDeactivateSelf`.
16. `POST /api/Admin/Users/{{agentB}}/reactivate` → 200; login as agent B → 200.
17. **Roles differ by department, requests follow the scope:** as admin `PUT /api/Admin/Memberships/{{adminJeddahCareMembershipId}}/role`
    `{ "role": "Agent", "rowVersion": … }` → 200; log in as admin again; `GET /api/Admin/Users` with
    `X-Crm-Scope: {{jeCare}}` → **403** naming `admin.users`; with `X-Crm-Scope: {{ryCare}}` → 200; then restore the
    role to `Administrator`.
18. `GET /api/Admin/Users/00000000-0000-0000-0000-00000000dead` → 404 `Identity.UserNotFound`.

**Files: `backend/requests/organisation.http`, `persistence.http`, `diagnostics.http`, `localization.http`** —
replace the `@admin`/`@agentA`/… user-id variables' use in headers: add login requests at the top (`# @name
loginAdmin`, `loginSupervisor`, `loginAgentA`, `loginAgentB` as needed) and file variables
`@adminToken = {{loginAdmin.response.body.accessToken}}` etc.; every `X-Dev-User: {{x}}` line becomes
`Authorization: Bearer {{xToken}}`. Keep the user-id variables where a body or query needs them. Header comments:
"Story 07: requests run as signed-in users (tokens expire after 2 minutes in Development — re-run the login requests
at the top)". In `organisation.http`: requests 7–8 read "No Authorization header"; request 9 (agent A on
`/api/Admin/Branches`) now expects **403 `Authorization.PermissionDenied`** naming `admin.organisation`; request 24
adds "re-run the supervisor login first — memberships travel on the token until the next sign-in or refresh".
`diagnostics.http` requests 2–3 (`/api/Agent/Sample`) need agent A's token; the `/api/Admin/Diagnostics/*` requests
need none (anonymous, Development-only).

### B16 — Documentation

- **`CLAUDE.md`** —
  - "Pipeline behaviours" bullet: append "Authorisation enforces the request's declaration: every MediatR request
    carries exactly one of `[RequirePermission(CrmPermissions.X)]`, `[RequireAuthenticatedUser]`,
    `[AllowAnonymousRequest]` (`Crm.BuildingBlocks.Application/Authorization`); `RequestAuthorizationGuard` refuses
    to start the app otherwise. Permissions are a code-defined catalogue with a fixed role map; effective
    permissions are the roles held in the departments of the active scope. 401 with no user, 403 naming the
    permission; the handler never runs."
  - "API surface" bullet: append "Agent and Admin controllers require the staff JWT by convention
    (`StaffAreaAuthorizationConvention`); `[AllowAnonymous]` only on Identity's sign-in actions and the
    Development-only diagnostics. Portal and Public are anonymous until stories 47/55."
  - "Scoping" bullet: append "Identity's `User` is deliberately not `ITenantScoped` (sign-in precedes the scope);
    its admin queries compare `TenantId` to the caller's tenant explicitly — the one hand-written tenant filter."
- **`backend/README.md`** — replace the `X-Dev-User` bullet (lines ~286–299) with "Sign-in (Story 07)": the four
  seeded emails, the password key `Identity:DevSeed:Password`, the signing key rule (`Identity__Jwt__SigningKey`
  outside Development; the app refuses to start without it), the refresh cookie, `auth.http` / `users.http`. Rename
  the section "Organisation scope and sign-in".
- **`AREAS.md`** — B6.

---

## Frontend Tasks

All paths are under `frontend/` unless stated. Use the **Write tool** for files with Arabic text. Logical utilities
only; every visible string a key; `<script setup lang="ts">`; no `any`, no `!`, no ts-ignore.

### F1 — Regenerate the API types

With the API running in Development: `pnpm --dir frontend gen:api`. Commit `openapi/{agent,admin}.json` and
`app/api/generated/{agent,admin}.ts` (portal/public unchanged or security-scheme-only diffs). Import types only from
`components['schemas'][…]` (`SignInResponse`, `MeDto`, `UserDto`, `UserDtoPagedResult` — check the generated name,
`MembershipDto`).

### F2 — Remove the dev login

- **Delete file: `app/pages/_dev/login.vue`.**
- `app/plugins/api.ts` — delete line 12 (`devUserCookie`), line 20 (the `X-Dev-User` header), `devUserCookieName` from
  the import (line 3), and the "Development only" half of the comment on lines 8–9.
- `app/utils/scope.ts` — delete lines 7–8 (`devUserCookieName`).
- `i18n/locales/{en,ar}/organisation.json` — delete `devLogin`.
- `i18n/locales/{en,ar}/shell.json` — delete the `login` block (moves to `identity.json`).

### F3 — Auth store, session, middleware, `useCan`

**Create file: `app/utils/permissions.ts`** — mirrors backend `CrmPermissions` verbatim (like `ProblemTypes` in
`app/api/problem.ts` lines 1–18):

```ts
/** Mirrors backend CrmPermissions.cs. Never rename a value. */
export const Permissions = {
  TicketsRead: 'tickets.read',
  TicketsWrite: 'tickets.write',
  TicketsAssign: 'tickets.assign',
  CustomersRead: 'customers.read',
  CustomersWrite: 'customers.write',
  AdminOrganisation: 'admin.organisation',
  AdminUsers: 'admin.users',
  AdminBranding: 'admin.branding',
} as const
export type Permission = (typeof Permissions)[keyof typeof Permissions]

/** Every /api/Agent/Auth/* call (login, refresh, logout): a 401 there never triggers a refresh. */
export const authPathPrefix = '/api/Agent/Auth/'
```

**Create file: `app/stores/auth.ts`** (Pinia setup store `useAuthStore`):

- Setup captures `const nuxtApp = useNuxtApp()` once (R12). State: `accessToken = shallowRef<string | null>(null)`,
  `me = ref<MeDto | null>(null)`, `status = ref<'anonymous' | 'restoring' | 'authenticated'>('anonymous')`.
- Getters: `isAuthenticated` (`accessToken !== null && me !== null`), `permissions` (`new Set(me.permissions ?? [])`).
- `async login(email, password): Promise<ApiResult<void>>` — `nuxtApp.$api.agent.POST('/api/Agent/Auth/login', {
  body: { email, password } })` through `useApi().call(…, { toastOnError: false })` obtained at setup; on success
  set `accessToken`, `await loadMe()`, reset the `crm-scope` cookie to null, reset the scope store, and call
  `nuxtApp.$i18n.setLocale(me.preferredLocale)` when it is `'en'`/`'ar'` and differs (applied at sign-in only).
- `async refresh(): Promise<boolean>` — `POST /api/Agent/Auth/refresh` with `body: {}` (the cookie carries the
  token); **must not throw** (catch everything → false); success → set `accessToken`, return true; else `clear()`,
  false.
- `async loadMe(): Promise<boolean>` — `GET /api/Agent/Me`; success → `me`, `status = 'authenticated'`.
- `restore()` — single-flight promise (a module-level `let restoring: Promise<boolean> | null`): `status =
  'restoring'`, `await refresh() && await loadMe()`, resets the promise in `finally`.
- `async logout()` — `POST /api/Agent/Auth/logout` with `body: {}` (errors ignored), then `clear()`, scope reset,
  `crm-scope` cookie null, `navigateTo('/login')`.
- `clear()` — `accessToken = null`, `me = null`, `status = 'anonymous'`.
- Every composable call after an `await` goes through `nuxtApp.runWithContext(() => …)`.

**Create file: `app/plugins/auth.client.ts`** — client-only (the SSR server keeps `anonymousSession`, as
`useAuthSession.ts` lines 3–5 require):

```ts
import { authPathPrefix } from '~/utils/permissions'

export default defineNuxtPlugin(() => {
  const auth = useAuthStore()
  useAuthSession().setAuthSession({
    accessToken: () => auth.accessToken,
    refresh: () => auth.refresh(),
    clear: () => auth.clear(),
    isRefreshRequest: (request) => new URL(request.url, window.location.origin).pathname.startsWith(authPathPrefix),
  })
})
```

`app/api/auth-session.ts` — update the `anonymousSession` comment (line 12): "Server side and before the client
auth plugin runs: nobody is signed in and refresh always fails." The `AuthSession` interface and
`app/api/crm-fetch.ts` are **unchanged** (Story 04 contract).

**Create file: `app/middleware/auth.global.ts`**

```ts
export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) return
  const auth = useAuthStore()
  const needsAuth = to.path.startsWith('/agent') || to.path.startsWith('/admin')
  if (needsAuth && !auth.isAuthenticated) {
    const restored = await auth.restore()
    if (!restored) return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }
  if (to.path === '/login' && auth.isAuthenticated) return navigateTo('/agent')
})
```

**Create file: `app/composables/useCan.ts`** — `export function useCan() { const auth = useAuthStore(); return {
can: (permission: Permission): boolean => auth.permissions.has(permission) } }`.

**File: `app/stores/scope.ts`** — delete `isAdministrator` (line 10) from state and return; add `reset()` (`myScope =
null`, `status = 'idle'`); `setActive` also calls `void useAuthStore().loadMe()` so effective permissions follow the
new scope (AC).

### F4 — Login page

**File: `app/pages/login.vue`** — rewrite (`definePageMeta({ layout: 'portal' })`):

- `<h1>` `t('identity.login.title')`; a `<form novalidate @submit.prevent="submit">` with `UiInput` email
  (`type="email"`, `autocomplete="username"`, `dir="ltr"`, `required`), `UiInput` password (`type="password"`,
  `autocomplete="current-password"`, `required`), `UiButton type="submit" :loading block`.
- 400 → `useFormErrors().apply` (fields `email`, `password`); 401 → an inline `role="alert"` paragraph
  `t('identity.login.invalid')` (never a toast, never a redirect — `isRefreshRequest` covers it); 429 →
  `t('identity.login.rateLimited')`; network/other → `t('identity.login.failed')`.
- Success → `navigateTo(safeRedirect)` where `safeRedirect` is `route.query.redirect` only when it is a string that
  starts with `/` and not `//` (open-redirect guard), else `/agent`.
- The first field is focused on mount; Enter submits; the password field has no visible length hint (not required).
- Remove the dev-login link.

### F5 — Agent shell: nav, user menu, account page

**File: `app/layouts/agent.vue`**

- `agentNav` (lines 90–98): Branches/Departments when `can(Permissions.AdminOrganisation)`, `{ to: '/admin/users',
  label: t('identity.nav.users') }` when `can(Permissions.AdminUsers)`, and `{ to: '/agent/account', label:
  t('identity.menu.account') }` always.
- Topbar (lines 37–42): `UiAvatar :name="auth.me?.displayName ?? '—'"`; next to it a `UiButton variant="ghost"
  size="sm"` `t('identity.menu.signOut')` calling `auth.logout()` (keyboard reachable; visible at 375 px — the text
  collapses to an `aria-label` + icon-free short label only if it overflows; verify at 375).

**Create file: `app/pages/agent/account.vue`** (`layout: 'agent'`) — profile summary (display name, email in
`<bdi dir="ltr">`, preferred language, memberships count) and a change-password form (current, new, confirm;
confirm mismatch is a client-side field error `identity.account.mismatch`; 400 → `useFormErrors` fields
`currentPassword`, `newPassword`; 204 → success toast `identity.account.changed` and clear the form). States:
the page renders from `auth.me` (always loaded by the middleware); a failed submit shows the error inline.

### F6 — Users admin screens

**Create file: `app/composables/useUsers.ts`** (shape of `useBranches.ts`): `list(search: Ref<string>, page:
Ref<number>)` → `useAsyncData('admin-users', …, { watch: [search, page] })` over `GET /api/Admin/Users` (pageSize
20; throws the problem like `useBranches.ts` lines 16–19); `get(id: Ref<string>)` → `useAsyncData('admin-user',
…)`; `create`, `updateProfile`, `deactivate`, `reactivate`, `resetPassword` returning `ApiResult` with
`toastOnError: false`.

**Create file: `app/composables/useMemberships.ts`** — `listForUser(userId: Ref<string>)` →
`useAsyncData('admin-user-memberships', …)` over `GET /api/Admin/Memberships?userId=`; `add(body)`,
`changeRole(id, body)`, `remove(id)`.

**Create file: `app/pages/admin/users/index.vue`** (`layout: 'agent'`), modelled on `admin/branches.vue`:

- Header: title, a search `<form role="search">` (`UiInput` `t('identity.users.search')` + `UiButton type="submit"`),
  "New user" button (`v-if="can(Permissions.AdminUsers)"`).
- `UiTable` columns: display name (a `NuxtLink` to `/admin/users/{id}`), email (`<bdi dir="ltr">`), language
  (`t('identity.locale.en|ar')`), status `UiBadge` (success Active / neutral Inactive / warning Locked), last sign-in
  (`useFormat().dateTime`, or `t('identity.fields.never')`).
- `UiPagination` under the table (`v-model:page`, `:page-size="20"`, `:total`, labels from `shell.pagination.*`);
  changing the search resets page to 1.
- States: loading (`UiTable :loading`), empty (`UiEmptyState t('identity.users.empty')`), error (`UiErrorState` +
  retry), forbidden (403 → `UiErrorState` `errors.problem.forbidden` with the problem detail), exactly as
  `admin/branches.vue` lines 14–38.
- Create `UiModal`: email (`dir="ltr"`), display name, preferred language `UiSelect` (en/ar), initial password
  (`type="password"`, `autocomplete="new-password"`); 400 → field errors (`email`, `displayName`,
  `preferredLocale`, `initialPassword`); 409 → field error on `email` with `errors.code.Identity.EmailTaken`; success
  → close, `navigateTo('/admin/users/' + id)`.

**Create file: `app/pages/admin/users/[id].vue`** (`layout: 'agent'`):

- Back link to `/admin/users`; `<h1>` display name; status badges.
- **Profile** card: display name + preferred language form (`PUT …/profile` with `rowVersion`); 409
  `Persistence.ConcurrencyConflict` → toast + refresh.
- **Account status** card: Deactivate (danger, confirm `UiModal` `identity.confirm.deactivate`) or Reactivate;
  hidden Deactivate for the signed-in user's own row (`auth.me.id === user.id`); "Reset password" opens a modal with
  a new-password field.
- **Department memberships** card: `UiTable` (department name via `useDepartments().list(ref(undefined),
  ref(false))` + `localizedName`, role `t('identity.roles.<Role>')`, actions: role `UiSelect` saving on change
  (`PUT /api/Admin/Memberships/{id}/role`), Remove (confirm)); an add row: department `UiSelect` (active departments
  the user is not yet in) + role `UiSelect` + Add; empty state `identity.detail.noMemberships`; 409
  `Organisation.MembershipExists` → toast. The memberships card is shown only when `can(Permissions.AdminOrganisation)`.
- States: loading skeleton (`UiSkeleton`), 404 → `UiEmptyState` `identity.users.notFound` with the back link,
  403 → forbidden error state, other errors → `UiErrorState` + retry.

### F7 — Copy

**Create files: `i18n/locales/en/identity.json`, `i18n/locales/ar/identity.json`** (top-level key `identity`):

| Key | en | ar |
|---|---|---|
| `login.title` / `login.submit` | Sign in / Sign in | تسجيل الدخول / تسجيل الدخول |
| `login.email` / `login.password` | Email / Password | البريد الإلكتروني / كلمة المرور |
| `login.invalid` | Email or password is incorrect. | البريد الإلكتروني أو كلمة المرور غير صحيحة. |
| `login.rateLimited` | Too many attempts. Wait a moment and try again. | محاولات كثيرة. انتظر قليلاً ثم حاول مرة أخرى. |
| `login.failed` | Sign-in failed. Try again. | تعذّر تسجيل الدخول. حاول مرة أخرى. |
| `nav.users` | Users | المستخدمون |
| `menu.account` / `menu.signOut` | My account / Sign out | حسابي / تسجيل الخروج |
| `account.title` / `account.changePassword` | My account / Change password | حسابي / تغيير كلمة المرور |
| `account.mismatch` | The passwords do not match. | كلمتا المرور غير متطابقتين. |
| `account.changed` | Password changed. Your other sessions were signed out. | تم تغيير كلمة المرور وتسجيل الخروج من جلساتك الأخرى. |
| `users.title` / `users.create` / `users.empty` / `users.notFound` | Users / New user / No users match your search / The user was not found | المستخدمون / مستخدم جديد / لا يوجد مستخدمون مطابقون لبحثك / لم يتم العثور على المستخدم |
| `users.search` / `users.searchAction` / `users.back` | Search by name or email / Search / Back to users | ابحث بالاسم أو البريد الإلكتروني / بحث / العودة إلى المستخدمين |
| `fields.displayName` / `email` / `locale` / `initialPassword` / `newPassword` / `currentPassword` / `confirmPassword` | Display name / Email / Preferred language / Initial password / New password / Current password / Confirm new password | الاسم المعروض / البريد الإلكتروني / اللغة المفضلة / كلمة المرور الأولية / كلمة المرور الجديدة / كلمة المرور الحالية / تأكيد كلمة المرور الجديدة |
| `fields.status` / `lastSignIn` / `never` / `department` / `role` / `actions` | Status / Last sign-in / Never / Department / Role / Actions | الحالة / آخر تسجيل دخول / أبداً / القسم / الدور / الإجراءات |
| `locale.en` / `locale.ar` | English / Arabic | الإنجليزية / العربية |
| `status.active` / `inactive` / `locked` | Active / Inactive / Locked | نشط / غير نشط / مقفل مؤقتاً |
| `roles.Administrator` / `Supervisor` / `Agent` | Administrator / Supervisor / Agent | مسؤول النظام / مشرف / موظف دعم |
| `actions.save` / `cancel` / `close` / `deactivate` / `reactivate` / `resetPassword` / `addMembership` / `remove` | Save / Cancel / Close / Deactivate / Reactivate / Reset password / Add to department / Remove | حفظ / إلغاء / إغلاق / إلغاء التفعيل / إعادة التفعيل / إعادة تعيين كلمة المرور / إضافة إلى قسم / إزالة |
| `detail.profile` / `status` / `memberships` / `noMemberships` | Profile / Account status / Department memberships / No memberships yet. The user can sign in but sees no data. | الملف الشخصي / حالة الحساب / عضويات الأقسام / لا توجد عضويات بعد. يمكن للمستخدم تسجيل الدخول لكنه لن يرى أي بيانات. |
| `confirm.deactivate` | Deactivate {name}? They are signed out everywhere and cannot sign in. | إلغاء تفعيل {name}؟ سيتم تسجيل خروجه من كل مكان ولن يتمكن من تسجيل الدخول. |
| `confirm.removeMembership` | Remove {name} from {department}? | إزالة {name} من {department}؟ |
| `saved` / `load.error` / `load.retry` | Saved / Could not load the data / Try again | تم الحفظ / تعذّر تحميل البيانات / حاول مرة أخرى |

**Files: `i18n/locales/{en,ar}/errors.json`** — add under `errors.code` (nested by the dot):
`Identity.InvalidCredentials` ("Email or password is incorrect." / "البريد الإلكتروني أو كلمة المرور غير صحيحة."),
`Identity.InvalidRefreshToken` ("Your session has ended. Sign in again." / "انتهت جلستك. سجّل الدخول مرة أخرى."),
`Identity.EmailTaken` ("A user with this email already exists." / "يوجد مستخدم بهذا البريد الإلكتروني بالفعل."),
`Identity.UserNotFound` ("The user was not found." / "لم يتم العثور على المستخدم."),
`Identity.UserAlreadyInactive` ("The user is already inactive." / "المستخدم غير نشط بالفعل."),
`Identity.UserAlreadyActive` ("The user is already active." / "المستخدم نشط بالفعل."),
`Identity.CannotDeactivateSelf` ("You cannot deactivate your own account." / "لا يمكنك إلغاء تفعيل حسابك."),
`Identity.CurrentPasswordInvalid` ("The current password is incorrect." / "كلمة المرور الحالية غير صحيحة."),
`Identity.EmailInvalid` ("The email address is not valid." / "عنوان البريد الإلكتروني غير صالح."),
`Authorization.PermissionDenied` ("You do not have permission to do this." / "ليست لديك صلاحية للقيام بذلك."),
`Authorization.Unauthenticated` ("Sign in to continue." / "سجّل الدخول للمتابعة."),
`Organisation.NoMemberships` ("You are not a member of any department yet." / "لست عضواً في أي قسم بعد.").

### F8 — `/_dev` pages

No new UI kit component or kit state is added (the screens compose existing kit components), so `/_dev/ui` is
unchanged. `/_dev/api`'s fake-transport scenarios A–E build their own `createCrmFetch` and stay green unchanged.

---

## Edge Cases & Failure Modes

- **Unknown email vs wrong password vs locked vs deactivated** → one 401 body `Identity.InvalidCredentials` and a
  dummy hash verify for the no-user paths (`SignInCommandHandler`, B9); only the log knows the reason. A malformed
  email that passes the validator's `NotEmpty` is also the generic 401 (`EmailAddress.TryCreate`).
- **Lockout race** — two concurrent wrong passwords for one user hit the `User` rowversion; the second save fails
  with `ConcurrencyConflictException` → 409 from `TransactionBehavior` (lines 91–98). Accepted (rare); the counter is
  never lost silently.
- **Concurrent sign-ins of one user** — `RecordSignInWhenSessionStarted` runs after commit; a rowversion clash there
  surfaces as 409 even though the session exists (`TransactionBehavior` catches it after commit). Rare; the user
  signs in again.
- **Two tabs refresh the same token at the same instant** → the second sees a rotated token → reuse → the family is
  revoked → both tabs go to `/login`. Tabs normally share the cookie jar, so sequential refreshes rotate cleanly;
  only a same-millisecond race hits this. Accepted and noted for Story 47's portal.
- **Refresh token stolen and replayed after the owner rotated it** → reuse detected, family revoked (the owner is
  also signed out — intended), Warning logged (`RefreshTokenFamily.Rotate`, B8).
- **Deactivated user with a live access token** → `AuthorizationBehavior` still passes until the token expires
  (≤ `AccessTokenLifetime`), but `GET /api/Agent/Me` answers 401 and refresh fails. Documented in Product rules.
- **Membership added/removed** → reaches the user at the next refresh; `organisation.http` request 24 re-logs in.
- **User with no memberships** → sign-in succeeds; `me.permissions = []`; every permissioned request is 403;
  `GetMyScope` 403 `Organisation.NoMemberships` (the scope switcher shows its retry button; the agent home is the
  empty state). `users.http` request 8.
- **Membership tenant ≠ user tenant** (impossible through the UI) → `AccessTokenSubjectFactory` drops them; the user
  sees nothing rather than another tenant's data.
- **`AddMembership` for a user id that does not exist in Identity** → accepted by Organisation (it holds user ids as
  plain Guids); the row is inert. Not validated in the MVP (would need an `IIdentityModuleApi`).
- **Forged `X-Crm-Scope`** still answers 403 in `OrganisationScopeMiddleware` before any endpoint; effective
  permissions can only shrink with the header, never grow.
- **Permission typo in an attribute** → the guard refuses to start (R5). A request with two declarations or none →
  refuses to start. A request returning a non-`Result` → refuses to start.
- **Validation before authorisation** — an unauthorised caller with an invalid body gets 400, not 403 (pipeline order,
  Product rules). No information about data leaks through a validator.
- **Missing or short signing key** → `JwtSettings.Validate()` throws in `IdentityModule.AddServices`; the host logs
  Fatal and exits. Missing `Identity:DevSeed:Password` in Development → Warning, no users seeded (sign-in impossible
  until set).
- **Token signed with another key / wrong audience / `alg: none`** → rejected by `TokenValidationParameters`
  (`ValidAlgorithms` = HS256 only) → 401.
- **Open redirect** via `/login?redirect=https://evil` → `safeRedirect` falls back to `/agent` (F4).
- **Login page 401 must not redirect** → `isRefreshRequest` covers `/api/Agent/Auth/*` (F3); a 401 from
  `/api/Agent/Me/change-password` does refresh (it is not under `Auth/`).
- **Page reload** → access token gone (memory only) → middleware `restore()` → one refresh through the cookie; no
  cookie or revoked → `/login?redirect=…`.
- **SSR** — `/login` and `/` render on the server with `anonymousSession`; the auth plugin is client-only; the
  middleware returns early on the server; `/agent/**` and `/admin/**` are SPA (`nuxt.config.ts` lines 29–30).
- **Stale `crm-scope` cookie after sign-in as another user** → cleared on login and logout (F3); `useMyScope`'s
  self-healing (Story 06) remains the backstop.
- **Arabic display names in the JWT** → UTF-8 JSON; the avatar initials use `Array.from` (`Avatar.vue`). Emails
  render in `<bdi dir="ltr">`.
- **Rate limiting** — behind a reverse proxy `RemoteIpAddress` is the proxy's (same caveat as the Public limiter,
  `RateLimitingServiceCollectionExtensions.cs` line 33); Development limits are 30/min.
- **Logs** — no password, refresh token, or access token is ever logged: request payloads are logged only at Debug and
  redacted by name (`password`, `token` fragments); handlers log ids only.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test project, test
framework, `*.spec.ts`, `*.test.ts`, or `tests/` folder.** The compiler, the `CRM0001` rules, the startup guard,
`backend/requests/auth.http` + `users.http`, and the browser walkthrough replace the suite. Run every row; record the
results (and which R1/R2/R3/R6/R11 path shipped) in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001`, no banned-API hit |
| 2 | Boundaries | read the five Identity `.csproj` files | only rule-compliant references (Organisation.Contracts only) |
| 3 | Migration | read `Identity/…/Migrations/*_Initial.cs` | schema `identity` only; unique `Email`, unique `TokenHash`; outbox tables |
| 4 | Risks R1–R12 | as stated in Prerequisites | each proven or its fallback recorded |
| 5 | Startup guard (AC) | R5 | refuses to start, names the request; restores cleanly |
| 6 | Sign-in / refresh / logout (AC) | `auth.http` 1–12, 19 | as listed |
| 7 | Generic failures + lockout (AC) | `auth.http` 13–16 | identical 401 bodies; lock after 5; unlock after the window |
| 8 | Change password | `auth.http` 17–18 | 400 cases; other sessions revoked; current session survives |
| 9 | Effective permissions (AC) | `auth.http` 7, `users.http` 17 | permissions and 403/200 follow `X-Crm-Scope` |
| 10 | 401/403 before the handler (AC) | `users.http` 3–4 + log | 401/403; the log has the `Permission admin.users denied` Warning and **no** `Handling ListUsersQuery` line (LoggingBehavior sits after AuthorizationBehavior, so its line proves the handler path was never entered) |
| 11 | User admin (AC) | `users.http` 1–2, 5–18 | as listed; deactivation blocks sign-in and revokes tokens |
| 12 | Old request files | `organisation.http`, `persistence.http`, `diagnostics.http`, `localization.http` | same results as before with bearer tokens; organisation request 9 now 403 `Authorization.PermissionDenied` |
| 13 | Log enrichment (Story 03 carry-forward) | any authenticated request; read the JSON log line | `UserId`, `TenantId`, `DepartmentId`, `BranchId`, `CorrelationId` present |
| 14 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0; lint prints `check-locales: 4 domain files, en/ar key sets match` |
| 15 | Login (AC) | `/login` in `en` and `ar` | wrong password → inline alert, no redirect; success → `/agent` (or the safe `redirect`); `supervisor@crm.local` switches the UI to Arabic on sign-in |
| 16 | Silent restore + expiry (AC) | reload `/agent`; wait 2 minutes; click a nav item that loads data | Network: one refresh on reload; after expiry the call 401s, one `refresh` 200, the original call retried 200 |
| 17 | Reuse forces re-login (AC) | copy the `crm_refresh` cookie value, refresh twice from `auth.http` with the old value, then use the app | the app lands on `/login` at the next refresh |
| 18 | `useCan()` (AC) | sign in as agent A | no Branches/Departments/Users nav; `/admin/users` typed directly → forbidden error state; the API answers 403 (`users.http` 3) |
| 19 | Users screens (AC) | as admin: search, page, create, edit profile, reset password, deactivate/reactivate, add/change/remove membership | each works; own row has no Deactivate; states: loading, empty (search "zzz"), error (API stopped), forbidden |
| 20 | Scope switch updates permissions | as supervisor switch to Billing | `/api/Agent/Me` re-fetched; permissions lose `tickets.assign` |
| 21 | Locales + widths + keyboard (AC) | rows 15–20 in `en` and `ar` at 375 / 768 / 1280, Tab/Shift-Tab/Enter/Esc only | RTL mirrored, no horizontal scroll (`document.documentElement.scrollWidth === innerWidth`), every control reachable, modals trap focus and Esc closes |
| 22 | Dev login gone | `grep -rn "X-Dev-User\|crm-dev-user\|IDevUserDirectory\|DevUserAuthentication" backend/src frontend/app backend/requests` | no hits |

---

## Migration / Rollback

- One new migration: `Identity/Initial` (schema `identity`: `Users`, `RefreshTokenFamilies`, `RefreshTokens`, the
  outbox pair). No change to `organisation` or `sample` schemas. Applied automatically in Development at startup.
- **Rollback:** `pwsh backend/scripts/ef.ps1 -Module Identity database update 0`, then revert the commit (the
  Story 06 dev login comes back with it). Or `docker compose -f backend/docker-compose.yml down -v`.
- **Half-applied state:** a failed Identity migration fails `MigrateAndSeedModulesAsync` at startup (loud). Missing
  JWT configuration fails earlier, in `AddServices`. Never hand-edit `__EFMigrationsHistory`.
- **Operational change:** every environment other than Development now needs `Identity__Jwt__SigningKey` before the
  API starts. Record it in the story report.

---

## Verification Steps

1. **Backend builds:** `dotnet restore backend/Crm.sln` (R1), `dotnet build backend/Crm.sln` → Test Plan rows 1–3.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` → prove R2–R5, R7–R11 in order, then run
   `backend/requests/auth.http` and `users.http` top to bottom → rows 4–11, 13; re-run the four older `.http` files
   → row 12.
3. **Frontend types:** with the API up, `pnpm --dir frontend gen:api` → only the expected snapshots and generated
   files change.
4. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build` →
   row 14.
5. **Frontend runs:** `pnpm --dir frontend dev` with the API up → R6, R12, then rows 15–21 in Edge, `en` and `ar`,
   375/768/1280, keyboard only.
6. **Regression:** `/_dev/api` Fake transport scenarios A–E PASS; `/_dev/ui` renders; row 22 grep is empty;
   `grep -rn "IgnoreOrganisationScope()" backend/src` still shows the definition and exactly one caller.

---

## Done Criteria

- [ ] Valid credentials return access + refresh tokens; invalid credentials (and unknown, locked, deactivated) return
      401 with the one generic message (`auth.http` 1, 13–15).
- [ ] An expired access token is refreshed once and the original request retried; reusing a rotated refresh token
      revokes the family and forces re-login (`auth.http` 8–10, 19; rows 16–17).
- [ ] Logout revokes the presented refresh token; deactivating a user blocks sign-in and revokes their tokens
      (`auth.http` 11; `users.http` 12–14).
- [ ] Five failed attempts lock the account for `Identity:Sessions:LockoutWindow` (`auth.http` 16).
- [ ] `EmailAddress` rejects a malformed address at construction (`EmailAddress.Create` guard; `users.http` 7 via the
      validator).
- [ ] Every request declares its authorisation; the app refuses to start if one does not (R5).
- [ ] Without the permission → 403 naming it; without a token → 401; the handler never runs (`users.http` 3–4,
      row 10).
- [ ] One user holds different roles in different departments and effective permissions follow the active scope
      (`auth.http` 7, `users.http` 17, row 20).
- [ ] `GET /api/Agent/Me` returns profile, locale, memberships, and permissions; `useCan()` hides actions and the API
      still rejects them from `users.http` (row 18).
- [ ] Login, account, and user screens work in `ar` RTL and `en` at 375/768/1280, fully keyboard-navigable, with
      loading, empty, and error states (rows 15, 19, 21).
- [ ] Every path in `auth.http` and `users.http` exercised against a running API; the four older `.http` files use
      bearer tokens and still pass.
- [ ] `X-Dev-User`, `IDevUserDirectory`, `DevUserDirectory`, `DevUserAuthenticationHandler`, `/_dev/login`, and
      `crm-dev-user` are gone (row 22); JWT is the only way in.
- [ ] `dotnet build backend/Crm.sln` clean with 0 warnings and no `CRM0001`; the Identity `Initial` migration is
      included; `pnpm typecheck`/`lint`/`build` clean; `gen:api` output committed.
- [ ] No secret in `appsettings.json`; the signing key comes from configuration outside Development.
- [ ] `CLAUDE.md`, `backend/README.md`, and `AREAS.md` updated (B16).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/identity-access/00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 11.**
