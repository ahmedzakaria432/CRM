<template>
  <div class="flex flex-col gap-1">
    <label :for="id" class="text-sm font-medium text-fg">
      {{ label }}
      <span v-if="required" aria-hidden="true" class="text-danger-600"> *</span>
    </label>
    <div class="relative">
      <select
        :id="id"
        v-model="model"
        :disabled="disabled"
        :required="required"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="describedBy"
        v-bind="$attrs"
        class="w-full appearance-none rounded-md border border-border bg-surface py-2 ps-3 pe-8 text-sm text-fg ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option
          v-for="option in options"
          :key="option.value"
          :value="option.value"
          :disabled="option.disabled"
        >
          {{ option.label }}
        </option>
      </select>
      <span
        aria-hidden="true"
        class="pointer-events-none absolute end-3 top-1/2 -translate-y-1/2 text-fg-muted"
        >▾</span
      >
    </div>
    <p v-if="hint && !error" :id="hintId" class="text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Option {
  value: string
  label: string
  disabled?: boolean
}
interface Props {
  label: string
  hint?: string
  error?: string
  disabled?: boolean
  required?: boolean
  placeholder?: string
  options: ReadonlyArray<Option>
}
defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<Props>(), {
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
