export interface Toast {
  id: string
  tone: 'info' | 'success' | 'warning' | 'danger'
  title: string
  description: string | null
  correlationId: string | null
  timeoutMs: number
}

export interface PushToastInput {
  tone: Toast['tone']
  title: string
  description?: string | null
  correlationId?: string | null
  /** Defaults to 5000ms, or 0 (stays until dismissed) for tone 'danger'. */
  timeoutMs?: number
}

const maxVisible = 5

export const useToasts = defineStore('toasts', () => {
  const toasts = ref<Toast[]>([])

  function push(input: PushToastInput): string {
    const id = crypto.randomUUID()
    const timeoutMs = input.timeoutMs ?? (input.tone === 'danger' ? 0 : 5000)
    toasts.value.push({
      id,
      tone: input.tone,
      title: input.title,
      description: input.description ?? null,
      correlationId: input.correlationId ?? null,
      timeoutMs,
    })
    if (toasts.value.length > maxVisible) {
      toasts.value.splice(0, toasts.value.length - maxVisible)
    }
    // Timers start only on the client: SSR has no timers to clean up and no user to see them expire.
    if (import.meta.client && timeoutMs > 0) {
      setTimeout(() => dismiss(id), timeoutMs)
    }
    return id
  }

  function dismiss(id: string): void {
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
  }

  function clear(): void {
    toasts.value = []
  }

  return { toasts, push, dismiss, clear }
})
