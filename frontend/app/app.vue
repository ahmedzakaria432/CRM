<template>
  <NuxtLoadingIndicator color="var(--crm-primary-500)" />
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToastRegion :dismiss-label="shellCopy.toast.dismiss" :reference-label="shellCopy.error.reference" />
</template>

<script setup lang="ts">
import { shellCopy } from '~/copy/en'
</script>
