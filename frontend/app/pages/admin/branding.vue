<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ t('branding.title') }}</h1>
    <p class="mt-1 max-w-prose text-sm text-fg-muted">{{ t('branding.intro') }}</p>

    <UiErrorState
      v-if="forbidden"
      class="mt-6"
      :title="t('errors.problem.forbidden')"
      :description="forbiddenDetail"
    />
    <UiErrorState
      v-else-if="error"
      class="mt-6"
      :title="t('branding.load.error')"
      :retry-label="t('branding.load.retry')"
      @retry="refresh()"
    />
    <div v-else-if="!settings" class="mt-6 grid gap-6 lg:grid-cols-2" aria-busy="true">
      <div class="flex flex-col gap-3">
        <UiSkeleton variant="rect" />
        <UiSkeleton variant="rect" />
        <UiSkeleton variant="text" :lines="3" />
      </div>
      <UiSkeleton variant="rect" />
    </div>

    <div v-else class="mt-6 grid gap-6 lg:grid-cols-2">
      <form class="flex min-w-0 flex-col gap-4" novalidate @submit.prevent="save()">
        <p v-if="!settings.isCustom" class="text-sm text-fg-muted">{{ t('branding.defaultNote') }}</p>

        <UiInput
          v-model="form.displayNameEn"
          dir="ltr"
          :label="t('branding.fields.displayNameEn')"
          :error="formErrors.errorFor('displayNameEn')"
          required
        />
        <UiInput
          v-model="form.displayNameAr"
          dir="rtl"
          lang="ar"
          :label="t('branding.fields.displayNameAr')"
          :error="formErrors.errorFor('displayNameAr')"
          required
        />
        <div class="grid gap-4 sm:grid-cols-2">
          <UiColorField
            v-model="form.primaryColour"
            :label="t('branding.fields.primary')"
            :error="formErrors.errorFor('primaryColour')"
          />
          <UiColorField
            v-model="form.accentColour"
            :label="t('branding.fields.accent')"
            :error="formErrors.errorFor('accentColour')"
          />
        </div>
        <UiSelect
          v-model="form.arabicFont"
          :label="t('branding.fields.arabicFont')"
          :options="fontOptions"
          :error="formErrors.errorFor('arabicFont')"
        />

        <div v-for="slot in logoSlots" :key="slot.key" class="flex flex-col gap-2">
          <div class="flex items-center gap-3">
            <div
              :data-theme="slot.key === 'dark' ? 'dark' : undefined"
              class="flex h-12 w-28 shrink-0 items-center justify-center overflow-hidden rounded-md border border-border bg-surface-raised"
            >
              <img
                v-if="logoSrc(slot.key)"
                :src="logoSrc(slot.key) ?? undefined"
                :alt="slot.label"
                class="h-10 w-auto max-w-full object-contain"
              />
            </div>
            <UiFileField
              :label="slot.label"
              :button-label="logoBusy[slot.key] ? t('branding.fields.uploading') : t('branding.fields.chooseFile')"
              accept="image/png,image/svg+xml"
              :hint="t('branding.fields.logoHint')"
              :error="logoErrors[slot.key]"
              :busy="logoBusy[slot.key]"
              @select="(file) => upload(slot.key, file)"
            />
            <UiButton
              v-if="form[slot.field]"
              variant="ghost"
              size="sm"
              type="button"
              @click="removeLogo(slot.key)"
            >
              {{ t('branding.fields.removeLogo') }}
            </UiButton>
          </div>
        </div>

        <p v-if="saveError" role="alert" class="text-sm text-danger-600">{{ saveError }}</p>
        <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
          {{ formErrors.formError.value }}
        </p>

        <div class="flex flex-wrap gap-2">
          <UiButton type="submit" :loading="saving">{{ t('branding.actions.save') }}</UiButton>
          <UiButton variant="secondary" type="button" :disabled="!settings.canRevert" @click="revertOpen = true">
            {{ t('branding.actions.revert') }}
          </UiButton>
        </div>
      </form>

      <section class="flex min-w-0 flex-col gap-4" :aria-label="t('branding.preview.title')">
        <h2 class="text-lg font-semibold text-fg">{{ t('branding.preview.title') }}</h2>
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div role="group" :aria-label="t('branding.preview.themeLabel')" class="flex gap-1">
            <UiButton
              v-for="option in themeOptions"
              :key="option.value"
              size="sm"
              :variant="previewTheme === option.value ? 'primary' : 'secondary'"
              :aria-pressed="previewTheme === option.value"
              @click="previewTheme = option.value"
            >
              {{ option.label }}
            </UiButton>
          </div>
        </div>
        <UiTabs v-model="previewTab" :tabs="previewTabs" :label="t('branding.preview.title')">
          <template #panel="{ id }">
            <BrandingPreviewAgent
              v-if="id === 'branding-preview-agent'"
              :display-name="previewName"
              :logo-url="previewLogo"
              :theme="previewTheme"
              :style-vars="previewVars"
            />
            <BrandingPreviewPortal
              v-else
              :display-name="previewName"
              :logo-url="previewLogo"
              :theme="previewTheme"
              :style-vars="previewVars"
            />
          </template>
        </UiTabs>
        <BrandingContrastList v-if="preview" :checks="preview.checks ?? []" />
      </section>
    </div>

    <UiModal
      v-model:open="revertOpen"
      :title="t('branding.actions.revert')"
      :close-label="t('branding.actions.close')"
      size="sm"
    >
      <p class="text-sm text-fg">{{ t('branding.actions.revertConfirm') }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="revertOpen = false">{{ t('branding.actions.cancel') }}</UiButton>
        <UiButton :loading="reverting" @click="confirmRevert()">{{ t('branding.actions.revert') }}</UiButton>
      </template>
    </UiModal>
  </div>
</template>

<script setup lang="ts">
import { problemOf, ProblemTypes, type Problem } from '~/api/problem'
import { problemMessage } from '~/api/problem-message'
import { useBrandingCommands } from '~/composables/branding/useBrandingCommands'
import { useBrandingPreview } from '~/composables/branding/useBrandingPreview'
import { useBrandingSettings } from '~/composables/branding/useBrandingSettings'
import type { Theme } from '~/composables/useTheme'
import { arabicFontKeys, brandLogoUrl, previewStyle } from '~/utils/branding'
import { localizedName } from '~/utils/scope'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const { $i18n } = useNuxtApp()
const { data: settings, error, refresh } = useBrandingSettings()
const commands = useBrandingCommands()
const brandingStore = useBrandingStore()
const formErrors = useFormErrors()

const maxLogoBytes = 512 * 1024

const problem = computed(() => problemOf(error.value))
const forbidden = computed(() => problem.value?.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => problem.value?.detail ?? undefined)

const form = reactive({
  displayNameEn: '',
  displayNameAr: '',
  primaryColour: '#145EC1',
  accentColour: '#0F766E',
  arabicFont: 'IbmPlexSansArabic',
  lightLogoId: null as string | null,
  darkLogoId: null as string | null,
})

let formLoaded = false

function loadForm(): void {
  const current = settings.value?.current
  if (!current) return
  form.displayNameEn = current.displayName?.en ?? ''
  form.displayNameAr = current.displayName?.ar ?? ''
  form.primaryColour = current.primaryColour ?? form.primaryColour
  form.accentColour = current.accentColour ?? form.accentColour
  form.arabicFont = current.arabicFont ?? 'IbmPlexSansArabic'
  form.lightLogoId = current.lightLogoId ?? null
  form.darkLogoId = current.darkLogoId ?? null
  formLoaded = true
}

// The form is filled from the server once, and again after a save or a revert - never on a background refetch (a
// department scope switch refetches every useAsyncData), which would silently discard what the admin is typing.
// The row version is read from the latest settings at submit time, so a refetch never makes a save stale.
watch(
  settings,
  () => {
    if (!formLoaded) loadForm()
  },
  { immediate: true },
)

const fontOptions = computed(() => arabicFontKeys.map((key) => ({ value: key, label: t(`branding.fonts.${key}`) })))

// --- preview ---------------------------------------------------------------------------------------------------
const primaryRef = toRef(form, 'primaryColour')
const accentRef = toRef(form, 'accentColour')
const { preview } = useBrandingPreview(primaryRef, accentRef)

const previewTheme = ref<Theme>('light')
const themeOptions = computed(() => [
  { value: 'light' as const, label: t('branding.preview.light') },
  { value: 'dark' as const, label: t('branding.preview.dark') },
])
const previewTab = ref('branding-preview-agent')
const previewTabs = computed(() => [
  { id: 'branding-preview-agent', label: t('branding.preview.agent') },
  { id: 'branding-preview-portal', label: t('branding.preview.portal') },
])
const previewVars = computed(() => previewStyle(preview.value?.tokens, previewTheme.value, form.arabicFont))
const previewName = computed(
  () =>
    localizedName({ ar: form.displayNameAr, en: form.displayNameEn }, locale.value) || t('shell.appName'),
)
const previewLogo = computed(() =>
  previewTheme.value === 'dark'
    ? brandLogoUrl(form.darkLogoId ?? form.lightLogoId)
    : brandLogoUrl(form.lightLogoId),
)

// --- logos -----------------------------------------------------------------------------------------------------
type LogoKey = 'light' | 'dark'
const logoSlots = computed(() => [
  { key: 'light' as const, field: 'lightLogoId' as const, label: t('branding.fields.lightLogo') },
  { key: 'dark' as const, field: 'darkLogoId' as const, label: t('branding.fields.darkLogo') },
])
const logoBusy = reactive<Record<LogoKey, boolean>>({ light: false, dark: false })
const logoErrors = reactive<Record<LogoKey, string | undefined>>({ light: undefined, dark: undefined })

function logoSrc(key: LogoKey): string | null {
  return brandLogoUrl(key === 'light' ? form.lightLogoId : form.darkLogoId)
}

function describe(failure: Problem): string {
  const message = problemMessage(failure, { t: (key) => $i18n.t(key), te: (key) => $i18n.te(key) })
  return message.description ?? message.title
}

async function upload(key: LogoKey, file: File): Promise<void> {
  logoErrors[key] = undefined
  // A client pre-check so a user never waits for (or sees) the framework's own size error.
  if (file.size > maxLogoBytes) {
    logoErrors[key] = t('errors.code.Organisation.LogoTooLarge')
    return
  }
  logoBusy[key] = true
  const result = await commands.uploadLogo(file)
  logoBusy[key] = false
  if (!result.ok) {
    logoErrors[key] = describe(result.problem)
    return
  }
  if (key === 'light') form.lightLogoId = result.data.id ?? null
  else form.darkLogoId = result.data.id ?? null
}

function removeLogo(key: LogoKey): void {
  logoErrors[key] = undefined
  if (key === 'light') {
    // A dark logo needs a light one (domain rule), so removing the light logo removes both.
    form.lightLogoId = null
    form.darkLogoId = null
  } else {
    form.darkLogoId = null
  }
}

// --- save / revert ---------------------------------------------------------------------------------------------
const saving = ref(false)
const saveError = ref<string | null>(null)

const fieldForCode: Readonly<Record<string, string>> = {
  'Organisation.DarkLogoWithoutLight': 'darkLogoId',
}

async function save(): Promise<void> {
  saving.value = true
  saveError.value = null
  formErrors.clear()

  const result = await commands.save({
    displayNameAr: form.displayNameAr,
    displayNameEn: form.displayNameEn,
    primaryColour: form.primaryColour,
    accentColour: form.accentColour,
    arabicFont: form.arabicFont,
    lightLogoId: form.lightLogoId,
    darkLogoId: form.darkLogoId,
    rowVersion: settings.value?.rowVersion ?? '',
  })
  saving.value = false

  if (result.ok) {
    useToasts().push({ tone: 'success', title: t('branding.actions.saved') })
    await refresh()
    loadForm()
    // The admin's own shell re-themes at once; other open tabs follow after a refresh.
    await brandingStore.loadForTenant()
    return
  }

  const failure = result.problem
  if (formErrors.apply(failure)) return
  if (failure.type === ProblemTypes.Conflict) {
    saveError.value = t('branding.conflict')
    await refresh()
    return
  }
  if (failure.code !== null && fieldForCode[failure.code] !== undefined) {
    formErrors.applyCode(failure, fieldForCode)
    return
  }
  // 422 contrast refusal (server-worded, in the request language), a missing logo (404), anything else.
  saveError.value = describe(failure)
}

const revertOpen = ref(false)
const reverting = ref(false)

async function confirmRevert(): Promise<void> {
  reverting.value = true
  saveError.value = null
  const result = await commands.revert(settings.value?.rowVersion ?? '')
  reverting.value = false
  revertOpen.value = false

  if (result.ok) {
    useToasts().push({ tone: 'success', title: t('branding.actions.reverted') })
    await refresh()
    loadForm()
    await brandingStore.loadForTenant()
    return
  }
  if (result.problem.type === ProblemTypes.Conflict) {
    saveError.value = t('branding.conflict')
    await refresh()
    return
  }
  saveError.value = describe(result.problem)
}
</script>
