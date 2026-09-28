# Areas — one per persona

Controllers live in each module's `Crm.Modules.<Module>.Presentation/Controllers/<Area>/` folder and carry
`[Area(CrmAreas.X)]`. The host (`Crm.Api`) publishes one Swagger document per Area, merging every module's
controllers for that Area. These notes record what each Area is for and which policy it needs.

## Agent Area

The agent workspace persona - ticket inbox, ticket detail, dashboards, tasks, and team collaboration.
Filled by stories 20, 21, 28-31.

<!-- Story 09: this Area requires an authenticated agent/supervisor/admin policy. No [Authorize] yet - stories 07 and 09 own authentication and the per-Area policies. -->

## Portal Area

The customer self-service persona - portal auth/registration, ticket submission and tracking,
request history, and the public help centre. Filled by stories 47-50.

<!-- Story 09: this Area requires an authenticated portal-customer policy. No [Authorize] yet - stories 07 and 09 own authentication and the per-Area policies. -->

## Admin Area

Tenant administration and reporting - identity/user/role management, audit log, SLA and automation
configuration, management dashboards and exports. Filled by stories 08-10, 51-54. Also the home for
Story 02's demonstration Sample CRUD endpoints, deleted in Story 11.

<!-- Story 09: this Area requires an authenticated tenant-admin policy. No [Authorize] yet - stories 07 and 09 own authentication and the per-Area policies. This is also the only Area where the unnamed IgnoreQueryFilters() escape hatch (CLAUDE.md, "Backend", Scoping) is permitted. -->

## Public Area

The versioned external API - public API key management, outbound webhooks, ERP integration.
Filled by stories 55-57.

<!-- Story 09: this Area requires API-key authentication, not the JWT policies the other three Areas use. No [Authorize] yet - stories 07, 09, and 55 own authentication for this Area. -->

