<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('tickets.inbox.title') }}</h1>
      <UiButton v-if="canWrite" @click="createOpen = true">{{ t('tickets.inbox.create') }}</UiButton>
    </div>

    <nav :aria-label="t('tickets.views.label')" class="mt-4 flex flex-wrap gap-2">
      <NuxtLink
        v-for="view in ticketViews"
        :key="view"
        :to="{ query: ticketFiltersToQuery({ ...filters, view, page: 1 }) }"
        :aria-current="filters.view === view ? 'page' : undefined"
        class="inline-flex items-center gap-2 rounded-full border px-3 py-1 text-sm focus-visible:focus-ring"
        :class="
          filters.view === view
            ? 'border-primary-600 bg-primary-50 text-primary-700'
            : 'border-border text-fg hover:bg-surface-raised'
        "
      >
        {{ t(`tickets.views.${view}`) }}
        <UiBadge v-if="counts !== undefined" size="sm">{{ format.number(counts[view] ?? 0) }}</UiBadge>
      </NuxtLink>
    </nav>

    <form role="search" class="mt-4" @submit.prevent="searchNow()">
      <div class="w-full max-w-md">
        <UiInput
          v-model="searchInput"
          type="search"
          maxlength="200"
          :label="t('tickets.inbox.search')"
          :hint="t('tickets.inbox.searchHint')"
        />
      </div>
    </form>

    <UiButton
      class="mt-4 lg:hidden"
      variant="secondary"
      aria-controls="ticket-filters"
      :aria-expanded="filtersOpen"
      @click="filtersOpen = !filtersOpen"
    >
      {{ t('tickets.filters.toggle', { count: format.number(activeFilterCount) }) }}
    </UiButton>
    <fieldset
      id="ticket-filters"
      class="mt-4 gap-3 sm:grid-cols-2 lg:grid lg:grid-cols-4"
      :class="filtersOpen ? 'grid' : 'hidden'"
    >
      <legend class="sr-only">{{ t('tickets.filters.label') }}</legend>
      <UiSelect v-model="statusFilter" :label="t('tickets.filters.status')" :options="statusOptions" />
      <UiSelect v-model="priorityFilter" :label="t('tickets.filters.priority')" :options="priorityOptions" />
      <UiSelect v-model="categoryFilter" :label="t('tickets.filters.category')" :options="categoryOptions" />
      <UiSelect
        v-if="filters.view === 'all'"
        v-model="assigneeFilter"
        :label="t('tickets.filters.assignee')"
        :options="assigneeOptions"
      />
      <UiSelect v-model="branchFilter" :label="t('tickets.filters.branch')" :options="branchOptions" />
      <UiSelect v-model="departmentFilter" :label="t('tickets.filters.department')" :options="departmentOptions" />
      <CustomerPicker v-model="customerModel" :label="t('tickets.filters.customer')" />
      <div class="lg:hidden">
        <UiSelect v-model="sortChoice" :label="t('tickets.filters.sort')" :options="sortOptions" />
      </div>
    </fieldset>
    <div v-if="hasFilters" class="mt-2">
      <UiButton variant="ghost" size="sm" @click="clearFilters()">{{ t('tickets.inbox.clearFilters') }}</UiButton>
    </div>

    <p class="mt-3 text-sm text-fg-muted" aria-live="polite">
      <template v-if="status === 'success'">{{ t('tickets.inbox.results', { count: format.number(total) }) }}</template>
    </p>

    <UiErrorState
      v-if="forbidden"
      class="mt-4"
      :title="t('errors.problem.forbidden')"
      :description="forbiddenDetail"
    />
    <UiErrorState
      v-else-if="error && status !== 'pending'"
      class="mt-4"
      :title="t('tickets.inbox.loadError')"
      :retry-label="t('tickets.inbox.retry')"
      @retry="refresh()"
    />
    <UiEmptyState
      v-else-if="status === 'success' && rows.length === 0"
      class="mt-4"
      :title="emptyTitle"
      :description="emptyDescription"
    >
      <template v-if="hasFilters || filters.view === 'mine' || canWrite" #actions>
        <UiButton v-if="hasFilters" variant="secondary" @click="clearFilters()">
          {{ t('tickets.inbox.clearFilters') }}
        </UiButton>
        <UiButton v-else-if="filters.view === 'mine'" variant="secondary" @click="update({ view: 'unassigned' })">
          {{ t('tickets.inbox.showUnassigned') }}
        </UiButton>
        <UiButton v-else-if="canWrite" @click="createOpen = true">{{ t('tickets.inbox.create') }}</UiButton>
      </template>
    </UiEmptyState>
    <template v-else>
      <UiTable
        v-model:sort="tableSort"
        class="mt-4 hidden lg:block"
        :caption="t('tickets.inbox.caption')"
        :columns="columns"
        :rows="rows"
        :row-key="(row) => row.id ?? ''"
        :loading="status === 'pending'"
      >
        <template #cell-number="{ row }">
          <NuxtLink :to="`/agent/tickets/${row.id}`" class="text-primary-600 underline">
            <bdi dir="ltr" class="font-mono">{{ row.number }}</bdi>
          </NuxtLink>
        </template>
        <template #cell-subject="{ row }">
          <div class="min-w-0">
            <NuxtLink :to="`/agent/tickets/${row.id}`" class="break-words font-medium text-primary-600 underline">
              {{ row.subject }}
            </NuxtLink>
            <div class="mt-1 text-xs text-fg-muted">
              <template v-if="row.customer">
                {{ customerName(row.customer) }} <bdi dir="ltr">{{ row.customer.number }}</bdi>
              </template>
              <template v-else>{{ t('tickets.inbox.unknownCustomer') }}</template>
            </div>
          </div>
        </template>
        <template #cell-status="{ row }">
          <UiBadge size="sm" :tone="ticketStatusTone(row.status)">{{ statusLabel(row.status) }}</UiBadge>
        </template>
        <template #cell-priority="{ row }">
          <UiBadge size="sm" :tone="ticketPriorityTone(row.priority)">{{ priorityLabel(row.priority) }}</UiBadge>
        </template>
        <template #cell-assignee="{ row }">
          <bdi v-if="row.assignedAgentName">{{ row.assignedAgentName }}</bdi>
          <span v-else class="text-fg-muted">{{ t('tickets.inbox.unassigned') }}</span>
        </template>
        <template #cell-modifiedAt="{ row }">{{ row.modifiedAtUtc ? format.dateTime(row.modifiedAtUtc) : '' }}</template>
        <template #cell-createdAt="{ row }">{{ row.createdAtUtc ? format.date(row.createdAtUtc) : '' }}</template>
      </UiTable>

      <ul class="mt-4 flex flex-col gap-2 lg:hidden">
        <template v-if="status === 'pending'">
          <li v-for="n in 3" :key="n" class="rounded-md border border-border p-3">
            <UiSkeleton :lines="3" />
          </li>
        </template>
        <template v-else>
          <li v-for="row in rows" :key="row.id">
            <NuxtLink
              :to="`/agent/tickets/${row.id}`"
              :aria-label="t('tickets.inbox.open', { number: row.number })"
              class="block rounded-md border border-border p-3 hover:bg-surface-raised focus-visible:focus-ring"
            >
              <div class="flex items-start justify-between gap-2 text-xs text-fg-muted">
                <bdi dir="ltr" class="font-mono">{{ row.number }}</bdi>
                <span>{{ row.createdAtUtc ? format.date(row.createdAtUtc) : '' }}</span>
              </div>
              <div class="mt-1 break-words font-medium text-fg">{{ row.subject }}</div>
              <div class="mt-1 text-sm text-fg-muted">
                <template v-if="row.customer">
                  {{ customerName(row.customer) }} <bdi dir="ltr">{{ row.customer.number }}</bdi>
                </template>
                <template v-else>{{ t('tickets.inbox.unknownCustomer') }}</template>
              </div>
              <div class="mt-2 flex flex-wrap items-center gap-2">
                <UiBadge size="sm" :tone="ticketStatusTone(row.status)">{{ statusLabel(row.status) }}</UiBadge>
                <UiBadge size="sm" :tone="ticketPriorityTone(row.priority)">{{ priorityLabel(row.priority) }}</UiBadge>
                <bdi v-if="row.assignedAgentName" class="text-xs text-fg-muted">{{ row.assignedAgentName }}</bdi>
                <span v-else class="text-xs text-fg-muted">{{ t('tickets.inbox.unassigned') }}</span>
              </div>
            </NuxtLink>
          </li>
        </template>
      </ul>

      <UiPagination
        v-if="total > ticketsPageSize"
        class="mt-4"
        :page="filters.page"
        :page-size="ticketsPageSize"
        :total="total"
        :label="t('shell.pagination.label')"
        :previous-label="t('shell.pagination.previous')"
        :next-label="t('shell.pagination.next')"
        :page-label="t('shell.pagination.page')"
        @update:page="(p: number) => update({ page: p })"
      />
    </template>

    <TicketCreateDrawer v-model:open="createOpen" @created="(ticket) => navigateTo(`/agent/tickets/${ticket.id}`)" />
  </div>
