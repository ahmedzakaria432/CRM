import type { components } from '~/api/generated/agent'
import type { ApiResult } from '~/composables/useApi'
import { scopeCookieName } from '~/utils/scope'

export type MeDto = components['schemas']['MeDto']

// Module-level, not store state: a promise cannot be serialised, and two route changes racing to restore the
// session must share one refresh (the refresh token rotates - a second concurrent call would trip reuse detection).
let restoring: Promise<boolean> | null = null

/** The signed-in staff user. The access token lives here, in memory only - never in a cookie or localStorage;
 * a reload restores the session with one silent refresh through the HttpOnly crm_refresh cookie. */
export const useAuthStore = defineStore('auth', () => {
  // Captured once: after an `await` inside an action the Nuxt instance can be lost, so every composable
  // call that follows an await goes through nuxtApp.runWithContext.
  const nuxtApp = useNuxtApp()
  const { clients, call } = useApi()

  const accessToken = shallowRef<string | null>(null)
  const me = ref<MeDto | null>(null)
  const status = ref<'anonymous' | 'restoring' | 'authenticated'>('anonymous')

  const isAuthenticated = computed(() => accessToken.value !== null && me.value !== null)
  const permissions = computed(() => new Set(me.value?.permissions ?? []))

  function clear(): void {
    accessToken.value = null
    me.value = null
    status.value = 'anonymous'
  }

  function resetScope(): void {
    nuxtApp.runWithContext(() => {
      const cookie = useCookie<string | null>(scopeCookieName, { sameSite: 'lax', maxAge: 60 * 60 * 24 * 365 })
      cookie.value = null
      useScopeStore().reset()
    })
  }

  async function loadMe(): Promise<ApiResult<MeDto>> {
    const result = await call(clients.agent.GET('/api/Agent/Me'), { toastOnError: false })
    if (result.ok) {
      me.value = result.data
      status.value = 'authenticated'
    }
    return result
  }

  async function login(email: string, password: string): Promise<ApiResult<void>> {
    const result = await call(clients.agent.POST('/api/Agent/Auth/login', { body: { email, password } }), {
      toastOnError: false,
    })
    if (!result.ok) return result

    accessToken.value = result.data.accessToken ?? null
    const loaded = await loadMe()
    if (!loaded.ok) {
      clear()
      return loaded
    }

    resetScope()

    // The profile language is applied at sign-in only; the switcher changes the cookie for the session.
    const preferred = loaded.data.preferredLocale
    if ((preferred === 'en' || preferred === 'ar') && preferred !== nuxtApp.$i18n.locale.value) {
      await nuxtApp.runWithContext(() => nuxtApp.$i18n.setLocale(preferred))
    }

    return { ok: true, data: undefined }
  }

  /** Called by createCrmFetch after a 401. Must not throw: any failure is "no session". */
  async function refresh(): Promise<boolean> {
    try {
      const result = await call(clients.agent.POST('/api/Agent/Auth/refresh', { body: {} }), { toastOnError: false })
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

  async function logout(): Promise<void> {
    try {
      await call(clients.agent.POST('/api/Agent/Auth/logout', { body: {} }), { toastOnError: false })
    } catch {
      // signing out never fails from the user's side
    }
    clear()
    resetScope()
    await nuxtApp.runWithContext(() => navigateTo('/login'))
  }

  return { accessToken, me, status, isAuthenticated, permissions, login, refresh, loadMe, restore, logout, clear }
})
