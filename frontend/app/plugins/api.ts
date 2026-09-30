import { createCrmFetch } from '~/api/crm-fetch'
import { createCrmClients } from '~/api/clients'
import { scopeCookieName } from '~/utils/scope'

export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig()

  // Story 06: the active department scope. Read here, not inside baseFetch - composables are only safe in the
  // plugin body.
  const scopeCookie = useCookie<string | null>(scopeCookieName, { sameSite: 'lax', maxAge: 60 * 60 * 24 * 365 })

  const crmFetch = createCrmFetch({
    baseFetch: (request) => {
      // The browser's own Accept-Language is the OS language, not the language the user picked (Task F2).
      const headers = new Headers(request.headers)
      headers.set('Accept-Language', nuxtApp.$i18n.locale.value)
      if (scopeCookie.value) headers.set('X-Crm-Scope', scopeCookie.value)
      return globalThis.fetch(new Request(request, { headers }))
    },
    session: () => useAuthSession().session.value,
    onUnauthenticated: async () => {
      await navigateTo({ path: '/login', query: { redirect: useRoute().fullPath } })
    },
  })

  // Browser: same-origin '/api' (dev proxy, Probe 9). Server: SSR has no origin to resolve a relative URL
  // against, so it calls the API directly via runtimeConfig.apiBase.
  const baseUrl = import.meta.server ? config.apiBase : ''
  const api = createCrmClients(baseUrl, crmFetch)

  // Declares $api's type automatically through the plugin's return value. No hand-written `declare module`.
  return { provide: { api } }
})
