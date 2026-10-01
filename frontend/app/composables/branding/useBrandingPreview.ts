import { debounce } from '~/utils/debounce'
import type { BrandingPreviewDto } from './types'

const hexPattern = /^#[0-9a-fA-F]{6}$/

/** Tokens and WCAG contrast checks for the colours in the form, refreshed 250 ms after the last change. The last good
 * preview stays on screen when a call fails, and a partly typed colour makes no call. */
export function useBrandingPreview(primary: Ref<string>, accent: Ref<string>) {
  const { clients, call } = useApi()
  const preview = ref<BrandingPreviewDto | null>(null)
  const status = ref<'idle' | 'loading' | 'error'>('idle')
  let latest = 0

  async function load(): Promise<void> {
    if (!hexPattern.test(primary.value) || !hexPattern.test(accent.value)) return
    const ticket = ++latest
    status.value = 'loading'
    const result = await call(
      clients.admin.GET('/api/Admin/Branding/preview', {
        params: { query: { primaryColour: primary.value, accentColour: accent.value } },
      }),
      { toastOnError: false },
    )
    if (ticket !== latest) return // a newer request superseded this one
    if (result.ok) {
      preview.value = result.data
      status.value = 'idle'
    } else {
      status.value = 'error'
    }
  }

  const debounced = debounce(() => void load(), 250)
  watch([primary, accent], () => debounced.run(), { immediate: true })
  onScopeDispose(() => debounced.cancel())

  return { preview, status }
}
