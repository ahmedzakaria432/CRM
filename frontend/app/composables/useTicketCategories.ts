import type { components } from '~/api/generated/admin'

export type TicketCategoryDto = components['schemas']['TicketCategoryDto']

export interface TicketCategoryForm {
  nameAr: string
  nameEn: string
  displayOrder: number
  isCustomerVisible: boolean
}

export function useTicketCategories() {
  const { clients, call } = useApi()

  function list(departmentId: Ref<string | undefined>, activeOnly: Ref<boolean>) {
    return useAsyncData(
      'admin-ticket-categories',
      async () => {
        const result = await call(
          clients.admin.GET('/api/Admin/TicketCategories', {
            params: { query: { departmentId: departmentId.value, activeOnly: activeOnly.value } },
          }),
          { toastOnError: false },
        )
        // Thrown (not returned) so useAsyncData's `error` carries the problem - the page tells a 403
        // (not an administrator) apart from a legitimately empty list.
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [departmentId, activeOnly] },
    )
  }

  function create(body: TicketCategoryForm & { departmentId: string }) {
    return call(clients.admin.POST('/api/Admin/TicketCategories', { body }), { toastOnError: false })
  }

  function update(id: string, body: TicketCategoryForm & { rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/TicketCategories/{id}', { params: { path: { id } }, body }), {
      toastOnError: false,
    })
  }

  function deactivate(id: string, rowVersion: string) {
    return call(
      clients.admin.POST('/api/Admin/TicketCategories/{id}/deactivate', { params: { path: { id } }, body: { rowVersion } }),
    )
  }

  function reactivate(id: string, rowVersion: string) {
    return call(
      clients.admin.POST('/api/Admin/TicketCategories/{id}/reactivate', { params: { path: { id } }, body: { rowVersion } }),
    )
  }

  return { list, create, update, deactivate, reactivate }
}
