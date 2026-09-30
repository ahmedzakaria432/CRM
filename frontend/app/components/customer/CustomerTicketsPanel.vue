<template>
  <section :aria-labelledby="headingId" class="rounded-md border border-border bg-surface p-4">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('customers.tickets.title') }}</h2>
      <UiButton v-if="canWrite && items.length > 0" size="sm" @click="createOpen = true">
        {{ t('customers.tickets.create') }}
      </UiButton>
    </div>

    <div class="mt-3">
      <UiSkeleton v-if="status === 'pending' && !data" :lines="3" />
      <UiErrorState
        v-else-if="error && !data"
        :title="t('customers.tickets.loadError')"
        :retry-label="t('customers.tickets.retry')"
        @retry="refresh()"
      />
      <UiEmptyState
        v-else-if="items.length === 0"
        :heading-level="3"
        :title="t('customers.tickets.empty')"
        :description="t('customers.tickets.emptyDescription')"
      >
        <template v-if="canWrite" #actions>
          <UiButton @click="createOpen = true">{{ t('customers.tickets.create') }}</UiButton>
        </template>
      </UiEmptyState>
      <template v-else>
        <ul class="divide-y divide-border">
          <li v-for="row in items" :key="row.id">
            <NuxtLink :to="`/agent/tickets/${row.id}`" class="block py-2 hover:bg-surface-raised focus-visible:focus-ring">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <bdi dir="ltr" class="font-mono text-xs text-fg-muted">{{ row.number }}</bdi>
                <span class="text-xs text-fg-muted">{{ row.createdAtUtc ? format.date(row.createdAtUtc) : '' }}</span>
              </div>
              <div class="mt-1 break-words text-sm font-medium text-fg">{{ row.subject }}</div>
              <div class="mt-1 flex flex-wrap gap-2">
                <UiBadge size="sm" :tone="ticketStatusTone(row.status)">{{ label('status', row.status) }}</UiBadge>
                <UiBadge size="sm" :tone="ticketPriorityTone(row.priority)">{{ label('priority', row.priority) }}</UiBadge>
              </div>
            </NuxtLink>
          </li>
        </ul>
        <NuxtLink
          v-if="total > items.length"
          :to="{ path: '/agent/tickets', query: { view: 'all', customer: customerId } }"
          class="mt-3 inline-block text-sm text-primary-600 underline"
        >
          {{ t('customers.tickets.viewAll', { count: format.number(total) }) }}
        </NuxtLink>
      </template>
    </div>

    <TicketCreateDrawer v-model:open="createOpen" :customer="preselected" @created="(ticket) => navigateTo(`/agent/tickets/${ticket.id}`)" />
  </section>
</template>

<script setup lang="ts">
import { customerPanelKey } from '~/composables/customers/useCustomer'
import { customerLookupOf } from '~/composables/customers/useCustomerLookup'
import type { CustomerDto } from '~/composables/customers/types'
import { useCustomerTickets } from '~/composables/tickets/useCustomerTickets'
import { Permissions } from '~/utils/permissions'
import { ticketPriorityTone, ticketStatusTone } from '~/utils/tickets'

interface Props {
  customerId: string
}
const props = defineProps<Props>()

const { t, te } = useI18n()
const format = useFormat()
const { can } = useCan()
const headingId = useId()
const { data, status, error, refresh } = useCustomerTickets(props.customerId)

const canWrite = computed(() => can(Permissions.TicketsWrite))
const createOpen = ref(false)
const items = computed(() => data.value?.items ?? [])
const total = computed(() => data.value?.total ?? 0)

// Read-only: the header panel loads the profile; this panel never fetches it a second time.
const profile = useNuxtData<CustomerDto>(customerPanelKey(props.customerId, 'profile')).data
const preselected = computed(() => (profile.value ? customerLookupOf(profile.value) : null))

function label(kind: 'status' | 'priority', value: string | null | undefined): string {
  const key = `tickets.${kind}.${value ?? ''}`
  return te(key) ? t(key) : (value ?? '')
}
</script>
