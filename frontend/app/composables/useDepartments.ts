import type { components } from '~/api/generated/admin'

export type DepartmentDto = components['schemas']['DepartmentDto']

export function useDepartments() {
  const { clients, call } = useApi()

  function list(branchId: Ref<string | undefined>, activeOnly: Ref<boolean>) {
    return useAsyncData(
      'admin-departments',
      async () => {
        const result = await call(
          clients.admin.GET('/api/Admin/Departments', {
            params: { query: { branchId: branchId.value, activeOnly: activeOnly.value } },
          }),
          { toastOnError: false },
        )
        // Thrown (not returned) so useAsyncData's `error` carries the problem - the page tells a 403
        // (not an administrator) apart from a legitimately empty list.
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [branchId, activeOnly] },
    )
  }

  function create(body: { branchId: string; code: string; nameAr: string; nameEn: string }) {
    return call(clients.admin.POST('/api/Admin/Departments', { body }), { toastOnError: false })
  }

  function rename(id: string, body: { nameAr: string; nameEn: string; rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/Departments/{id}/name', { params: { path: { id } }, body }), {
      toastOnError: false,
    })
  }

  function deactivate(id: string, rowVersion: string) {
    return call(
      clients.admin.POST('/api/Admin/Departments/{id}/deactivate', { params: { path: { id } }, body: { rowVersion } }),
    )
  }

  return { list, create, rename, deactivate }
}
