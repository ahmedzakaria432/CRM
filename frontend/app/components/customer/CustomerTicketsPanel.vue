<template>
  <!-- Story 20 (ticket UI) replaces the body with this customer's tickets; the section shell stays. -->
  <section :aria-labelledby="headingId" class="rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('customers.tickets.title') }}</h2>
    <UiEmptyState
      :heading-level="3"
      :title="t('customers.tickets.empty')"
      :description="t('customers.tickets.emptyDescription')"
    />
  </section>
</template>

<script setup lang="ts">
const { t } = useI18n()
const headingId = useId()
</script>
