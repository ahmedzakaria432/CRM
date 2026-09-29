<template>
  <span
    class="inline-flex items-center justify-center overflow-hidden rounded-full bg-primary-100 font-medium text-primary-800"
    :class="sizeClasses"
    role="img"
    :aria-label="name"
  >
    <img v-if="showImage" :src="src" alt="" class="size-full object-cover" @error="handleError" />
    <span v-else aria-hidden="true">{{ initials }}</span>
  </span>
</template>

<script setup lang="ts">
interface Props {
  name: string
  src?: string
  size?: 'sm' | 'md' | 'lg'
}
const props = withDefaults(defineProps<Props>(), { src: undefined, size: 'md' })

const failed = ref(false)
const showImage = computed(() => Boolean(props.src) && !failed.value)

// Array.from, not word[0]: takes the first code point, so an Arabic combining mark or a surrogate-pair
// character is never split.
const initials = computed(() => {
  const words = props.name.trim().split(/\s+/).filter(Boolean).slice(0, 2)
  return words
    .map((word) => Array.from(word)[0] ?? '')
    .join('')
    .toUpperCase()
})

const sizeClasses = computed(
  () =>
    ({
      sm: 'size-8 text-xs',
      md: 'size-10 text-sm',
      lg: 'size-12 text-base',
    })[props.size],
)

function handleError(): void {
  failed.value = true
}
</script>
