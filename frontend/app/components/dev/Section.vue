<template>
  <section :id="anchorId" class="scroll-mt-24 border-b border-border py-8 last:border-0">
    <h2 class="mb-4 text-lg font-semibold text-fg">{{ title }}</h2>
    <div class="grid gap-4 md:grid-cols-2">
      <DevThemeColumn theme="light">
        <slot />
      </DevThemeColumn>
      <DevThemeColumn theme="dark">
        <slot />
      </DevThemeColumn>
    </div>
  </section>
</template>

<script setup lang="ts">
// Renders the same default-slot markup twice, once per forced data-theme (Probe 5: @theme inline lets a
// nested [data-theme] subtree re-resolve independently of the page's own theme cookie).
defineProps<{ title: string; anchorId: string }>()
</script>
