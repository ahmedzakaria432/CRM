# Story 14 — Customer 360 workspace UI

## Prerequisites

- **Stories 01–07 and 11 are implemented and committed** (latest `07abdda` "Implement Story 11"). Story 11's
  Coordination bullet for this story ([`11-story-customer-aggregate-profiles-contact-details-and-notes.md`](11-story-customer-aggregate-profiles-contact-details-and-notes.md)
  lines 89–93) is the contract: consume exactly the Agent endpoints of `CustomersController`, map every `Customers.*`
  error code to an `errors.code.Customers.*` key, show the duplicate warning from a **200** `created: false`
  response and re-send with `confirmDuplicate: true`, and build `CustomerPicker` on `GET /api/Agent/Customers/lookup`.
  Its "Product rules" (lines 196–261) are binding here: contact rules, pinned-first notes, rowVersion on every
  profile/status/contact command (notes take none), scope 403 vs 404, lookup = active customers only.
- **Module:** none (frontend only). Reads the **`Customers`** module over HTTP. **No backend change, no new endpoint,
  no `.http` change, no migration.** `frontend/app/api/generated/agent.ts` already holds every Customers path and
  schema (lines 183–1553, schemas 1617–1860) — Story 11 committed them, so **`pnpm gen:api` is not run** unless
  Verification step 1 finds drift.
- **MVP scope (2026-09-29).** Absorbs story 13 in minimal form: the page has a **Tickets panel placeholder** (empty
  state only) that story 20 fills. **Deferred:** interaction timeline and counters, attachments, merge UI, saved filter
  presets, CSV export, email/WhatsApp/call quick actions, history tab, note author names (see Product rules), **any
  test suite**.
- **Checked during planning (2026-09-30, from files, no runs):** `frontend/package.json` — Nuxt **4.5.2**, Vue
  3.5.43, `@nuxtjs/i18n` 10.6.0, `openapi-fetch` 0.17.0, Tailwind 4.3.3, TypeScript 5.9.3, vue-tsc 3.3.11,
  pnpm **12.6.0**. Nuxt 4.5.2's `useAsyncData` handler receives `{ signal }` and defaults to `dedupe: 'cancel'`
  (`node_modules/nuxt/dist/app/composables/asyncData.d.ts` lines 7–9, 52; `asyncData.js` lines 328–372: a superseded
  call is aborted and its result discarded). `openapi-fetch` 0.17.0 accepts `signal` in every call's options
  (`dist/index.d.ts` line 96, `Omit<RequestInit, "body" | "headers">`). **No new npm package. No NuGet change.**
  This machine has **no Python** and **no `pnpm` on PATH** until `corepack enable pnpm --install-directory "$APPDATA/npm"`;
  Arabic text breaks Bash heredocs — use the **Write tool** for every file with Arabic in it.
- **Named risks — prove each one first, in this order, before building on it** (planning ran nothing):
  - **R1 — `pnpm` is available.** **Prove:** `corepack enable pnpm --install-directory "$APPDATA/npm"`, then
    `pnpm --version` prints `12.6.0`, then `pnpm --dir frontend install --frozen-lockfile` exits 0.
  - **R2 — component names from `app/components/customer/`.** Nuxt prefixes a component with its folder but drops a
    prefix the file name already starts with, so `components/customer/CustomerPicker.vue` must register as
    `CustomerPicker` (not `CustomerCustomerPicker`). **Prove:** after F1's first file exists, run
    `pnpm --dir frontend exec nuxt prepare` and grep `frontend/.nuxt/components.d.ts` for `'CustomerPicker'` and
    `'CustomerContactsPanel'`. **Fallback (no other):** import each one explicitly
    (`import CustomerPicker from '~/components/customer/CustomerPicker.vue'`) where it is used; record it.
  - **R3 — cancellation reaches the network.** `createCrmFetch.withHeaders` (`app/api/crm-fetch.ts` lines 23–32)
    rebuilds the request with `new Request(request, { headers })`, which must keep the original `signal`.
    **Prove:** list page, DevTools Network throttled to "Slow 3G", type `najd` quickly: earlier
    `/api/Agent/Customers?...` rows show **(canceled)**, only the last one completes, the table shows Najd Logistics,
    and **no** toast or error state appears. **Fallback:** in `withHeaders` pass the signal explicitly —
    `new Request(request, { headers, signal: request.signal })` — the 401 retry contract is untouched; record it.
    Stale results are discarded by Nuxt either way (`asyncData.js` line 353), so correctness never depends on R3.
  - **R4 — panel write-through with `useNuxtData`.** After a contact command, the profile panel must hold the new
    `rowVersion` (the command bumped it — Story 11 R3). **Prove:** detail page of Najd Logistics → add a contact →
    **without reloading**, Edit profile → Save → **200**, not 409. **Fallback:** in `useCustomer.publish`, replace the
    write-through with `reload()` (refetch all three panels) after every command; record it.
  - **R5 — `Drawer size="lg"` at 375 px.** `UiDrawer` sets `w-[28rem]` (448 px, `app/components/ui/Drawer.vue`
    line 8); the UA `dialog` max-width should clamp it. **Prove:** at 375 px the create drawer fits with no
    horizontal page scroll and the close button is visible. **Fallback:** change line 8's lg class to
    `'w-full max-w-[28rem]'` (and md to `'w-full max-w-80'`), check `/_dev/ui` Drawer section at 375, record it.
  - **R6 — vue-i18n message syntax.** `@`, `|`, `{`, `}` are special in vue-i18n messages; the copy below uses `{name}`,
    `{count}`, `{values}` placeholders only and **no `@` or `|` anywhere**. **Prove:** `pnpm --dir frontend dev`, open
    both pages in `ar` and `en`: no `[intlify]` compile warning in the browser console.
  - **R7 — no seeded user lacks `customers.write`.** Every role holds it (`CrmPermissions.cs` lines 22–29), so "hide
    write actions" cannot be walked with a real account. **Prove:** Vue DevTools → Pinia → `auth` → edit
    `me.permissions` to remove `"customers.write"` → the list's "New customer" button and every Edit / Deactivate /
    Add contact / Make primary / Remove / Pin / Add note control disappear; restore it and they return.
  - **R8 — forcing a single panel failure.** **Prove:** open Najd Logistics once (chunks cached), go back to the list,
    DevTools → Network → Offline, click Najd again: header, contacts, and notes each show their **own** error state
    with a retry; the Tickets panel, nav, locale switcher, and "Back to customers" still work. Go Online, click
    **only** the contacts panel's retry: contacts load, the other two still show their error state until their own
    retry is clicked.
- **Coordination:**
  - **Story 20** (ticket UI) uses `<CustomerPicker v-model="customer" :label="…" :error="…" />` from F6 (model type
    `CustomerLookupDto | null`) and replaces `CustomerTicketsPanel.vue`'s body with the customer's tickets; the panel
    shell (heading, `<section>`, loading/empty/error slots) stays.
  - **Story 13** (deferred) replaces the placeholder with a timeline; **Story 47** (portal) does not use these pages.

---

## Story Goal

Agents find a customer fast and see everything about them on one page.

1. **`/agent/customers`** — a server-side paged table (20 per page) with a debounced search box (name in Arabic or
   English, number, email, phone), filters for status, type, branch, and department, sortable Name / Number /
   Created columns, and every filter, the sort, and the page reflected in the URL (reload and back keep them).
   Below 768 px the table becomes stacked cards with a sort select.
2. **`/agent/customers/[id]`** — a header panel (name, customer number, type, status, owning department, legal name,
   preferred language, created) with Edit and Deactivate/Reactivate, a Contacts panel (add, edit, make primary,
   remove), a Notes panel (add, pin/unpin, remove), and a Tickets placeholder panel. Header, contacts, and notes
   **load independently**, each with its own loading skeleton, empty state, and error state with retry.
3. **Create and edit** in a drawer; 400 field errors and 422 domain errors land on the right field in the active
   locale. **Duplicate warning** before create, with "Create anyway".
4. **`CustomerPicker`** — a reusable async, keyboard- and screen-reader-operable combobox over the lookup endpoint,
   exercised on `/_dev/api`, consumed by story 20.
5. Write actions are hidden without `customers.write` (`useCan()`); the API still enforces it.
6. All copy in `en` and `ar`; logical utilities only; verified at 375 / 768 / 1280 px.

**Not in scope:** everything deferred in Prerequisites; any backend change; **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Frontend — Nuxt + Vue 3 + TypeScript + Tailwind" (RTL, i18n, `useApi`, `useFormErrors`, `useFormat`,
   `<bdi dir="ltr">`, `/_dev/ui` rule) and "Testing policy".
2. [`11-story-customer-aggregate-profiles-contact-details-and-notes.md`](11-story-customer-aggregate-profiles-contact-details-and-notes.md)
   — Prerequisites "Coordination" (lines 88–93) and "Product rules" (lines 196–261, especially the error-code list
   at lines 251–260).
3. `backend/src/Modules/Customers/Crm.Modules.Customers.Presentation/Controllers/Agent/CustomersController.cs` —
   lines 31–153 (every route and verb: contact remove is **`POST …/contacts/{contactId}/remove`** with a rowVersion
   body; note remove is **`DELETE …/notes/{noteId}`**; pin is **`PUT …/notes/{noteId}/pin`** with `{ isPinned }`),
   lines 156–176 (request records). Search query parameters at lines 34–39 (`descending` defaults to `true`).
4. `backend/src/Modules/Customers/Crm.Modules.Customers.Application/Commands/CreateCustomer/CreateCustomerCommandValidator.cs`
   lines 174–190 (400 field keys: `displayNameEn`, `type`, `legalName`, `preferredLanguage`, `contacts[0].name` …;
   no format rule for email/phone — those are 422), `Queries/SearchCustomers/SearchCustomersQueryValidator.cs`
   (page ≥ 1, pageSize 1–100, search ≤ 200), `Queries/LookupCustomers/LookupCustomersQueryValidator.cs` (take 1–25),
   `Specifications/CustomerSort.cs` (`createdAt`, `number`, `nameEn`, `nameAr`), `CustomersErrors.cs` lines 19–20
   (`Organisation.DepartmentRequired` is a **400 validation** error on field `departmentId`).
