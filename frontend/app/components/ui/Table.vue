<template>
  <div class="overflow-x-auto rounded-md border border-border">
    <table class="w-full border-collapse text-start text-sm">
      <caption class="sr-only">
        {{
          caption
        }}
      </caption>
      <thead>
        <tr class="border-b border-border bg-surface-raised">
          <th
            v-for="column in columns"
            :key="column.key"
            scope="col"
            :aria-sort="column.sortable ? ariaSort(column.key) : undefined"
            class="p-2 font-medium text-fg"
            :class="column.align === 'end' ? 'text-end' : 'text-start'"
          >
            <button
              v-if="column.sortable"
              type="button"
              class="inline-flex items-center gap-1 rounded-sm font-medium text-fg ui-focus:focus-ring ui-hover:underline"
              @click="toggleSort(column.key)"
            >
              {{ column.label }}<span aria-hidden="true" class="text-fg-muted">{{ sortGlyph(column.key) }}</span>
            </button>
            <template v-else>{{ column.label }}</template>
          </th>
        </tr>
      </thead>
      <tbody :aria-busy="loading">
        <template v-if="loading">
          <tr v-for="row in skeletonRows" :key="row" class="border-b border-border last:border-0">
            <td v-for="column in columns" :key="column.key" class="p-2">
              <UiSkeleton variant="text" />
            </td>
          </tr>
        </template>
        <template v-else-if="rows.length === 0">
          <tr>
            <td :colspan="columns.length" class="p-4">
              <slot name="empty" />
            </td>
          </tr>
        </template>
        <template v-else>
          <tr v-for="row in rows" :key="rowKey(row)" class="border-b border-border last:border-0">
            <td
              v-for="column in columns"
              :key="column.key"
              class="p-2"
              :class="column.align === 'end' ? 'text-end' : 'text-start'"
            >
              <slot :name="`cell-${column.key}`" :row="row" />
            </td>
          </tr>
        </template>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts" generic="Row">
import type { TableSort } from '~/utils/table'

interface Column {
  key: string
  label: string
  align?: 'start' | 'end'
  sortable?: boolean
}
interface Props {
  columns: ReadonlyArray<Column>
  rows: ReadonlyArray<Row>
  rowKey: (row: Row) => string
  /** Always visually hidden (sr-only): a visible table heading belongs above the table, not in it. */
  caption: string
  loading?: boolean
}
withDefaults(defineProps<Props>(), { loading: false })
const sort = defineModel<TableSort | undefined>('sort', { default: undefined })

function ariaSort(key: string): 'none' | 'ascending' | 'descending' {
  const current = sort.value
  if (current === undefined || current.key !== key) return 'none'
  return current.descending ? 'descending' : 'ascending'
}

function sortGlyph(key: string): string {
  const current = sort.value
  if (current === undefined || current.key !== key) return '↕'
  return current.descending ? '↓' : '↑'
}

function toggleSort(key: string): void {
  const current = sort.value
  sort.value =
    current !== undefined && current.key === key
      ? { key, descending: !current.descending }
      : { key, descending: false }
}

const skeletonRows = computed(() => Array.from({ length: 5 }, (_, index) => index))
</script>
