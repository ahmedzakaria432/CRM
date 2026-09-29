<template>
  <UiButton v-if="target" variant="ghost" size="sm" :aria-label="t('shell.locale.switchTo', { language: target.name })" @click="setLocale(target.code)">
    <span :lang="target.code">{{ target.name }}</span>
  </UiButton>
</template>

<script setup lang="ts">
// A shell component: it may use useI18n directly. The app/components/ui/ kit still contains no copy and no
// i18n - every kit label comes from a prop.
const { t, locale, locales, setLocale } = useI18n()

const target = computed(() => {
  for (const entry of locales.value) {
    if (typeof entry === 'object' && entry.code !== locale.value) {
      return entry
    }
  }
  return undefined
})
</script>