5. `backend/src/Modules/Customers/Crm.Modules.Customers.Domain/CustomerDomainErrors.cs` lines 8–33 — every 422 code
   and its English text (the `en` copy in F3 is based on these).
6. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ApiControllerBase.cs` lines 36–68 and
   `ErrorHandling/GlobalExceptionHandler.cs` lines 53–56, 119–131 — every problem carries `code` as an extension.
7. `frontend/app/api/generated/agent.ts` — paths lines 183–1553; schemas `CreateCustomerRequest`,
   `CreateCustomerOutcome`, `CustomerDto`, `CustomerListItemDto`, `CustomerListItemDtoPagedResult`,
   `CustomerLookupDto`, `ContactDto`, `CustomerNoteDto`, `DuplicateCandidateDto`, `MyDepartmentDto` (lines 1617–1810).
8. `frontend/app/composables/useApi.ts` lines 18–63 (`call`, `toastOnError`, the private `maybeToast`),
   `useFormErrors.ts` lines 4–37 (only 400s today), `useCan.ts`, `useFormat.ts`, `useUsers.ts` lines 8–44 (the
   list/get composable shape: `useAsyncData`, `toastOnError: false`, **throw the problem**),
   `useMyScope.ts`; `app/stores/scope.ts` (`myScope.departments`, `setActive` → `refreshNuxtData()`);
   `app/stores/auth.ts` lines 13–24 (`me.id`, `permissions`); `app/utils/scope.ts` lines 14–19 (`localizedName`);
   `app/utils/permissions.ts`; `app/api/problem.ts` lines 20–31, 103–112 (camelCase field keys), 179–185
   (`problemOf`); `app/api/problem-message.ts`; `app/api/crm-fetch.ts` lines 23–32 (R3).
9. `frontend/app/pages/admin/users/index.vue` (list page precedent: search form, `UiTable` slots, `UiPagination`,
   forbidden vs error states, modal form with `useFormErrors`) and `app/pages/admin/users/[id].vue` script
   (rowVersion, `afterWrite` 409 → reload, confirm modals).
10. `frontend/app/components/ui/Table.vue` (whole file — F2 adds sorting), `Drawer.vue` line 8 (R5), `Input.vue`,
    `Select.vue`, `Textarea.vue`, `EmptyState.vue` (`actions` slot), `ErrorState.vue` (`retry` event),
    `Skeleton.vue`, `Badge.vue`, `Modal.vue`, `Pagination.vue`, `Spinner.vue`.
11. `frontend/app/layouts/agent.vue` lines 95–110 (`agentNav`).
12. `frontend/app/pages/_dev/ui.vue` lines 121–143 (Select section — the shape F2's Checkbox section copies),
    182–214 (Table section), 521–541 (`sections` index), 557–570 (table sample data);
    `frontend/app/pages/_dev/api.vue` lines 79–103 (panel 3, the last section) and 107–118 (script head).
13. `frontend/i18n/locales/en/errors.json` (whole file) and its `ar` twin; `en/shell.json` (`shell.pagination.*`);
    `en/organisation.json` lines 7–11 (`organisation.scope.option`); `frontend/scripts/check-locales.ts` (same keys
    on both sides, no empty strings); `frontend/eslint.config.mjs` (physical-direction ban, bare-string ban with its
    allowlist at lines 44–45); `frontend/nuxt.config.ts` lines 7–9 (new locale files load automatically).

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **URL is the list state.** Query keys: `q` (search), `status` (`Active`|`Inactive`), `type`
  (`Individual`|`Organisation`), `branch` (uuid), `department` (uuid), `sort` (`createdAt`|`number`|`nameEn`|`nameAr`),
  `dir` (`asc`|`desc`), `page` (≥ 1). Defaults (`sort=createdAt`, `dir=desc`, `page=1`, empty filters) are omitted
  from the URL. Unknown or malformed values are dropped when parsed (never sent to the API). Search keystrokes use
  `router.replace` (no history entry per keystroke); filter, sort, and page changes use `router.push`. Any filter or
  search change resets `page` to 1.
- **Search** is debounced **300 ms**, trimmed, `maxlength` 200. The list refetches only from the URL; a superseded
  request is aborted (Nuxt `dedupe: 'cancel'` + `signal`) and its result discarded.
- **Branch and department filter options** come from the caller's own scope (`scope.myScope.departments`, all of
  them, active or not) — agents have no access to the Admin organisation endpoints. Choosing a branch narrows the
  department options to that branch and clears a department from another branch.
- **Name sort follows the UI language:** the Name column sorts by `nameAr` in `ar` and `nameEn` in `en`; the header
  shows the Name column as sorted for either key. A new column sort starts ascending, except Created, which starts
  descending.
- **Detail panels load independently:** header, contacts, and notes each run their own
  `GET /api/Agent/Customers/{id}` under their own `useAsyncData` key (the query is cached server-side, Story 11
  R10). Every command returns the full `CustomerDto`; it is **written into every panel that has data** (so the next
  command sends the new `rowVersion`); a panel in its error state keeps it until its own retry. Story 20's tickets
  panel will call its own endpoint.
- **Create form sends at most one contact** (the optional "Primary contact" block, `isPrimary: true`); more
  contacts are added on the detail page. So a create-time 422 about a contact always maps to `contacts[0].*`.
  The block is sent only when any of its fields is non-blank.
- **Department on create:** the Department select shows only when the active scope is "All my departments" **and**
  the caller has more than one active department; otherwise it is omitted and `ResolveWriteTarget` decides.
- **Legal name** shows only when type is Organisation; switching to Individual sends `legalName: null`.
- **Duplicate warning:** a `created: false` response replaces the form's footer with the candidate list (number,
  name, status, matched emails/phones, an "Open" link) and "Create anyway" / "Back to the form". "Create anyway"
  re-sends the **same** body with `confirmDuplicate: true`. Editing any field hides the warning (the next submit
  re-checks).
- **Error placement:** a 400 lands on fields through `useFormErrors().apply`; a 422 (or 400/403) whose `code` is in
  the form's code→field map lands on that field with the `errors.code.<code>` text in the active locale
  (`useFormErrors().applyCode`); any other 422 lands on the form (`formError`); a 409 toasts
  `errors.code.Persistence.ConcurrencyConflict` and reloads the customer; anything else toasts. Non-form actions
  (status, make primary, remove contact, pin, remove note) use `useApi`'s default toast.
- **Note author:** `CustomerNoteDto` carries only `authorId`; the UI shows "You" when it equals `auth.me.id`, else
  "A colleague". Author names are deferred (no agent-readable user directory exists).
- **Tickets panel** is a static placeholder: an empty state with no action and no loading/error state (it loads
  nothing). This is the one documented exception to "every panel has an empty state with a next action".
- **Identifiers** (customer number, email, phone) render inside `<bdi dir="ltr">`; dates through `useFormat()`.
- **Composables** live in `app/composables/customers/` and are **imported explicitly** (Nuxt auto-imports only the
  top level of `composables/`); customer components live in `app/components/customer/` (R2).

---

## Frontend Tasks

All paths are under `frontend/`. `<script setup lang="ts">`; no `any`, no `!`, no ts-ignore, no `as` cast except
`as const`; logical utilities only (`ms-/me-/ps-/pe-/start-/end-/text-start/text-end/border-s/border-e`); every
visible string a key; `ui-hover:`/`ui-focus:` inside `app/components/ui/`. Use the **Write tool** for every file
with Arabic in it. `noUncheckedIndexedAccess` is on (Nuxt 4 default): index reads are `T | undefined`.

### F1 — Shared helpers

#### F1a — `useApi` exposes its toast

File: `app/composables/useApi.ts`. Add `toast: (problem: Problem) => void` to the return type (lines 18–21) and
return `toast: (problem) => maybeToast(problem)` next to `call` (line 63). Behaviour is `maybeToast` unchanged (it
still skips validation and 401). No existing caller changes.

#### F1b — `useFormErrors().applyCode`

File: `app/composables/useFormErrors.ts`. Add, and return it next to `apply`:

```ts
import { problemMessage } from '~/api/problem-message'
// inside useFormErrors(), before the functions:
const { $i18n } = useNuxtApp()
const translate = { t: (key: string) => $i18n.t(key), te: (key: string) => $i18n.te(key) }

/** A problem whose `code` names a field in `fieldForCode` puts the localized errors.code.* text on that field
 * (keeping any other field errors the problem carried). Any other 422 domain rule goes to the form. Returns false
 * for everything else - the caller decides (apply() for a plain 400, a toast otherwise). */
function applyCode(problem: Problem, fieldForCode: Readonly<Record<string, string>>): boolean {
  const field: string | undefined = problem.code === null ? undefined : fieldForCode[problem.code]
  const message = problemMessage(problem, translate).description ?? problem.title
  if (field !== undefined) {
    fieldErrors.value = { ...problem.fieldErrors, [field]: [message] }
    formError.value = null
    return true
  }
  if (problem.type === ProblemTypes.DomainRule) {
    fieldErrors.value = {}
    formError.value = message
    return true
  }
  return false
}
```

Every form in this story handles a failed result in this order:
`if (problem.type === ProblemTypes.Conflict) { api.toast(problem); await customer.reload(); return }` →
`if (formErrors.applyCode(problem, map)) return` → `if (formErrors.apply(problem)) return` → `api.toast(problem)`.

#### F1c — Customer utilities

Create file: `app/utils/customers.ts`

```ts
import type { LocationQuery, LocationQueryRaw } from 'vue-router'

export const customerStatuses = ['Active', 'Inactive'] as const
export type CustomerStatusName = (typeof customerStatuses)[number]
export const customerTypes = ['Individual', 'Organisation'] as const
export type CustomerTypeName = (typeof customerTypes)[number]
export const customerSorts = ['createdAt', 'number', 'nameEn', 'nameAr'] as const
export type CustomerSortKey = (typeof customerSorts)[number]
export const customersPageSize = 20

export interface CustomerFilters {
  q: string
  status: CustomerStatusName | ''
  type: CustomerTypeName | ''
  branchId: string
  departmentId: string
  sort: CustomerSortKey
  descending: boolean
  page: number
}

