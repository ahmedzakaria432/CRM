import type { CreateTicketRequest } from './types'

/** Every command returns an ApiResult and never toasts: the workspace decides how a refusal is shown (D6). */
export function useTicketCommands() {
  const { clients, call } = useApi()
  const agent = clients.agent
  const quiet = { toastOnError: false }
  return {
    create: (body: CreateTicketRequest) => call(agent.POST('/api/Agent/Tickets', { body }), quiet),
    transition: (id: string, action: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/transitions', { params: { path: { id } }, body: { action, rowVersion } }), quiet),
    claim: (id: string) => call(agent.POST('/api/Agent/Tickets/{id}/claim', { params: { path: { id } } }), quiet),
    assign: (id: string, agentId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/assign', { params: { path: { id } }, body: { agentId, rowVersion } }), quiet),
    unassign: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/unassign', { params: { path: { id } }, body: { rowVersion } }), quiet),
    transfer: (id: string, departmentId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Tickets/{id}/transfer', { params: { path: { id } }, body: { departmentId, rowVersion } }), quiet),
    changePriority: (id: string, priority: string, rowVersion: string) =>
      call(agent.PUT('/api/Agent/Tickets/{id}/priority', { params: { path: { id } }, body: { priority, rowVersion } }), quiet),
    changeCategory: (id: string, categoryId: string, rowVersion: string) =>
      call(agent.PUT('/api/Agent/Tickets/{id}/category', { params: { path: { id } }, body: { categoryId, rowVersion } }), quiet),
  }
}