</template>

<script setup lang="ts">
import { useTicketSearch } from '~/composables/tickets/useTicketSearch'
import { useTicketQueues } from '~/composables/tickets/useTicketQueues'
import { useTicketAgents } from '~/composables/tickets/useTicketAgents'
import { useTicketCategoryOptions } from '~/composables/tickets/useTicketCategoryOptions'
import { customerLookupOf, useCustomerLookup } from '~/composables/customers/useCustomerLookup'
import type { CustomerLookupDto } from '~/composables/customers/types'
import type { TicketCustomerRefDto } from '~/composables/tickets/types'
import { ProblemTypes, problemOf } from '~/api/problem'
import { Permissions } from '~/utils/permissions'
import { localizedName } from '~/utils/scope'
import { debounce } from '~/utils/debounce'
import type { TableSort } from '~/utils/table'
import {
  ticketFiltersToQuery,
  ticketPriorities,
  ticketPriorityTone,
  ticketSorts,
  ticketStatuses,
  ticketStatusTone,
  ticketViews,
  ticketsPageSize,
  type TicketPriorityName,
  type TicketSortKey,
  type TicketStatusName,
} from '~/utils/tickets'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const router = useRouter()
const { can } = useCan()
const format = useFormat()
const scope = useScopeStore()
const auth = useAuthStore()
const lookup = useCustomerLookup()
const { filters, update, data, status, error, refresh } = useTicketSearch()
const { data: queueCounts } = useTicketQueues()

