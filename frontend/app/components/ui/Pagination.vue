<template>
  <nav :aria-label="label" class="flex items-center justify-center gap-1">
    <button
      type="button"
      :disabled="page <= 1"
      class="rounded-md px-2 py-1 text-sm ui-focus:focus-ring ui-hover:bg-surface-raised disabled:cursor-not-allowed disabled:opacity-50"
      :aria-label="previousLabel"
      @click="goTo(page - 1)"
    >
      ‹
    </button>
    <template v-for="(item, index) in items" :key="item.type === 'page' ? `p-${item.value}` : `e-${index}`">
      <span v-if="item.type === 'ellipsis'" aria-hidden="true" class="px-2 text-fg-muted">…</span>
      <button
        v-else
        type="button"
        class="rounded-md px-3 py-1 text-sm ui-focus:focus-ring ui-hover:bg-surface-raised"
        :class="item.value === page ? 'bg-primary-600 text-fg-inverse' : 'text-fg'"
        :aria-current="item.value === page ? 'page' : undefined"
        :aria-label="`${pageLabel} ${item.value}`"
        @click="goTo(item.value)"
      >
        {{ item.value }}
      </button>
    </template>
    <button
      type="button"
      :disabled="page >= pageCount"
      class="rounded-md px-2 py-1 text-sm ui-focus:focus-ring ui-hover:bg-surface-raised disabled:cursor-not-allowed disabled:opacity-50"
      :aria-label="nextLabel"
      @click="goTo(page + 1)"
    >
      ›
    </button>
  </nav>
</template>

<script setup lang="ts">
interface Props {
  pageSize: number
  total: number
  label: string
  previousLabel: string
  nextLabel: string
  pageLabel: string
}
const props = defineProps<Props>()
const page = defineModel<number>('page', { default: 1 })

const pageCount = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))

type PageItem = { type: 'page'; value: number } | { type: 'ellipsis' }

// Windowed list: first, last, current, and one neighbour on each side; gaps become an ellipsis.
const items = computed<PageItem[]>(() => {
  const count = pageCount.value
  const current = page.value
  const windowRadius = 1
  const pages = new Set<number>([1, count, current])
  for (let offset = -windowRadius; offset <= windowRadius; offset++) {
    const candidate = current + offset
    if (candidate >= 1 && candidate <= count) pages.add(candidate)
  }
  const sorted = Array.from(pages).sort((a, b) => a - b)
  const result: PageItem[] = []
  let previous: number | null = null
  for (const value of sorted) {
    if (previous !== null && value - previous > 1) {
      result.push({ type: 'ellipsis' })
    }
    result.push({ type: 'page', value })
    previous = value
  }
  return result
})

function goTo(value: number): void {
  if (value < 1 || value > pageCount.value) return
  page.value = value
}
</script>
