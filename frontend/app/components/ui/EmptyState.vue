<template>
  <div class="flex flex-col items-center gap-3 p-8 text-center">
    <div v-if="$slots.icon" aria-hidden="true" class="text-fg-muted">
      <slot name="icon" />
    </div>
    <component :is="headingTag" class="text-base font-semibold text-fg">{{ title }}</component>
    <p v-if="description" class="max-w-prose text-sm text-fg-muted">{{ description }}</p>
    <div v-if="$slots.actions" class="mt-2 flex gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string
  description?: string
  headingLevel?: 2 | 3
}
const props = withDefaults(defineProps<Props>(), { description: undefined, headingLevel: 2 })

const headingTag = computed(() => `h${props.headingLevel}` as const)
</script>
