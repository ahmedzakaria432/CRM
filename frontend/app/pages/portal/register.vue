<template>
  <div class="mx-auto w-full max-w-sm">
    <h1 class="text-2xl font-bold text-fg">{{ t('portal.register.title') }}</h1>

    <div v-if="done" class="mt-6 flex flex-col gap-4">
      <p role="status" class="text-sm text-fg">{{ t('portal.register.done') }}</p>
      <div>
        <UiButton @click="goToSignIn()">{{ t('portal.register.signIn') }}</UiButton>
      </div>
    </div>

    <template v-else>
      <p class="mt-2 text-sm text-fg-muted">{{ t('portal.register.intro') }}</p>
      <form ref="formRef" class="mt-6 flex flex-col gap-4" novalidate @submit.prevent="submit()">
        <UiInput
          v-model="email"
          type="email"
          autocomplete="username"
          dir="ltr"
          :label="t('portal.register.email')"
          :error="formErrors.errorFor('email')"
          required
        />
        <UiInput
          v-model="password"
          type="password"
          autocomplete="new-password"
          dir="ltr"
          :label="t('portal.register.password')"
          :hint="t('portal.register.passwordHint')"
          :error="formErrors.errorFor('password')"
          required
        />
        <UiInput
          v-model="confirmPassword"
          type="password"
          autocomplete="new-password"
          dir="ltr"
          :label="t('portal.register.confirmPassword')"
          :error="mismatch ? t('portal.register.mismatch') : undefined"
          required
        />
        <p v-if="alertMessage" role="alert" class="text-sm text-danger-600">{{ alertMessage }}</p>
        <UiButton type="submit" :loading="submitting" block>{{ t('portal.register.submit') }}</UiButton>
      </form>
      <p class="mt-4 text-sm">
        <NuxtLink to="/portal/login" class="text-primary-600 underline focus-visible:focus-ring">
          {{ t('portal.register.haveAccount') }}
        </NuxtLink>
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'portal' })

const { t } = useI18n()
const portal = usePortalAuthStore()
const formErrors = useFormErrors()

const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const submitting = ref(false)
const mismatch = ref(false)
const done = ref(false)
const alertMessage = ref<string | null>(null)
const formRef = useTemplateRef<HTMLFormElement>('formRef')

onMounted(() => {
  formRef.value?.querySelector('input')?.focus()
})

async function goToSignIn(): Promise<void> {
  await navigateTo('/portal/login')
}

async function submit(): Promise<void> {
  if (submitting.value) return
  formErrors.clear()
  alertMessage.value = null
  mismatch.value = password.value !== confirmPassword.value
  if (mismatch.value) return

  submitting.value = true
  const result = await portal.register(email.value, password.value)
  submitting.value = false

  // The answer is the same whether or not the email belongs to a customer (204): never sign in automatically, never reveal.
  if (result.ok) {
    done.value = true
    return
  }

  if (formErrors.apply(result.problem)) return

  if (result.problem.status === 429) alertMessage.value = t('portal.register.rateLimited')
  else alertMessage.value = t('portal.register.failed')
}
</script>
