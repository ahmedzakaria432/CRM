import { createCrmFetch } from '~/api/crm-fetch'
import { createCrmClients } from '~/api/clients'

export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig()

  const crmFetch = createCrmFetch({
    baseFetch: (request) => {
      // The browser's own Accept-Language is the OS language, not the language the user picked (Task F2).
      const headers = new Headers(request.headers)
      headers.set('Accept-Language', nuxtApp.$i18n.locale.value)
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
