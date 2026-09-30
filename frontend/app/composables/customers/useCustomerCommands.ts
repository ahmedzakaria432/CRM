import type { AddContactRequest, CreateCustomerRequest, UpdateContactRequest, UpdateCustomerRequest } from './types'

export function useCustomerCommands() {
  const { clients, call, toast } = useApi()
  const agent = clients.agent
  const quiet = { toastOnError: false }
  const path = (id: string) => ({ params: { path: { id } } })

  return {
    toast,
    create: (body: CreateCustomerRequest) => call(agent.POST('/api/Agent/Customers', { body }), quiet),
    update: (id: string, body: UpdateCustomerRequest) => call(agent.PUT('/api/Agent/Customers/{id}', { ...path(id), body }), quiet),
    deactivate: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/deactivate', { ...path(id), body: { rowVersion } })),
    reactivate: (id: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/reactivate', { ...path(id), body: { rowVersion } })),
    addContact: (id: string, body: AddContactRequest) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts', { ...path(id), body }), quiet),
    updateContact: (id: string, contactId: string, body: UpdateContactRequest) =>
      call(agent.PUT('/api/Agent/Customers/{id}/contacts/{contactId}', { params: { path: { id, contactId } }, body }), quiet),
    setPrimaryContact: (id: string, contactId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts/{contactId}/primary', { params: { path: { id, contactId } }, body: { rowVersion } })),
    removeContact: (id: string, contactId: string, rowVersion: string) =>
      call(agent.POST('/api/Agent/Customers/{id}/contacts/{contactId}/remove', { params: { path: { id, contactId } }, body: { rowVersion } })),
    addNote: (id: string, body: string) => call(agent.POST('/api/Agent/Customers/{id}/notes', { ...path(id), body: { body } }), quiet),
    pinNote: (id: string, noteId: string, isPinned: boolean) =>
      call(agent.PUT('/api/Agent/Customers/{id}/notes/{noteId}/pin', { params: { path: { id, noteId } }, body: { isPinned } })),
    removeNote: (id: string, noteId: string) =>
      call(agent.DELETE('/api/Agent/Customers/{id}/notes/{noteId}', { params: { path: { id, noteId } } })),
  }
}
