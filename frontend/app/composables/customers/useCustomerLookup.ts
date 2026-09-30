import type { CustomerLookupDto } from './types'
import type { ApiResult } from '~/composables/useApi'

export const customerLookupTake = 10

/** The picker's use case: active customers only (Story 11). The caller owns cancellation. */
export function useCustomerLookup() {
  const { clients, call } = useApi()
  function search(term: string, signal: AbortSignal): Promise<ApiResult<CustomerLookupDto[]>> {
    return call(
      clients.agent.GET('/api/Agent/Customers/lookup', { params: { query: { search: term, take: customerLookupTake } }, signal }),
      { toastOnError: false },
    )
  }
  return { search }
}
