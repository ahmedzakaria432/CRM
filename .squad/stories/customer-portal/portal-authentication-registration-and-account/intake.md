# Story 47 — Portal authentication registration and account

Story intake for the **Customer Portal** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-portal/portal-authentication-registration-and-account/intake.md`.

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-47`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-portal`

---

## Title

```
Portal authentication registration and account
```

---

## Description

The customer's own way in. It shares the identity infrastructure from story 07 but is a separate audience with
sharply narrower permissions, because a portal user is not a staff user with fewer rights.

- Portal identity as a distinct audience on the story 07 token stack: a separate JWT audience, a separate
  permission set, and a hard rule that a portal token can never satisfy an agent endpoint.
- Registration paths: self-service sign-up with email or phone verification, and an invitation issued by an agent
  from a contact on the customer aggregate in story 11.
- Contact linking as the central invariant: a portal account is always bound to exactly one `Contact`, and
  therefore to one customer. Registration with an email already on a contact links to it after verification rather
  than creating a duplicate customer.
- Multi-contact accounts: a contact flagged as an account administrator may see the whole customer's tickets, while
  an ordinary contact sees only their own, configured per customer.
- Verification and recovery: email and SMS verification codes, password reset, and change-email with re-verification.
- Account self-service: profile, preferred language, preferred contact channel, notification preferences feeding
  story 36, and password change.
- Portal-specific protections: rate limiting per IP and per account, CAPTCHA on registration and reset, and a
  registration flow that cannot be used to enumerate which emails are known.
- Frontend: portal layout from story 04, sign-up, sign-in, verification, reset, and account pages, all bilingual
  with correct RTL and working on mobile.

## Acceptance criteria

- [ ] A portal token is rejected by every agent endpoint, verified by presenting one to a representative sample of
      agent APIs.
- [ ] An agent token is rejected by portal-only endpoints where scope demands it.
- [ ] Self-service registration requires verification before the account becomes usable.
- [ ] Registering with an email already present on a contact links the account to that contact after verification,
      and never creates a second customer.
- [ ] An agent-issued invitation creates a linked portal account bound to the intended contact.
- [ ] An account-administrator contact sees all of the customer's tickets; an ordinary contact sees only their own.
- [ ] A portal user cannot read, list, or infer any other customer's data through any endpoint.
- [ ] Registration and reset responses are identical for known and unknown addresses, so neither can be used for
      enumeration.
- [ ] Exceeding the registration or reset rate limit returns 429 and writes an audit entry.
- [ ] Changing the account email requires verification of the new address before it takes effect.
- [ ] Notification preferences set here are honoured by story 36.
- [ ] Every portal page renders correctly in Arabic RTL on a 375px viewport and is keyboard operable.

## Dependencies

- **Blocked by / related ids:** CRM-07, CRM-11, CRM-04, CRM-36.
- **Depends on code areas or other stories:** the identity stack from story 07, contacts from story 11, the portal
  layout from story 04, and notification preferences from story 36.

## Technical hints

- Enforce the audience separation in the token validation configuration, not in per-endpoint checks. One
  configuration mistake is easier to spot than forty missing checks, and there is no test suite here.
- Bind the portal account to the contact, not to the customer. Every other rule follows cleanly from that.
- Make the enumeration-safe responses identical down to timing where practical, and verify by hand.

## Out of scope

- Ticket submission and tracking. Those are stories 48 and 49.
- Social or federated sign-in.
- Any test project.


---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10. Domain-Driven Design inside Clean Architecture (`Crm.Domain` <- `Crm.Application` <- `Crm.Infrastructure` / `Crm.Api`), CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server, minimal APIs, `Result<T>` returns, RFC 9457 problem details.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
