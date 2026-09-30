<template>
  <section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('tickets.properties.title') }}</h2>
    <p v-if="workspace.offered.value.size === 0" class="mt-2 text-sm text-fg-muted">
      {{ t('tickets.properties.readOnly') }}
    </p>

    <dl v-if="ticket" class="mt-3 flex flex-col gap-4">
      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.status') }}</dt>
        <dd class="mt-1 flex flex-col gap-2">
          <span>
            <UiBadge :tone="ticketStatusTone(ticket.status)">{{ statusText }}</UiBadge>
          </span>
          <div v-if="statusActions.length > 0">
            <p class="text-xs text-fg-muted">{{ t('tickets.properties.statusActions') }}</p>
            <div class="mt-1 flex flex-wrap gap-2">
              <UiButton
                v-for="action in statusActions"
                :key="action"
                size="sm"
                :variant="action === 'Cancel' ? 'ghost' : 'secondary'"
                :disabled="workspace.busy.value !== null"
                :loading="workspace.busy.value === action"
                @click="onStatusAction(action)"
              >
                {{ t(`tickets.actions.${action}`) }}
              </UiButton>
            </div>
          </div>
          <p v-else-if="workspace.offered.value.size > 0" class="text-xs text-fg-muted">
            {{ t('tickets.properties.noStatusActions') }}
          </p>
        </dd>
      </div>

      <div>
        <dt class="sr-only">{{ t('tickets.properties.priority') }}</dt>
        <dd>
          <UiSelect
            v-model="priorityModel"
            :label="t('tickets.properties.priority')"
            :options="priorityOptions"
            :disabled="!workspace.offered.value.has('ChangePriority') || workspace.busy.value !== null"
          />
        </dd>
      </div>

      <div>
        <dt class="sr-only">{{ t('tickets.properties.category') }}</dt>
        <dd>
          <UiSelect
            v-model="categoryModel"
            :label="t('tickets.properties.category')"
            :options="categoryOptions"
            :disabled="!workspace.offered.value.has('ChangeCategory') || workspace.busy.value !== null"
          />
          <p v-if="categories.error.value" class="mt-1 text-xs text-danger-600">
            {{ t('tickets.properties.optionsError') }}
            <button type="button" class="underline focus-visible:focus-ring" @click="categories.refresh()">
              {{ t('tickets.properties.retry') }}
            </button>
          </p>
        </dd>
      </div>

      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.assignee') }}</dt>
        <dd class="mt-1 flex flex-col gap-2">
          <span class="break-words text-sm text-fg">
            <bdi v-if="ticket.assignedAgentName">{{ ticket.assignedAgentName }}</bdi>
            <template v-else>{{ t('tickets.detail.unassigned') }}</template>
          </span>
          <div v-if="workspace.offered.value.has('Claim') || workspace.offered.value.has('Unassign')" class="flex flex-wrap gap-2">
            <UiButton
              v-if="workspace.offered.value.has('Claim')"
              size="sm"
              variant="secondary"
              :disabled="workspace.busy.value !== null"
              :loading="workspace.busy.value === 'Claim'"
              @click="claim()"
            >
              {{ t('tickets.actions.Claim') }}
            </UiButton>
            <UiButton
              v-if="workspace.offered.value.has('Unassign')"
              size="sm"
              variant="ghost"
              :disabled="workspace.busy.value !== null"
              :loading="workspace.busy.value === 'Unassign'"
              @click="unassign()"
            >
              {{ t('tickets.actions.Unassign') }}
            </UiButton>
          </div>
          <div v-if="workspace.offered.value.has('Assign')" class="flex flex-col gap-2">
            <UiSelect
              v-model="assignTarget"
              :label="t('tickets.properties.assignTo')"
              :options="agentOptions"
              :disabled="otherAgents.length === 0 || workspace.busy.value !== null"
              :hint="otherAgents.length === 0 && !agents.error.value ? t('tickets.properties.noAgents') : undefined"
            />
            <p v-if="agents.error.value" class="text-xs text-danger-600">
              {{ t('tickets.properties.optionsError') }}
              <button type="button" class="underline focus-visible:focus-ring" @click="agents.refresh()">
                {{ t('tickets.properties.retry') }}
              </button>
            </p>
            <div>
              <UiButton
                size="sm"
                variant="secondary"
                :disabled="assignTarget === '' || workspace.busy.value !== null"
                :loading="workspace.busy.value === 'Assign'"
                @click="assign()"
              >
                {{ t('tickets.actions.Assign') }}
              </UiButton>
            </div>
          </div>
        </dd>
      </div>

      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.department') }}</dt>
        <dd class="mt-1 flex flex-col gap-2">
          <span class="break-words text-sm text-fg">{{ departmentName }}</span>
          <div v-if="workspace.offered.value.has('Transfer')" class="flex flex-col gap-2">
            <UiSelect
              v-model="transferTarget"
              :label="t('tickets.properties.transferTo')"
              :options="transferOptions"
              :disabled="workspace.busy.value !== null"
            />
            <p v-if="targets.error.value" class="text-xs text-danger-600">
              {{ t('tickets.properties.optionsError') }}
              <button type="button" class="underline focus-visible:focus-ring" @click="targets.refresh()">
                {{ t('tickets.properties.retry') }}
              </button>
            </p>
            <div>
              <UiButton
                size="sm"
                variant="secondary"
                :disabled="transferTarget === '' || workspace.busy.value !== null"
                :loading="workspace.busy.value === 'Transfer'"
                @click="transferOpen = true"
              >
                {{ t('tickets.actions.Transfer') }}
              </UiButton>
            </div>
          </div>
        </dd>
      </div>

      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.channel') }}</dt>
        <dd class="mt-1 text-sm text-fg">{{ channelText }}</dd>
      </div>
      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.createdAt') }}</dt>
        <dd class="mt-1 text-sm text-fg">{{ ticket.createdAtUtc ? format.dateTime(ticket.createdAtUtc) : '—' }}</dd>
      </div>
      <div>
        <dt class="text-xs font-medium text-fg-muted">{{ t('tickets.properties.firstResponse') }}</dt>
        <dd class="mt-1 text-sm text-fg">
          {{ ticket.firstRespondedAtUtc ? format.dateTime(ticket.firstRespondedAtUtc) : '—' }}
        </dd>
      </div>
    </dl>

    <UiModal v-model:open="cancelOpen" size="sm" :title="t('tickets.confirm.cancelTitle')" :close-label="t('tickets.confirm.close')">
      <p class="text-sm text-fg-muted">{{ t('tickets.confirm.cancelDescription') }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="cancelOpen = false">{{ t('tickets.confirm.back') }}</UiButton>
        <UiButton variant="danger" @click="confirmCancel()">{{ t('tickets.actions.Cancel') }}</UiButton>
      </template>
    </UiModal>

    <UiModal v-model:open="transferOpen" size="sm" :title="t('tickets.confirm.transferTitle')" :close-label="t('tickets.confirm.close')">
      <p class="text-sm text-fg-muted">{{ t('tickets.confirm.transferDescription', { department: transferTargetName }) }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="transferOpen = false">{{ t('tickets.confirm.back') }}</UiButton>
        <UiButton @click="confirmTransfer()">{{ t('tickets.actions.Transfer') }}</UiButton>
      </template>
    </UiModal>
  </section>
</template>

<script setup lang="ts">
import { useTicketAgents } from '~/composables/tickets/useTicketAgents'
import { useTicketCategoryOptions } from '~/composables/tickets/useTicketCategoryOptions'
import { useTicketCommands } from '~/composables/tickets/useTicketCommands'
import { useTicketWorkspaceContext } from '~/composables/tickets/useTicketWorkspace'
import { useTransferTargets } from '~/composables/tickets/useTransferTargets'
import { localizedName } from '~/utils/scope'
import { ticketPriorities, ticketStatusActions, ticketStatusTone, type TicketStatusAction } from '~/utils/tickets'

const { t, te, locale } = useI18n()
const format = useFormat()
const scope = useScopeStore()
const toasts = useToasts()
const workspace = useTicketWorkspaceContext()
const commands = useTicketCommands()
const ticket = workspace.ticket
const id = workspace.id
const headingId = useId()

const departmentId = computed(() => ticket.value?.departmentId)
const categories = useTicketCategoryOptions(`agent-ticket-categories-${id}`, computed(() => departmentId.value))
const agents = useTicketAgents(
  `agent-ticket-agents-${id}`,
  computed(() => departmentId.value),
  computed(() => workspace.offered.value.has('Assign')),
)
const targets = useTransferTargets(
  `agent-ticket-transfer-targets-${id}`,
  computed(() => workspace.offered.value.has('Transfer')),
)

// The row version is read at click time: the last command's response moved it.
const rowVersion = (): string => ticket.value?.rowVersion ?? ''

const statusText = computed(() => {
  const key = `tickets.status.${ticket.value?.status ?? ''}`
  return te(key) ? t(key) : (ticket.value?.status ?? '')
})
const channelText = computed(() => {
  const key = `tickets.channel.${ticket.value?.channel ?? ''}`
  return te(key) ? t(key) : (ticket.value?.channel ?? '')
})

// --- status ---
const statusActions = computed(() => ticketStatusActions.filter((action) => workspace.offered.value.has(action)))
const cancelOpen = ref(false)

function onStatusAction(action: TicketStatusAction): void {
  if (action === 'Cancel') {
    cancelOpen.value = true
    return
  }
  void workspace.run(action, () => commands.transition(id, action, rowVersion()))
}
function confirmCancel(): void {
  cancelOpen.value = false
  void workspace.run('Cancel', () => commands.transition(id, 'Cancel', rowVersion()))
}

// --- priority ---
const priorityOptions = computed(() => ticketPriorities.map((value) => ({ value, label: t(`tickets.priority.${value}`) })))
const priorityModel = computed<string>({
  get: () => ticket.value?.priority ?? '',
  set: (value) => {
    if (value !== '' && value !== ticket.value?.priority) {
      void workspace.run('ChangePriority', () => commands.changePriority(id, value, rowVersion()))
    }
  },
})

// --- category (D10) ---
const categoryOptions = computed(() => {
  const current = ticket.value?.category
  const list = (categories.data.value ?? []).flatMap((category) =>
    category.id === undefined ? [] : [{ value: category.id, label: localizedName(category.name, locale.value) }],
  )
  const options: { value: string; label: string; disabled?: boolean }[] = []
  if (current === undefined) options.push({ value: '', label: t('tickets.properties.noCategory'), disabled: true })
  options.push(...list)
  if (current?.id !== undefined && !list.some((option) => option.value === current.id)) {
    options.push({
      value: current.id,
      label: t('tickets.properties.inactiveCategory', { name: localizedName(current.name, locale.value) }),
      disabled: true,
    })
  }
  return options
})
const categoryModel = computed<string>({
  get: () => ticket.value?.category?.id ?? '',
  set: (value) => {
    if (value !== '' && value !== ticket.value?.category?.id) {
      void workspace.run('ChangeCategory', () => commands.changeCategory(id, value, rowVersion()))
    }
  },
})

// --- assignee (D10) ---
const otherAgents = computed(() =>
  (agents.data.value ?? []).filter((agent) => agent.id !== undefined && agent.id !== ticket.value?.assignedAgentId),
)
const agentOptions = computed(() => [
  { value: '', label: t('tickets.properties.chooseAgent') },
  ...otherAgents.value.flatMap((agent) =>
    agent.id === undefined ? [] : [{ value: agent.id, label: agent.displayName ?? '' }],
  ),
])
const assignTarget = ref('')

function claim(): void {
  void workspace.run('Claim', () => commands.claim(id))
}
function unassign(): void {
  void workspace.run('Unassign', () => commands.unassign(id, rowVersion()))
}
async function assign(): Promise<void> {
  const agentId = assignTarget.value
  if (agentId === '') return
  const dto = await workspace.run('Assign', () => commands.assign(id, agentId, rowVersion()))
  if (dto !== null) assignTarget.value = ''
}

// --- department / transfer (D9) ---
const transferTarget = ref('')
const transferOpen = ref(false)

const departmentName = computed(() => {
  const target = (targets.data.value ?? []).find((department) => department.id === departmentId.value)
  if (target !== undefined) return localizedName({ ar: target.nameAr, en: target.nameEn }, locale.value)
  const own = (scope.myScope?.departments ?? []).find((department) => department.departmentId === departmentId.value)
  return own === undefined ? '—' : localizedName(own.departmentName, locale.value)
})
function labelOf(department: { nameAr?: string | null; nameEn?: string | null; code?: string | null }): string {
  return `${localizedName({ ar: department.nameAr, en: department.nameEn }, locale.value)} · ${department.code ?? ''}`
}
const transferOptions = computed(() => [
  { value: '', label: t('tickets.properties.chooseDepartment') },
  ...(targets.data.value ?? []).flatMap((department) =>
    department.id === undefined || department.id === departmentId.value
      ? []
      : [{ value: department.id, label: labelOf(department) }],
  ),
])
const transferTargetName = computed(() => {
  const target = (targets.data.value ?? []).find((department) => department.id === transferTarget.value)
  return target === undefined ? '' : localizedName({ ar: target.nameAr, en: target.nameEn }, locale.value)
})

async function confirmTransfer(): Promise<void> {
  transferOpen.value = false
  const target = transferTarget.value
  if (target === '') return
  const department = transferTargetName.value
  const dto = await workspace.run('Transfer', () => commands.transfer(id, target, rowVersion()))
  if (dto === null) return
  transferTarget.value = ''
  const mine = scope.myScope?.activeDepartmentId
    ? [scope.myScope.activeDepartmentId]
    : (scope.myScope?.departments ?? []).map((d) => d.departmentId)
  if (!mine.includes(dto.departmentId)) {
    toasts.push({ tone: 'success', title: t('tickets.detail.transferred', { department }) })
    await navigateTo('/agent/tickets')
  }
}
</script>
