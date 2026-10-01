<template>
  <!-- The nested data-theme re-resolves the semantic roles; the inline style overrides the primary/accent scales. -->
  <div :data-theme="theme" :style="styleVars" class="overflow-hidden rounded-lg border border-border bg-surface text-fg">
    <div class="flex items-center gap-3 border-b border-border px-3 py-2">
      <img v-if="logoUrl" :src="logoUrl" :alt="displayName" class="h-7 w-auto max-w-32 object-contain" />
      <span v-else class="truncate text-base font-semibold">{{ displayName }}</span>
    </div>
    <div class="flex flex-col sm:flex-row">
      <nav class="flex gap-1 border-b border-border p-2 sm:w-36 sm:shrink-0 sm:flex-col sm:border-e sm:border-b-0">
        <span class="rounded-md border-s-4 border-accent-600 bg-surface-raised px-2 py-1.5 text-sm font-medium">
          {{ t('branding.preview.navTickets') }}
        </span>
        <span class="rounded-md border-s-4 border-transparent px-2 py-1.5 text-sm font-medium">
          {{ t('branding.preview.navCustomers') }}
        </span>
      </nav>
      <div class="flex min-w-0 flex-1 flex-col items-start gap-3 p-3">
        <h3 class="text-base font-semibold">{{ t('branding.preview.heading') }}</h3>
        <p class="text-sm text-fg-muted">{{ t('branding.preview.body') }}</p>
        <div class="flex flex-wrap items-center gap-3">
          <UiBadge tone="primary">{{ t('branding.preview.badge') }}</UiBadge>
          <span class="text-sm text-primary-600 underline">{{ t('branding.preview.link') }}</span>
        </div>
        <UiButton size="sm" tabindex="-1">{{ t('branding.preview.action') }}</UiButton>
      </div>
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
