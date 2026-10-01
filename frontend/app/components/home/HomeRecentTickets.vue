<template>
  <section aria-labelledby="home-recent-title">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <h2 id="home-recent-title" class="text-lg font-semibold text-fg">{{ t('home.agent.recent.title') }}</h2>
      <NuxtLink to="/agent/tickets?view=mine" class="text-sm text-primary-600 underline focus-visible:focus-ring">
        {{ t('home.agent.recent.viewAll') }}
      </NuxtLink>
    </div>
    <ul v-if="loading" class="mt-3 flex flex-col gap-2">
      <li v-for="n in 3" :key="n" class="rounded-md border border-border p-3">
        <UiSkeleton :lines="2" />
      </li>
    </ul>
    <UiErrorState
      v-else-if="error && !data"
      class="mt-3"
      :title="t('home.agent.recent.loadError')"
      :retry-label="t('home.agent.recent.retry')"
      @retry="refresh()"
    />
    <UiEmptyState
      v-else-if="(data ?? []).length === 0"
      class="mt-3"
      :heading-level="3"
      :title="t('home.agent.recent.empty')"
      :description="t('home.agent.recent.emptyDescription')"
    >
      <template #actions>
        <NuxtLink
          to="/agent/tickets?view=unassigned"
          class="inline-flex items-center justify-center rounded-md border border-border bg-surface-raised px-4 py-2.5 text-base font-medium text-fg hover:bg-surface-sunken focus-visible:focus-ring"
        >
          {{ t('home.agent.recent.emptyAction') }}
        </NuxtLink>
      </template>
    </UiEmptyState>
    <ul v-else class="mt-3 flex flex-col gap-2">
      <li v-for="row in data ?? []" :key="row.id">
        <NuxtLink
          :to="`/agent/tickets/${row.id}`"
          class="block rounded-md border border-border bg-surface p-3 hover:bg-surface-raised focus-visible:focus-ring"
        >
          <div class="flex items-start justify-between gap-2 text-xs text-fg-muted">
            <bdi dir="ltr" class="font-mono">{{ row.number }}</bdi>
            <span>{{ row.createdAtUtc ? format.date(row.createdAtUtc) : '' }}</span>
          </div>
          <div dir="auto" class="mt-1 break-words font-medium text-fg">{{ row.subject }}</div>
          <div class="mt-2 flex flex-wrap items-center gap-2">
            <UiBadge size="sm" :tone="ticketStatusTone(row.status)">{{ statusLabel(row.status) }}</UiBadge>
            <span class="text-sm text-fg-muted">
              <template v-if="row.customer">
                {{ customerName(row.customer) }} <bdi dir="ltr">{{ row.customer.number }}</bdi>
              </template>
              <template v-else>{{ t('tickets.inbox.unknownCustomer') }}</template>
            </span>
          </div>
        </NuxtLink>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { useMyRecentTickets } from '~/composables/tickets/useMyRecentTickets'
import type { TicketCustomerRefDto } from '~/composables/tickets/types'
import { localizedName } from '~/utils/scope'
import { ticketStatuses, ticketStatusTone, type TicketStatusName } from '~/utils/tickets'

const { t, locale } = useI18n()
const format = useFormat()
const { data, status, error, refresh } = useMyRecentTickets()

const loading = computed(() => (status.value === 'idle' || status.value === 'pending') && !data.value)

function isStatus(value: string): value is TicketStatusName {
  return ticketStatuses.some((candidate) => candidate === value)
}
function statusLabel(value: string | null | undefined): string {
  return value !== null && value !== undefined && isStatus(value) ? t(`tickets.status.${value}`) : ''
}
function customerName(customer: TicketCustomerRefDto): string {
  return localizedName({ ar: customer.nameAr, en: customer.nameEn }, locale.value)
}
</script>
