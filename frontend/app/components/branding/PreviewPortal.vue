<template>
  <div :data-theme="theme" :style="styleVars" class="overflow-hidden rounded-lg border border-border bg-surface text-fg">
    <div class="flex items-center gap-3 border-b border-t-4 border-border border-t-accent-600 px-3 py-2">
      <img v-if="logoUrl" :src="logoUrl" :alt="displayName" class="h-7 w-auto max-w-32 object-contain" />
      <span v-else class="truncate text-base font-semibold">{{ displayName }}</span>
    </div>
    <div class="flex flex-col items-start gap-3 p-4">
      <h3 class="text-base font-semibold">{{ t('branding.preview.signInTitle') }}</h3>
      <UiInput :label="t('branding.preview.email')" dir="ltr" disabled class="w-full max-w-xs" />
      <UiInput :label="t('branding.preview.password')" type="password" dir="ltr" disabled class="w-full max-w-xs" />
      <UiButton size="sm" tabindex="-1">{{ t('branding.preview.signIn') }}</UiButton>
      <span class="text-sm text-primary-600 underline">{{ t('branding.preview.register') }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Theme } from '~/composables/useTheme'

interface Props {
  displayName: string
  logoUrl: string | null
  theme: Theme
  styleVars: Record<string, string>
}
defineProps<Props>()
const { t } = useI18n()
</script>
