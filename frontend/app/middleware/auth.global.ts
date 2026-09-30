// Authenticated area: /agent/** and /admin/** (both SPA). Guest page: /login. Client only - the SSR server never
// holds a session (see plugins/auth.client.ts), and /agent and /admin are not server-rendered anyway.
export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) return

  const auth = useAuthStore()
  const needsAuth = to.path.startsWith('/agent') || to.path.startsWith('/admin')

  if (needsAuth && !auth.isAuthenticated) {
    const restored = await auth.restore()
    if (!restored) return navigateTo({ path: '/login', query: { redirect: to.fullPath } })
  }

  if (to.path === '/login' && auth.isAuthenticated) return navigateTo('/agent')
})
