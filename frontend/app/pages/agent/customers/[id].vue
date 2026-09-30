<template>
  <div>
    <NuxtLink to="/agent/customers" class="text-sm text-primary-600 underline">{{ t('customers.detail.back') }}</NuxtLink>

    <UiEmptyState
      v-if="!valid"
      class="mt-4"
      :title="t('customers.detail.notFound')"
      :description="t('customers.detail.notFoundDescription')"
    />
    <div v-else class="mt-4 flex flex-col gap-4">
      <CustomerHeaderPanel :customer-id="id" />
      <div class="grid gap-4 md:grid-cols-2">
        <CustomerContactsPanel :customer-id="id" />
        <CustomerNotesPanel :customer-id="id" />
      </div>
      <CustomerTicketsPanel />
    </div>
  </div>
</template>

<script setup lang="ts">
import { isGuid } from '~/utils/customers'

definePageMeta({ layout: 'agent', key: (route) => route.path })

const { t } = useI18n()
const route = useRoute()
const id = String(route.params.id)
const valid = isGuid(id)
</script>
