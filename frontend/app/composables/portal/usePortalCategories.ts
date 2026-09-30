/** The categories a customer may pick when raising a request (Story 47). Browser-only data (D17). */
export function usePortalCategories() {
  const { clients, call } = useApi()

  return useAsyncData(
    'portal-categories',
    async (_nuxtApp, { signal }) => {
      const result = await call(clients.portal.GET('/api/Portal/TicketCategories', { signal }), { toastOnError: false })
      if (!result.ok) throw result.problem
      return result.data
    },
    { server: false },
  )
}
