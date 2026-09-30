/** GET /api/Agent/Tickets/queues: the counts on the inbox view tabs. */
export function useTicketQueues() {
  const { clients, call } = useApi()
  return useAsyncData('agent-ticket-queues', async (_nuxtApp, { signal }) => {
    const result = await call(clients.agent.GET('/api/Agent/Tickets/queues', { signal }), { toastOnError: false })
    if (!result.ok) throw result.problem
    return result.data
  })
}
