<template>
  <section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">
    <div class="flex items-center justify-between gap-2">
      <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('customers.contacts.title') }}</h2>
      <UiButton v-if="canWrite && contacts.length > 0" size="sm" variant="secondary" @click="openAdd()">
        {{ t('customers.contacts.add') }}
      </UiButton>
    </div>

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
        v-else-if="data && contacts.length === 0"
        :heading-level="3"
        :title="t('customers.contacts.empty')"
        :description="t('customers.contacts.emptyDescription')"
      >
        <template v-if="canWrite" #actions>
          <UiButton @click="openAdd()">{{ t('customers.contacts.add') }}</UiButton>
        </template>
      </UiEmptyState>
      <ul v-else class="divide-y divide-border">
        <li v-for="contact in contacts" :key="contact.id" class="flex flex-col gap-2 py-3 first:pt-0 last:pb-0">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="break-words font-medium text-fg">{{ contact.name }}</span>
              <UiBadge v-if="contact.isPrimary" tone="primary" size="sm">{{ t('customers.contacts.primary') }}</UiBadge>
              <UiBadge v-if="contact.hasPortalAccess" tone="info" size="sm">
                {{ t('customers.contacts.portalAccess') }}
              </UiBadge>
            </div>
            <p v-if="contact.jobTitle" class="break-words text-sm text-fg-muted">{{ contact.jobTitle }}</p>
            <p v-if="contact.email" class="break-all text-sm">
              <a :href="`mailto:${contact.email}`" class="text-primary-600 underline">
                <bdi dir="ltr">{{ contact.email }}</bdi>
              </a>
            </p>
            <p v-if="contact.phone" class="text-sm">
              <a :href="`tel:${contact.phone}`" class="text-primary-600 underline">
                <bdi dir="ltr">{{ contact.phone }}</bdi>
              </a>
            </p>
          </div>
          <div
            v-if="canWrite"
            role="group"
            class="flex flex-wrap gap-1"
            :aria-label="t('customers.contacts.actionsFor', { name: contact.name ?? '' })"
          >
            <UiButton size="sm" variant="ghost" @click="openEdit(contact)">{{ t('customers.contacts.edit') }}</UiButton>
            <UiButton v-if="!contact.isPrimary" size="sm" variant="ghost" @click="makePrimary(contact)">
              {{ t('customers.contacts.setPrimary') }}
            </UiButton>
            <UiButton
              v-if="contact.hasPortalAccess && contact.email"
              size="sm"
              variant="ghost"
              @click="openPortalPassword(contact)"
            >
              {{ t('customers.contacts.setPortalPassword') }}
            </UiButton>
            <UiButton size="sm" variant="ghost" @click="askRemove(contact)">{{ t('customers.contacts.remove') }}</UiButton>
          </div>
        </li>
      </ul>
    </div>

    <CustomerContactModal
      v-if="data && canWrite"
      v-model:open="modalOpen"
      :customer="data"
      :contact="editing"
      @saved="customer.publish"
      @conflict="customer.reload()"
    />
    <CustomerPortalPasswordModal
      v-if="canWrite"
      v-model:open="portalPasswordOpen"
      :customer-id="customerId"
      :contact="portalContact"
    />
    <UiModal
      v-if="canWrite"
      v-model:open="removeOpen"
      :title="t('customers.contacts.removeTitle')"
      :close-label="t('customers.contacts.close')"
    >
      <p class="text-sm text-fg">{{ t('customers.contacts.removeDescription', { name: removing?.name ?? '' }) }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="removeOpen = false">{{ t('customers.contacts.cancel') }}</UiButton>
        <UiButton variant="danger" :loading="busy" @click="confirmRemove()">{{ t('customers.contacts.remove') }}</UiButton>
      </template>
    </UiModal>
  </section>
</template>

<script setup lang="ts">
import { useCustomer } from '~/composables/customers/useCustomer'
import { useCustomerCommands } from '~/composables/customers/useCustomerCommands'
import type { ContactDto, CustomerDto } from '~/composables/customers/types'
import { ProblemTypes, problemOf } from '~/api/problem'
import type { Problem } from '~/api/problem'
import { Permissions } from '~/utils/permissions'

interface Props {
  customerId: string
}
const props = defineProps<Props>()

const { t } = useI18n()
const { can } = useCan()
const toasts = useToasts()
const commands = useCustomerCommands()
const customer = useCustomer(props.customerId)
const { data, status, error, refresh } = customer.load('contacts')
const headingId = useId()

const canWrite = computed(() => can(Permissions.CustomersWrite))
const contacts = computed(() => data.value?.contacts ?? [])
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)

const modalOpen = ref(false)
const editing = ref<ContactDto | undefined>(undefined)
const removeOpen = ref(false)
const removing = ref<ContactDto | undefined>(undefined)
const busy = ref(false)
const portalPasswordOpen = ref(false)
const portalContact = ref<ContactDto | undefined>(undefined)

function openPortalPassword(contact: ContactDto): void {
  portalContact.value = contact
  portalPasswordOpen.value = true
}

function openAdd(): void {
  editing.value = undefined
  modalOpen.value = true
}
function openEdit(contact: ContactDto): void {
  editing.value = contact
  modalOpen.value = true
}
function askRemove(contact: ContactDto): void {
  removing.value = contact
  removeOpen.value = true
}

async function finish(
  result: { ok: true; data: CustomerDto } | { ok: false; problem: Problem },
  successKey: string,
): Promise<void> {
  if (result.ok) {
    customer.publish(result.data)
    toasts.push({ tone: 'success', title: t(successKey) })
    return
  }
  // useApi already toasted; a stale rowVersion means the page must catch up.
  if (result.problem.type === ProblemTypes.Conflict) await customer.reload()
}

async function makePrimary(contact: ContactDto): Promise<void> {
  const current = data.value
  if (current === undefined || contact.id === undefined) return
  await finish(await commands.setPrimaryContact(props.customerId, contact.id, current.rowVersion ?? ''), 'customers.contacts.primarySet')
}

async function confirmRemove(): Promise<void> {
  const current = data.value
  const target = removing.value
  if (busy.value || current === undefined || target?.id === undefined) return
  busy.value = true
  const result = await commands.removeContact(props.customerId, target.id, current.rowVersion ?? '')
  busy.value = false
  removeOpen.value = false
  await finish(result, 'customers.contacts.removed')
}
</script>
