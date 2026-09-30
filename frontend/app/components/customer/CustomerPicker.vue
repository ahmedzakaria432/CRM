<template>
  <div class="flex flex-col gap-1">
    <label :for="inputId" class="text-sm font-medium text-fg">
      {{ label }}
      <span v-if="required" aria-hidden="true" class="text-danger-600"> *</span>
    </label>
    <div class="relative">
      <input
        :id="inputId"
        ref="inputRef"
        v-model="term"
        type="text"
        role="combobox"
        autocomplete="off"
        aria-autocomplete="list"
        :aria-expanded="open"
        :aria-controls="listboxId"
        :aria-activedescendant="open && activeIndex >= 0 ? optionId(activeIndex) : undefined"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="describedBy"
        :aria-required="required ? 'true' : undefined"
        :disabled="disabled"
        :placeholder="t('customers.picker.placeholder')"
        class="w-full rounded-md border border-border bg-surface py-2 ps-3 text-sm text-fg placeholder:text-fg-muted ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
        :class="model ? 'pe-16' : 'pe-3'"
        @input="onInput()"
        @keydown="onKeydown"
        @blur="open = false"
      />
      <UiButton
        v-if="model && !disabled"
        size="sm"
        variant="ghost"
        class="absolute end-1 top-1/2 -translate-y-1/2"
        @click="clear()"
      >
        {{ t('customers.picker.clear') }}
      </UiButton>
      <ul
        v-show="open"
        :id="listboxId"
        role="listbox"
        :aria-label="label"
        class="absolute inset-x-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-md border border-border bg-surface shadow-lg"
      >
        <li
          v-for="(item, index) in results"
          :id="optionId(index)"
          :key="item.id"
          role="option"
          :aria-selected="index === activeIndex"
          class="cursor-pointer px-3 py-2 text-sm"
          :class="index === activeIndex ? 'bg-surface-raised' : ''"
          @mousedown.prevent="select(item)"
        >
          <div class="flex items-baseline justify-between gap-2">
            <span class="min-w-0 break-words font-medium text-fg">{{ localizedName(item.displayName, locale) }}</span>
            <bdi dir="ltr" class="shrink-0 font-mono text-xs text-fg-muted">{{ item.number }}</bdi>
          </div>
          <div v-if="item.primaryEmail || item.primaryPhone" class="text-xs text-fg-muted">
            <bdi dir="ltr">{{ item.primaryEmail || item.primaryPhone }}</bdi>
          </div>
        </li>
        <li v-if="state === 'loading'" role="presentation" class="flex items-center gap-2 px-3 py-2 text-sm text-fg-muted">
          <UiSpinner size="sm" />
          <span>{{ t('customers.picker.searching') }}</span>
        </li>
        <li
          v-else-if="state === 'ready' && results.length === 0"
          role="presentation"
          class="px-3 py-2 text-sm text-fg-muted"
        >
          {{ t('customers.picker.empty') }}
        </li>
        <li
          v-else-if="state === 'error'"
          role="presentation"
          class="flex items-center justify-between gap-2 px-3 py-2 text-sm text-danger-600"
        >
          <span>{{ t('customers.picker.error') }}</span>
          <UiButton size="sm" variant="secondary" @mousedown.prevent="run()">{{ t('customers.picker.retry') }}</UiButton>
        </li>
      </ul>
    </div>
    <p class="sr-only" aria-live="polite">{{ announcement }}</p>
    <p v-if="hint && !error" :id="hintId" class="text-xs text-fg-muted">{{ hint }}</p>
    <p v-if="error" :id="errorId" role="alert" class="text-xs text-danger-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { useCustomerLookup } from '~/composables/customers/useCustomerLookup'
import type { CustomerLookupDto } from '~/composables/customers/types'
import { debounce } from '~/utils/debounce'
import { localizedName } from '~/utils/scope'