const canWrite = computed(() => can(Permissions.TicketsWrite))
const createOpen = ref(false)
const filtersOpen = ref(false)

const rows = computed(() => data.value?.items ?? [])
const total = computed(() => data.value?.total ?? 0)
const counts = computed(() => queueCounts.value ?? undefined)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => problemOf(error.value)?.detail ?? undefined)

const agents = useTicketAgents(
  'agent-ticket-agents-inbox',
  computed(() => undefined),
  computed(() => filters.value.view === 'all'),
)
const categories = useTicketCategoryOptions(
  'agent-ticket-categories-inbox',
  computed(() => filters.value.departmentId || undefined),
)

function customerName(customer: TicketCustomerRefDto): string {
  return localizedName({ ar: customer.nameAr, en: customer.nameEn }, locale.value)
}

// --- search: debounced, written to the URL with replace (no history entry per keystroke) ---
const searchInput = ref(filters.value.q)
// The last term this page wrote to the URL: the URL echo of our own (older) write must not overwrite newer typing.
let lastSent = filters.value.q
function sendSearch(value: string): void {
  lastSent = value.trim()
  void update({ q: lastSent }, 'replace')
}
const debounced = debounce(sendSearch, 300)
watch(searchInput, (value) => debounced.run(value))
onScopeDispose(() => debounced.cancel())
// Back/forward: the URL is the state, so the box follows it.
watch(
  () => filters.value.q,
  (q) => {
    if (q === lastSent) return
    lastSent = q
    searchInput.value = q
  },
)

