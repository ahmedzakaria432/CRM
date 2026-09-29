<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ shellCopy.nav.home }}</h1>
    <UiEmptyState class="mt-6" title="Nothing here yet" description="Ticket, customer, and queue screens arrive in later stories." />
  </div>
</template>

<script setup lang="ts">
import { shellCopy } from '~/copy/en'

definePageMeta({ layout: 'agent' })
</script>
