<template>
  <dialog
    ref="dialogRef"
    :aria-labelledby="titleId"
    class="w-full rounded-lg border border-border bg-surface p-0 text-fg backdrop:bg-overlay"
    :class="{
      'max-w-sm': size === 'sm',
      'max-w-md': size === 'md',
      'max-w-lg': size === 'lg',
    }"
    @close="handleClose"
    @click="handleBackdropClick"
  >
    <div class="flex flex-col gap-4 p-4" @click.stop>
      <div class="flex items-start justify-between gap-4">
        <h2 :id="titleId" class="text-lg font-semibold">{{ title }}</h2>
        <UiButton variant="ghost" size="sm" :aria-label="closeLabel" @click="handleClose">✕</UiButton>
      </div>
      <div><slot /></div>
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
}
withDefaults(defineProps<Props>(), { size: 'md' })
const open = defineModel<boolean>('open', { default: false })

const dialogRef = useTemplateRef<HTMLDialogElement>('dialogRef')
const titleId = useId()
let opener: HTMLElement | null = null

// Native <dialog>.showModal() gives us the focus trap, Esc handling, and inert background for free. It
// only exists in the browser, so this runs client-side only.
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
