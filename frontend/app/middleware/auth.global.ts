// Two authenticated areas, both client-guarded (the SSR server never holds a session, see plugins/auth.client.ts):
//  - staff: /agent/** and /admin/** (SPA); guest page /login;
//  - portal customer (Story 47): /portal/** (SSR layout, client data); guest pages /portal/login and /portal/register.
// /portal/** is server-rendered, so the early return below means its guard runs on the client during hydration (R7).
//  - `/` (Story 59): a returning user goes to their home; decided on the server from the session hint cookies (D2/D3), so the
//    landing never renders and then redirects.
import { portalSessionHintCookie, sessionHintCookieOptions, staffSessionHintCookie } from '~/utils/sessionHint'

const portalGuestPaths = ['/portal/login', '/portal/register']

/** Where a signed-in user belongs. Staff first (D3). On the server only the hints exist (no session there - D17). */
function signedInHome(): '/agent' | '/portal' | null {
  const staffHint = useCookie<boolean | null>(staffSessionHintCookie, sessionHintCookieOptions).value === true
  const portalHint = useCookie<boolean | null>(portalSessionHintCookie, sessionHintCookieOptions).value === true
  if (import.meta.client) {
    if (useAuthStore().isAuthenticated || staffHint) return '/agent'
    if (usePortalAuthStore().isAuthenticated || portalHint) return '/portal'
    return null
  }
  if (staffHint) return '/agent'
  if (portalHint) return '/portal'
  return null
}

export default defineNuxtRouteMiddleware(async (to) => {
  if (to.path === '/') {
    // The server already decided on the first load; redirecting during hydration would mismatch its HTML (R7).
    if (import.meta.client && useNuxtApp().isHydrating) return
    const home = signedInHome()
    if (home !== null) return navigateTo(home)
    return
  }

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
