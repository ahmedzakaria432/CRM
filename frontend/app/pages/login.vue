<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ shellCopy.login.title }}</h1>
    <p class="mt-4 max-w-prose text-fg-muted">{{ shellCopy.login.pending }}</p>
    <p v-if="redirectTarget" class="mt-2 text-sm text-fg-muted">{{ redirectTarget }}</p>
  </div>
</template>

<script setup lang="ts">
import { shellCopy } from '~/copy/en'

// Placeholder redirect target. Story 07 replaces this file with the real sign-in screen.
definePageMeta({ layout: 'portal' })

const route = useRoute()
const redirectTarget = computed(() => {
  const value = route.query.redirect
  return typeof value === 'string' ? value : undefined
})
</script>
