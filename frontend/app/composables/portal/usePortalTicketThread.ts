/** One ticket's public conversation (Story 47). load() = the first page; page(after) = the entries with sequence > after
 * (next page, or everything added since - the cursor never duplicates or skips an entry). Browser-only data (D17). */
export function usePortalTicketThread(ticketId: string) {
  const { clients, call } = useApi()

  function fetchPage(after?: number, signal?: AbortSignal) {
    return call(
      clients.portal.GET('/api/Portal/Tickets/{id}/thread', {
        params: { path: { id: ticketId }, query: { after, limit: 50 } },
        signal,
      }),
      { toastOnError: false },
    )
  }

  function load() {
    return useAsyncData(
      `portal-ticket-thread-${ticketId}`,
      async (_nuxtApp, { signal }) => {
        const result = await fetchPage(undefined, signal)
        if (!result.ok) throw result.problem
        return result.data
      },
      { server: false },
    )
  }

  // A 400 goes to useFormErrors(), never a toast.
  const reply = (text: string) =>
    call(clients.portal.POST('/api/Portal/Tickets/{id}/replies', { params: { path: { id: ticketId } }, body: { body: text } }), {
      toastOnError: false,
    })

  return { load, page: (after: number) => fetchPage(after), reply }
}
