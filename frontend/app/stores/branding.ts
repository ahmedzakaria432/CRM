import type { Theme } from '~/composables/useTheme'
import { brandLogoUrl, buildBrandCss, type PublicBrandingDto } from '~/utils/branding'
import { cachedServerBranding } from '~/utils/brandingServerCache'
import { localizedName } from '~/utils/scope'

/** The organisation's look (Story 58). null means the product defaults (tokens.css): a failed load, an unknown
 * organisation code and an organisation that never branded all end up here, and none of them throws. */
export const useBrandingStore = defineStore('branding', () => {
  const { clients, call } = useApi()

  const branding = ref<PublicBrandingDto | null>(null)
  const css = computed(() => buildBrandCss(branding.value))

  /** Pre-login surfaces: the organisation named by runtimeConfig.public.organisationCode (D5), anonymously. */
  async function loadPublic(): Promise<void> {
    const code = useRuntimeConfig().public.organisationCode
    const load = async (): Promise<PublicBrandingDto | null> => {
      try {
        const result = await call(
          clients.public.GET('/api/Public/Branding', { params: { query: { organisation: code } } }),
          { toastOnError: false },
        )
        return result.ok ? result.data : null
      } catch {
        return null
      }
    }
    // On the server the answer is shared for a few seconds (D13); in the browser it is a plain call.
    const loaded = import.meta.server ? await cachedServerBranding(code, load) : await load()
    branding.value = loaded
  }

  /** The agent/admin shell: the signed-in staff user's own tenant (D5). A failure keeps what is already shown. */
  async function loadForTenant(): Promise<void> {
    const result = await call(clients.agent.GET('/api/Agent/Branding'), { toastOnError: false })
    if (result.ok) branding.value = result.data
  }

  function displayName(locale: string): string | null {
    const name = branding.value?.displayName
    return name ? localizedName(name, locale) || null : null
  }

  function logoUrl(theme: Theme): string | null {
    const current = branding.value
    if (!current) return null
    return brandLogoUrl(theme === 'dark' ? (current.darkLogoId ?? current.lightLogoId) : current.lightLogoId)
  }

  return { branding, css, loadPublic, loadForTenant, displayName, logoUrl }
})
