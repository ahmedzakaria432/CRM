<template>
  <NuxtLayout name="portal">
    <UiEmptyState v-if="error.statusCode === 404" :title="shellCopy.error.title" :description="description">
      <template #actions>
        <UiButton @click="goHome">{{ shellCopy.error.home }}</UiButton>
      </template>
    </UiEmptyState>
    <UiErrorState
      v-else
      :title="shellCopy.error.title"
      :description="description"
      :retry-label="shellCopy.error.retry"
      @retry="goHome"
    />
  </NuxtLayout>
</template>

<script setup lang="ts">
import type { NuxtError } from '#app'
import { shellCopy } from '~/copy/en'

const props = defineProps<{ error: NuxtError }>()

// Never render error.stack or error.message for a 5xx in production; import.meta.dev may show the message.
const description = computed(() => (import.meta.dev ? props.error.message : undefined))

function goHome(): void {
  clearError({ redirect: '/' })
}
</script>
