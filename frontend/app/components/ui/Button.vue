<template>
  <button
    :type="type"
    :disabled="isDisabled"
    :aria-busy="loading"
    :class="[
      'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50',
      sizeClasses,
      variantClasses,
      block ? 'w-full' : '',
    ]"
  >
    <UiSpinner v-if="loading" size="sm" />
    <span><slot /></span>
  </button>
</template>

<script setup lang="ts">
interface Props {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  type?: 'button' | 'submit' | 'reset'
  disabled?: boolean
  loading?: boolean
  block?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  type: 'button',
  disabled: false,
  loading: false,
  block: false,
})

const isDisabled = computed(() => props.disabled || props.loading)

const sizeClasses = computed(
  () =>
    ({
      sm: 'px-2.5 py-1.5 text-xs',
      md: 'px-3 py-2 text-sm',
      lg: 'px-4 py-2.5 text-base',
    })[props.size],
)

const variantClasses = computed(
  () =>
    ({
      primary: 'bg-primary-600 text-fg-inverse ui-hover:bg-primary-700',
      secondary: 'border border-border bg-surface-raised text-fg ui-hover:bg-surface-sunken',
      ghost: 'bg-transparent text-fg ui-hover:bg-surface-raised',
      danger: 'bg-danger-600 text-fg-inverse ui-hover:bg-danger-700',
    })[props.variant],
)
</script>
