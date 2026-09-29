<template>
  <span
    aria-hidden="true"
    class="inline-block animate-spin rounded-full border-current border-t-transparent"
    :class="sizeClasses"
  />
</template>

<script setup lang="ts">
interface Props {
  size?: 'sm' | 'md' | 'lg'
}
const props = withDefaults(defineProps<Props>(), { size: 'md' })

const sizeClasses = computed(
  () =>
    ({
      sm: 'size-4 border-2',
      md: 'size-5 border-2',
      lg: 'size-6 border-[3px]',
    })[props.size],
)
</script>
