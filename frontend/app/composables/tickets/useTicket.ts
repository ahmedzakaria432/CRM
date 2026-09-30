import type { components } from '~/api/generated/agent'

export type TicketDto = components['schemas']['TicketDto']

/** One ticket (Story 18: the header of the minimal detail page; Story 20 extends it with the workspace). */
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
