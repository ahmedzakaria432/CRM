/** Agent home (Story 59, D6): the newest tickets assigned to me. Same endpoint and parameter names as the inbox
 * (useTicketSearch); its own key so it never overwrites the inbox list. */
export const myRecentTicketsCount = 5

export function useMyRecentTickets() {
  const { clients, call } = useApi()
  return useAsyncData('agent-home-recent-tickets', async (_nuxtApp, { signal }) => {
    const result = await call(
      clients.agent.GET('/api/Agent/Tickets', {
        params: {
          query: { queue: 'mine', sort: 'createdAt', descending: true, page: 1, pageSize: myRecentTicketsCount },
        },
        signal,
      }),
      { toastOnError: false },
    )
    if (!result.ok) throw result.problem
    return result.data.items ?? []
  })
}
