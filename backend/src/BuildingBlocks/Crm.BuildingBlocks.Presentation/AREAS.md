# Areas — one per persona

Controllers live in each module's `Crm.Modules.<Module>.Presentation/Controllers/<Area>/` folder and carry
`[Area(CrmAreas.X)]`. The host (`Crm.Api`) publishes one Swagger document per Area, merging every module's
controllers for that Area. These notes record what each Area is for and which policy it needs.

## Agent Area

The agent workspace persona - ticket inbox, ticket detail, dashboards, tasks, and team collaboration.
Filled by stories 20, 21, 28-31. Story 11: the `Customers` module's `CustomersController` (`customers.read`/`customers.write`). Story 15: the `Tickets` module's `TicketsController` and `TicketCategoriesController` (`tickets.read`/`tickets.write`/`tickets.assign`). Story 18: the thread (`GET …/Tickets/{id}/thread`), replies, customer replies and internal notes on `TicketsController` (`tickets.read`/`tickets.write`).

<!-- Story 07: every controller here requires the Staff JWT (`StaffAreaAuthorizationConvention`); each MediatR request declares its permission (`[RequirePermission]`). `[AllowAnonymous]` only on the Identity sign-in actions and the Development-only diagnostics. -->

## Portal Area

The customer self-service persona - portal auth/registration, ticket submission and tracking,
request history, and the public help centre. Filled by stories 47-50.

<!-- Anonymous until story 47 (portal JWT). -->

## Admin Area

Tenant administration and reporting - identity/user/role management, audit log, SLA and automation
configuration, management dashboards and exports. Filled by stories 08-10, 51-54. Also the home for (Story 06) the `Organisation`
module's Branches/Departments/Memberships controllers, whose requests require `admin.organisation`. Also (Story 15) the `Tickets` module's `TicketCategoriesController` (`admin.organisation`).

<!-- Story 07: every controller here requires the Staff JWT (`StaffAreaAuthorizationConvention`); the permission is
     declared per request (`[RequirePermission(CrmPermissions.Admin...)]`), so a signed-in agent gets 403 naming it.
     No Area lifts the organisation-scope filter - the only lift is the sign-in membership lookup
     (Specification.IgnoreOrganisationScope(), CLAUDE.md, "Backend", Scoping). -->

## Public Area

The versioned external API - public API key management, outbound webhooks, ERP integration.
Filled by stories 55-57.

Rate limited per client IP by the global limiter (`AddCrmRateLimiting`, `RateLimiting:Public`) - every controller
in this Area, in any module, with no attribute to add. 429 + `Retry-After`. Stories 27 and 47 add named policies
(per email, per account) with `[EnableRateLimiting]` for anonymous endpoints outside this Area.

<!-- Anonymous until story 55 (API keys). -->

