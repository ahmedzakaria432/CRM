<template>
  <span v-if="variant === 'text'" aria-hidden="true" class="block space-y-2">
    <span
      v-for="line in lineArray"
      :key="line"
      class="block h-3 w-full rounded-sm bg-neutral-200 motion-safe:animate-pulse"
    />
  </span>
  <span
    v-else
    aria-hidden="true"
    class="block bg-neutral-200 motion-safe:animate-pulse"
    :class="variant === 'circle' ? 'aspect-square rounded-full' : 'h-24 w-full rounded-md'"
  />
</template>

<script setup lang="ts">
interface Props {
  variant?: 'text' | 'rect' | 'circle'
  lines?: number
}
const props = withDefaults(defineProps<Props>(), { variant: 'text', lines: 1 })

const lineArray = computed(() => Array.from({ length: Math.max(1, props.lines) }, (_, index) => index))
</script>
