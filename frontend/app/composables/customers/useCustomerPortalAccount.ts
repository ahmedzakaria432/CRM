/** An agent sets the initial (or a new) portal password of a portal-enabled contact (Story 47, D19). Never toasts: the
 * modal shows a 400 on its field and any other refusal inline. */
export function useCustomerPortalAccount() {
  const { clients, call } = useApi()

  return {
    setPassword: (customerId: string, contactId: string, password: string) =>
      call(clients.agent.POST('/api/Agent/PortalAccounts', { body: { customerId, contactId, password } }), {
        toastOnError: false,
      }),
  }
}
