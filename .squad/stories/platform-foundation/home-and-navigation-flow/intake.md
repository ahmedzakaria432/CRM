# Story 59 — Home and navigation flow

Story intake for the **Platform Foundation** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/platform-foundation/home-and-navigation-flow/intake.md`.

---

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`
- **Module (backend):** none expected (frontend); reads `Tickets` (queue counts, search), `Identity` (me), `Organisation`
  (public branding). Add a backend read only if a needed one is truly missing, through the owning module.

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-59`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `platform-foundation`, `ux`

---

## Title

```
Home and navigation flow
```

---

## Why (owner feedback after the MVP, 2026-10-01)

The MVP features all work, but nothing leads to them. `/` is the Story 04 placeholder (app name and a tagline, no
links), and `/agent` is an empty state. A first-time user opening the app cannot tell where staff sign in, where
customers go, or what to do next. This story connects the entry points into one obvious flow. It is a **minimal
subset** of the deferred agent dashboard (`next-steps-stories/stories/agent-workspace/agent-dashboard`): no widgets
beyond what is listed here.

## Description

- **Landing page `/` (SSR, branded, portal layout):** the tenant's branding (logo, name, colours from Story 58), a
  short welcome, and two clear entry cards: **"Staff sign-in"** → `/login` and **"Customer portal"** → `/portal`
  (sign-in / register). A signed-in staff user is sent to `/agent`, and a signed-in portal customer to `/portal`.
  Works without an API call failing the page: if branding is unavailable it falls back to the product defaults.
- **Agent home `/agent`:** replaces the empty state with a usable start page:
  - a greeting with the user's name and active scope (department/branch);
  - count cards for **My tickets**, **Unassigned**, and **All in scope** (existing queue counts from Story 15). Each
    card links to the inbox with that view preselected (`/agent/tickets?view=…`);
  - **My recent tickets**: the newest 5 tickets assigned to me (existing search endpoint), each linking to the ticket,
    with an empty state that points to the Unassigned view;
  - quick actions: **New ticket** (opens the Story 20 create drawer or navigates to the inbox with it open),
    **Find customer** (`/agent/customers`), and for admins **Branding** / **Users**, shown only with the matching
    permission (`useCan()`).
- **Portal home `/portal`:** the signed-in customer lands on a page that greets them and has a prominent **"New
  request"** action next to their requests list. An empty list shows an empty state with that action.
- **Navigation consistency:**
  - the brand mark in every layout links to that layout's home (`/`, `/agent`, `/portal`);
  - after sign-in, staff go to the `redirect` query or `/agent`, and customers to the `redirect` query or `/portal`;
  - after sign-out, staff go to `/login` and customers to `/portal/login`;
  - the staff login page links to "Customer portal", and the portal login page links back to `/`.
- All copy is `en` + `ar` keys; loading, empty, and error states on every panel.
- **Demo seed users for every case (owner request, 2026-10-01; Development seed only):** extend the existing dev
  seeders in their owning modules (Organisation, Identity, Customers, Tickets) so every role and scope case can be
  signed into and demonstrated, with the shared dev password. At least:
  - staff:
    - an Administrator;
    - a Supervisor in more than one department, so the scope switcher, assign and transfer all apply;
    - agents in different departments and in different branches, so isolation is visible;
    - an agent with no tickets, to show the empty states;
    - a deactivated user, whose sign-in is refused;
  - portal:
    - a customer's primary contact, who is account admin and sees all of the customer's requests;
    - a non-primary contact who sees only their own requests;
    - a customer with no requests, to show the empty state;
    - a contact whose portal access is revoked;
  - tickets spread across statuses, priorities, departments, assigned and unassigned, so the home counts, inbox
    tabs and portal tabs all show data.

  Seeding stays idempotent with well-known ids, as the existing seeders do, and never runs outside Development. A
  table of every seeded account (email, role, department/branch, what it demonstrates) goes in the root `README.md`
  "Demo accounts" section and in `backend/README.md`.

## Acceptance criteria

- [ ] `/` shows the branded landing with both entry points, server-rendered, in `en` and `ar`.
- [ ] Visiting `/` while signed in as staff lands on `/agent`; as a portal customer, on `/portal`.
- [ ] `/agent` shows the three queue counts matching the inbox tabs, and each card opens the inbox on that view.
- [ ] `/agent` lists my newest assigned tickets, or an empty state that links to Unassigned.
- [ ] Quick actions respect permissions: an agent sees no admin links; an admin does.
- [ ] Each count/list panel has a loading skeleton, an empty state, and an error state with retry that does not break
      the rest of the page.
- [ ] Portal home shows the "New request" action and an empty state for a customer with no requests.
- [ ] Sign-in and sign-out land on the pages listed above, for staff and for customers.
- [ ] Every seeded account signs in (or is refused, for the deactivated user and the revoked portal contact) and
      shows the case it is meant to show; re-running the API does not duplicate any seed row; the README tables list
      every seeded account.
- [ ] Walked in `en` and `ar` at 375px, 768px, and 1280px; no physical-direction utility and no literal colour in the
      diff; the Docker stack (`docker compose up -d --build`, http://localhost:8080) serves the new pages.

## Dependencies

- **Blocked by / related ids:** CRM-15, CRM-20, CRM-47, CRM-58 (all done).
- **Depends on code areas or other stories:** queue counts and ticket search (Story 15), the inbox views and create
  drawer (Story 20), portal session and pages (Story 47), branding plugin and `BrandMark` (Story 58), `useCan()`
  (Story 07).

## Technical hints

- One composable per use case under `app/composables/` (for example `useAgentHome`); reuse the Story 20 ticket
  composables rather than calling the API differently.
- Keep `/agent/**` an SPA and `/` and `/portal/**` server-rendered (`nuxt.config.ts` route rules).
- The redirect-if-signed-in logic belongs with the existing global auth middleware and portal guard; it must not cause
  a hydration mismatch on the SSR `/` page (Story 47's R7 lesson).

## Out of scope (deferred)

- The full agent dashboard (story 31): SLA widgets, charts, team views, personalisation.
- Notifications, search across everything, onboarding tours. Any test suite.