function searchNow(): void {
  debounced.cancel()
  sendSearch(searchInput.value)
}

// --- filters ---
const activeFilterCount = computed(() => {
  const f = filters.value
  return [f.q, f.status, f.priority, f.categoryId, f.assigneeId, f.departmentId, f.branchId, f.customerId].filter(
    (value) => value !== '',
  ).length
})
const hasFilters = computed(() => activeFilterCount.value > 0)

function clearFilters(): void {
  searchInput.value = ''
  lastSent = ''
  debounced.cancel()
  const view = filters.value.view
  void router.push({ query: view === 'mine' ? {} : { view } })
}

function isStatus(value: string): value is TicketStatusName {
  return ticketStatuses.some((candidate) => candidate === value)
}
function isPriority(value: string): value is TicketPriorityName {
  return ticketPriorities.some((candidate) => candidate === value)
}

const statusFilter = computed<string>({
  get: () => filters.value.status,
  set: (value) => void update({ status: isStatus(value) ? value : '' }),
})
const priorityFilter = computed<string>({
  get: () => filters.value.priority,
  set: (value) => void update({ priority: isPriority(value) ? value : '' }),
})
const categoryFilter = computed<string>({
  get: () => filters.value.categoryId,
  set: (categoryId) => void update({ categoryId }),
})
const assigneeFilter = computed<string>({
  get: () => filters.value.assigneeId,
  set: (assigneeId) => void update({ assigneeId }),
})

const allOption = computed(() => ({ value: '', label: t('tickets.filters.all') }))
const statusOptions = computed(() => [
  {
    value: '',
    label: filters.value.view === 'all' ? t('tickets.filters.statusAny') : t('tickets.filters.statusWorking'),
  },
  ...ticketStatuses.map((value) => ({ value, label: t(`tickets.status.${value}`) })),
])
const priorityOptions = computed(() => [
  allOption.value,
  ...ticketPriorities.map((value) => ({ value, label: t(`tickets.priority.${value}`) })),
])
const categoryOptions = computed(() => [
  allOption.value,
  ...(categories.data.value ?? []).flatMap((category) =>
    category.id === undefined ? [] : [{ value: category.id, label: localizedName(category.name, locale.value) }],
  ),
])
const assigneeOptions = computed(() => {
  const me = auth.me?.id
  return [
    { value: '', label: t('tickets.filters.anyone') },
    ...(me === undefined ? [] : [{ value: me, label: t('tickets.filters.me') }]),
    ...(agents.data.value ?? []).flatMap((agent) =>
      agent.id === undefined || agent.id === me ? [] : [{ value: agent.id, label: agent.displayName ?? '' }],
    ),
  ]
})

const myDepartments = computed(() => scope.myScope?.departments ?? [])
const branchOptions = computed(() => {
  const seen = new Set<string>()
  const options = [allOption.value]
  for (const department of myDepartments.value) {
    const branchId = department.branchId
    if (branchId === undefined || seen.has(branchId)) continue
    seen.add(branchId)
    options.push({ value: branchId, label: localizedName(department.branchName, locale.value) })
  }
  return options
})
const departmentOptions = computed(() => [
  allOption.value,
  ...myDepartments.value
    .filter((department) => filters.value.branchId === '' || department.branchId === filters.value.branchId)
    .flatMap((department) =>
      department.departmentId === undefined
        ? []
        : [{ value: department.departmentId, label: localizedName(department.departmentName, locale.value) }],
    ),
])

const branchFilter = computed<string>({
  get: () => filters.value.branchId,
  set: (branchId) => {
    const current = myDepartments.value.find((department) => department.departmentId === filters.value.departmentId)
    const keep = branchId === '' || current === undefined || current.branchId === branchId
    void update({ branchId, ...(keep ? {} : { departmentId: '' }) })
  },
})
const departmentFilter = computed<string>({
  get: () => filters.value.departmentId,
  set: (departmentId) => void update({ departmentId }),
})

