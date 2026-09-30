import type { components } from '~/api/generated/admin'

export type UserDto = components['schemas']['UserDto']
export type UserPage = components['schemas']['UserDtoPagedResult']

export const usersPageSize = 20

export function useUsers() {
  const { clients, call } = useApi()

  function list(search: Ref<string>, page: Ref<number>) {
    return useAsyncData(
      'admin-users',
      async () => {
        const result = await call(
          clients.admin.GET('/api/Admin/Users', {
            params: {
              query: { search: search.value === '' ? undefined : search.value, page: page.value, pageSize: usersPageSize },
            },
          }),
          { toastOnError: false },
        )
        // Thrown (not returned) so useAsyncData's `error` carries the problem - the page tells a 403
        // (no admin.users permission) apart from a legitimately empty list.
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [search, page] },
    )
  }

  function get(id: Ref<string>) {
    return useAsyncData(
      'admin-user',
      async () => {
        const result = await call(clients.admin.GET('/api/Admin/Users/{id}', { params: { path: { id: id.value } } }), {
          toastOnError: false,
        })
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [id] },
    )
  }

  function create(body: { email: string; displayName: string; preferredLocale: string; initialPassword: string }) {
    return call(clients.admin.POST('/api/Admin/Users', { body }), { toastOnError: false })
  }

  function updateProfile(id: string, body: { displayName: string; preferredLocale: string; rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/Users/{id}/profile', { params: { path: { id } }, body }))
  }

  function deactivate(id: string, rowVersion: string) {
    return call(clients.admin.POST('/api/Admin/Users/{id}/deactivate', { params: { path: { id } }, body: { rowVersion } }))
  }

  function reactivate(id: string, rowVersion: string) {
    return call(clients.admin.POST('/api/Admin/Users/{id}/reactivate', { params: { path: { id } }, body: { rowVersion } }))
  }

  function resetPassword(id: string, body: { newPassword: string; rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/Users/{id}/password', { params: { path: { id } }, body }))
  }

  return { list, get, create, updateProfile, deactivate, reactivate, resetPassword }
}
