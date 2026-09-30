export type { TicketDto } from './types'

/** One ticket (Story 20: loaded by the ticket workspace). */
export function useTicket(id: string) {
  const { clients, call } = useApi()

  function load(immediate = true) {
    return useAsyncData(`agent-ticket-${id}`, async (_nuxtApp, { signal }) => {
      const result = await call(clients.agent.GET('/api/Agent/Tickets/{id}', { params: { path: { id } }, signal }), {
        toastOnError: false,
      })
      if (!result.ok) throw result.problem
      return result.data
    }, { immediate })
  }

  return { load }
}
