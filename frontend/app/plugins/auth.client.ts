import { authPathPrefix } from '~/utils/permissions'

// Client-only: the SSR server keeps anonymousSession (a module-level session shared across requests must never
// hold one user's tokens - see useAuthSession).
export default defineNuxtPlugin(() => {
  const auth = useAuthStore()
  useAuthSession().setAuthSession({
    accessToken: () => auth.accessToken,
    refresh: () => auth.refresh(),
    clear: () => auth.clear(),
    isRefreshRequest: (request) => new URL(request.url, window.location.origin).pathname.startsWith(authPathPrefix),
  })
})
