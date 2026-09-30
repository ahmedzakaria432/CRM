import { parseTicketFilters, ticketFiltersToQuery, ticketsPageSize, type TicketFilters } from '~/utils/tickets'

/** The inbox use case: the URL is the state (D2); the list refetches only from it. */
export function useTicketSearch() {
  const route = useRoute()
  const router = useRouter()
  const { clients, call } = useApi()

  const filters = computed(() => parseTicketFilters(route.query))

  function update(patch: Partial<TicketFilters>, mode: 'push' | 'replace' = 'push'): Promise<unknown> {
    // Every change except paging itself starts again on page 1 (search, filters, view and sort alike).
    const resetsPage = Object.keys(patch).some((key) => key !== 'page')
    const next: TicketFilters = { ...filters.value, ...patch, ...(resetsPage ? { page: 1 } : {}) }
    const location = { query: ticketFiltersToQuery(next) }
    return mode === 'replace' ? router.replace(location) : router.push(location)
  }

  const result = useAsyncData('agent-tickets', async (_nuxtApp, { signal }) => {
    const f = filters.value
    const response = await call(
      clients.agent.GET('/api/Agent/Tickets', {
        params: {
          query: {
            queue: f.view,
            search: f.q === '' ? undefined : f.q,
            status: f.status === '' ? undefined : [f.status],
            priority: f.priority === '' ? undefined : [f.priority],
            categoryId: f.categoryId === '' ? undefined : f.categoryId,
            assigneeId: f.view === 'all' && f.assigneeId !== '' ? f.assigneeId : undefined, // D3
            departmentId: f.departmentId === '' ? undefined : f.departmentId,
            branchId: f.branchId === '' ? undefined : f.branchId,
            customerId: f.customerId === '' ? undefined : f.customerId,
            sort: f.sort,
            descending: f.descending,
            page: f.page,
            pageSize: ticketsPageSize,
          },
        },
        signal,
      }),
      { toastOnError: false },
    )
    if (!response.ok) throw response.problem
    return response.data
  })
  // Not useAsyncData's `watch` option (see useCustomerSearch): refresh() runs immediately and aborts the superseded request.
  watch(filters, () => void result.refresh())

  return { filters, update, ...result }
}
