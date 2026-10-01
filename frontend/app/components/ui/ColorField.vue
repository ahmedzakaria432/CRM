<template>
  <div class="flex flex-col gap-1">
    <label :for="id" class="text-sm font-medium text-fg">
      {{ label }}
      <span v-if="required" aria-hidden="true" class="text-danger-600"> *</span>
    </label>
    <div class="flex items-center gap-2">
      <input
        type="color"
        :value="swatchValue"
        :disabled="disabled"
        :aria-label="label"
        class="h-10 w-12 shrink-0 cursor-pointer rounded-md border border-border bg-surface ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
        @input="onPick"
      />
      <input
        :id="id"
        v-model="text"
        type="text"
        dir="ltr"
        maxlength="7"
        autocomplete="off"
        spellcheck="false"
        :disabled="disabled"
        :required="required"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="describedBy"
        v-bind="$attrs"
        class="w-full min-w-0 rounded-md border border-border bg-surface px-3 py-2 font-mono text-sm text-fg placeholder:text-fg-muted ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
        @input="onType"
      />
    </div>
    <p v-if="hint && !error" :id="hintId" class="text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label: string
  hint?: string
  error?: string
  disabled?: boolean
  required?: boolean
}
defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<Props>(), {
  hint: undefined,
  error: undefined,
  disabled: false,
  required: false,
})
// #RRGGBB. Partial typing stays local to the text box: the model only changes on a complete, valid value.
const model = defineModel<string>()

const hexPattern = /^#[0-9a-fA-F]{6}$/
const text = ref(model.value ?? '')

watch(model, (value) => {
  if (value !== undefined && value.toUpperCase() !== text.value.toUpperCase()) text.value = value
})

// <input type="color"> only accepts lower-case #rrggbb; without a valid value the browser shows its own default.
const swatchValue = computed(() => (hexPattern.test(text.value) ? text.value.toLowerCase() : undefined))

function onType(): void {
  if (hexPattern.test(text.value)) model.value = text.value.toUpperCase()
}

function onPick(event: Event): void {
  if (!(event.target instanceof HTMLInputElement)) return
  text.value = event.target.value.toUpperCase()
  model.value = text.value
}

const id = useId()
const hintId = computed(() => (props.hint ? `${id}-hint` : undefined))
const errorId = computed(() => (props.error ? `${id}-error` : undefined))
const describedBy = computed(() => [hintId.value, errorId.value].filter(Boolean).join(' ') || undefined)
</script>
