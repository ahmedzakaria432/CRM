import { authPathPrefix, portalAuthPathPrefix } from '~/utils/permissions'

// Client-only: the SSR server keeps anonymousSession (a module-level session shared across requests must never
// hold one user's tokens - see useAuthSession). Story 47: two sessions, staff and portal customer.
export default defineNuxtPlugin(() => {
  const auth = useAuthStore()
  const portal = usePortalAuthStore()
  const sessions = useAuthSession()

  sessions.setAuthSession({
    accessToken: () => auth.accessToken,
    refresh: () => auth.refresh(),
    clear: () => auth.clear(),
    isRefreshRequest: (request) => new URL(request.url, window.location.origin).pathname.startsWith(authPathPrefix),
  })

  sessions.setPortalSession({
    accessToken: () => portal.accessToken,
    refresh: () => portal.refresh(),
    clear: () => portal.clear(),
    isRefreshRequest: (request) =>
      new URL(request.url, window.location.origin).pathname.startsWith(portalAuthPathPrefix),
  })
})
