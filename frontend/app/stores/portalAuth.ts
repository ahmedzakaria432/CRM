import type { components } from '~/api/generated/portal'
import type { ApiResult } from '~/composables/useApi'

export type PortalMeDto = components['schemas']['PortalMeDto']

// Module-level, like stores/auth.ts: two route changes racing to restore the session must share one refresh (the
// refresh token rotates - a second concurrent call would trip reuse detection).
let restoring: Promise<boolean> | null = null
// Single-flight refresh: a restore and a data request that hit a 401 at the same moment share ONE rotation of the cookie.
let refreshing: Promise<boolean> | null = null

/** The signed-in portal customer (Story 47). The access token lives here, in memory only; a reload restores the
 * session with one silent refresh through the HttpOnly crm_portal_refresh cookie. A second session next to the
 * staff one (stores/auth.ts): the two never share tokens, cookies or state. */
export const usePortalAuthStore = defineStore('portalAuth', () => {
  // Captured once: after an `await` inside an action the Nuxt instance can be lost, so every composable
  // call that follows an await goes through nuxtApp.runWithContext.
  const nuxtApp = useNuxtApp()
  const { clients, call } = useApi()

  const accessToken = shallowRef<string | null>(null)
  const me = ref<PortalMeDto | null>(null)
  const status = ref<'anonymous' | 'restoring' | 'authenticated'>('anonymous')

  const isAuthenticated = computed(() => accessToken.value !== null && me.value !== null)
  const isAccountAdmin = computed(() => me.value?.isAccountAdmin === true)

  function clear(): void {
    accessToken.value = null
    me.value = null
    status.value = 'anonymous'
  }

  async function loadMe(): Promise<ApiResult<PortalMeDto>> {
    const result = await call(clients.portal.GET('/api/Portal/Me'), { toastOnError: false })
    if (result.ok) {
      me.value = result.data
      status.value = 'authenticated'
    }
    return result
  }

  async function applyPreferredLocale(preferred: string): Promise<void> {
    if ((preferred === 'en' || preferred === 'ar') && preferred !== nuxtApp.$i18n.locale.value) {
      await nuxtApp.runWithContext(() => nuxtApp.$i18n.setLocale(preferred))
    }
  }

  async function login(email: string, password: string): Promise<ApiResult<void>> {
    const result = await call(clients.portal.POST('/api/Portal/Auth/login', { body: { email, password } }), {
      toastOnError: false,
    })
    if (!result.ok) return result

    accessToken.value = result.data.accessToken ?? null
    const loaded = await loadMe()
    if (!loaded.ok) {
      clear()
      return loaded
    }

    // The profile language is applied at sign-in only; the switcher changes the cookie for the session.
    await applyPreferredLocale(loaded.data.preferredLocale ?? '')

    return { ok: true, data: undefined }
  }

  /** Answers 204 for every well-formed request: the response never says whether the email belongs to a customer. */
  async function register(email: string, password: string): Promise<ApiResult<void>> {
    const result = await call(
      clients.portal.POST('/api/Portal/Auth/register', {
        body: { email, password, preferredLocale: nuxtApp.$i18n.locale.value },
      }),
      { toastOnError: false },
    )
    return result.ok ? { ok: true, data: undefined } : result
  }

  /** Called by createCrmFetch after a 401. Must not throw: any failure is "no session". */
  function refresh(): Promise<boolean> {
    refreshing ??= doRefresh().finally(() => {
      refreshing = null
    })
    return refreshing
  }

  async function doRefresh(): Promise<boolean> {
    try {
      const result = await call(clients.portal.POST('/api/Portal/Auth/refresh', { body: {} }), { toastOnError: false })
      if (result.ok && result.data.accessToken) {
        accessToken.value = result.data.accessToken
        return true
      }
    } catch {
      // fall through: a refresh that throws is a refresh that failed
    }
    clear()
    return false
  }

  /** Restores the session after a reload: one refresh through the cookie, then the profile. */
  function restore(): Promise<boolean> {
    if (restoring !== null) return restoring
    status.value = 'restoring'
    restoring = (async () => {
      try {
        const refreshed = await refresh()
        if (!refreshed) return false
        const loaded = await loadMe()
        if (!loaded.ok) {
          clear()
          return false
        }
        return true
      } finally {
        restoring = null
        if (status.value === 'restoring') status.value = 'anonymous'
      }
    })()
    return restoring
  }

  /** Writes the profile returned by PUT /api/Portal/Me back into the session. */
  function applyMe(next: PortalMeDto): void {
    me.value = next
  }

  async function logout(): Promise<void> {
    try {
      await call(clients.portal.POST('/api/Portal/Auth/logout', { body: {} }), { toastOnError: false })
    } catch {
      // signing out never fails on the customer side
    }
    clear()
    await nuxtApp.runWithContext(() => navigateTo('/portal/login'))
  }

  return {
    accessToken,
    me,
    status,
    isAuthenticated,
    isAccountAdmin,
    login,
    register,
    refresh,
    loadMe,
    restore,
    applyMe,
    logout,
    clear,
  }
})
