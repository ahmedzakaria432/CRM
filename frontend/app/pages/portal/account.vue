<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ t('portal.account.title') }}</h1>

    <!-- No session yet (server render, first client render, restore in flight): the skeleton; the guard redirects when there is none. -->
    <UiSkeleton v-if="!portal.me" class="mt-6" :lines="4" />
    <div v-else class="mt-6 grid gap-6 lg:grid-cols-2">
      <section class="rounded-md border border-border p-4" :aria-labelledby="profileHeadingId">
        <h2 :id="profileHeadingId" class="text-lg font-semibold text-fg">{{ t('portal.account.profile') }}</h2>
        <p class="mt-2 text-sm text-fg-muted">
          {{ t('portal.account.email') }}: <bdi dir="ltr" class="text-fg">{{ portal.me.email }}</bdi>
        </p>
        <form class="mt-4 flex flex-col gap-3" novalidate @submit.prevent="saveProfile()">
          <UiInput
            v-model="profile.displayName"
            maxlength="200"
            :label="t('portal.account.displayName')"
            :error="profileErrors.errorFor('displayName')"
            required
          />
          <UiSelect
            v-model="profile.preferredLocale"
            :label="t('portal.account.language')"
            :options="languageOptions"
            :error="profileErrors.errorFor('preferredLocale')"
          />
          <p v-if="profileErrors.formError.value" role="alert" class="text-sm text-danger-600">
            {{ profileErrors.formError.value }}
          </p>
          <div>
            <UiButton type="submit" :loading="savingProfile">{{ t('portal.account.save') }}</UiButton>
          </div>
        </form>
      </section>

      <section class="rounded-md border border-border p-4" :aria-labelledby="passwordHeadingId">
        <h2 :id="passwordHeadingId" class="text-lg font-semibold text-fg">{{ t('portal.account.changePassword') }}</h2>
        <form class="mt-4 flex flex-col gap-3" novalidate @submit.prevent="savePassword()">
          <UiInput
            v-model="password.currentPassword"
            type="password"
            autocomplete="current-password"
            dir="ltr"
            :label="t('portal.account.currentPassword')"
            :error="passwordErrors.errorFor('currentPassword')"
            required
          />
          <UiInput
            v-model="password.newPassword"
            type="password"
            autocomplete="new-password"
            dir="ltr"
            :label="t('portal.account.newPassword')"
            :hint="t('portal.account.passwordRules')"
            :error="passwordErrors.errorFor('newPassword')"
            required
          />
          <UiInput
            v-model="password.confirmPassword"
            type="password"
            autocomplete="new-password"
            dir="ltr"
            :label="t('portal.account.confirmPassword')"
            :error="mismatch ? t('portal.account.mismatch') : undefined"
            required
          />
          <p v-if="passwordErrors.formError.value" role="alert" class="text-sm text-danger-600">
            {{ passwordErrors.formError.value }}
          </p>
          <div>
            <UiButton type="submit" :loading="savingPassword">{{ t('portal.account.save') }}</UiButton>
          </div>
        </form>
      </section>
    </div>

    <div class="mt-6">
      <UiButton variant="secondary" @click="portal.logout()">{{ t('portal.account.signOut') }}</UiButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { usePortalProfile } from '~/composables/portal/usePortalProfile'

definePageMeta({ layout: 'portal' })

const { t, locale, setLocale } = useI18n()
const portal = usePortalAuthStore()
const profileApi = usePortalProfile()
const profileErrors = useFormErrors()
const passwordErrors = useFormErrors()

const profileHeadingId = useId()
const passwordHeadingId = useId()

const profile = reactive({ displayName: portal.me?.displayName ?? '', preferredLocale: portal.me?.preferredLocale ?? locale.value })
// A reload restores the session after this page is set up: follow the profile once it arrives.
watch(
  () => portal.me,
  (me) => {
    if (me) {
      profile.displayName = me.displayName ?? ''
      profile.preferredLocale = me.preferredLocale ?? locale.value
    }
  },
)

const languageOptions = computed(() => [
  { value: 'en', label: t('portal.account.languages.en') },
  { value: 'ar', label: t('portal.account.languages.ar') },
])

const savingProfile = ref(false)

async function saveProfile(): Promise<void> {
  if (savingProfile.value) return
  savingProfile.value = true
  profileErrors.clear()
  const result = await profileApi.update(profile.displayName, profile.preferredLocale)
  savingProfile.value = false

  if (!result.ok) {
    profileErrors.apply(result.problem)
    return
  }

  portal.applyMe(result.data)
  useToasts().push({ tone: 'success', title: t('portal.account.saved') })
  const next = result.data.preferredLocale
  if ((next === 'en' || next === 'ar') && next !== locale.value) await setLocale(next)
}

const password = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const savingPassword = ref(false)
const mismatch = ref(false)

async function savePassword(): Promise<void> {
  if (savingPassword.value) return
  passwordErrors.clear()
  mismatch.value = password.newPassword !== password.confirmPassword
  if (mismatch.value) return

  savingPassword.value = true
  const result = await profileApi.changePassword(password.currentPassword, password.newPassword)
  savingPassword.value = false

  if (result.ok) {
    password.currentPassword = ''
    password.newPassword = ''
    password.confirmPassword = ''
    useToasts().push({ tone: 'success', title: t('portal.account.changed') })
    return
  }

  passwordErrors.apply(result.problem)
}
</script>
