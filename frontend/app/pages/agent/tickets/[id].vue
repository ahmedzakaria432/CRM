<template>
  <div>
    <NuxtLink to="/agent/tickets" class="text-sm text-primary-600 underline">{{ t('tickets.detail.backToTickets') }}</NuxtLink>

    <UiEmptyState
      v-if="!valid || notFound"
      class="mt-4"
      :title="t('tickets.detail.notFound')"
      :description="t('tickets.detail.notFoundDescription')"
    >
      <template #actions>
        <NuxtLink to="/agent/tickets" class="text-sm text-primary-600 underline">{{ t('tickets.detail.backToTickets') }}</NuxtLink>
      </template>
    </UiEmptyState>
    <div v-else class="mt-2 flex flex-col gap-4">
      <template v-if="status === 'pending' && !ticket">
        <UiSkeleton :lines="3" />
        <UiSkeleton :lines="6" />
      </template>
      <UiErrorState
        v-else-if="forbidden"
        :title="t('errors.problem.forbidden')"
        :description="t('errors.code.Organisation.OutOfScope')"
      />
      <UiErrorState
        v-else-if="error && !ticket"
        :title="t('tickets.detail.loadError')"
        :retry-label="t('tickets.thread.retry')"
        @retry="refresh()"
      />
      <template v-else-if="ticket">
        <TicketWorkspaceHeader />
        <div
          role="group"
          :aria-label="t('tickets.detail.sections')"
          class="flex gap-1 rounded-md border border-border p-1 lg:hidden"
        >
          <button
            v-for="item in regions"
            :key="item.id"
            type="button"
            :aria-pressed="region === item.id"
            class="flex-1 rounded-sm px-3 py-2 text-sm font-medium focus-visible:focus-ring"
            :class="region === item.id ? 'bg-primary-50 text-primary-700' : 'text-fg-muted hover:bg-surface-raised'"
            @click="region = item.id"
          >
            {{ item.label }}
          </button>
        </div>
        <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
          <div class="flex min-w-0 flex-col gap-4" :class="shown('conversation')">
            <TicketThread
              :ticket-id="id"
              :version="version"
              :customer-label="customerLabel ?? undefined"
              :contact-id="ticket.contactId ?? undefined"
            />
            <TicketComposer />
          </div>
          <div class="flex min-w-0 flex-col gap-4">
            <div :class="shown('details')"><TicketPropertiesPanel /></div>
            <div :class="shown('customer')">
              <TicketCustomerPanel v-if="ticket.customerId" :customer-id="ticket.customerId" :contact-id="ticket.contactId" />
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ProblemTypes, problemOf } from '~/api/problem'
import { isGuid } from '~/utils/customers'
import { ticketWorkspaceKey, useTicketWorkspace } from '~/composables/tickets/useTicketWorkspace'

definePageMeta({ layout: 'agent', key: (route) => route.path })

const { t } = useI18n()
const route = useRoute()
const id = String(route.params.id)
// An invalid id never reaches the API: the not-found state renders instead.
const valid = isGuid(id)
const workspace = useTicketWorkspace(id, valid)
provide(ticketWorkspaceKey, workspace)
const { ticket, status, error, refresh, version, customerLabel } = workspace

const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)

// Below 1024px the side panels become a three-way switch (D8); from 1024px everything shows.
type Region = 'conversation' | 'details' | 'customer'
const region = ref<Region>('conversation')
const regions = computed(() => [
  { id: 'conversation' as const, label: t('tickets.detail.conversation') },
  { id: 'details' as const, label: t('tickets.detail.details') },
  { id: 'customer' as const, label: t('tickets.detail.customer') },
])
const shown = (value: Region): string => (region.value === value ? '' : 'max-lg:hidden')
</script>
