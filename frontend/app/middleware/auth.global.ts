// Two authenticated areas, both client-guarded (the SSR server never holds a session, see plugins/auth.client.ts):
//  - staff: /agent/** and /admin/** (SPA); guest page /login;
//  - portal customer (Story 47): /portal/** (SSR layout, client data); guest pages /portal/login and /portal/register.
// /portal/** is server-rendered, so the early return below means its guard runs on the client during hydration (R7).
const portalGuestPaths = ['/portal/login', '/portal/register']

export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) return

  if (to.path === '/portal' || to.path.startsWith('/portal/')) {
    // The first (server-rendered) load: redirecting during hydration would mismatch the server HTML, so the
    // plugins/portalGuard.client.ts hook does it right after mount instead (R7 fallback).
    if (useNuxtApp().isHydrating) return

    const portal = usePortalAuthStore()
    const isGuestPage = portalGuestPaths.includes(to.path)

    if (!isGuestPage && !portal.isAuthenticated) {
      const restored = await portal.restore()
      if (!restored) return navigateTo({ path: '/portal/login', query: { redirect: to.fullPath } })
    }

    if (isGuestPage && portal.isAuthenticated) return navigateTo('/portal')
    return
  }

  const auth = useAuthStore()
  const needsAuth = to.path.startsWith('/agent') || to.path.startsWith('/admin')

  if (needsAuth && !auth.isAuthenticated) {
    const restored = await auth.restore()
    if (!restored) return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }

  if (to.path === '/login' && auth.isAuthenticated) return navigateTo('/agent')
})
