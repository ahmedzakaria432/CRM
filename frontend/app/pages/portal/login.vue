<template>
  <div class="mx-auto w-full max-w-sm">
    <h1 class="text-2xl font-bold text-fg">{{ t('portal.login.title') }}</h1>
    <form ref="formRef" class="mt-6 flex flex-col gap-4" novalidate @submit.prevent="submit()">
      <UiInput
        v-model="email"
        type="email"
        autocomplete="username"
        dir="ltr"
        :label="t('portal.login.email')"
        :error="formErrors.errorFor('email')"
        required
      />
      <UiInput
        v-model="password"
        type="password"
        autocomplete="current-password"
        dir="ltr"
        :label="t('portal.login.password')"
        :error="formErrors.errorFor('password')"
        required
      />
      <p v-if="alertMessage" role="alert" class="text-sm text-danger-600">{{ alertMessage }}</p>
      <UiButton type="submit" :loading="submitting" block>{{ t('portal.login.submit') }}</UiButton>
    </form>
    <p class="mt-4 flex flex-col gap-2 text-sm">
      <NuxtLink to="/portal/register" class="text-primary-600 underline focus-visible:focus-ring">
        {{ t('portal.login.noAccount') }}
      </NuxtLink>
      <NuxtLink to="/" class="text-primary-600 underline focus-visible:focus-ring">
        {{ t('portal.login.backHome') }}
      </NuxtLink>
    </p>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'portal' })

const { t } = useI18n()
const route = useRoute()
const portal = usePortalAuthStore()
const formErrors = useFormErrors()

const email = ref('')
const password = ref('')
const submitting = ref(false)
const alertMessage = ref<string | null>(null)
const formRef = useTemplateRef<HTMLFormElement>('formRef')

// Open-redirect guard: only a same-origin path (starts with a single "/") is followed after sign-in.
const safeRedirect = computed(() => {
  const value = route.query.redirect
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') ? value : '/portal'
})

onMounted(() => {
  formRef.value?.querySelector('input')?.focus()
})

async function submit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  formErrors.clear()
  alertMessage.value = null

  const result = await portal.login(email.value, password.value)
  submitting.value = false

  if (result.ok) {
    await navigateTo(safeRedirect.value)
    return
  }

  if (formErrors.apply(result.problem)) return

  // A wrong password is an inline message, never a toast or a redirect.
  if (result.problem.status === 401) alertMessage.value = t('portal.login.invalid')
  else if (result.problem.status === 429) alertMessage.value = t('portal.login.rateLimited')
  else alertMessage.value = t('portal.login.failed')
}
</script>