export const defaultCustomerFilters: Readonly<CustomerFilters> = {
  q: '', status: '', type: '', branchId: '', departmentId: '', sort: 'createdAt', descending: true, page: 1,
}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
export function isGuid(value: string): boolean { return guidPattern.test(value) }

function oneOf<T extends string>(values: ReadonlyArray<T>, value: string): T | undefined {
  return values.find((candidate) => candidate === value)
}

function first(value: LocationQuery[string] | undefined): string {
  const item = Array.isArray(value) ? value[0] : value
  return typeof item === 'string' ? item.trim() : ''
}

export function parseCustomerFilters(query: LocationQuery): CustomerFilters {
  const page = Number.parseInt(first(query.page), 10)
  const branch = first(query.branch)
  const department = first(query.department)
  return {
    q: first(query.q).slice(0, 200),
    status: oneOf(customerStatuses, first(query.status)) ?? '',
    type: oneOf(customerTypes, first(query.type)) ?? '',
    branchId: isGuid(branch) ? branch : '',
    departmentId: isGuid(department) ? department : '',
    sort: oneOf(customerSorts, first(query.sort)) ?? 'createdAt',
    descending: first(query.dir) !== 'asc',
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

/** Only non-default values reach the URL. */
export function customerFiltersToQuery(filters: CustomerFilters): LocationQueryRaw {
  const query: LocationQueryRaw = {}
  if (filters.q !== '') query.q = filters.q
  if (filters.status !== '') query.status = filters.status
  if (filters.type !== '') query.type = filters.type
  if (filters.branchId !== '') query.branch = filters.branchId
  if (filters.departmentId !== '') query.department = filters.departmentId
  if (filters.sort !== 'createdAt') query.sort = filters.sort
  if (!filters.descending) query.dir = 'asc'
  if (filters.page !== 1) query.page = String(filters.page)
  return query
}

/** code -> field key maps for applyCode (F1b). Field keys are the camelCase request paths useFormErrors uses. */
export const contactFieldForCode: Readonly<Record<string, string>> = {
  'Customers.ContactNameRequired': 'name',
  'Customers.ContactNameTooLong': 'name',
  'Customers.JobTitleTooLong': 'jobTitle',
  'Customers.EmailInvalid': 'email',
  'Customers.PhoneInvalid': 'phone',
  'Customers.ContactChannelRequired': 'email',
  'Customers.PortalAccessRequiresEmail': 'hasPortalAccess',
  'Customers.DuplicateContactEmail': 'email',
  'Customers.DuplicateContactPhone': 'phone',
}

export const profileFieldForCode: Readonly<Record<string, string>> = {
  'Customers.NameRequired': 'displayNameEn',
  'Customers.NameTooLong': 'displayNameEn',
  'Customers.TypeUnsupported': 'type',
  'Customers.LegalNameRequired': 'legalName',
  'Customers.LegalNameNotAllowed': 'legalName',
  'Customers.LegalNameTooLong': 'legalName',
  'Customers.LanguageUnsupported': 'preferredLanguage',
}

export const createFieldForCode: Readonly<Record<string, string>> = {
  ...profileFieldForCode,
  ...Object.fromEntries(Object.entries(contactFieldForCode).map(([code, field]) => [code, `contacts[0].${field}`])),
  'Organisation.DepartmentRequired': 'departmentId',
  'Organisation.OutOfScope': 'departmentId',
}

export const noteFieldForCode: Readonly<Record<string, string>> = {
  'Customers.NoteBodyRequired': 'body',
  'Customers.NoteBodyTooLong': 'body',
}

export function statusTone(status: string | null | undefined): 'success' | 'neutral' {
  return status === 'Active' ? 'success' : 'neutral'
}
```

`vue-router` is a direct dependency (`package.json`), so the type import resolves.

#### F1d — Debounce

Create file: `app/utils/debounce.ts`

```ts
/** Trailing-edge debounce with cancel. The caller cancels it on unmount (onScopeDispose). */
export function debounce<Args extends unknown[]>(fn: (...args: Args) => void, ms: number) {
  let timer: ReturnType<typeof setTimeout> | undefined
  return {
    run(...args: Args): void {
      if (timer !== undefined) clearTimeout(timer)
      timer = setTimeout(() => { timer = undefined; fn(...args) }, ms)
    },
    cancel(): void {
      if (timer !== undefined) clearTimeout(timer)
      timer = undefined
    },
  }
}
```

### F2 — UI kit additions (each gets a `/_dev/ui` section)

#### F2a — `UiTable` sortable columns

Create file: `app/utils/table.ts` — `export interface TableSort { key: string; descending: boolean }`.

File: `app/components/ui/Table.vue`.
- `Column` (script, lines 55–59) gains `sortable?: boolean`.
- Add `import type { TableSort } from '~/utils/table'` and
  `const sort = defineModel<TableSort | undefined>('sort', { default: undefined })`.
- Replace the `<th>` body (template lines 11–19): keep `scope="col"` and the classes; add
  `:aria-sort="column.sortable ? ariaSort(column.key) : undefined"`; when `column.sortable`, render
  `<button type="button" class="inline-flex items-center gap-1 rounded-sm font-medium text-fg ui-focus:focus-ring ui-hover:underline" @click="toggleSort(column.key)">{{ column.label }}<span aria-hidden="true" class="text-fg-muted">{{ sortGlyph(column.key) }}</span></button>`,
  else `{{ column.label }}` as today. The glyph comes from a function (a mustache, not a bare string).
- Functions (read `sort.value` into a `const current` before testing it — no optional-chain narrowing):
  `ariaSort(key)` → `'none'` unless `current?.key === key`, then `'descending'`/`'ascending'`;
  `sortGlyph(key)` → `'↕'` / `'↓'` / `'↑'` the same way;
  `toggleSort(key)` → same key: flip `descending`; new key: `{ key, descending: false }`. (The page overrides the
  starting direction for Created — Product rules — by translating in its own setter.)
- No sort model given → columns render exactly as today (the Users table is unchanged).

File: `app/pages/_dev/ui.vue` — in the Table section (lines 182–214) add a `DevStateRow label="sortable"` with the
sample table, `tableColumns` given `sortable: true` on `name`, and `v-model:sort="tableSort"` (`ref<TableSort | undefined>({ key: 'name', descending: false })`),
plus a `<p class="text-xs text-fg-muted">` echoing `tableSort`. The sample rows are not re-sorted (sorting is the
server's job) — the row only proves the header states, `aria-sort`, and keyboard activation.

#### F2b — `UiCheckbox`

Create file: `app/components/ui/Checkbox.vue` — same prop shape as `Input.vue` (`label`, `hint?`, `error?`,
`disabled?`), `defineOptions({ inheritAttrs: false })`, `const model = defineModel<boolean>({ default: false })`,
`useId()` ids and `aria-describedby` exactly like `Input.vue` lines 45–48. Template:

```vue
<div class="flex flex-col gap-1">
  <div class="flex items-start gap-2">
    <input :id="id" v-model="model" type="checkbox" :disabled="disabled" :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="describedBy" v-bind="$attrs"
      class="mt-0.5 size-4 shrink-0 accent-primary-600 ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50" />
    <label :for="id" class="text-sm text-fg">{{ label }}</label>
  </div>
  <p v-if="hint && !error" :id="hintId" class="ps-6 text-xs text-fg-muted">{{ hint }}</p>
  <p v-if="error" :id="errorId" role="alert" class="ps-6 text-xs text-danger-600">{{ error }}</p>
</div>
```

File: `app/pages/_dev/ui.vue` — add `<!-- Checkbox -->` `DevSection anchor-id="checkbox" title="Checkbox"` right
after the Select section (after line 143), with state rows default, checked, hover / focus (`data-force="hover"` /
`data-force="focus"` like the Input section), disabled, error (`error="Portal access needs an email address."`),
long label (`:label="longSentence"`) and Arabic (`:label="arabicString"`); add `{ id: 'checkbox', title: 'Checkbox' }`
after `select` in `sections` (line 521 onward).

### F3 — Copy

Use the **Write tool**. `pnpm lint` runs `check-locales.ts`: same keys on both sides, one top-level key per file.

#### F3a — `errors.json` codes

Files: `i18n/locales/en/errors.json` and `i18n/locales/ar/errors.json` — add a `"Customers"` object under
`errors.code`, after `"Identity"`:

| Key | en | ar |
|---|---|---|
| `CustomerNotFound` | The customer was not found. | لم يتم العثور على العميل. |
| `ContactNotFound` | The contact was not found on this customer. | لم يتم العثور على جهة الاتصال لدى هذا العميل. |
| `NoteNotFound` | The note was not found on this customer. | لم يتم العثور على الملاحظة لدى هذا العميل. |
| `ScopeRequired` | A customer must belong to a department. | يجب أن يتبع العميل قسمًا. |
| `NumberInvalid` | The customer number could not be generated. Try again. | تعذّر إنشاء رقم العميل. حاول مرة أخرى. |
| `TypeUnsupported` | Choose Individual or Organisation. | اختر فرد أو منشأة. |
| `NameRequired` | Enter the customer's name in Arabic or English. | أدخل اسم العميل بالعربية أو بالإنجليزية. |
| `NameTooLong` | A customer name cannot be more than 200 characters. | لا يمكن أن يزيد اسم العميل على 200 حرف. |
| `LegalNameRequired` | An organisation needs a legal name. | يجب إدخال الاسم القانوني للمنشأة. |
| `LegalNameNotAllowed` | An individual customer cannot have a legal name. | لا يمكن أن يكون للعميل الفرد اسم قانوني. |
| `LegalNameTooLong` | A legal name cannot be more than 300 characters. | لا يمكن أن يزيد الاسم القانوني على 300 حرف. |
| `LanguageUnsupported` | Choose English or Arabic. | اختر الإنجليزية أو العربية. |
| `AlreadyActive` | The customer is already active. | العميل نشط بالفعل. |
| `AlreadyInactive` | The customer is already inactive. | العميل غير نشط بالفعل. |
| `EmailInvalid` | The email address is not valid. | عنوان البريد الإلكتروني غير صالح. |
| `PhoneInvalid` | Use the international format, starting with the country code. | استخدم الصيغة الدولية التي تبدأ برمز الدولة. |
| `ContactNameRequired` | A contact needs a name. | يجب إدخال اسم جهة الاتصال. |
| `ContactNameTooLong` | A contact name cannot be more than 200 characters. | لا يمكن أن يزيد اسم جهة الاتصال على 200 حرف. |
| `JobTitleTooLong` | A job title cannot be more than 100 characters. | لا يمكن أن يزيد المسمى الوظيفي على 100 حرف. |
| `ContactChannelRequired` | Enter an email or a phone number. | أدخل بريدًا إلكترونيًا أو رقم هاتف. |
| `PortalAccessRequiresEmail` | Portal access needs an email address. | يتطلب الدخول إلى البوابة عنوان بريد إلكتروني. |
| `DuplicateContactEmail` | Another contact of this customer already has this email. | لدى جهة اتصال أخرى لهذا العميل البريد الإلكتروني نفسه. |
| `DuplicateContactPhone` | Another contact of this customer already has this phone number. | لدى جهة اتصال أخرى لهذا العميل رقم الهاتف نفسه. |
| `PrimaryContactRemoval` | Make another contact primary before removing this one. | اجعل جهة اتصال أخرى رئيسية قبل حذف هذه. |
| `NoteBodyRequired` | A note cannot be empty. | لا يمكن أن تكون الملاحظة فارغة. |
| `NoteBodyTooLong` | A note cannot be more than 4000 characters. | لا يمكن أن تزيد الملاحظة على 4000 حرف. |
| `NoteAuthorRequired` | A note needs an author. | يجب أن يكون للملاحظة كاتب. |

#### F3b — `customers.json`

Create files: `i18n/locales/en/customers.json` and `i18n/locales/ar/customers.json` (loaded automatically,
`nuxt.config.ts` lines 7–9). Exactly these keys, both sides:

`en`:

```json
{
  "customers": {
    "nav": { "customers": "Customers" },
    "list": {
      "title": "Customers", "caption": "Customers", "create": "New customer", "search": "Search",
      "searchHint": "Name in Arabic or English, customer number, email, or phone",
      "results": "{count} customers", "empty": "No customers match these filters",
      "emptyDescription": "Try another search or clear the filters.", "emptyNone": "No customers yet",
      "emptyNoneDescription": "Create the first customer to start.", "clearFilters": "Clear filters",
      "loadError": "The customers could not be loaded", "retry": "Try again", "open": "Open {name}"
    },
    "filters": {
      "label": "Filters", "status": "Status", "type": "Type", "branch": "Branch", "department": "Department",
      "all": "All", "sort": "Sort by"
    },
    "sort": {
      "newest": "Newest first", "oldest": "Oldest first", "number": "Customer number",
      "nameAsc": "Name, A to Z", "nameDesc": "Name, Z to A"
    },
    "columns": {
      "name": "Name", "number": "Number", "type": "Type", "status": "Status", "primaryContact": "Primary contact",
      "createdAt": "Created"
    },
    "status": { "Active": "Active", "Inactive": "Inactive" },
    "type": { "Individual": "Individual", "Organisation": "Organisation" },
    "language": { "en": "English", "ar": "Arabic" },
    "fields": {
      "displayNameEn": "Name in English", "displayNameAr": "Name in Arabic",
      "nameHint": "Enter at least one of the two names.", "type": "Customer type", "legalName": "Legal name",
      "preferredLanguage": "Preferred language", "department": "Department", "number": "Customer number",
      "createdAt": "Created"
    },
    "form": {
      "createTitle": "New customer", "editTitle": "Edit customer", "primaryContact": "Primary contact",
      "primaryContactHint": "Optional. You can add more contacts on the customer page.",
      "create": "Create customer", "save": "Save", "cancel": "Cancel", "close": "Close",
      "created": "Customer created", "saved": "Customer saved"
    },
    "duplicate": {
      "title": "This may be a duplicate",
      "description": "These customers already have one of the contact details you entered. Open one of them, or create the new customer anyway.",
      "matched": "Matches {values}", "open": "Open", "createAnyway": "Create anyway", "back": "Back to the form"
    },
    "detail": {
      "back": "Back to customers", "headerLabel": "Customer summary", "notFound": "Customer not found",
      "notFoundDescription": "The link may be wrong, or the customer was removed.",
      "loadError": "This section could not be loaded", "retry": "Try again", "edit": "Edit",
      "deactivate": "Deactivate", "reactivate": "Reactivate", "deactivateTitle": "Deactivate this customer?",
      "deactivateDescription": "An inactive customer stays readable and editable, but cannot be picked for new tickets.",
      "confirm": "Deactivate", "cancel": "Cancel", "deactivated": "Customer deactivated",
      "reactivated": "Customer reactivated"
    },
    "contacts": {
      "title": "Contacts", "add": "Add contact", "addTitle": "Add contact", "editTitle": "Edit contact",
      "edit": "Edit", "remove": "Remove", "setPrimary": "Make primary", "primary": "Primary",
      "portalAccess": "Portal access", "empty": "No contacts yet",
      "emptyDescription": "Add a contact so the team can reach this customer.",
      "removeTitle": "Remove this contact?", "removeDescription": "{name} will be removed from this customer.",
      "saved": "Contact saved", "removed": "Contact removed", "primarySet": "Primary contact changed",
      "actionsFor": "Actions for {name}", "save": "Save", "cancel": "Cancel", "close": "Close",
      "fields": {
        "name": "Name", "jobTitle": "Job title", "email": "Email", "phone": "Phone",
        "phoneHint": "International format with the country code", "isPrimary": "Primary contact",
        "isPrimaryFirst": "The first contact is always the primary contact.",
        "hasPortalAccess": "Can sign in to the customer portal"
      }
    },
    "notes": {
      "title": "Notes", "body": "New note", "bodyHint": "Internal only. Customers never see notes.",
      "add": "Add note", "pin": "Pin", "unpin": "Unpin", "pinned": "Pinned", "remove": "Remove",
      "removeTitle": "Remove this note?", "removeDescription": "The note will be deleted for everyone.",
      "empty": "No notes yet", "emptyDescription": "Write the first note for the team.", "byYou": "You",
      "byColleague": "A colleague", "added": "Note added", "removed": "Note removed", "cancel": "Cancel",
      "close": "Close"
    },
    "tickets": {
      "title": "Tickets", "empty": "No tickets yet", "emptyDescription": "This customer's tickets will appear here."
    },
    "picker": {
      "placeholder": "Name, number, email, or phone", "searching": "Searching", "results": "{count} customers found",
      "empty": "No active customer matches", "error": "The search failed", "retry": "Try again", "clear": "Clear"
    }
  }
}
```

`ar` (same keys, same order):

```json
{
  "customers": {
    "nav": { "customers": "العملاء" },
    "list": {
      "title": "العملاء", "caption": "العملاء", "create": "عميل جديد", "search": "بحث",
      "searchHint": "الاسم بالعربية أو بالإنجليزية، أو رقم العميل، أو البريد الإلكتروني، أو الهاتف",
      "results": "عدد العملاء: {count}", "empty": "لا يوجد عملاء مطابقون لهذه المرشحات",
      "emptyDescription": "جرّب بحثًا آخر أو امسح المرشحات.", "emptyNone": "لا يوجد عملاء بعد",
      "emptyNoneDescription": "أنشئ أول عميل للبدء.", "clearFilters": "مسح المرشحات",
      "loadError": "تعذّر تحميل العملاء", "retry": "حاول مرة أخرى", "open": "فتح {name}"
    },
    "filters": {
      "label": "المرشحات", "status": "الحالة", "type": "النوع", "branch": "الفرع", "department": "القسم",
      "all": "الكل", "sort": "الترتيب حسب"
    },
    "sort": {
      "newest": "الأحدث أولًا", "oldest": "الأقدم أولًا", "number": "رقم العميل",
      "nameAsc": "الاسم تصاعديًا", "nameDesc": "الاسم تنازليًا"
    },
    "columns": {
      "name": "الاسم", "number": "الرقم", "type": "النوع", "status": "الحالة",
      "primaryContact": "جهة الاتصال الرئيسية", "createdAt": "تاريخ الإنشاء"
    },
    "status": { "Active": "نشط", "Inactive": "غير نشط" },
    "type": { "Individual": "فرد", "Organisation": "منشأة" },
    "language": { "en": "الإنجليزية", "ar": "العربية" },
    "fields": {
      "displayNameEn": "الاسم بالإنجليزية", "displayNameAr": "الاسم بالعربية",
      "nameHint": "أدخل أحد الاسمين على الأقل.", "type": "نوع العميل", "legalName": "الاسم القانوني",
      "preferredLanguage": "اللغة المفضلة", "department": "القسم", "number": "رقم العميل",
      "createdAt": "تاريخ الإنشاء"
    },
    "form": {
      "createTitle": "عميل جديد", "editTitle": "تعديل العميل", "primaryContact": "جهة الاتصال الرئيسية",
      "primaryContactHint": "اختياري. يمكنك إضافة جهات اتصال أخرى من صفحة العميل.",
      "create": "إنشاء العميل", "save": "حفظ", "cancel": "إلغاء", "close": "إغلاق",
      "created": "تم إنشاء العميل", "saved": "تم حفظ العميل"
    },
    "duplicate": {
      "title": "قد يكون هذا العميل مكررًا",
      "description": "لدى هؤلاء العملاء إحدى بيانات الاتصال التي أدخلتها. افتح أحدهم، أو أنشئ العميل الجديد على أي حال.",
      "matched": "يطابق {values}", "open": "فتح", "createAnyway": "إنشاء على أي حال", "back": "العودة إلى النموذج"
    },
    "detail": {
      "back": "العودة إلى العملاء", "headerLabel": "ملخص العميل", "notFound": "لم يتم العثور على العميل",
      "notFoundDescription": "قد يكون الرابط غير صحيح، أو تمت إزالة العميل.",
      "loadError": "تعذّر تحميل هذا القسم", "retry": "حاول مرة أخرى", "edit": "تعديل",
      "deactivate": "إلغاء التنشيط", "reactivate": "إعادة التنشيط",
      "deactivateTitle": "هل تريد إلغاء تنشيط هذا العميل؟",
      "deactivateDescription": "يبقى العميل غير النشط قابلًا للعرض والتعديل، لكن لا يمكن اختياره لتذاكر جديدة.",
      "confirm": "إلغاء التنشيط", "cancel": "إلغاء", "deactivated": "تم إلغاء تنشيط العميل",
      "reactivated": "تمت إعادة تنشيط العميل"
    },
    "contacts": {
      "title": "جهات الاتصال", "add": "إضافة جهة اتصال", "addTitle": "إضافة جهة اتصال",
      "editTitle": "تعديل جهة الاتصال", "edit": "تعديل", "remove": "حذف", "setPrimary": "جعلها رئيسية",
      "primary": "رئيسية", "portalAccess": "دخول البوابة", "empty": "لا توجد جهات اتصال بعد",
      "emptyDescription": "أضف جهة اتصال ليتمكن الفريق من التواصل مع هذا العميل.",
      "removeTitle": "هل تريد حذف جهة الاتصال هذه؟", "removeDescription": "ستتم إزالة {name} من هذا العميل.",
      "saved": "تم حفظ جهة الاتصال", "removed": "تم حذف جهة الاتصال", "primarySet": "تم تغيير جهة الاتصال الرئيسية",
      "actionsFor": "إجراءات {name}", "save": "حفظ", "cancel": "إلغاء", "close": "إغلاق",
      "fields": {
        "name": "الاسم", "jobTitle": "المسمى الوظيفي", "email": "البريد الإلكتروني", "phone": "الهاتف",
        "phoneHint": "بالصيغة الدولية مع رمز الدولة", "isPrimary": "جهة الاتصال الرئيسية",
        "isPrimaryFirst": "أول جهة اتصال تكون دائمًا جهة الاتصال الرئيسية.",
        "hasPortalAccess": "يمكنه تسجيل الدخول إلى بوابة العملاء"
      }
    },
    "notes": {
      "title": "الملاحظات", "body": "ملاحظة جديدة", "bodyHint": "داخلية فقط. لا يرى العملاء الملاحظات أبدًا.",
      "add": "إضافة ملاحظة", "pin": "تثبيت", "unpin": "إلغاء التثبيت", "pinned": "مثبتة", "remove": "حذف",
      "removeTitle": "هل تريد حذف هذه الملاحظة؟", "removeDescription": "سيتم حذف الملاحظة للجميع.",
      "empty": "لا توجد ملاحظات بعد", "emptyDescription": "اكتب أول ملاحظة للفريق.", "byYou": "أنت",
      "byColleague": "أحد الزملاء", "added": "تمت إضافة الملاحظة", "removed": "تم حذف الملاحظة",
      "cancel": "إلغاء", "close": "إغلاق"
    },
    "tickets": {
      "title": "التذاكر", "empty": "لا توجد تذاكر بعد", "emptyDescription": "ستظهر تذاكر هذا العميل هنا."
    },
    "picker": {
      "placeholder": "الاسم أو الرقم أو البريد الإلكتروني أو الهاتف", "searching": "جارٍ البحث",
      "results": "عدد العملاء الذين تم العثور عليهم: {count}", "empty": "لا يوجد عميل نشط مطابق",
      "error": "تعذّر البحث", "retry": "حاول مرة أخرى", "clear": "مسح"
    }
  }
}
```

`{count}` is always passed pre-formatted (`format.number(n)`) so Arabic shows Arabic-Indic digits.

### F4 — Composables (`app/composables/customers/`, imported explicitly)

Every composable follows `useUsers.ts`: reads throw the problem inside `useAsyncData` (so `problemOf(error)` works);
commands return `ApiResult`. Types come only from `~/api/generated/agent` `components['schemas']`.

Create file: `app/composables/customers/types.ts`

```ts
import type { components } from '~/api/generated/agent'
export type CustomerDto = components['schemas']['CustomerDto']
export type CustomerListItemDto = components['schemas']['CustomerListItemDto']
export type CustomerLookupDto = components['schemas']['CustomerLookupDto']
export type ContactDto = components['schemas']['ContactDto']
export type CustomerNoteDto = components['schemas']['CustomerNoteDto']
export type DuplicateCandidateDto = components['schemas']['DuplicateCandidateDto']
export type CreateCustomerRequest = components['schemas']['CreateCustomerRequest']
export type UpdateCustomerRequest = components['schemas']['UpdateCustomerRequest']
export type AddContactRequest = components['schemas']['AddContactRequest']
export type UpdateContactRequest = components['schemas']['UpdateContactRequest']
```

Create file: `app/composables/customers/useCustomerSearch.ts`

```ts
import { customerFiltersToQuery, customersPageSize, parseCustomerFilters, type CustomerFilters } from '~/utils/customers'

/** The list use case: the URL is the state (Product rules); the table refetches only from it. */
export function useCustomerSearch() {
  const route = useRoute()
  const router = useRouter()
  const { clients, call } = useApi()

  const filters = computed(() => parseCustomerFilters(route.query))

  function update(patch: Partial<CustomerFilters>, mode: 'push' | 'replace' = 'push'): Promise<unknown> {
    // Every change except paging itself starts again on page 1 (search, filters, and sort alike).
    const resetsPage = Object.keys(patch).some((key) => key !== 'page')
    const next: CustomerFilters = { ...filters.value, ...patch, ...(resetsPage ? { page: 1 } : {}) }
    const location = { query: customerFiltersToQuery(next) }
    return mode === 'replace' ? router.replace(location) : router.push(location)
  }

  const result = useAsyncData(
    'agent-customers',
    async (_nuxtApp, { signal }) => {
      const f = filters.value
      const response = await call(
        clients.agent.GET('/api/Agent/Customers', {
          params: {
            query: {
              search: f.q === '' ? undefined : f.q,
              status: f.status === '' ? undefined : f.status,
              type: f.type === '' ? undefined : f.type,
              branchId: f.branchId === '' ? undefined : f.branchId,
              departmentId: f.departmentId === '' ? undefined : f.departmentId,
              sort: f.sort,
              descending: f.descending,
              page: f.page,
              pageSize: customersPageSize,
            },
          },
          signal,
        }),
        { toastOnError: false },
      )
      if (!response.ok) throw response.problem
      return response.data
    },
    { watch: [filters] },
  )

  return { filters, update, ...result }
}
```

Create file: `app/composables/customers/useCustomer.ts`

```ts
import type { CustomerDto } from './types'

export type CustomerPanel = 'profile' | 'contacts' | 'notes'
const panels: ReadonlyArray<CustomerPanel> = ['profile', 'contacts', 'notes']
export const customerPanelKey = (id: string, panel: CustomerPanel): string => `agent-customer-${panel}-${id}`

/** One customer page (id is fixed per mount - the page's key is route.path). Each panel loads on its own key;
 * publish() writes a command's CustomerDto into every panel that already has data (R4). */
export function useCustomer(id: string) {
  const nuxtApp = useNuxtApp()
  const { clients, call } = useApi()
  // Created in setup so their scope disposal is tracked; the computed getter/setter resolves the key lazily.
  const slots = panels.map((panel) => useNuxtData<CustomerDto>(customerPanelKey(id, panel)).data)

  function load(panel: CustomerPanel) {
    return useAsyncData(customerPanelKey(id, panel), async (_nuxtApp, { signal }) => {
      const result = await call(clients.agent.GET('/api/Agent/Customers/{id}', { params: { path: { id } }, signal }), {
        toastOnError: false,
      })
      if (!result.ok) throw result.problem
      return result.data
    })
  }

  function publish(customer: CustomerDto): void {
    for (const slot of slots) {
      if (slot.value) slot.value = customer
    }
  }

  function reload(): Promise<void> {
    return nuxtApp.runWithContext(() => refreshNuxtData(panels.map((panel) => customerPanelKey(id, panel))))
  }

  return { load, publish, reload }
}
```

If `useNuxtData<CustomerDto>` does not accept a type argument in 4.5.2, drop it and type the loop variable — the
typecheck decides; **no cast**.

Create file: `app/composables/customers/useCustomerCommands.ts` — the write use cases (one function per use case,
all `toastOnError: false` for form commands, default toasting for the rest):

```ts
import type { AddContactRequest, CreateCustomerRequest, UpdateContactRequest, UpdateCustomerRequest } from './types'

export function useCustomerCommands() {
  const { clients, call, toast } = useApi()
  const agent = clients.agent
  const quiet = { toastOnError: false }
  const path = (id: string) => ({ params: { path: { id } } })

  return {
    toast,
    create: (body: CreateCustomerRequest) => call(agent.POST('/api/Agent/Customers', { body }), quiet),
    update: (id: string, body: UpdateCustomerRequest) => call(agent.PUT('/api/Agent/Customers/{id}', { ...path(id), body }), quiet),
    deactivate: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/deactivate', { ...path(id), body: { rowVersion } })),
    reactivate: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/reactivate', { ...path(id), body: { rowVersion } })),
    addContact: (id: string, body: AddContactRequest) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts', { ...path(id), body }), quiet),
    updateContact: (id: string, contactId: string, body: UpdateContactRequest) =>
      call(agent.PUT('/api/Agent/Customers/{id}/contacts/{contactId}', { params: { path: { id, contactId } }, body }), quiet),
    setPrimaryContact: (id: string, contactId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts/{contactId}/primary', { params: { path: { id, contactId } }, body: { rowVersion } })),
    removeContact: (id: string, contactId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts/{contactId}/remove', { params: { path: { id, contactId } }, body: { rowVersion } })),
    addNote: (id: string, body: string) => call(agent.POST('/api/Agent/Customers/{id}/notes', { ...path(id), body: { body } }), quiet),
    pinNote: (id: string, noteId: string, isPinned: boolean) =>
      call(agent.PUT('/api/Agent/Customers/{id}/notes/{noteId}/pin', { params: { path: { id, noteId } }, body: { isPinned } })),
    removeNote: (id: string, noteId: string) =>
      call(agent.DELETE('/api/Agent/Customers/{id}/notes/{noteId}', { params: { path: { id, noteId } } })),
  }
}
```

If spreading `path(id)` confuses openapi-fetch's overloads, write `params: { path: { id } }` inline (typecheck decides).

Create file: `app/composables/customers/useCustomerLookup.ts`

```ts
import type { CustomerLookupDto } from './types'
import type { ApiResult } from '~/composables/useApi'

export const customerLookupTake = 10

/** The picker's use case: active customers only (Story 11). The caller owns cancellation. */
export function useCustomerLookup() {
  const { clients, call } = useApi()
  function search(term: string, signal: AbortSignal): Promise<ApiResult<CustomerLookupDto[]>> {
    return call(
      clients.agent.GET('/api/Agent/Customers/lookup', { params: { query: { search: term, take: customerLookupTake } }, signal }),
      { toastOnError: false },
    )
  }
  return { search }
}
```

### F5 — Customer list page

Create file: `app/pages/agent/customers/index.vue` — `definePageMeta({ layout: 'agent' })`.

Script: `const { filters, update, data, status, error, refresh } = useCustomerSearch()`; `useCan()`,
`canWrite = computed(() => can(Permissions.CustomersWrite))`; `useFormat()`; `useScopeStore()`; `useI18n()`.

- **Search:** `searchInput = ref(filters.value.q)`; `const debounced = debounce((value: string) => void update({ q: value.trim() }, 'replace'), 300)`;
  `watch(searchInput, (value) => debounced.run(value))`; `onScopeDispose(() => debounced.cancel())`;
  `watch(() => filters.value.q, (q) => { if (q !== searchInput.value.trim()) searchInput.value = q })` (back button).
  A `<form role="search" @submit.prevent>` with `UiInput v-model="searchInput" type="search" maxlength="200"
  :label="t('customers.list.search')" :hint="t('customers.list.searchHint')"`; Enter runs `debounced.cancel()` then
  `update({ q: searchInput.value.trim() }, 'replace')` immediately.
- **Filters** (a `fieldset` with a `sr-only` `legend` `customers.filters.label`, `grid gap-3 sm:grid-cols-2 lg:grid-cols-4`):
  four `UiSelect`s — status (`''` = `customers.filters.all`, then `customerStatuses` → `customers.status.*`), type (same
  with `customerTypes`), branch (unique `branchId`s of `scope.myScope?.departments ?? []`, label
  `localizedName(branchName, locale)`), department (departments of the chosen branch, or all when no branch; label
  `localizedName(departmentName, locale)`). Each is a `computed({ get, set })` over `filters` calling `update`.
  Setting a branch clears a department whose `branchId` differs (one `update` call with both keys). A
  "Clear filters" ghost button shows when any of `q/status/type/branchId/departmentId` is set →
  `searchInput.value = ''`, `debounced.cancel()`, `router.push({ query: {} })`.
- **Sort:** `tableSort = computed<TableSort | undefined>({ get, set })`: get maps `nameEn`/`nameAr` → key `name`,
  `number` → `number`, `createdAt` → `createdAt`; set maps key `name` → `locale.value === 'ar' ? 'nameAr' : 'nameEn'`,
  and when the key changed to `createdAt` forces `descending: true` (Product rules); then `update({ sort, descending })`.
  Below `md`, a `UiSelect class="md:hidden"` `customers.filters.sort` with options `createdAt:desc` newest,
  `createdAt:asc` oldest, `number:asc` number, `name:asc` nameAsc, `name:desc` nameDesc (value parsed back through
  the same setter).
- **Header row:** `h1` `customers.list.title`; `UiButton v-if="canWrite"` `customers.list.create` →
  `createOpen = true`. Under the filters: `<p class="text-sm text-fg-muted" aria-live="polite">` with
  `t('customers.list.results', { count: format.number(total) })` when `status === 'success'`.
- **States:** exactly the users page order (`admin/users/index.vue` lines 15–65): forbidden (`problemOf(error)?.type
  === ProblemTypes.Forbidden`) → `UiErrorState` `errors.problem.forbidden` + detail; other error (and
  `status !== 'pending'`) → `UiErrorState` `customers.list.loadError` with `retry-label` → `refresh()`; else the
  results. **Empty:** when any filter is set → `UiEmptyState` `customers.list.empty` / `emptyDescription` with a
  "Clear filters" action; when none → `emptyNone` / `emptyNoneDescription` with the "New customer" action
  (`v-if="canWrite"`).
- **Table (`md` and up):** `UiTable class="hidden md:block" v-model:sort="tableSort"` columns
  `name` (sortable), `number` (sortable), `type`, `status`, `primaryContact`, `createdAt` (sortable, `align: 'end'`);
  `:loading="status === 'pending'"`. Cells: name → `NuxtLink :to="`/agent/customers/${row.id}`"` with
  `localizedName(row.displayName, locale)`; number → `<bdi dir="ltr" class="font-mono">`; type/status →
  `UiBadge` (`statusTone`) with `customers.type.*` / `customers.status.*`; primary contact → name, then email or
  phone in `<bdi dir="ltr">` on a second `text-xs text-fg-muted` line; created → `format.date(row.createdAtUtc)`.
- **Cards (below `md`):** `<ul class="flex flex-col gap-2 md:hidden">`; while pending, three `li`s with
  `UiSkeleton :lines="3"`; else one `li` per row: a block `NuxtLink` with
  `class="block rounded-md border border-border p-3 hover:bg-surface-raised focus-visible:focus-ring"` (pages are
  outside `components/ui/`, so plain `hover:`/`focus-visible:`), name + number on the first line, badges on the
  second, primary contact on the third.
- **Empty:** when `status === 'success' && rows.length === 0`, render the `UiEmptyState` (above) **alone** and render
  neither the table nor the cards; the table's `#empty` slot is not used.
- **Pagination:** `UiPagination v-if="total > customersPageSize"` `:page="filters.page"`
  `@update:page="(p) => update({ page: p })"`, labels from `shell.pagination.*`.
- **Create drawer:** `<CustomerFormDrawer v-model:open="createOpen" mode="create" @created="(c) => navigateTo(`/agent/customers/${c.id}`)" />`.

File: `app/layouts/agent.vue` — in `agentNav` (lines 100–110) insert after Home:
`...(can(Permissions.CustomersRead) ? [{ to: '/agent/customers', label: t('customers.nav.customers') }] : [])`.

### F6 — Components (`app/components/customer/`)

Every panel is a `<section :aria-labelledby="headingId" class="rounded-md border border-border bg-surface p-4">`
with an `h2` (`text-base font-semibold`) and its own states: **loading** → `UiSkeleton` (header `:lines="4"`, lists
`:lines="3"`); **error** → `UiErrorState :title="t('customers.detail.loadError')" :retry-label="t('customers.detail.retry')"
@retry="refresh()"` (forbidden → `errors.code.Organisation.OutOfScope` as description, no retry); **empty** →
`UiEmptyState :heading-level="3"` with its action. Buttons that write are `v-if="canWrite"`.

#### F6a — `CustomerHeaderPanel.vue`

Props: `customerId: string`. `const customer = useCustomer(props.customerId)`, `const { data, status, error, refresh } = customer.load('profile')`.
- `notFound` (`problemOf(error)?.type === ProblemTypes.NotFound`) → `UiEmptyState` `customers.detail.notFound` /
  `notFoundDescription` with a `NuxtLink` "Back to customers" action; forbidden and other errors as above.
- Data: `h1` (the page's only h1) with `localizedName(displayName, locale)`; when both languages exist and differ, the
  other one below in `text-sm text-fg-muted` with `:lang`/`dir` of that language (`lang="ar" dir="rtl"` or
  `lang="en" dir="ltr"`); `<bdi dir="ltr">` number; type and status badges; a `<dl class="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">`
  of department (`localizedName` of the `scope.myScope?.departments` entry whose `departmentId` equals
  `customerDepartmentId`, plus its branch via `organisation.scope.option`; `—` when not found), legal name (only when
  Organisation), preferred language (`customers.language.*`), created (`format.date`).
- Actions (`flex flex-wrap gap-2`): Edit → `editOpen = true` (renders `<CustomerFormDrawer mode="edit" :customer="data"
  @saved="customer.publish" @conflict="customer.reload()" />`); Deactivate (status Active) → confirm `UiModal` (`deactivateTitle`/`Description`,
  buttons `cancel` + danger `confirm`) → `useCustomerCommands().deactivate(id, data.rowVersion ?? '')`; Reactivate
  (status Inactive) → no confirm. On `ok` → `customer.publish(result.data)` + success toast
  (`deactivated`/`reactivated`); on 409 → `customer.reload()` (the default toast already showed); other failures
  are toasted by `useApi`.

#### F6b — `CustomerFormDrawer.vue` (create and edit)

Props: `mode: 'create' | 'edit'`, `customer?: CustomerDto`; `v-model:open`; emits `created: [CustomerDto]`,
`saved: [CustomerDto]`. `UiDrawer size="lg"` title `form.createTitle`/`form.editTitle`, close `form.close`.

- `form = reactive({ displayNameEn, displayNameAr, type: 'Individual', legalName, preferredLanguage: 'ar' | 'en'
  (default: current UI locale), departmentId: '', contact: { name, jobTitle, email, phone, hasPortalAccess: false } })`.
  `watch(open, (value) => { if (value) reset() })` — reset fills from `customer` in edit mode, clears in create mode,
  clears `formErrors`, hides the duplicate warning.
- Fields: `UiInput` displayNameEn (`dir="ltr" lang="en"`), displayNameAr (`dir="rtl" lang="ar"`), both with hint
  `fields.nameHint`; `UiSelect` type (`customers.type.*`); `UiInput` legalName `v-if="form.type === 'Organisation'"`
  `required`; `UiSelect` preferredLanguage (`customers.language.*`). Create only: `UiSelect` department when
  `showDepartment` (Product rules: `scope.myScope?.activeDepartmentId` is null/undefined **and**
  `scope.activeDepartments.length > 1`; options via `organisation.scope.option`); a `fieldset` "Primary contact"
  (`legend` `form.primaryContact`, hint `form.primaryContactHint`) with name, job title, email (`type="email"
  dir="ltr"`), phone (`type="tel" dir="ltr"`, hint `contacts.fields.phoneHint`), `UiCheckbox` hasPortalAccess.
- Errors: `:error="formErrors.errorFor('displayNameEn')"` etc.; contact fields read `contacts[0].name`,
  `contacts[0].email`, `contacts[0].phone`, `contacts[0].jobTitle`, `contacts[0].hasPortalAccess`; department reads
  `departmentId`. `formErrors.formError` renders as `<p role="alert" class="text-sm text-danger-600">`.
- Body builders: `legalName: form.type === 'Organisation' ? form.legalName.trim() || null : null`; blank names →
  `null`; `contacts: hasContact ? [{ name, jobTitle: blank → null, email: blank → null, phone: blank → null,
  isPrimary: true, hasPortalAccess }] : []`; `departmentId: showDepartment && form.departmentId !== '' ? form.departmentId : null`.
  Edit sends `UpdateCustomerRequest` with `rowVersion: props.customer?.rowVersion ?? ''` read **at submit time**.
- `submit(confirmDuplicate = false)`: guard `submitting`; `formErrors.clear()`; create →
  `commands.create({ ...body, confirmDuplicate })`. `ok && data.created && data.customer` → toast `form.created`,
  close, `emit('created', data.customer)`; `ok && !data.created` → `duplicates.value = data.duplicateCandidates ?? []`
  (the warning shows). Edit `ok` → toast `form.saved`, close, `emit('saved', data)`. Failures: the F1b order with
  `createFieldForCode` (create) or `profileFieldForCode` (edit). On 409 (edit only): `commands.toast(problem)` and
  `emit('conflict')`; the drawer stays open with the agent's input; `CustomerHeaderPanel` handles `@conflict` with
  `customer.reload()`, so the `customer` prop (and its `rowVersion`) is fresh for the next submit. Emits therefore
  are `created: [CustomerDto]`, `saved: [CustomerDto]`, `conflict: []`.
- **Duplicate warning** (`v-if="duplicates.length > 0"`, `role="alert"`, `rounded-md border border-warning-500 bg-warning-50 p-3 text-warning-700`):
  title + description; a list of candidates — `NuxtLink :to="`/agent/customers/${c.id}`" target="_blank"` with
  `localizedName`, `<bdi dir="ltr">` number, status badge, `t('customers.duplicate.matched', { values: [...matchedEmails, ...matchedPhones].join('، ' in ar / ', ' in en) })`
  with the values wrapped by `isolateLtr` (`app/utils/format.ts` line 15). Footer while shown: `duplicate.back`
  (hides the warning) and `duplicate.createAnyway` → `submit(true)`. `watch(form, () => { duplicates.value = [] }, { deep: true })`.
- Footer otherwise: `form.cancel` (close) and primary `form.create` / `form.save` with `:loading="submitting"`;
  a hidden submit button inside the form as on the users page (line 104) so Enter submits.

#### F6c — `CustomerContactsPanel.vue` and `CustomerContactModal.vue`

`CustomerContactsPanel.vue` — props `customerId`; `useCustomer(id).load('contacts')`. Heading `contacts.title`
with an "Add contact" button (`v-if="canWrite && contacts.length > 0"`). List `<ul class="divide-y divide-border">`:
each contact — name (+ "Primary" `UiBadge tone="primary"` and "Portal access" `UiBadge tone="info"` when set),
job title, email as `<a :href="`mailto:${email}`"><bdi dir="ltr">`, phone as `<a :href="`tel:${phone}`"><bdi dir="ltr">`;
actions (`flex flex-wrap gap-1`, `role="group" :aria-label="t('contacts.actionsFor', { name })"`): Edit, Make
primary (`v-if="!isPrimary"`), Remove. Empty → `contacts.empty` / `emptyDescription` + "Add contact" action.
Make primary → `setPrimaryContact(id, contactId, rowVersion)`; Remove → confirm `UiModal` (`removeTitle`,
`removeDescription` with `{ name }`) → `removeContact(...)`; `ok` → `publish` + toast (`primarySet`/`removed`);
409 → `reload()`. A 422 `Customers.PrimaryContactRemoval` arrives as the default toast with the localized text.
The panel renders one `<CustomerContactModal v-model:open="modalOpen" :customer="data" :contact="editing"
@saved="customer.publish" @conflict="customer.reload()" />` (`editing` is `undefined` for add).

`CustomerContactModal.vue` — props `customer: CustomerDto`, `contact?: ContactDto`; `v-model:open`; emits
`saved: [CustomerDto]`, `conflict: []`. `UiModal` title `contacts.addTitle`/`editTitle`. Fields name (required),
job title, email (`type="email" dir="ltr"`), phone (`type="tel" dir="ltr"`, hint `phoneHint`), `UiCheckbox`
hasPortalAccess, and in **add** mode `UiCheckbox` isPrimary — checked and disabled with hint `isPrimaryFirst` when
the customer has no contacts. Submit: add → `AddContactRequest` / edit → `UpdateContactRequest`, `rowVersion:
customer.rowVersion ?? ''` at submit time, blank optionals → `null`; failures in the F1b order with
`contactFieldForCode` (409 → `api.toast` + `emit('conflict')`); `ok` → emit `saved`, close, toast `contacts.saved`.

#### F6d — `CustomerNotesPanel.vue`

Props `customerId`; `useCustomer(id).load('notes')`. When `canWrite`: a `<form>` with `UiTextarea v-model="body"
:rows="3" maxlength="4000" :label="t('notes.body')" :hint="t('notes.bodyHint')" :error="noteErrors.errorFor('body')"`
and `notes.add` submit (disabled while blank or submitting) → `addNote(id, body.trim())`; `ok` → `publish`, clear the
textarea, toast `notes.added`; failures → F1b order with `noteFieldForCode`. List (server order: pinned first, then
newest): each note `whitespace-pre-line break-words` body, a "Pinned" badge, `format.dateTime(createdAtUtc)`, author
(`authorId === auth.me?.id` → `notes.byYou` else `notes.byColleague`), Pin/Unpin (`pinNote(id, noteId, !isPinned)`),
Remove (confirm modal → `removeNote`). `ok` → `publish`. Empty → `notes.empty` / `emptyDescription` with the action
"Add note" that focuses the textarea (`useTemplateRef` on the `UiTextarea` root → `querySelector('textarea')?.focus()`),
shown only when `canWrite`.

#### F6e — `CustomerTicketsPanel.vue`

Static: section + heading `tickets.title` + `UiEmptyState :heading-level="3" :title="t('customers.tickets.empty')"
:description="t('customers.tickets.emptyDescription')"`. A comment names story 20 as the owner of its content.

#### F6f — `CustomerPicker.vue`

Props: `label: string`, `hint?: string`, `error?: string`, `disabled?: boolean`, `required?: boolean`.
`const model = defineModel<CustomerLookupDto | null>({ default: null })`. State: `term`, `open`, `results`,
`state: 'idle' | 'loading' | 'ready' | 'error'`, `activeIndex` (-1). Ids from `useId()`: input, listbox, hint, error,
`optionId(i) = `${listboxId}-${i}``.

```ts
const lookup = useCustomerLookup()
let controller: AbortController | null = null
let sequence = 0

async function run(): Promise<void> {
  const value = term.value.trim()
  controller?.abort()
  if (value === '') { controller = null; results.value = []; state.value = 'idle'; open.value = false; return }
  const current = new AbortController()
  controller = current
  const mine = ++sequence
  state.value = 'loading'
  open.value = true
  const result = await lookup.search(value, current.signal)
  if (mine !== sequence || current.signal.aborted) return // a newer keystroke owns the list
  if (!result.ok) { state.value = 'error'; results.value = []; activeIndex.value = -1; return }
  results.value = result.data
  state.value = 'ready'
  activeIndex.value = result.data.length > 0 ? 0 : -1
}
const debounced = debounce(() => void run(), 250)
onScopeDispose(() => { debounced.cancel(); controller?.abort() })
```

- Input: `role="combobox"`, `aria-autocomplete="list"`, `:aria-expanded="open"`, `:aria-controls="listboxId"`,
  `:aria-activedescendant="open && activeIndex >= 0 ? optionId(activeIndex) : undefined"`, `aria-invalid`/
  `aria-describedby` as `Input.vue`, `autocomplete="off"`, placeholder `picker.placeholder`. `@input`: if `model`
  is set, set it to `null` (typing replaces the selection); `debounced.run()`. `@keydown`: `ArrowDown` (open when
  results exist, else `run()`; move to next, wrapping), `ArrowUp` (previous, wrapping), `Enter` (when open and an
  option is active: `preventDefault`, `select`), `Escape` (open → close; closed with a selection → clear it and
  `term = ''`), `Tab` (close, no selection). `@blur` → close. When `model` is set, the input shows
  `${localizedName(displayName, locale)} · ${number}` and a ghost `UiButton size="sm"` `picker.clear` (clears and focuses
  the input) sits at `end-1` inside the field.
- Popup `<ul role="listbox" :id="listboxId" :aria-label="label" v-show="open" class="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-md border border-border bg-surface shadow-lg">`
  inside a `relative` wrapper. Options: `li role="option" :id :aria-selected="i === activeIndex"`, `@mousedown.prevent="select(item)"`,
  active option `bg-surface-raised`; content name + `<bdi dir="ltr">` number, second line primary email/phone in
  `<bdi dir="ltr">`. Non-option rows (`role="presentation"`, not focusable): loading (`UiSpinner size="sm"` +
  `picker.searching`), empty (`picker.empty`), error (`picker.error` + a `UiButton size="sm" variant="secondary"`
  `@mousedown.prevent="run()"` `picker.retry`).
- Live region: `<p class="sr-only" aria-live="polite">{{ announcement }}</p>` — loading → `picker.searching`; ready →
  `picker.results` with `format.number(results.length)` or `picker.empty`; error → `picker.error`.
- `select(item)`: `model.value = item`, `term.value = label`, `open = false`, `activeIndex = -1`.
- Keep the hint/error paragraphs identical to `Input.vue` lines 19–20.

File: `app/pages/_dev/api.vue` — add `<section>` "4. CustomerPicker (live lookup)" after panel 3 (after line 103):
`<CustomerPicker v-model="pickedCustomer" label="Customer" hint="Active customers only" />`, a second instance
with `error="Choose a customer"`, and a `<pre class="text-xs">` of `pickedCustomer`. Script:
`const pickedCustomer = ref<CustomerLookupDto | null>(null)` (type from `~/composables/customers/types`).

### F7 — Customer detail page

Create file: `app/pages/agent/customers/[id].vue`

```ts
definePageMeta({ layout: 'agent', key: (route) => route.path })
const route = useRoute()
const id = String(route.params.id)
const valid = isGuid(id)
```

Template: a `NuxtLink to="/agent/customers" class="text-sm text-primary-600 underline"` with the text
`customers.detail.back` (no arrow glyph — nothing to mirror). When `!valid` → `UiEmptyState` `detail.notFound` with the back action. Else:

```vue
<div class="mt-4 flex flex-col gap-4">
  <CustomerHeaderPanel :customer-id="id" />
  <div class="grid gap-4 md:grid-cols-2">
    <CustomerContactsPanel :customer-id="id" />
    <CustomerNotesPanel :customer-id="id" />
  </div>
  <CustomerTicketsPanel />
</div>
```

`useHead({ title })` with the customer's localized name once the profile panel has data is **not** added (no other
page sets titles). Each panel owns its own `useCustomer(id)`; the three share keys, so `publish`/`reload` from any
panel reach all of them.

### F8 — Docs

File: `frontend/README.md`, section "The review surface: `/_dev/ui` and `/_dev/api"` (lines 144–156) — add one
sentence to the `/_dev/api` bullet: panel 4 exercises `CustomerPicker` against the live lookup (sign in first).

---

## Edge Cases & Failure Modes

- **Fast typing** → each keystroke restarts the 300 ms debounce; each URL change re-runs the `agent-customers` fetch;
  Nuxt aborts the superseded call (`asyncData.js` line 328) and ignores its result (line 353); the aborted
  `useApi.call` returns a network problem that is thrown and swallowed as an `AbortError`-cancelled promise — **no
  toast** because the search composable passes `toastOnError: false`. (R3.)
- **Picker stale response** → `mine !== sequence` drops it (F6f); the previous request is aborted first.
- **Back/forward** → `filters` recompute from the URL, the table refetches, and the search box re-syncs (F5).
- **Hand-edited URL** (`?status=foo&page=-3&branch=x`) → `parseCustomerFilters` drops the bad values; the API never
  sees them (no 400 state from a typo).
- **Filter outside the caller's scope** (a pasted `department` uuid) → the API returns an empty page (Story 11 rule),
  shown as "No customers match these filters".
- **Scope switch** (`ShellScopeSwitcher`) → `scope.setActive` → `refreshNuxtData()` refetches the list and every
  panel; a customer now outside scope → each panel shows the forbidden state (403 `Organisation.OutOfScope`).
- **Unknown id / non-uuid id** → header `notFound` empty state with "Back to customers"; non-uuid never calls the API
  (F7 `isGuid`).
- **Concurrent edit** (another agent changed the customer) → 409 → toast `Persistence.ConcurrencyConflict` →
  `reload()` → the next attempt sends the fresh `rowVersion`; the open form keeps what the agent typed.
- **Contact command then profile edit** without reload → must not 409 (R4, `publish`).
- **Two agents adding notes** never conflict (notes take no rowVersion, Story 11 rules).
- **Organisation without legal name** → 422 `Customers.LegalNameRequired` → `applyCode` puts the localized text under
  Legal name. **Individual with legal name** cannot be sent (the field is hidden and nulled).
- **Malformed email / phone on create** → 422 `EmailInvalid`/`PhoneInvalid` → under `contacts[0].email`/`phone`.
- **Duplicate** → 200 `created: false` → warning, nothing written; "Create anyway" → `confirmDuplicate: true`; a
  malformed value is 422 before the duplicate check (Story 11), so the warning never hides a field error.
- **Ambiguous department on create** (scope "all", several departments, none chosen) → 400 `Organisation.DepartmentRequired`
  with field `departmentId` → `applyCode` replaces the English server text with the localized one.
- **Remove the primary while others exist** → 422 `Customers.PrimaryContactRemoval` → default toast, localized.
- **Deactivate twice** (stale page) → 422 `Customers.AlreadyInactive` → toast; the panel keeps its data.
- **Offline / API down** → each panel's own error state (R8); commands toast `errors.problem.network`.
- **Long names, long notes, Arabic in an English field** → `break-words`, `min-w-0` on flex children, `truncate` only
  in table cells; notes keep line breaks (`whitespace-pre-line`).
- **Mixed-direction identifiers** in Arabic → `<bdi dir="ltr">`; matched values in the duplicate message via `isolateLtr`.
- **No `customers.write`** → no write control renders (R7); a direct API call would still get 403.
- **No `customers.read`** → no nav item; a direct visit gets the 403 forbidden state.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add a test framework,
`*.spec.ts`, `*.test.ts`, or `tests/` folder.** The type checker, the linter (physical-direction ban, bare-string
ban, locale parity), `/_dev/ui`, `/_dev/api`, and the manual walk below replace the suite. Record each row's
result, and which R2/R3/R4/R5 path shipped, in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Types / lint / build | `pnpm --dir frontend typecheck`, `lint`, `build` | exit 0 each |
| 2 | No physical utilities in the diff | `pnpm lint` (the `vue/no-restricted-class` + `no-restricted-syntax` rules) and the grep in Verification step 3 | lint clean; grep prints nothing |
| 3 | Risks R1–R8 | as stated in Prerequisites | each proven or its fallback recorded |
| 4 | List paging/sort/filter server-side (AC) | agent A: sort Name/Number/Created, filter status/type, page with a 1-row page (`?page=2` after creating 20+ customers, or check the request query in Network) | every change is a new `/api/Agent/Customers?…` request; no client filtering; URL mirrors state; reload keeps it |
| 5 | Debounce + cancel (AC) | R3 procedure | one request per pause; cancelled rows; results match the final term |
| 6 | Panel failure isolation (AC) | R8 procedure | each panel fails and retries on its own |
| 7 | 422 on the field in the locale (AC) | `ar`: New customer → type منشأة, no legal name → submit; then a contact email `not-an-email` | Arabic text under Legal name, then under the contact email |
| 8 | Duplicate warning (AC) | New customer with contact email `ahmed@example.com` | warning lists Ahmed Al-Qahtani (C-000001) with the matched email; nothing created (list unchanged); "Create anyway" creates and opens it |
| 9 | Contacts | Najd Logistics: add (primary on), edit, make primary, remove non-primary, try removing primary | one primary at a time; toasts; PrimaryContactRemoval toast localized |
| 10 | Notes | add, pin, unpin, remove | pinned first; "You" as author |
| 11 | Status | deactivate (confirm), reactivate | badge flips; picker no longer finds it while inactive |
| 12 | Picker (AC) | `/_dev/api` panel 4, keyboard only + NVDA/Narrator | Tab in, type, ArrowDown/Up, Enter selects, Esc closes/clears, results announced, error row + retry offline |
| 13 | Permission hiding | R7 | write controls vanish/return |
| 14 | States (AC) | per panel: skeleton (Slow 3G), empty (a new customer with no contacts/notes), error (R8) | present everywhere; empty states carry their action |
| 15 | Responsive + RTL (AC) | 375 / 768 / 1280 in `en` and `ar`, list and detail, drawers and modals | cards below 768, table from 768; panels stack; no horizontal scroll; mirrored layout in `ar` |
| 16 | `/_dev/ui` | Checkbox and sortable Table sections, both themes, 375 | every state renders |
| 17 | Regression | `/admin/users` list and detail, `/agent/account`, `/_dev/api` panels 1–3 | unchanged behaviour (Table without `v-model:sort`, `useFormErrors.apply`) |

---

## Verification Steps

1. **Setup:** R1. `git status` clean apart from this story. Confirm no drift: with the API running,
   `pnpm --dir frontend gen:api` must leave `openapi/*.json` and `app/api/generated/*.ts` **unchanged**
   (`git diff --stat frontend/openapi frontend/app/api/generated` empty); if it changes them, commit the regenerated
   files with this story and re-read the Customers schemas before F4.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` (Development, :5187, seeded). No backend file
   changes in this story — `git diff --stat backend` must stay empty.
3. **Frontend gates:** `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`, `pnpm --dir frontend build`
   (Test Plan rows 1–2). Then, from the repo root (Git Bash, new and modified files staged with `git add -N` first):

   ```bash
   git diff -- frontend/app | grep -nE '(^|[[:space:]":])-?(ml|mr|pl|pr|left|right|text-left|text-right|border-l|border-r|rounded-l|rounded-r)-' 
   ```

   must print nothing (inspect any hit — e.g. an `inset-x-0` is fine, a `left-2` is not).
4. **Frontend runs:** `pnpm --dir frontend dev`; sign in as `agent.a@crm.local` / `Crm_Dev_2026!`
   (`backend/requests/auth.http` lines 5–24) → prove R2–R8 in order → Test Plan rows 4–16; then sign in as
   `supervisor@crm.local` (two departments) with scope "All my departments" → New customer shows the Department
   select; submit without it → the field error in the active locale; `agent.b@crm.local` sees only Red Sea Trading.
5. **Regression:** Test Plan row 17.

---

## Done Criteria

- [ ] The list pages, sorts, and filters server-side (every change is a new request; no client-side filtering); the
      URL holds `q/status/type/branch/department/sort/dir/page`.
- [ ] Search is debounced (300 ms), cancels in-flight requests, and never renders results from a stale keystroke.
- [ ] A failing header, contacts, or notes panel shows its own error state with retry; the rest of the page stays usable.
- [ ] A 422 domain error lands on the specific field in the active locale (`applyCode` + the `errors.code.Customers.*` keys).
- [ ] The duplicate warning appears before anything is created and offers "Create anyway".
- [ ] `CustomerPicker` searches asynchronously and is keyboard and screen-reader operable (combobox + listbox,
      `aria-activedescendant`, live region).
- [ ] Every data panel has a loading skeleton, an empty state with a next action, and an error state with retry
      (Tickets placeholder: documented exception).
- [ ] Write actions are hidden without `customers.write`.
- [ ] Walked in `en` and `ar` at 375, 768, and 1280 px; no physical-direction utility in the diff.
- [ ] `UiCheckbox` and the sortable `UiTable` state have `/_dev/ui` sections; `CustomerPicker` is on `/_dev/api`.
- [ ] `pnpm typecheck` / `lint` / `build` clean; no backend change; no new npm package.
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `.squad/plans/customer-management/00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 15.**
