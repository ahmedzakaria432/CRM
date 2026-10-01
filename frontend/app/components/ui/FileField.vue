<template>
  <div class="flex flex-col gap-1">
    <span :id="labelId" class="text-sm font-medium text-fg">{{ label }}</span>
    <input
      ref="inputRef"
      type="file"
      :accept="accept"
      :disabled="disabled || busy"
      class="sr-only"
      tabindex="-1"
      aria-hidden="true"
      @change="onChange"
    />
    <div>
      <UiButton
        variant="secondary"
        size="sm"
        :loading="busy"
        :disabled="disabled"
        :aria-labelledby="`${labelId} ${buttonId}`"
        :aria-describedby="describedBy"
        :aria-invalid="error ? 'true' : undefined"
        @click="inputRef?.click()"
      >
        <span :id="buttonId">{{ buttonLabel }}</span>
      </UiButton>
    </div>
    <p v-if="hint && !error" :id="hintId" class="text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  label: string
  buttonLabel: string
  accept?: string
  hint?: string
  error?: string
  disabled?: boolean
  busy?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  accept: undefined,
  hint: undefined,
  error: undefined,
  disabled: false,
  busy: false,
})
const emit = defineEmits<{ select: [file: File] }>()

const inputRef = useTemplateRef<HTMLInputElement>('inputRef')

function onChange(): void {
  const input = inputRef.value
  const file = input?.files?.[0]
  if (file) emit('select', file)
  // Reset so picking the same file twice still fires a change.
  if (input) input.value = ''
}

const id = useId()
const labelId = `${id}-label`
const buttonId = `${id}-button`
const hintId = computed(() => (props.hint ? `${id}-hint` : undefined))
const errorId = computed(() => (props.error ? `${id}-error` : undefined))
const describedBy = computed(() => [hintId.value, errorId.value].filter(Boolean).join(' ') || undefined)
</script>
