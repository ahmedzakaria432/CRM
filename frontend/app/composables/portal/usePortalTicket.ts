/** One of the customer's own tickets (Story 47). Browser-only data (D17). A 404 covers unknown and not-yours alike. */
export function usePortalTicket(ticketId: string) {
  const { clients, call } = useApi()

  return useAsyncData(
    `portal-ticket-${ticketId}`,
    async (_nuxtApp, { signal }) => {
      const result = await call(
        clients.portal.GET('/api/Portal/Tickets/{id}', { params: { path: { id: ticketId } }, signal }),
        { toastOnError: false },
      )
      if (!result.ok) throw result.problem
      return result.data
    },
    { server: false },
  )
}
