<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ t('identity.account.title') }}</h1>

    <UiEmptyState v-if="!auth.me" class="mt-6" :title="t('identity.load.error')" />
    <div v-else class="mt-6 grid gap-6 lg:grid-cols-2">
      <section class="rounded-md border border-border p-4" :aria-labelledby="profileHeadingId">
        <h2 :id="profileHeadingId" class="text-lg font-semibold text-fg">{{ t('identity.detail.profile') }}</h2>
        <dl class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
          <dt class="text-fg-muted">{{ t('identity.fields.displayName') }}</dt>
          <dd class="text-fg">{{ auth.me.displayName }}</dd>
          <dt class="text-fg-muted">{{ t('identity.fields.email') }}</dt>
          <dd class="text-fg"><bdi dir="ltr">{{ auth.me.email }}</bdi></dd>
          <dt class="text-fg-muted">{{ t('identity.fields.locale') }}</dt>
          <dd class="text-fg">{{ t(`identity.locale.${auth.me.preferredLocale === 'ar' ? 'ar' : 'en'}`) }}</dd>
          <dt class="text-fg-muted">{{ t('identity.detail.memberships') }}</dt>
          <dd class="text-fg">{{ format.number(auth.me.memberships?.length ?? 0) }}</dd>
        </dl>
      </section>

      <section class="rounded-md border border-border p-4" :aria-labelledby="passwordHeadingId">
        <h2 :id="passwordHeadingId" class="text-lg font-semibold text-fg">{{ t('identity.account.changePassword') }}</h2>
        <form class="mt-4 flex flex-col gap-3" novalidate @submit.prevent="submit()">
          <UiInput
            v-model="form.currentPassword"
            type="password"
            autocomplete="current-password"
            dir="ltr"
            :label="t('identity.fields.currentPassword')"
            :error="formErrors.errorFor('currentPassword')"
            required
          />
          <UiInput
            v-model="form.newPassword"
            type="password"
            autocomplete="new-password"
            dir="ltr"
            :label="t('identity.fields.newPassword')"
            :hint="t('identity.account.passwordRules')"
            :error="formErrors.errorFor('newPassword')"
            required
          />
          <UiInput
            v-model="form.confirmPassword"
            type="password"
            autocomplete="new-password"
            dir="ltr"
            :label="t('identity.fields.confirmPassword')"
            :error="mismatch ? t('identity.account.mismatch') : undefined"
            required
          />
          <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
            {{ formErrors.formError.value }}
          </p>
          <div>
            <UiButton type="submit" :loading="submitting">{{ t('identity.actions.save') }}</UiButton>
          </div>
        </form>
      </section>
    </div>

    <div class="mt-6">
      <UiButton variant="secondary" @click="auth.logout()">{{ t('identity.menu.signOut') }}</UiButton>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'agent' })

const { t } = useI18n()
const auth = useAuthStore()
const format = useFormat()
const { clients, call } = useApi()
const formErrors = useFormErrors()

const profileHeadingId = useId()
const passwordHeadingId = useId()

const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const submitting = ref(false)
const mismatch = ref(false)

async function submit(): Promise<void> {
  formErrors.clear()
  mismatch.value = form.newPassword !== form.confirmPassword
  if (mismatch.value) return

  submitting.value = true
  const result = await call(
    clients.agent.POST('/api/Agent/Me/change-password', {
      body: { currentPassword: form.currentPassword, newPassword: form.newPassword },
    }),
  )
  submitting.value = false

  if (result.ok) {
    form.currentPassword = ''
    form.newPassword = ''
    form.confirmPassword = ''
    useToasts().push({ tone: 'success', title: t('identity.account.changed') })
    return
  }

  // A 400 is shown on the fields; anything else has already raised the shared error toast.
  formErrors.apply(result.problem)
}
</script>
