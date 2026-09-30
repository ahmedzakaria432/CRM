<template>
  <dialog
    ref="dialogRef"
    :aria-labelledby="titleId"
    class="fixed inset-y-0 m-0 hidden h-full max-h-none flex-col bg-surface open:flex p-0 text-fg backdrop:bg-overlay"
    :class="[
      side === 'start' ? 'start-0 end-auto border-e border-border' : 'end-0 start-auto border-s border-border',
      size === 'sm' ? 'w-64 max-w-full' : size === 'lg' ? 'w-full max-w-[28rem]' : 'w-full max-w-80',
    ]"
    @close="handleClose"
    @click="handleBackdropClick"
  >
    <div class="flex h-full flex-col gap-4 overflow-y-auto p-4" @click.stop>
      <div class="flex items-start justify-between gap-4">
        <h2 :id="titleId" class="text-lg font-semibold">{{ title }}</h2>
        <UiButton variant="ghost" size="sm" :aria-label="closeLabel" @click="handleClose">✕</UiButton>
      </div>
      <div class="flex-1"><slot /></div>
      <div v-if="$slots.footer" class="flex justify-end gap-2">
        <slot name="footer" />
      </div>
    </div>
  </dialog>
</template>

<script setup lang="ts">
interface Props {
  title: string
  closeLabel: string
  size?: 'sm' | 'md' | 'lg'
  side?: 'start' | 'end'
}
withDefaults(defineProps<Props>(), { size: 'md', side: 'end' })
const open = defineModel<boolean>('open', { default: false })

const dialogRef = useTemplateRef<HTMLDialogElement>('dialogRef')
const titleId = useId()
let opener: HTMLElement | null = null

watch(
  open,
  (value) => {
    if (!import.meta.client) return
    const dialog = dialogRef.value
    if (dialog === null) return
    if (value) {
      opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
      if (!dialog.open) dialog.showModal()
    } else if (dialog.open) {
      dialog.close()
    }
  },
  { immediate: true },
)

function handleClose(): void {
  open.value = false
  opener?.focus()
}

function handleBackdropClick(event: MouseEvent): void {
  if (dialogRef.value !== null && event.target === dialogRef.value) {
    handleClose()
  }
}
</script>
