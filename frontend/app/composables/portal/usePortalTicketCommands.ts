import type { components } from '~/api/generated/portal'

export type PortalSubmitTicketRequest = components['schemas']['PortalSubmitTicketRequest']

/** The customer's ticket commands (Story 47). Every command returns an ApiResult and never toasts: the page decides how a
 * refusal is shown (a 400 on the fields, a 409 as "try again", a 422 inline). */
export function usePortalTicketCommands() {
  const { clients, call } = useApi()
  const quiet = { toastOnError: false }

  return {
    submit: (body: PortalSubmitTicketRequest) => call(clients.portal.POST('/api/Portal/Tickets', { body }), quiet),
    requestClosure: (id: string) =>
      call(clients.portal.POST('/api/Portal/Tickets/{id}/request-closure', { params: { path: { id } } }), quiet),
    reopen: (id: string) =>
      call(clients.portal.POST('/api/Portal/Tickets/{id}/reopen', { params: { path: { id } } }), quiet),
  }
}