// --- customer filter (D13): the picker model follows the URL id; typing in the picker clears it ---
const customerModel = ref<CustomerLookupDto | null>(null)
watch(
  () => filters.value.customerId,
  async (id) => {
    if (id === '') {
      customerModel.value = null
      return
    }
    if (customerModel.value?.id === id) return
    const result = await lookup.get(id)
    if (result.ok && filters.value.customerId === id) customerModel.value = customerLookupOf(result.data)
  },
  { immediate: true },
)
watch(customerModel, (value) => {
  const id = value?.id ?? ''
  if (id !== filters.value.customerId && (value !== null || filters.value.customerId !== '')) void update({ customerId: id })
})

// --- sort (D4) ---
function isSortKey(value: string): value is TicketSortKey {
  return ticketSorts.some((candidate) => candidate === value)
}

const tableSort = computed<TableSort | undefined>({
  get: () => ({ key: filters.value.sort, descending: filters.value.descending }),
  set: (value) => {
    if (value === undefined || !isSortKey(value.key)) return
    // A newly chosen column starts newest / most urgent first, except Number (ascending).
    const descending = value.key !== filters.value.sort ? value.key !== 'number' : value.descending
    void update({ sort: value.key, descending })
  },
})

const sortChoices = ['createdAt:desc', 'createdAt:asc', 'modifiedAt:desc', 'priority:desc', 'number:asc'] as const
const sortOptions = computed(() => [
  { value: 'createdAt:desc', label: t('tickets.sort.newest') },
  { value: 'createdAt:asc', label: t('tickets.sort.oldest') },
  { value: 'modifiedAt:desc', label: t('tickets.sort.updated') },
  { value: 'priority:desc', label: t('tickets.sort.priority') },
  { value: 'number:asc', label: t('tickets.sort.number') },
])
const sortChoice = computed<string>({
  get: () => `${filters.value.sort}:${filters.value.descending ? 'desc' : 'asc'}`,
  set: (value) => {
    const choice = sortChoices.find((candidate) => candidate === value)
    if (choice === undefined) return
    const [key, direction] = choice.split(':')
    if (key === undefined || !isSortKey(key)) return
    void update({ sort: key, descending: direction === 'desc' })
  },
})

const columns = computed(() => [
  { key: 'number', label: t('tickets.columns.number'), sortable: true },
  { key: 'subject', label: t('tickets.columns.subject') },
  { key: 'status', label: t('tickets.columns.status') },
  { key: 'priority', label: t('tickets.columns.priority'), sortable: true },
  { key: 'assignee', label: t('tickets.columns.assignee') },
  { key: 'modifiedAt', label: t('tickets.columns.modifiedAt'), sortable: true },
  { key: 'createdAt', label: t('tickets.columns.createdAt'), sortable: true, align: 'end' as const },
])

function statusLabel(value: string | null | undefined): string {
  return value !== null && value !== undefined && isStatus(value) ? t(`tickets.status.${value}`) : ''
}
function priorityLabel(value: string | null | undefined): string {
  return value !== null && value !== undefined && isPriority(value) ? t(`tickets.priority.${value}`) : ''
}

// --- empty states ---
const emptyTitle = computed(() => {
  if (hasFilters.value) return t('tickets.inbox.empty')
  switch (filters.value.view) {
    case 'mine': return t('tickets.inbox.emptyMine')
    case 'unassigned': return t('tickets.inbox.emptyUnassigned')
    default: return t('tickets.inbox.emptyAll')
  }
})
const emptyDescription = computed(() => {
  if (hasFilters.value) return t('tickets.inbox.emptyDescription')
  switch (filters.value.view) {
    case 'mine': return t('tickets.inbox.emptyMineDescription')
    case 'unassigned': return t('tickets.inbox.emptyUnassignedDescription')
    default: return t('tickets.inbox.emptyAllDescription')
  }
})
</script>
