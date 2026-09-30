/** The customer 360 Tickets panel (D14): the customer's newest 10 tickets, every status. */
export function useCustomerTickets(customerId: string) {
  const { clients, call } = useApi()
  return useAsyncData(`agent-customer-tickets-${customerId}`, async (_nuxtApp, { signal }) => {
    const result = await call(
      clients.agent.GET('/api/Agent/Tickets', {
        params: { query: { customerId, queue: 'all', sort: 'createdAt', descending: true, page: 1, pageSize: 10 } },
        signal,
      }),
      { toastOnError: false },
    )
    if (!result.ok) throw result.problem
    return result.data
  })
}
