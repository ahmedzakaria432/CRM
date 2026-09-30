<template>
  <section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('tickets.customerPanel.title') }}</h2>
    <div class="mt-3">
      <UiSkeleton v-if="status === 'pending' && !data" :lines="3" />
      <UiEmptyState
        v-else-if="notFound"
        :heading-level="3"
        :title="t('tickets.customerPanel.notFound')"
        :description="t('tickets.customerPanel.notFoundDescription')"
      />
      <UiErrorState
        v-else-if="forbidden"
        :title="t('errors.problem.forbidden')"
        :description="t('errors.code.Organisation.OutOfScope')"
      />
      <UiErrorState
        v-else-if="error && !data"
        :title="t('tickets.customerPanel.loadError')"
        :retry-label="t('tickets.customerPanel.retry')"
        @retry="refresh()"
      />
      <div v-else-if="data" class="flex flex-col gap-3">
        <div class="min-w-0">
          <p class="break-words font-medium text-fg">{{ localizedName(data.displayName, locale) }}</p>
          <bdi dir="ltr" class="font-mono text-xs text-fg-muted">{{ data.number }}</bdi>
        </div>
        <NuxtLink :to="`/agent/customers/${customerId}`" class="text-sm text-primary-600 underline">
          {{ t('tickets.customerPanel.open') }}
        </NuxtLink>
        <div class="min-w-0 border-t border-border pt-3">
          <p class="text-xs font-medium text-fg-muted">
            {{ contactId ? t('tickets.customerPanel.contact') : t('tickets.customerPanel.primaryContact') }}
          </p>
          <div v-if="contact" class="mt-1 flex flex-col gap-1 text-sm">
            <span class="break-words text-fg"><bdi>{{ contact.name }}</bdi></span>
            <a v-if="contact.email" :href="`mailto:${contact.email}`" class="break-all text-primary-600 underline">
              <bdi dir="ltr">{{ contact.email }}</bdi>
            </a>
            <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="text-primary-600 underline">
              <bdi dir="ltr">{{ contact.phone }}</bdi>
            </a>
          </div>
          <p v-else class="mt-1 text-sm text-fg-muted">{{ t('tickets.customerPanel.noContact') }}</p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useCustomer } from '~/composables/customers/useCustomer'
import { useTicketWorkspaceContext } from '~/composables/tickets/useTicketWorkspace'
import { ProblemTypes, problemOf } from '~/api/problem'
import { localizedName } from '~/utils/scope'

interface Props {
  customerId: string
  contactId?: string | null
}
const props = withDefaults(defineProps<Props>(), { contactId: null })

const { t, locale } = useI18n()
const workspace = useTicketWorkspaceContext()
const headingId = useId()
const { data, status, error, refresh } = useCustomer(props.customerId).load('profile')

const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)

// The ticket's own contact, else the customer's primary contact.
const contact = computed(() => {
  const contacts = data.value?.contacts ?? []
  if (props.contactId) return contacts.find((candidate) => candidate.id === props.contactId)
  return contacts.find((candidate) => candidate.isPrimary)
})

// The thread labels inbound messages with the contact's name, else the customer's.
watch(
  [data, contact, locale],
  () => {
    workspace.customerLabel.value = contact.value?.name ?? (data.value ? localizedName(data.value.displayName, locale.value) : null)
  },
  { immediate: true },
)
</script>
