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
            class="p-2 font-medium text-fg"
            :class="column.align === 'end' ? 'text-end' : 'text-start'"
          >
            {{ column.label }}
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
interface Column {
  key: string
  label: string
  align?: 'start' | 'end'
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

const skeletonRows = computed(() => Array.from({ length: 5 }, (_, index) => index))
</script>
