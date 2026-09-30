import type { ComputedRef } from 'vue'

/** GET /api/Agent/Tickets/transfer-targets (tickets.assign). enabled=false skips the request. */
export function useTransferTargets(key: string, enabled: ComputedRef<boolean>) {
  const { clients, call } = useApi()
  return useAsyncData(
    key,
    async (_nuxtApp, { signal }) => {
      if (!enabled.value) return []
      const result = await call(clients.agent.GET('/api/Agent/Tickets/transfer-targets', { signal }), { toastOnError: false })
      if (!result.ok) throw result.problem
      return result.data
    },
    { watch: [enabled] },
  )
}
