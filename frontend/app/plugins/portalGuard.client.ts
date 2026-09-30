// R7 fallback: /portal/** is server-rendered and the server never holds a session, so the first load restores the
// customer session right after hydration and redirects to the portal login when there is none.
const guestPaths = ['/portal/login', '/portal/register']

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.hook('app:mounted', async () => {
    const route = useRoute()
    const path = route.path
    if (path !== '/portal' && !path.startsWith('/portal/')) return
    if (guestPaths.includes(path)) return

    const portal = usePortalAuthStore()
    if (portal.isAuthenticated) return
    const restored = await portal.restore()
    if (!restored) await navigateTo({ path: '/portal/login', query: { redirect: route.fullPath } })
  })
})
