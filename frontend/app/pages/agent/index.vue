<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ t('shell.nav.home') }}</h1>
    <UiEmptyState class="mt-6" :title="t('shell.agentHome.emptyTitle')" :description="t('shell.agentHome.emptyDescription')" />
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'agent' })

const { t } = useI18n()
</script>
