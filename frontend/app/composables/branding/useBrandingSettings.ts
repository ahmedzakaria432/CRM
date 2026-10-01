/** The admin view of the organisation's branding, with the live row version. Client-side only (the page is an SPA route). */
export function useBrandingSettings() {
  const { clients, call } = useApi()

  return useAsyncData(
    'admin-branding',
    async () => {
      const result = await call(clients.admin.GET('/api/Admin/Branding'), { toastOnError: false })
      // Thrown (not returned) so useAsyncData's `error` carries the problem - the page tells a 403
      // (no admin.branding) apart from a failed load.
      if (!result.ok) throw result.problem
      return result.data
    },
    { server: false },
  )
}
