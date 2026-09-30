<template>
  <header v-if="ticket" class="sticky top-0 z-10 -mx-4 border-b border-border bg-surface px-4 py-3">
    <div class="flex flex-wrap items-center gap-2">
      <bdi dir="ltr" class="font-mono text-sm text-fg-muted">{{ ticket.number }}</bdi>
      <UiBadge :tone="ticketStatusTone(ticket.status)">{{ statusLabel }}</UiBadge>
      <UiBadge :tone="ticketPriorityTone(ticket.priority)">{{ priorityLabel }}</UiBadge>
      <span class="text-sm text-fg-muted">
        <template v-if="ticket.assignedAgentName">
          {{ t('tickets.detail.assignedTo', { name: ticket.assignedAgentName }) }}
        </template>
        <template v-else>{{ t('tickets.detail.unassigned') }}</template>
      </span>
    </div>
    <h1 class="mt-1 break-words text-xl font-semibold text-fg">{{ ticket.subject }}</h1>
    <div
      v-if="primaryActions.length > 0"
      role="group"
      :aria-label="t('tickets.actions.label')"
      class="mt-2 flex flex-wrap gap-2"
    >
      <UiButton
        v-for="action in primaryActions"
        :key="action"
        size="sm"
        :variant="action === 'Claim' ? 'primary' : 'secondary'"
        :disabled="workspace.busy.value !== null"
        :loading="workspace.busy.value === action"
        @click="perform(action)"
      >
        {{ t(`tickets.actions.${action}`) }}
      </UiButton>
    </div>
    <div
      v-if="workspace.actionError.value"
      role="alert"
      class="mt-2 flex items-start justify-between gap-2 rounded-md border border-danger-500 bg-danger-50 p-2 text-sm text-danger-700"
    >
      <span class="min-w-0 break-words">{{ workspace.actionError.value }}</span>
      <UiButton size="sm" variant="ghost" @click="workspace.actionError.value = null">
        {{ t('tickets.actions.dismiss') }}
      </UiButton>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useTicketCommands } from '~/composables/tickets/useTicketCommands'
import { useTicketWorkspaceContext } from '~/composables/tickets/useTicketWorkspace'
import { ticketPriorityTone, ticketStatusTone } from '~/utils/tickets'

const { t, te } = useI18n()
const workspace = useTicketWorkspaceContext()
const commands = useTicketCommands()
const ticket = workspace.ticket

const statusLabel = computed(() => {
  const key = `tickets.status.${ticket.value?.status ?? ''}`
  return te(key) ? t(key) : (ticket.value?.status ?? '')
})
const priorityLabel = computed(() => {
  const key = `tickets.priority.${ticket.value?.priority ?? ''}`
  return te(key) ? t(key) : (ticket.value?.priority ?? '')
})

// D9: Assign to me, Resolve and Reopen are the primary actions; everything else lives in the properties panel.
const primaryActions = computed(() =>
  (['Claim', 'Resolve', 'Reopen'] as const).filter((action) => workspace.offered.value.has(action)),
)

function perform(action: 'Claim' | 'Resolve' | 'Reopen'): void {
  const id = workspace.id
  // The row version is read at click time: the last command's response moved it.
  const rowVersion = ticket.value?.rowVersion ?? ''
  void workspace.run(action, () =>
    action === 'Claim' ? commands.claim(id) : commands.transition(id, action, rowVersion),
  )
}
</script>
