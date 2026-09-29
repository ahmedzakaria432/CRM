<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ shellCopy.appName }}</h1>
    <p class="mt-4 max-w-prose text-fg-muted">
      A multi-department, multi-branch, bilingual customer support CRM.
    </p>
  </div>
</template>

<script setup lang="ts">
import { shellCopy } from '~/copy/en'

// The Lighthouse accessibility target (Done Criteria): portal layout, SSR, no API call.
definePageMeta({ layout: 'portal' })
</script>
