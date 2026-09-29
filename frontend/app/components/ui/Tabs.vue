<template>
  <div>
    <div role="tablist" :aria-label="label" class="flex gap-1 border-b border-border">
      <button
        v-for="tab in tabs"
        :id="`${tab.id}-tab`"
        :key="tab.id"
        :ref="(el) => setTabRef(tab.id, el)"
        type="button"
        role="tab"
        :aria-selected="isActive(tab)"
        :aria-controls="`${tab.id}-panel`"
        :tabindex="isActive(tab) ? 0 : -1"
        :disabled="tab.disabled"
        class="rounded-t-md px-3 py-2 text-sm font-medium ui-focus:focus-ring disabled:cursor-not-allowed disabled:opacity-50"
        :class="isActive(tab) ? 'border-b-2 border-primary-600 text-primary-700' : 'text-fg-muted ui-hover:text-fg'"
        @click="selectTab(tab)"
        @keydown="handleKeydown($event, tab)"
      >
        {{ tab.label }}
      </button>
    </div>
    <div
      v-for="tab in tabs"
      v-show="isActive(tab)"
      :id="`${tab.id}-panel`"
      :key="tab.id"
      role="tabpanel"
      :aria-labelledby="`${tab.id}-tab`"
      tabindex="0"
      class="p-3"
    >
      <slot :id="tab.id" name="panel" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Tab {
  id: string
  label: string
  disabled?: boolean
}
interface Props {
  tabs: ReadonlyArray<Tab>
  label: string
}
const props = defineProps<Props>()
// Automatic activation: the active tab id. Story 05 swaps ArrowLeft/ArrowRight under dir="rtl".
const active = defineModel<string>()

const tabRefs = new Map<string, HTMLButtonElement>()

function setTabRef(id: string, el: Element | ComponentPublicInstance | null): void {
  if (el instanceof HTMLButtonElement) {
    tabRefs.set(id, el)
  } else {
    tabRefs.delete(id)
  }
}

function isActive(tab: Tab): boolean {
  return active.value === tab.id || (active.value === undefined && props.tabs[0]?.id === tab.id)
}

function selectTab(tab: Tab): void {
  if (tab.disabled) return
  active.value = tab.id
}

function focusTab(id: string): void {
  tabRefs.get(id)?.focus()
}

function enabledTabs(): ReadonlyArray<Tab> {
  return props.tabs.filter((tab) => !tab.disabled)
}

function handleKeydown(event: KeyboardEvent, tab: Tab): void {
  const enabled = enabledTabs()
  const index = enabled.findIndex((candidate) => candidate.id === tab.id)
  if (index === -1) return

  let nextIndex: number | null = null
  switch (event.key) {
    case 'ArrowRight':
      nextIndex = (index + 1) % enabled.length
      break
    case 'ArrowLeft':
      nextIndex = (index - 1 + enabled.length) % enabled.length
      break
    case 'Home':
      nextIndex = 0
      break
    case 'End':
      nextIndex = enabled.length - 1
      break
    default:
      return
  }
  event.preventDefault()
  const next = enabled[nextIndex]
  if (next === undefined) return
  selectTab(next)
  focusTab(next.id)
}
</script>
