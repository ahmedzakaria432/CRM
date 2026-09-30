<template>
  <div class="flex flex-col gap-1">
    <div class="flex items-start gap-2">
      <input
        :id="id"
        v-model="model"
        type="checkbox"
        :disabled="disabled"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="describedBy"
        v-bind="$attrs"
        class="mt-0.5 size-4 shrink-0 accent-primary-600 ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
      />
      <label :for="id" class="text-sm text-fg">{{ label }}</label>
    </div>
    <p v-if="hint && !error" :id="hintId" class="ps-6 text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="ps-6 text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label: string
  hint?: string
  error?: string
  disabled?: boolean
}
defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<Props>(), {
  hint: undefined,
  error: undefined,
  disabled: false,
})
const model = defineModel<boolean>({ default: false })

const id = useId()
const hintId = computed(() => (props.hint ? `${id}-hint` : undefined))
const errorId = computed(() => (props.error ? `${id}-error` : undefined))
const describedBy = computed(() => [hintId.value, errorId.value].filter(Boolean).join(' ') || undefined)
</script>
