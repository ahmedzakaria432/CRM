<template>
  <img v-if="logoUrl" :src="logoUrl" :alt="name" class="h-8 w-auto min-w-0 max-w-40 shrink object-contain" />
  <span v-else class="truncate text-lg font-semibold">{{ name }}</span>
</template>

<script setup lang="ts">
// The organisation's logo (or its name) in a shell header. Logos are shown only through <img> (Story 58, S2).
const { t, locale } = useI18n()
const branding = useBrandingStore()
const { theme } = useTheme()

const name = computed(() => branding.displayName(locale.value) ?? t('shell.appName'))
const logoUrl = computed(() => branding.logoUrl(theme.value))
</script>
