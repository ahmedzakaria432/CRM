<template>
  <div class="mx-auto w-full max-w-2xl">
    <h1 class="text-2xl font-bold text-fg">{{ t('portal.new.title') }}</h1>
    <form class="mt-6 flex flex-col gap-4" novalidate @submit.prevent="submit()">
      <div>
        <UiSelect
          v-model="form.categoryId"
          :label="t('portal.new.category')"
          :options="categoryOptions"
          :disabled="categoriesLoading"
          :error="formErrors.errorFor('categoryId')"
        />
        <p v-if="categoriesError" role="alert" class="mt-1 text-xs text-danger-600">{{ t('portal.tickets.loadError') }}</p>
      </div>
      <UiInput
        v-model="form.subject"
        maxlength="200"
        :label="t('portal.new.subject')"
        :error="formErrors.errorFor('subject')"
        required
      />
      <UiTextarea
        v-model="form.description"
        :rows="6"
        maxlength="4000"
        :label="t('portal.new.description')"
        :error="formErrors.errorFor('description')"
        required
      />
      <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">{{ formErrors.formError.value }}</p>
      <p v-if="alertMessage" role="alert" class="text-sm text-danger-600">{{ alertMessage }}</p>
      <div class="flex flex-wrap gap-2">
        <UiButton type="submit" :loading="submitting">{{ t('portal.new.submit') }}</UiButton>
        <UiButton variant="secondary" @click="cancel()">{{ t('portal.detail.cancel') }}</UiButton>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { usePortalCategories } from '~/composables/portal/usePortalCategories'
import { usePortalTicketCommands } from '~/composables/portal/usePortalTicketCommands'
import { localizedName } from '~/utils/scope'
import { submitFieldForCode } from '~/utils/portal'

definePageMeta({ layout: 'portal' })

const { t, locale } = useI18n()
const formErrors = useFormErrors()
const commands = usePortalTicketCommands()
const { data: categories, status: categoriesStatus, error: categoriesError } = usePortalCategories()

const form = reactive({ categoryId: '', subject: '', description: '' })
const submitting = ref(false)
const alertMessage = ref<string | null>(null)

const categoriesLoading = computed(() => (categoriesStatus.value === 'idle' || categoriesStatus.value === 'pending') && !categories.value)
const categoryOptions = computed(() => [
  { value: '', label: t('portal.new.noCategory') },
  ...(categories.value ?? []).map((category) => ({
    value: category.id ?? '',
    label: localizedName({ ar: category.nameAr, en: category.nameEn }, locale.value),
  })),
])

async function cancel(): Promise<void> {
  await navigateTo('/portal')
}

async function submit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  formErrors.clear()
  alertMessage.value = null

  const result = await commands.submit({
    subject: form.subject,
    description: form.description,
    categoryId: form.categoryId === '' ? null : form.categoryId,
  })
  submitting.value = false

  if (result.ok) {
    useToasts().push({ tone: 'success', title: t('portal.new.submitted') })
    await navigateTo(`/portal/tickets/${result.data.id}`)
    return
  }

  // A 400 is shown on the fields (never a toast); a known code lands on its field; anything else is inline.
  if (formErrors.apply(result.problem)) return
  if (formErrors.applyCode(result.problem, submitFieldForCode)) return
  alertMessage.value = t('portal.new.failed')
}
</script>
