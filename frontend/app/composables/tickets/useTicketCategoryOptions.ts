import type { ComputedRef } from 'vue'

/** GET /api/Agent/TicketCategories (active categories of the department, or of every department in scope).
 * key: one per consumer ('agent-ticket-categories-inbox', `agent-ticket-categories-${ticketId}`). */
export function useTicketCategoryOptions(key: string, departmentId: ComputedRef<string | undefined>) {
  const { clients, call } = useApi()
  return useAsyncData(
    key,
    async (_nuxtApp, { signal }) => {
      const result = await call(
        clients.agent.GET('/api/Agent/TicketCategories', { params: { query: { departmentId: departmentId.value } }, signal }),
        { toastOnError: false },
      )
      if (!result.ok) throw result.problem
      return result.data
    },
    { watch: [departmentId] },
  )
}
