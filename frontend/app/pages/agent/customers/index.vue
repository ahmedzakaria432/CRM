<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('customers.list.title') }}</h1>
      <UiButton v-if="canWrite" @click="createOpen = true">{{ t('customers.list.create') }}</UiButton>
    </div>

    <form role="search" class="mt-4" @submit.prevent="searchNow()">
      <div class="w-full max-w-md">
        <UiInput
          v-model="searchInput"
          type="search"
          maxlength="200"
          :label="t('customers.list.search')"
          :hint="t('customers.list.searchHint')"
        />
      </div>
    </form>

    <fieldset class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <legend class="sr-only">{{ t('customers.filters.label') }}</legend>
      <UiSelect v-model="statusFilter" :label="t('customers.filters.status')" :options="statusOptions" />
      <UiSelect v-model="typeFilter" :label="t('customers.filters.type')" :options="typeOptions" />
      <UiSelect v-model="branchFilter" :label="t('customers.filters.branch')" :options="branchOptions" />
      <UiSelect v-model="departmentFilter" :label="t('customers.filters.department')" :options="departmentOptions" />
      <div class="md:hidden">
        <UiSelect v-model="sortChoice" :label="t('customers.filters.sort')" :options="sortOptions" />
      </div>
    </fieldset>
    <div v-if="hasFilters" class="mt-2">
      <UiButton variant="ghost" size="sm" @click="clearFilters()">{{ t('customers.list.clearFilters') }}</UiButton>
    </div>

    <p class="mt-3 text-sm text-fg-muted" aria-live="polite">
      <template v-if="status === 'success'">{{ t('customers.list.results', { count: format.number(total) }) }}</template>
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
      :title="t('customers.list.loadError')"
      :retry-label="t('customers.list.retry')"
      @retry="refresh()"
    />
    <UiEmptyState
      v-else-if="status === 'success' && rows.length === 0"
      class="mt-4"
      :title="hasFilters ? t('customers.list.empty') : t('customers.list.emptyNone')"
      :description="hasFilters ? t('customers.list.emptyDescription') : t('customers.list.emptyNoneDescription')"
    >
      <template v-if="hasFilters || canWrite" #actions>
        <UiButton v-if="hasFilters" variant="secondary" @click="clearFilters()">
          {{ t('customers.list.clearFilters') }}
        </UiButton>
        <UiButton v-else @click="createOpen = true">{{ t('customers.list.create') }}</UiButton>
      </template>
    </UiEmptyState>
    <template v-else>
      <UiTable
        v-model:sort="tableSort"
        class="mt-4 hidden md:block"
        :caption="t('customers.list.caption')"
        :columns="columns"
        :rows="rows"
        :row-key="(row) => row.id ?? ''"
        :loading="status === 'pending'"
      >
        <template #cell-name="{ row }">
          <NuxtLink :to="`/agent/customers/${row.id}`" class="font-medium text-primary-600 underline">
            {{ localizedName(row.displayName, locale) }}
          </NuxtLink>
        </template>
        <template #cell-number="{ row }"><bdi dir="ltr" class="font-mono">{{ row.number }}</bdi></template>
        <template #cell-type="{ row }">
          <UiBadge>{{ typeLabel(row.type) }}</UiBadge>
        </template>
        <template #cell-status="{ row }">
          <UiBadge :tone="statusTone(row.status)">{{ statusLabel(row.status) }}</UiBadge>
        </template>
        <template #cell-primaryContact="{ row }">
          <div v-if="row.primaryContactName" class="min-w-0">
            <div class="truncate"><bdi>{{ row.primaryContactName }}</bdi></div>
            <div v-if="row.primaryEmail || row.primaryPhone" class="truncate text-xs text-fg-muted">
              <bdi dir="ltr">{{ row.primaryEmail || row.primaryPhone }}</bdi>
            </div>
          </div>
        </template>
        <template #cell-createdAt="{ row }">{{ row.createdAtUtc ? format.date(row.createdAtUtc) : '' }}</template>
      </UiTable>

      <ul class="mt-4 flex flex-col gap-2 md:hidden">
        <template v-if="status === 'pending'">
          <li v-for="n in 3" :key="n" class="rounded-md border border-border p-3">
            <UiSkeleton :lines="3" />
          </li>
        </template>
        <template v-else>
          <li v-for="row in rows" :key="row.id">
            <NuxtLink
              :to="`/agent/customers/${row.id}`"
              :aria-label="t('customers.list.open', { name: localizedName(row.displayName, locale) })"
              class="block rounded-md border border-border p-3 hover:bg-surface-raised focus-visible:focus-ring"
            >
              <div class="flex items-start justify-between gap-2">
                <span class="min-w-0 break-words font-medium text-fg">{{ localizedName(row.displayName, locale) }}</span>
                <bdi dir="ltr" class="shrink-0 font-mono text-xs text-fg-muted">{{ row.number }}</bdi>
              </div>
              <div class="mt-2 flex flex-wrap gap-2">
                <UiBadge size="sm">{{ typeLabel(row.type) }}</UiBadge>
                <UiBadge size="sm" :tone="statusTone(row.status)">{{ statusLabel(row.status) }}</UiBadge>
              </div>
              <div v-if="row.primaryContactName" class="mt-2 min-w-0 text-sm text-fg-muted">
                <bdi class="break-words">{{ row.primaryContactName }}</bdi>
                <bdi v-if="row.primaryEmail || row.primaryPhone" dir="ltr" class="block break-all text-xs">
                  {{ row.primaryEmail || row.primaryPhone }}
                </bdi>
              </div>
            </NuxtLink>
          </li>
        </template>
      </ul>

      <UiPagination
        v-if="total > customersPageSize"
        class="mt-4"
        :page="filters.page"
        :page-size="customersPageSize"
        :total="total"
        :label="t('shell.pagination.label')"
        :previous-label="t('shell.pagination.previous')"
        :next-label="t('shell.pagination.next')"
        :page-label="t('shell.pagination.page')"
        @update:page="(p: number) => update({ page: p })"
      />
    </template>

    <CustomerFormDrawer v-model:open="createOpen" mode="create" @created="(c) => navigateTo(`/agent/customers/${c.id}`)" />
  </div>
