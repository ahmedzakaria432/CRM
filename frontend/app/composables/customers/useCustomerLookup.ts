import type { CustomerDto, CustomerLookupDto } from './types'
import type { ApiResult } from '~/composables/useApi'

export const customerLookupTake = 10

/** A CustomerDto as the picker model (Story 20: preselected customer, URL-restored filter). */
export function customerLookupOf(customer: CustomerDto): CustomerLookupDto {
  const primary = (customer.contacts ?? []).find((contact) => contact.isPrimary)
  return {
    id: customer.id,
    number: customer.number,
    displayName: customer.displayName,
    primaryEmail: primary?.email ?? null,
    primaryPhone: primary?.phone ?? null,
  }
}

/** The picker's use case: active customers only (Story 11). The caller owns cancellation. */
export function useCustomerLookup() {
  const { clients, call } = useApi()
  function search(term: string, signal: AbortSignal): Promise<ApiResult<CustomerLookupDto[]>> {
    return call(
      clients.agent.GET('/api/Agent/Customers/lookup', { params: { query: { search: term, take: customerLookupTake } }, signal }),
      { toastOnError: false },
    )
  }
  function get(id: string, signal?: AbortSignal): Promise<ApiResult<CustomerDto>> {
    return call(clients.agent.GET('/api/Agent/Customers/{id}', { params: { path: { id } }, signal }), { toastOnError: false })
  }
  return { search, get }
}
