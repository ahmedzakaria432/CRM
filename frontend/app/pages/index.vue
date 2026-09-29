<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ t('shell.appName') }}</h1>
    <p class="mt-4 max-w-prose text-fg-muted">
      {{ t('shell.home.tagline') }}
    </p>
  </div>
</template>

<script setup lang="ts">
// The Lighthouse accessibility target (Done Criteria): portal layout, SSR, no API call.
definePageMeta({ layout: 'portal' })

const { t } = useI18n()
</script>
