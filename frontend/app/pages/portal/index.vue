<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('portal.tickets.title') }}</h1>
      <UiButton @click="goToNew()">{{ t('portal.tickets.newRequest') }}</UiButton>
    </div>

    <UiTabs class="mt-4" :model-value="filters.tab" :tabs="tabs" :label="t('portal.tickets.tabsLabel')" @update:model-value="changeTab">
      <template #panel="{ id }">
        <div v-if="id === filters.tab">
          <div class="grid gap-3 sm:grid-cols-2">
            <form role="search" @submit.prevent="searchNow()">
              <UiInput v-model="searchInput" type="search" maxlength="200" :label="t('portal.tickets.search')" />
            </form>
            <UiSelect v-model="statusFilter" :label="t('portal.tickets.statusFilter')" :options="statusOptions" />
          </div>

          <UiSkeleton v-if="loading" class="mt-4" :lines="6" />
          <UiErrorState
            v-else-if="error && !data"
            class="mt-4"
            :title="t('portal.tickets.loadError')"
            :retry-label="t('portal.tickets.retry')"
            @retry="refresh()"
          />
          <UiEmptyState
            v-else-if="rows.length === 0"
            class="mt-4"
            :title="hasFilters ? t('portal.tickets.emptyFiltered') : t('portal.tickets.empty')"
            :description="hasFilters ? undefined : t('portal.tickets.emptyDescription')"
          >
            <template v-if="!hasFilters" #actions>
              <UiButton @click="goToNew()">{{ t('portal.tickets.emptyAction') }}</UiButton>
            </template>
          </UiEmptyState>
          <template v-else>
            <ul class="mt-4 flex flex-col gap-3">
              <li v-for="row in rows" :key="row.id">
                <NuxtLink
                  :to="`/portal/tickets/${row.id}`"
                  class="block rounded-md border border-border bg-surface p-4 hover:bg-surface-raised focus-visible:focus-ring"
                >
                  <div class="flex flex-wrap items-center justify-between gap-2">
                    <span class="text-xs text-fg-muted">
                      {{ t('portal.tickets.number') }} <bdi dir="ltr" class="font-mono">{{ row.number }}</bdi>
                    </span>
                    <UiBadge :tone="portalStatusTone(row.status)" size="sm">{{ statusLabel(row.status) }}</UiBadge>
                  </div>
                  <p dir="auto" class="mt-2 break-words text-base font-semibold text-fg">{{ row.subject }}</p>
                  <p class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-fg-muted">
                    <span>{{ categoryName(row.category) }}</span>
                    <span v-if="row.updatedAtUtc">
                      {{ t('portal.tickets.updated', { when: format.dateTime(row.updatedAtUtc) }) }}
                    </span>
                  </p>
                </NuxtLink>
              </li>
            </ul>
            <UiPagination
              v-if="total > portalPageSize"
              class="mt-4"
              :page="filters.page"
              :page-size="portalPageSize"
              :total="total"
              :label="t('portal.tickets.pagination.label')"
              :previous-label="t('portal.tickets.pagination.previous')"
              :next-label="t('portal.tickets.pagination.next')"
              :page-label="t('portal.tickets.pagination.page')"
              @update:page="(page) => void update({ page }, 'push')"
            />
          </template>
        </div>
      </template>
    </UiTabs>
  </div>
</template>

<script setup lang="ts">
import type { PortalTicketCategoryDto } from '~/composables/portal/types'
import { usePortalTicketList } from '~/composables/portal/usePortalTicketList'
import { debounce } from '~/utils/debounce'
import { localizedName } from '~/utils/scope'
import {
  portalPageSize,
  portalStatusKey,
  portalStatusTone,
  portalTabs,
  statusesOfTab,
  type PortalStatusName,
  type PortalTab,
} from '~/utils/portal'

definePageMeta({ layout: 'portal' })

const { t, locale } = useI18n()
const format = useFormat()
const { filters, update, data, status, error, refresh } = usePortalTicketList()

const rows = computed(() => data.value?.items ?? [])
const total = computed(() => data.value?.total ?? 0)
// Data loads in the browser only, so the server render (and the first client render) is the skeleton: idle counts as pending.
const loading = computed(() => (status.value === 'idle' || status.value === 'pending') && !data.value)
const hasFilters = computed(() => filters.value.q !== '' || filters.value.status !== '')

const tabs = computed(() => portalTabs.map((tab) => ({ id: tab, label: t(`portal.tickets.tabs.${tab}`) })))

function isTab(value: string | undefined): value is PortalTab {
  return portalTabs.some((candidate) => candidate === value)
}
function changeTab(value: string | undefined): void {
  if (isTab(value) && value !== filters.value.tab) void update({ tab: value })
}

const statusOptions = computed(() => [
  { value: '', label: t('portal.tickets.allStatuses') },
  ...statusesOfTab(filters.value.tab).map((name) => ({ value: name, label: t(`portal.status.${name}`) })),
])
const statusFilter = computed<string>({
  get: () => filters.value.status,
  set: (value) => {
    const next: PortalStatusName | '' = statusesOfTab(filters.value.tab).find((name) => name === value) ?? ''
    void update({ status: next })
  },
})

function statusLabel(value: string | null | undefined): string {
  const key = portalStatusKey(value)
  return key === null ? (value ?? '') : t(key)
}

function categoryName(category: PortalTicketCategoryDto | undefined): string {
  return category == null
    ? t('portal.tickets.noCategory')
    : localizedName({ ar: category.nameAr, en: category.nameEn }, locale.value)
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

async function goToNew(): Promise<void> {
  await navigateTo('/portal/tickets/new')
}
</script>
