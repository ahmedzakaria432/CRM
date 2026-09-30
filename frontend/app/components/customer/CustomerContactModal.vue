<template>
  <UiModal
    v-model:open="open"
    :title="contact ? t('customers.contacts.editTitle') : t('customers.contacts.addTitle')"
    :close-label="t('customers.contacts.close')"
  >
    <form class="flex flex-col gap-3" novalidate @submit.prevent="submit()">
      <UiInput
        v-model="form.name"
        maxlength="200"
        :label="t('customers.contacts.fields.name')"
        :error="formErrors.errorFor('name')"
        required
      />
      <UiInput
        v-model="form.jobTitle"
        maxlength="100"
        :label="t('customers.contacts.fields.jobTitle')"
        :error="formErrors.errorFor('jobTitle')"
      />
      <UiInput
        v-model="form.email"
        type="email"
        dir="ltr"
        autocomplete="off"
        :label="t('customers.contacts.fields.email')"
        :error="formErrors.errorFor('email')"
      />
      <UiInput
        v-model="form.phone"
        type="tel"
        dir="ltr"
        autocomplete="off"
        :label="t('customers.contacts.fields.phone')"
        :hint="t('customers.contacts.fields.phoneHint')"
        :error="formErrors.errorFor('phone')"
      />
      <UiCheckbox
        v-model="form.hasPortalAccess"
        :label="t('customers.contacts.fields.hasPortalAccess')"
        :error="formErrors.errorFor('hasPortalAccess')"
      />
      <UiCheckbox
        v-if="!contact"
        v-model="form.isPrimary"
        :disabled="isFirstContact"
        :label="t('customers.contacts.fields.isPrimary')"
        :hint="isFirstContact ? t('customers.contacts.fields.isPrimaryFirst') : undefined"
      />
      <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
        {{ formErrors.formError.value }}
      </p>
      <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
    </form>
    <template #footer>
      <UiButton variant="secondary" @click="open = false">{{ t('customers.contacts.cancel') }}</UiButton>
      <UiButton :loading="submitting" @click="submit()">{{ t('customers.contacts.save') }}</UiButton>
    </template>
  </UiModal>
</template>

<script setup lang="ts">
import { useCustomerCommands } from '~/composables/customers/useCustomerCommands'
import type { ContactDto, CustomerDto } from '~/composables/customers/types'
import { ProblemTypes } from '~/api/problem'
import { contactFieldForCode } from '~/utils/customers'

interface Props {
  customer: CustomerDto
  contact?: ContactDto
}
const props = withDefaults(defineProps<Props>(), { contact: undefined })
const emit = defineEmits<{ saved: [CustomerDto]; conflict: [] }>()
const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const toasts = useToasts()
const commands = useCustomerCommands()
const formErrors = useFormErrors()
const submitting = ref(false)

const form = reactive({ name: '', jobTitle: '', email: '', phone: '', hasPortalAccess: false, isPrimary: false })
const isFirstContact = computed(() => (props.customer.contacts ?? []).length === 0)

watch(open, (value) => {
  if (!value) return
  const source = props.contact
  form.name = source?.name ?? ''
  form.jobTitle = source?.jobTitle ?? ''
  form.email = source?.email ?? ''
  form.phone = source?.phone ?? ''
  form.hasPortalAccess = source?.hasPortalAccess ?? false
  form.isPrimary = source === undefined && isFirstContact.value
  formErrors.clear()
})

function blankToNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

async function submit(): Promise<void> {
  const customerId = props.customer.id
  if (submitting.value || customerId === undefined) return
  submitting.value = true
  formErrors.clear()

  const common = {
    name: form.name.trim(),
    jobTitle: blankToNull(form.jobTitle),
    email: blankToNull(form.email),
    phone: blankToNull(form.phone),
    hasPortalAccess: form.hasPortalAccess,
    // Read at submit time: an earlier command bumped it.
    rowVersion: props.customer.rowVersion ?? '',
  }
  const contactId = props.contact?.id
  const result =
    props.contact !== undefined && contactId !== undefined
      ? await commands.updateContact(customerId, contactId, common)
      : await commands.addContact(customerId, { ...common, isPrimary: form.isPrimary || isFirstContact.value })
  submitting.value = false

  if (result.ok) {
    toasts.push({ tone: 'success', title: t('customers.contacts.saved') })
    open.value = false
    emit('saved', result.data)
    return
  }
  if (result.problem.type === ProblemTypes.Conflict) {
    commands.toast(result.problem)
    emit('conflict')
    return
  }
  if (formErrors.applyCode(result.problem, contactFieldForCode)) return
  if (formErrors.apply(result.problem)) return
  commands.toast(result.problem)
}
</script>
