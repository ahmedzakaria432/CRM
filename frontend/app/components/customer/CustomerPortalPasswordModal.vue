<template>
  <UiModal v-model:open="open" :title="t('customers.portal.title')" :close-label="t('customers.portal.close')">
    <form class="flex flex-col gap-3" novalidate @submit.prevent="submit()">
      <p class="text-sm text-fg-muted">{{ t('customers.portal.description', { name: contact?.name ?? '' }) }}</p>
      <UiInput
        v-model="password"
        type="password"
        autocomplete="new-password"
        dir="ltr"
        :label="t('customers.portal.password')"
        :hint="t('customers.portal.hint')"
        :error="formErrors.errorFor('password')"
        required
      />
      <p v-if="alertMessage" role="alert" class="text-sm text-danger-600">{{ alertMessage }}</p>
      <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
    </form>
    <template #footer>
      <UiButton variant="secondary" @click="open = false">{{ t('customers.contacts.cancel') }}</UiButton>
      <UiButton :loading="submitting" @click="submit()">{{ t('customers.portal.save') }}</UiButton>
    </template>
  </UiModal>
</template>

<script setup lang="ts">
import { problemMessage } from '~/api/problem-message'
import type { ContactDto } from '~/composables/customers/types'
import { useCustomerPortalAccount } from '~/composables/customers/useCustomerPortalAccount'

interface Props {
  customerId: string
  contact?: ContactDto
}
const props = withDefaults(defineProps<Props>(), { contact: undefined })
const open = defineModel<boolean>('open', { default: false })

const { t, te } = useI18n()
const formErrors = useFormErrors()
const account = useCustomerPortalAccount()

const password = ref('')
const submitting = ref(false)
const alertMessage = ref<string | null>(null)

// A fresh form every time the modal opens.
watch(open, (value) => {
  if (!value) return
  password.value = ''
  alertMessage.value = null
  formErrors.clear()
})

async function submit(): Promise<void> {
  const contactId = props.contact?.id
  if (submitting.value || contactId === undefined) return
  submitting.value = true
  formErrors.clear()
  alertMessage.value = null

  const result = await account.setPassword(props.customerId, contactId, password.value)
  submitting.value = false

  if (result.ok) {
    useToasts().push({ tone: 'success', title: t('customers.portal.saved') })
    password.value = ''
    open.value = false
    return
  }

  // A 400 lands on the field; 403/404/409/422 are shown inline with their localised text.
  if (formErrors.apply(result.problem)) return
  const message = problemMessage(result.problem, { t: (key) => t(key), te: (key) => te(key) })
  alertMessage.value = message.description ?? message.title
}
</script>
