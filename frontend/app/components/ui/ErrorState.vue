<template>
  <div role="alert" class="flex flex-col items-center gap-3 p-8 text-center">
    <h2 class="text-base font-semibold text-danger-700">{{ title }}</h2>
    <p v-if="description" class="max-w-prose text-sm text-fg-muted">{{ description }}</p>
    <p v-if="correlationId && referenceLabel" class="font-mono text-xs text-fg-muted">
      {{ referenceLabel }}: {{ correlationId }}
    </p>
    <UiButton v-if="retryLabel" variant="secondary" @click="emit('retry')">{{ retryLabel }}</UiButton>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string
  description?: string
  correlationId?: string
  referenceLabel?: string
  retryLabel?: string
}
defineProps<Props>()
const emit = defineEmits<{ retry: [] }>()
</script>
