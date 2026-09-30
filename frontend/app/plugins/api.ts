import { createCrmFetch } from '~/api/crm-fetch'
import { createCrmClients } from '~/api/clients'
import { isPortalApiRequest } from '~/utils/permissions'
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
      // Story 47: the staff department scope never travels on a portal request (a customer has no departments).
      if (scopeCookie.value && !isPortalApiRequest(request)) headers.set('X-Crm-Scope', scopeCookie.value)
      return globalThis.fetch(new Request(request, { headers }))
    },
    // Story 47: one session per audience - /api/Portal/* uses the portal customer session, everything else the staff one.
    session: (request) => {
      const sessions = useAuthSession()
      return isPortalApiRequest(request) ? sessions.portalSession.value : sessions.session.value
    },
    onUnauthenticated: async (request) => {
      await navigateTo({
        path: isPortalApiRequest(request) ? '/portal/login' : '/login',
        query: { redirect: useRoute().fullPath },
      })
    },
  })

  // Browser: same-origin '/api' (dev proxy, Probe 9). Server: SSR has no origin to resolve a relative URL
  // against, so it calls the API directly via runtimeConfig.apiBase.
  const baseUrl = import.meta.server ? config.apiBase : ''
  const api = createCrmClients(baseUrl, crmFetch)

  // Declares $api's type automatically through the plugin's return value. No hand-written `declare module`.
  return { provide: { api } }
})
