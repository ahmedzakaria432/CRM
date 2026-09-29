<template>
  <div class="flex w-full items-start gap-3 rounded-md border p-3 shadow-lg" :class="toneClasses">
    <div class="min-w-0 flex-1">
      <p class="break-words font-medium">{{ title }}</p>
      <p v-if="description" class="mt-1 break-words text-sm">{{ description }}</p>
      <p v-if="correlationId" class="mt-1 break-words font-mono text-xs">
        {{ referenceLabel }}: {{ correlationId }}
      </p>
    </div>
    <button
      type="button"
      class="shrink-0 rounded-md p-1 ui-focus:focus-ring ui-hover:opacity-75"
      :aria-label="dismissLabel"
      @click="emit('dismiss')"
    >
      ✕
    </button>
  </div>
</template>

<script setup lang="ts">
interface Props {
  tone: 'info' | 'success' | 'warning' | 'danger'
  title: string
  description?: string
  correlationId?: string
  dismissLabel: string
  referenceLabel: string
}
const props = defineProps<Props>()
const emit = defineEmits<{ dismiss: [] }>()

const toneClasses = computed(
  () =>
    ({
      info: 'border-info-500 bg-info-50 text-info-700',
      success: 'border-success-500 bg-success-50 text-success-700',
      warning: 'border-warning-500 bg-warning-50 text-warning-700',
      danger: 'border-danger-500 bg-danger-50 text-danger-700',
    })[props.tone],
)
</script>
