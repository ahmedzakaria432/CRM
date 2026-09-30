<template>
  <section
    v-if="tabs.length > 0"
    :aria-labelledby="headingId"
    class="rounded-md border border-border bg-surface"
  >
    <h2 :id="headingId" class="sr-only">{{ t('tickets.composer.label') }}</h2>
    <UiTabs v-model="active" :tabs="tabs" :label="t('tickets.composer.label')">
      <template #panel="{ id }">
        <form
          class="flex flex-col gap-3 rounded-md p-2"
          :class="id === noteTab ? 'bg-warning-50' : ''"
          novalidate
          @submit.prevent="submit(id)"
        >
          <UiTextarea
            v-model="drafts[id]"
            :rows="4"
            maxlength="10000"
            :label="id === noteTab ? t('tickets.composer.noteLabel') : t('tickets.composer.replyLabel')"
            :hint="id === noteTab ? t('tickets.composer.noteHint') : t('tickets.composer.replyHint')"
            :error="formErrors.errorFor('body')"
          />
          <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
            {{ formErrors.formError.value }}
          </p>
          <div
            v-if="conflict"
            role="alert"
            class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-warning-500 bg-warning-50 p-2 text-sm text-warning-700"
          >
            <span>{{ t('tickets.composer.conflict') }}</span>
            <UiButton size="sm" variant="secondary" :loading="sending" @click="submit(id)">
              {{ t('tickets.composer.sendAgain') }}
            </UiButton>
          </div>
          <div class="flex justify-end">
            <UiButton type="submit" :loading="sending" :disabled="(drafts[id] ?? '').trim() === ''">
              {{ id === noteTab ? t('tickets.composer.addNote') : t('tickets.composer.sendReply') }}
            </UiButton>
          </div>
        </form>
      </template>
    </UiTabs>
  </section>
</template>

<script setup lang="ts">
import { useTicketThread } from '~/composables/tickets/useTicketThread'
import { useTicketWorkspaceContext } from '~/composables/tickets/useTicketWorkspace'
import { bodyFieldForCode } from '~/utils/tickets'

const replyTab = 'ticket-composer-reply'
const noteTab = 'ticket-composer-note'

const { t } = useI18n()
const workspace = useTicketWorkspaceContext()
const thread = useTicketThread(workspace.id)
const formErrors = useFormErrors()
const api = useApi()
const toasts = useToasts()
const headingId = useId()

const tabs = computed(() => [
  ...(workspace.offered.value.has('Reply') ? [{ id: replyTab, label: t('tickets.composer.reply') }] : []),
  ...(workspace.offered.value.has('AddNote') ? [{ id: noteTab, label: t('tickets.composer.note') }] : []),
])
const active = ref<string>()
watch(
  tabs,
  (list) => {
    if (!list.some((tab) => tab.id === active.value)) active.value = list[0]?.id
  },
  { immediate: true },
)
// Switching tabs drops the other tab's field errors.
watch(active, () => {
  formErrors.clear()
  conflict.value = false
})

// One draft per tab; a failed send keeps it.
const drafts = reactive<Record<string, string>>({ [replyTab]: '', [noteTab]: '' })
const sending = ref(false)
const conflict = ref(false)

async function submit(id: string): Promise<void> {
  if (sending.value) return
  const text = (drafts[id] ?? '').trim()
  if (text === '') return
  sending.value = true
  formErrors.clear()
  conflict.value = false
  const isReply = id === replyTab
  const result = isReply ? await thread.reply(text) : await thread.addNote(text)
  sending.value = false
  if (result.ok) {
    workspace.publish(result.data)
    drafts[id] = ''
    toasts.push({ tone: 'success', title: t(isReply ? 'tickets.composer.sent' : 'tickets.composer.noted') })
    return
  }
  const problem = result.problem
  if (problem.code === 'Persistence.ConcurrencyConflict') {
    conflict.value = true
    return
  }
  if (formErrors.apply(problem)) return
  if (formErrors.applyCode(problem, bodyFieldForCode)) {
    // The ticket may have changed under us: refresh so the tabs follow the real status.
    await workspace.refresh()
    return
  }
  api.toast(problem)
}
</script>
