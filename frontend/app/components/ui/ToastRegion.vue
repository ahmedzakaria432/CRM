<template>
  <div class="pointer-events-none fixed bottom-4 end-4 z-50 flex w-full max-w-sm flex-col gap-2">
    <div role="status" aria-live="polite" class="flex flex-col gap-2">
      <UiToast
        v-for="toast in nonDanger"
        :key="toast.id"
        class="pointer-events-auto"
        :tone="toast.tone"
        :title="toast.title"
        :description="toast.description ?? undefined"
        :correlation-id="toast.correlationId ?? undefined"
        :dismiss-label="dismissLabel"
        :reference-label="referenceLabel"
        @dismiss="toastsStore.dismiss(toast.id)"
      />
    </div>
    <div role="alert" class="flex flex-col gap-2">
      <UiToast
        v-for="toast in danger"
        :key="toast.id"
        class="pointer-events-auto"
        :tone="toast.tone"
        :title="toast.title"
        :description="toast.description ?? undefined"
        :correlation-id="toast.correlationId ?? undefined"
        :dismiss-label="dismissLabel"
        :reference-label="referenceLabel"
        @dismiss="toastsStore.dismiss(toast.id)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  dismissLabel: string
  referenceLabel: string
}
defineProps<Props>()

const toastsStore = useToasts()
const nonDanger = computed(() => toastsStore.toasts.filter((toast) => toast.tone !== 'danger'))
const danger = computed(() => toastsStore.toasts.filter((toast) => toast.tone === 'danger'))
</script>
