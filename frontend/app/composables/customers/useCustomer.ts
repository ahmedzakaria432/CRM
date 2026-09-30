import type { CustomerDto } from './types'

export type CustomerPanel = 'profile' | 'contacts' | 'notes'
const panels: ReadonlyArray<CustomerPanel> = ['profile', 'contacts', 'notes']
export const customerPanelKey = (id: string, panel: CustomerPanel): string => `agent-customer-${panel}-${id}`

/** One customer page (id is fixed per mount - the page's key is route.path). Each panel loads on its own key;
 * publish() writes a command's CustomerDto into every panel that already has data (R4). */
export function useCustomer(id: string) {
  const nuxtApp = useNuxtApp()
  const { clients, call } = useApi()
  // Created in setup so their scope disposal is tracked; the computed getter/setter resolves the key lazily.
  const slots = panels.map((panel) => useNuxtData<CustomerDto>(customerPanelKey(id, panel)).data)

  function load(panel: CustomerPanel) {
    return useAsyncData(customerPanelKey(id, panel), async (_nuxtApp, { signal }) => {
      const result = await call(clients.agent.GET('/api/Agent/Customers/{id}', { params: { path: { id } }, signal }), {
        toastOnError: false,
      })
      if (!result.ok) throw result.problem
      return result.data
    })
  }

  function publish(customer: CustomerDto): void {
    for (const slot of slots) {
      if (slot.value) slot.value = customer
    }
  }

  function reload(): Promise<void> {
    return nuxtApp.runWithContext(() => refreshNuxtData(panels.map((panel) => customerPanelKey(id, panel))))
  }

  return { load, publish, reload }
}
