import { customerFiltersToQuery, customersPageSize, parseCustomerFilters, type CustomerFilters } from '~/utils/customers'

/** The list use case: the URL is the state (Product rules); the table refetches only from it. */
export function useCustomerSearch() {
  const route = useRoute()
  const router = useRouter()
  const { clients, call } = useApi()

  const filters = computed(() => parseCustomerFilters(route.query))

  function update(patch: Partial<CustomerFilters>, mode: 'push' | 'replace' = 'push'): Promise<unknown> {
    // Every change except paging itself starts again on page 1 (search, filters, and sort alike).
    const resetsPage = Object.keys(patch).some((key) => key !== 'page')
    const next: CustomerFilters = { ...filters.value, ...patch, ...(resetsPage ? { page: 1 } : {}) }
    const location = { query: customerFiltersToQuery(next) }
    return mode === 'replace' ? router.replace(location) : router.push(location)
  }

  const result = useAsyncData(
    'agent-customers',
    async (_nuxtApp, { signal }) => {
      const f = filters.value
      const response = await call(
        clients.agent.GET('/api/Agent/Customers', {
          params: {
            query: {
              search: f.q === '' ? undefined : f.q,
              status: f.status === '' ? undefined : f.status,
              type: f.type === '' ? undefined : f.type,
              branchId: f.branchId === '' ? undefined : f.branchId,
              departmentId: f.departmentId === '' ? undefined : f.departmentId,
              sort: f.sort,
              descending: f.descending,
              page: f.page,
              pageSize: customersPageSize,
            },
          },
          signal,
        }),
        { toastOnError: false },
      )
      if (!response.ok) throw response.problem
      return response.data
    },
  )
  // Not useAsyncData's `watch` option: Nuxt 4.5 debounces watch-triggered runs per tick and queues them behind the
  // request in flight, so a slow superseded request would finish before the newest one started. refresh() runs
  // immediately and its default dedupe ('cancel') aborts the superseded request through the signal.
  watch(filters, () => void result.refresh())

  return { filters, update, ...result }
}
