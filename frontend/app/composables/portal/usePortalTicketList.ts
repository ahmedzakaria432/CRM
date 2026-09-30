import {
  parsePortalFilters,
  portalFiltersToQuery,
  portalPageSize,
  type PortalTicketFilters,
} from '~/utils/portal'

/** The portal "my requests" use case: the URL is the state; the list refetches only from it. Data loads in the
 * browser only (D17: the SSR server never holds a session), so the status is idle until the page is mounted. */
export function usePortalTicketList() {
  const route = useRoute()
  const router = useRouter()
  const { clients, call } = useApi()

  const filters = computed(() => parsePortalFilters(route.query))

  function update(patch: Partial<PortalTicketFilters>, mode: 'push' | 'replace' = 'push'): Promise<unknown> {
    // Every change except paging itself starts again on page 1; a tab change drops a status of the other tab.
    const resetsPage = Object.keys(patch).some((key) => key !== 'page')
    const next: PortalTicketFilters = {
      ...filters.value,
      ...patch,
      ...(patch.tab !== undefined && patch.status === undefined ? { status: '' as const } : {}),
      ...(resetsPage ? { page: 1 } : {}),
    }
    const location = { query: portalFiltersToQuery(next) }
    return mode === 'replace' ? router.replace(location) : router.push(location)
  }

  const result = useAsyncData(
    'portal-tickets',
    async (_nuxtApp, { signal }) => {
      const f = filters.value
      const response = await call(
        clients.portal.GET('/api/Portal/Tickets', {
          params: {
            query: {
              tab: f.tab,
              status: f.status === '' ? undefined : f.status,
              search: f.q === '' ? undefined : f.q,
              page: f.page,
              pageSize: portalPageSize,
            },
          },
          signal,
        }),
        { toastOnError: false },
      )
      if (!response.ok) throw response.problem
      return response.data
    },
    { server: false },
  )
  // Not useAsyncData's `watch` option: refresh() runs immediately and aborts the superseded request.
  watch(filters, () => void result.refresh())

  return { filters, update, ...result }
}
