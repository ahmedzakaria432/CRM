import type { components } from '~/api/generated/admin'

export type BranchDto = components['schemas']['BranchDto']

export function useBranches() {
  const { clients, call } = useApi()

  function list(activeOnly: Ref<boolean>) {
    return useAsyncData(
      'admin-branches',
      async () => {
        const result = await call(
          clients.admin.GET('/api/Admin/Branches', { params: { query: { activeOnly: activeOnly.value } } }),
          { toastOnError: false },
        )
        // Thrown (not returned) so useAsyncData's `error` carries the problem - the page tells a 403
        // (not an administrator) apart from a legitimately empty list.
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [activeOnly] },
    )
  }

  function create(body: { code: string; nameAr: string; nameEn: string }) {
    return call(clients.admin.POST('/api/Admin/Branches', { body }), { toastOnError: false })
  }

  function rename(id: string, body: { nameAr: string; nameEn: string; rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/Branches/{id}/name', { params: { path: { id } }, body }), {
      toastOnError: false,
    })
  }

  function deactivate(id: string, rowVersion: string) {
    return call(clients.admin.POST('/api/Admin/Branches/{id}/deactivate', { params: { path: { id } }, body: { rowVersion } }))
  }

  return { list, create, rename, deactivate }
}