</template>

<script setup lang="ts">
import { useCustomerSearch } from '~/composables/customers/useCustomerSearch'
import { ProblemTypes, problemOf } from '~/api/problem'
import { Permissions } from '~/utils/permissions'
import { localizedName } from '~/utils/scope'
import { debounce } from '~/utils/debounce'
import type { TableSort } from '~/utils/table'
import {
  customerSorts,
  customerStatuses,
  customerTypes,
  customersPageSize,
  statusTone,
  type CustomerSortKey,
  type CustomerStatusName,
  type CustomerTypeName,
} from '~/utils/customers'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const router = useRouter()
const { can } = useCan()
const format = useFormat()
const scope = useScopeStore()
const { filters, update, data, status, error, refresh } = useCustomerSearch()

const canWrite = computed(() => can(Permissions.CustomersWrite))
const createOpen = ref(false)

const rows = computed(() => data.value?.items ?? [])
const total = computed(() => data.value?.total ?? 0)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => problemOf(error.value)?.detail ?? undefined)

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
const hasFilters = computed(() => {
  const f = filters.value
  return f.q !== '' || f.status !== '' || f.type !== '' || f.branchId !== '' || f.departmentId !== ''
})

function clearFilters(): void {
  searchInput.value = ''
  lastSent = ''
  debounced.cancel()
  void router.push({ query: {} })
}

function isStatus(value: string): value is CustomerStatusName {
  return customerStatuses.some((candidate) => candidate === value)
}
function isType(value: string): value is CustomerTypeName {
  return customerTypes.some((candidate) => candidate === value)
}

const statusFilter = computed<string>({
  get: () => filters.value.status,
  set: (value) => void update({ status: isStatus(value) ? value : '' }),
})
const typeFilter = computed<string>({
  get: () => filters.value.type,
  set: (value) => void update({ type: isType(value) ? value : '' }),
})

const allOption = computed(() => ({ value: '', label: t('customers.filters.all') }))
const statusOptions = computed(() => [
  allOption.value,
  ...customerStatuses.map((value) => ({ value, label: t(`customers.status.${value}`) })),
])
const typeOptions = computed(() => [
  allOption.value,
  ...customerTypes.map((value) => ({ value, label: t(`customers.type.${value}`) })),
])

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

// --- sort ---
function isSortKey(value: string): value is CustomerSortKey {
  return customerSorts.some((candidate) => candidate === value)
}

function sortFor(key: string): CustomerSortKey {
  if (key === 'name') return locale.value === 'ar' ? 'nameAr' : 'nameEn'
  return isSortKey(key) ? key : 'createdAt'
}

const tableSort = computed<TableSort | undefined>({
  get: () => {
    const f = filters.value
    const key = f.sort === 'nameEn' || f.sort === 'nameAr' ? 'name' : f.sort
    return { key, descending: f.descending }
  },
  set: (value) => {
    if (value === undefined) return
    const previous = filters.value.sort
    const sort = sortFor(value.key)
    // A new column starts ascending, except Created, which starts newest first.
    const descending = sort === 'createdAt' && previous !== 'createdAt' ? true : value.descending
    void update({ sort, descending })
  },
})

const sortChoices = ['createdAt:desc', 'createdAt:asc', 'number:asc', 'name:asc', 'name:desc'] as const
const sortOptions = computed(() => [
  { value: 'createdAt:desc', label: t('customers.sort.newest') },
  { value: 'createdAt:asc', label: t('customers.sort.oldest') },
  { value: 'number:asc', label: t('customers.sort.number') },
  { value: 'name:asc', label: t('customers.sort.nameAsc') },
  { value: 'name:desc', label: t('customers.sort.nameDesc') },
])
const sortChoice = computed<string>({
  get: () => {
    const f = filters.value
    const key = f.sort === 'nameEn' || f.sort === 'nameAr' ? 'name' : f.sort
    return `${key}:${f.descending ? 'desc' : 'asc'}`
  },
  set: (value) => {
    const choice = sortChoices.find((candidate) => candidate === value)
    if (choice === undefined) return
    const [key, direction] = choice.split(':')
    if (key === undefined) return
    void update({ sort: sortFor(key), descending: direction === 'desc' })
  },
})

const columns = computed(() => [
  { key: 'name', label: t('customers.columns.name'), sortable: true },
  { key: 'number', label: t('customers.columns.number'), sortable: true },
  { key: 'type', label: t('customers.columns.type') },
  { key: 'status', label: t('customers.columns.status') },
  { key: 'primaryContact', label: t('customers.columns.primaryContact') },
  { key: 'createdAt', label: t('customers.columns.createdAt'), sortable: true, align: 'end' as const },
])

function typeLabel(type: string | null | undefined): string {
  return type !== null && type !== undefined && isType(type) ? t(`customers.type.${type}`) : ''
}
function statusLabel(value: string | null | undefined): string {
  return value !== null && value !== undefined && isStatus(value) ? t(`customers.status.${value}`) : ''
}
</script>
