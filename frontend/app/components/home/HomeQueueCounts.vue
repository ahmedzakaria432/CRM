<template>
  <section aria-labelledby="home-queues-title">
    <h2 id="home-queues-title" class="text-lg font-semibold text-fg">{{ t('home.agent.queues.title') }}</h2>
    <div v-if="loading" class="mt-3 grid gap-3 sm:grid-cols-3">
      <UiSkeleton v-for="view in ticketViews" :key="view" variant="rect" class="h-24" />
    </div>
    <UiErrorState
      v-else-if="error && !data"
      class="mt-3"
      :title="t('home.agent.queues.loadError')"
      :retry-label="t('home.agent.queues.retry')"
      @retry="refresh()"
    />
    <template v-else>
      <ul class="mt-3 grid gap-3 sm:grid-cols-3">
        <li v-for="view in ticketViews" :key="view">
          <NuxtLink
            :to="`/agent/tickets?view=${view}`"
            class="flex h-full flex-col gap-1 rounded-lg border border-border bg-surface p-4 hover:bg-surface-raised focus-visible:focus-ring"
          >
            <span class="text-sm text-fg-muted">{{ t(`tickets.views.${view}`) }}</span>
            <span class="text-3xl font-bold text-fg">{{ format.number(data?.[view] ?? 0) }}</span>
          </NuxtLink>
        </li>
      </ul>
      <p v-if="allZero" class="mt-2 text-sm text-fg-muted">{{ t('home.agent.queues.empty') }}</p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { useTicketQueues } from '~/composables/tickets/useTicketQueues'
import { ticketViews } from '~/utils/tickets'

// Same key as the inbox tabs (useTicketQueues), so the cards cannot disagree with the tab badges (Story 59 D7).
const { t } = useI18n()
const format = useFormat()
const { data, status, error, refresh } = useTicketQueues()

const loading = computed(() => (status.value === 'idle' || status.value === 'pending') && !data.value)
const allZero = computed(() => data.value != null && ticketViews.every((view) => (data.value?.[view] ?? 0) === 0))
</script>
