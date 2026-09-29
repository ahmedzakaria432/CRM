<template>
  <span
    class="inline-flex items-center rounded-full px-2 py-0.5 font-medium"
    :class="[toneClasses, size === 'sm' ? 'text-xs' : 'text-sm']"
  >
    <slot />
  </span>
</template>

<script setup lang="ts">
interface Props {
  tone?: 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info'
  size?: 'sm' | 'md'
}
const props = withDefaults(defineProps<Props>(), { tone: 'neutral', size: 'md' })

const toneClasses = computed(
  () =>
    ({
      neutral: 'bg-neutral-100 text-neutral-800',
      primary: 'bg-primary-100 text-primary-800',
      success: 'bg-success-50 text-success-700',
      warning: 'bg-warning-50 text-warning-700',
      danger: 'bg-danger-50 text-danger-700',
      info: 'bg-info-50 text-info-700',
    })[props.tone],
)
</script>
