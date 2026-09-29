<template>
  <div class="flex flex-col gap-1">
    <label :for="id" class="text-sm font-medium text-fg">
      {{ label }}
      <span v-if="required" aria-hidden="true" class="text-danger-600"> *</span>
    </label>
    <textarea
      :id="id"
      v-model="model"
      :rows="rows"
      :disabled="disabled"
      :required="required"
      :placeholder="placeholder"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="describedBy"
      v-bind="$attrs"
      class="rounded-md border border-border bg-surface px-3 py-2 text-sm text-fg placeholder:text-fg-muted ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
    />
    <p v-if="hint && !error" :id="hintId" class="text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label: string
  rows?: number
  hint?: string
  error?: string
  disabled?: boolean
  required?: boolean
  placeholder?: string
}
defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<Props>(), {
  rows: 4,
  hint: undefined,
  error: undefined,
  disabled: false,
  required: false,
  placeholder: undefined,
})
const model = defineModel<string>()

const id = useId()
const hintId = computed(() => (props.hint ? `${id}-hint` : undefined))
const errorId = computed(() => (props.error ? `${id}-error` : undefined))
const describedBy = computed(() => [hintId.value, errorId.value].filter(Boolean).join(' ') || undefined)
</script>
