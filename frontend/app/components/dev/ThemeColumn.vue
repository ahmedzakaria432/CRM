<template>
  <div :data-theme="theme" class="rounded-lg border border-border bg-surface p-4 text-fg">
    <p class="mb-3 text-xs font-semibold uppercase tracking-wide text-fg-muted">{{ theme }}</p>
    <div class="flex flex-col">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
// Dev-only review chrome (excluded from production, modules/dev-only-pages.ts). Not part of the UI kit,
// so it is free to sit outside app/components/ui/.
defineProps<{ theme: 'light' | 'dark' }>()
</script>
