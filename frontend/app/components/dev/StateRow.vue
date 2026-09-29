<template>
  <div class="flex flex-col gap-1.5 border-b border-border py-3 last:border-0">
    <span class="text-xs font-medium uppercase tracking-wide text-fg-muted">{{ label }}</span>
    <div class="flex flex-wrap items-center gap-2">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ label: string }>()
</script>
