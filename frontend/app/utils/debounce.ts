/** Trailing-edge debounce with cancel. The caller cancels it on unmount (onScopeDispose). */
export function debounce<Args extends unknown[]>(fn: (...args: Args) => void, ms: number) {
  let timer: ReturnType<typeof setTimeout> | undefined
  return {
    run(...args: Args): void {
      if (timer !== undefined) clearTimeout(timer)
      timer = setTimeout(() => { timer = undefined; fn(...args) }, ms)
    },
    cancel(): void {
      if (timer !== undefined) clearTimeout(timer)
      timer = undefined
    },
  }
}
