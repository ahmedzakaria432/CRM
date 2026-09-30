import type { components } from '~/api/generated/agent'

export type TicketThreadPageDto = components['schemas']['TicketThreadPageDto']
export type TicketThreadEntryDto = components['schemas']['TicketThreadEntryDto']

/** One ticket's conversation (Story 18). load() = the first page; page(after) = the entries with sequence > after
 * (next page, or everything added since - the cursor never duplicates or skips an entry). */
export function useTicketThread(ticketId: string) {
  const { clients, call } = useApi()

  function fetchPage(after?: number, signal?: AbortSignal) {
    return call(
      clients.agent.GET('/api/Agent/Tickets/{id}/thread', {
        params: { path: { id: ticketId }, query: { after, limit: 50 } },
        signal,
      }),
      { toastOnError: false },
    )
  }

  function load() {
    return useAsyncData(`agent-ticket-thread-${ticketId}`, async (_nuxtApp, { signal }) => {
      const result = await fetchPage(undefined, signal)
      if (!result.ok) throw result.problem
      return result.data
    })
  }

  const body = (text: string) => ({ params: { path: { id: ticketId } }, body: { body: text } })
  // Used by Story 20's composer; a 400 goes to useFormErrors(), never a toast.
  const reply = (text: string) => call(clients.agent.POST('/api/Agent/Tickets/{id}/replies', body(text)), { toastOnError: false })
  const addNote = (text: string) => call(clients.agent.POST('/api/Agent/Tickets/{id}/notes', body(text)), { toastOnError: false })
  const recordCustomerReply = (text: string) =>
    call(clients.agent.POST('/api/Agent/Tickets/{id}/customer-replies', body(text)), { toastOnError: false })

  return { load, page: (after: number) => fetchPage(after), reply, addNote, recordCustomerReply }
}