interface Props {
  label: string
  hint?: string
  error?: string
  disabled?: boolean
  required?: boolean
}
const props = withDefaults(defineProps<Props>(), { hint: undefined, error: undefined, disabled: false, required: false })
const model = defineModel<CustomerLookupDto | null>({ default: null })

const { t, locale } = useI18n()
const format = useFormat()
const lookup = useCustomerLookup()
const inputRef = useTemplateRef<HTMLInputElement>('inputRef')

const term = ref('')
const open = ref(false)
const results = ref<CustomerLookupDto[]>([])
const state = ref<'idle' | 'loading' | 'ready' | 'error'>('idle')
const activeIndex = ref(-1)

const inputId = useId()
const listboxId = useId()
const hintId = computed(() => (props.hint ? `${inputId}-hint` : undefined))
const errorId = computed(() => (props.error ? `${inputId}-error` : undefined))
const describedBy = computed(() => [hintId.value, errorId.value].filter(Boolean).join(' ') || undefined)
const optionId = (index: number): string => `${listboxId}-${index}`

function labelOf(item: CustomerLookupDto): string {
  return `${localizedName(item.displayName, locale.value)} · ${item.number ?? ''}`
}

// When a value arrives from outside (v-model), show it.
watch(
  model,
  (value) => {
    if (value !== null) term.value = labelOf(value)
  },
  { immediate: true },
)

let controller: AbortController | null = null
let sequence = 0

async function run(): Promise<void> {
  const value = term.value.trim()
  controller?.abort()
  if (value === '') {
    controller = null
    results.value = []
    state.value = 'idle'
    open.value = false
    return
  }
  const current = new AbortController()
  controller = current
  const mine = ++sequence
  state.value = 'loading'
  open.value = true
  const result = await lookup.search(value, current.signal)
  if (mine !== sequence || current.signal.aborted) return // a newer keystroke owns the list
  if (!result.ok) {
    state.value = 'error'
    results.value = []
    activeIndex.value = -1
    return
  }
  results.value = result.data
  state.value = 'ready'
  activeIndex.value = result.data.length > 0 ? 0 : -1
}
const debounced = debounce(() => void run(), 250)
onScopeDispose(() => {
  debounced.cancel()
  controller?.abort()
})

const announcement = computed(() => {
  if (state.value === 'loading') return t('customers.picker.searching')
  if (state.value === 'error') return t('customers.picker.error')
  if (state.value === 'ready') {
    return results.value.length > 0
      ? t('customers.picker.results', { count: format.number(results.value.length) })
      : t('customers.picker.empty')
  }
  return ''
})

function onInput(): void {
  // Typing replaces the selection.
  if (model.value !== null) model.value = null
  debounced.run()
}

function select(item: CustomerLookupDto): void {
  debounced.cancel()
  controller?.abort()
  sequence++
  model.value = item
  term.value = labelOf(item)
  open.value = false
  activeIndex.value = -1
  state.value = 'idle'
}

function clear(): void {
  debounced.cancel()
  controller?.abort()
  sequence++
  model.value = null
  term.value = ''
  results.value = []
  state.value = 'idle'
  open.value = false
  inputRef.value?.focus()
}

function move(step: 1 | -1): void {
  const count = results.value.length
  if (count === 0) return
  activeIndex.value = (activeIndex.value + step + count) % count
}

function onKeydown(event: KeyboardEvent): void {
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      if (!open.value && results.value.length === 0) void run()
      else {
        open.value = true
        move(1)
      }
      break
    case 'ArrowUp':
      event.preventDefault()
      if (open.value) move(-1)
      break
    case 'Enter': {
      const item = results.value[activeIndex.value]
      if (open.value && item !== undefined) {
        event.preventDefault()
        select(item)
      }
      break
    }
    case 'Escape':
      if (open.value) {
        event.preventDefault()
        open.value = false
      } else if (model.value !== null || term.value !== '') {
        event.preventDefault()
        clear()
      }
      break
    case 'Tab':
      open.value = false
      break
    default:
      break
  }
}
</script>
