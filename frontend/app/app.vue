<template>
  <NuxtLoadingIndicator color="var(--crm-primary-500)" />
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToastRegion :dismiss-label="t('shell.toast.dismiss')" :reference-label="t('shell.error.reference')" />
</template>

<script setup lang="ts">
const { t } = useI18n()
</script>
