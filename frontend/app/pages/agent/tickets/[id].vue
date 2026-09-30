<template>
  <div>
    <NuxtLink to="/agent" class="text-sm text-primary-600 underline">{{ t('tickets.detail.back') }}</NuxtLink>

    <UiEmptyState
      v-if="!valid || notFound"
      class="mt-4"
      :title="t('tickets.detail.notFound')"
      :description="t('tickets.detail.notFoundDescription')"
    />
    <div v-else class="mt-4 flex flex-col gap-4">
      <UiSkeleton v-if="status === 'pending' && !ticket" :lines="2" />
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
      <header v-else-if="ticket" class="flex flex-col gap-2">
        <div class="flex flex-wrap items-center gap-2">
          <bdi dir="ltr" class="text-sm font-medium text-fg-muted">{{ ticket.number }}</bdi>
          <UiBadge>{{ statusLabel }}</UiBadge>
        </div>
        <h1 class="break-words text-xl font-semibold text-fg">{{ ticket.subject }}</h1>
      </header>
      <TicketThread v-if="!forbidden" :ticket-id="id" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useTicket } from '~/composables/tickets/useTicket'
import { ProblemTypes, problemOf } from '~/api/problem'
import { isGuid } from '~/utils/customers'

// Story 20 replaces this page with the ticket workspace; TicketThread stays as its centre column.
definePageMeta({ layout: 'agent', key: (route) => route.path })

const { t, te } = useI18n()
const route = useRoute()
const id = String(route.params.id)
const valid = isGuid(id)

// An invalid id never reaches the API: the not-found state renders instead.
const { data: ticket, status, error, refresh } = useTicket(id).load(valid)

const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)
const statusLabel = computed(() => {
  const key = `tickets.status.${ticket.value?.status ?? ''}`
  return te(key) ? t(key) : (ticket.value?.status ?? '')
})
</script>
