import type { ComputedRef } from 'vue'

/** GET /api/Agent/Tickets/agents. key: one per consumer ('agent-ticket-agents-inbox', `agent-ticket-agents-${ticketId}`).
 * enabled=false skips the request (the caller may not use the list) and returns an empty list. */
export function useTicketAgents(key: string, departmentId: ComputedRef<string | undefined>, enabled: ComputedRef<boolean>) {
  const { clients, call } = useApi()
  return useAsyncData(
    key,
    async (_nuxtApp, { signal }) => {
      if (!enabled.value) return []
      const result = await call(
        clients.agent.GET('/api/Agent/Tickets/agents', { params: { query: { departmentId: departmentId.value } }, signal }),
        { toastOnError: false },
      )
      if (!result.ok) throw result.problem
      return result.data
    },
    { watch: [departmentId, enabled] },
  )
}
