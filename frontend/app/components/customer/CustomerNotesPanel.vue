<template>
  <section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('customers.notes.title') }}</h2>

    <form v-if="canWrite && data" ref="formRef" class="mt-3 flex flex-col gap-2" novalidate @submit.prevent="add()">
      <UiTextarea
        v-model="body"
        :rows="3"
        maxlength="4000"
        :label="t('customers.notes.body')"
        :hint="t('customers.notes.bodyHint')"
        :error="noteErrors.errorFor('body')"
      />
      <p v-if="noteErrors.formError.value" role="alert" class="text-sm text-danger-600">
        {{ noteErrors.formError.value }}
      </p>
      <div>
        <UiButton type="submit" :loading="adding" :disabled="body.trim() === ''">{{ t('customers.notes.add') }}</UiButton>
      </div>
    </form>

    <div class="mt-3">
      <UiSkeleton v-if="status === 'pending' && !data" :lines="3" />
      <UiErrorState
        v-else-if="forbidden"
        :title="t('errors.problem.forbidden')"
        :description="t('errors.code.Organisation.OutOfScope')"
      />
      <UiErrorState
        v-else-if="error && !data"
        :title="t('customers.detail.loadError')"
        :retry-label="t('customers.detail.retry')"
        @retry="refresh()"
      />
      <UiEmptyState
        v-else-if="data && notes.length === 0"
        :heading-level="3"
        :title="t('customers.notes.empty')"
        :description="t('customers.notes.emptyDescription')"
      >
        <template v-if="canWrite" #actions>
          <UiButton @click="focusBody()">{{ t('customers.notes.add') }}</UiButton>
        </template>
      </UiEmptyState>
      <ul v-else class="flex flex-col gap-3">
        <li v-for="note in notes" :key="note.id" class="rounded-md border border-border p-3">
          <p class="whitespace-pre-line break-words text-sm text-fg">{{ note.body }}</p>
          <div class="mt-2 flex flex-wrap items-center gap-2 text-xs text-fg-muted">
            <UiBadge v-if="note.isPinned" tone="warning" size="sm">{{ t('customers.notes.pinned') }}</UiBadge>
            <span>{{ note.createdAtUtc ? format.dateTime(note.createdAtUtc) : '' }}</span>
            <span>{{ note.authorId === auth.me?.id ? t('customers.notes.byYou') : t('customers.notes.byColleague') }}</span>
          </div>
          <div v-if="canWrite" class="mt-2 flex flex-wrap gap-1">
            <UiButton size="sm" variant="ghost" @click="pin(note)">
              {{ note.isPinned ? t('customers.notes.unpin') : t('customers.notes.pin') }}
            </UiButton>
            <UiButton size="sm" variant="ghost" @click="askRemove(note)">{{ t('customers.notes.remove') }}</UiButton>
          </div>
        </li>
      </ul>
    </div>

    <UiModal
      v-if="canWrite"
      v-model:open="removeOpen"
      :title="t('customers.notes.removeTitle')"
      :close-label="t('customers.notes.close')"
    >
      <p class="text-sm text-fg">{{ t('customers.notes.removeDescription') }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="removeOpen = false">{{ t('customers.notes.cancel') }}</UiButton>
        <UiButton variant="danger" :loading="busy" @click="confirmRemove()">{{ t('customers.notes.remove') }}</UiButton>
      </template>
    </UiModal>
  </section>
</template>

<script setup lang="ts">
import { useCustomer } from '~/composables/customers/useCustomer'
import { useCustomerCommands } from '~/composables/customers/useCustomerCommands'
import type { CustomerNoteDto } from '~/composables/customers/types'
import { ProblemTypes, problemOf } from '~/api/problem'
import { Permissions } from '~/utils/permissions'
import { noteFieldForCode } from '~/utils/customers'

interface Props {
  customerId: string
}
const props = defineProps<Props>()

const { t } = useI18n()
const { can } = useCan()
const format = useFormat()
const auth = useAuthStore()
const toasts = useToasts()
const commands = useCustomerCommands()
const noteErrors = useFormErrors()
const customer = useCustomer(props.customerId)
const { data, status, error, refresh } = customer.load('notes')
const headingId = useId()
const formRef = useTemplateRef<HTMLFormElement>('formRef')

const canWrite = computed(() => can(Permissions.CustomersWrite))
const notes = computed(() => data.value?.notes ?? [])
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)

const body = ref('')
const adding = ref(false)
const busy = ref(false)
const removeOpen = ref(false)
const removing = ref<CustomerNoteDto | undefined>(undefined)

function focusBody(): void {
  formRef.value?.querySelector('textarea')?.focus()
}

async function add(): Promise<void> {
  if (adding.value || body.value.trim() === '') return
  adding.value = true
  noteErrors.clear()
  const result = await commands.addNote(props.customerId, body.value.trim())
  adding.value = false
  if (result.ok) {
    customer.publish(result.data)
    body.value = ''
    toasts.push({ tone: 'success', title: t('customers.notes.added') })
    return
  }
  if (noteErrors.applyCode(result.problem, noteFieldForCode)) return
  if (noteErrors.apply(result.problem)) return
  commands.toast(result.problem)
}

async function pin(note: CustomerNoteDto): Promise<void> {
  if (note.id === undefined) return
  const result = await commands.pinNote(props.customerId, note.id, !note.isPinned)
  if (result.ok) customer.publish(result.data)
}

function askRemove(note: CustomerNoteDto): void {
  removing.value = note
  removeOpen.value = true
}

async function confirmRemove(): Promise<void> {
  const target = removing.value
  if (busy.value || target?.id === undefined) return
  busy.value = true
  const result = await commands.removeNote(props.customerId, target.id)
  busy.value = false
  removeOpen.value = false
  if (result.ok) {
    customer.publish(result.data)
    toasts.push({ tone: 'success', title: t('customers.notes.removed') })
  }
}
</script>
