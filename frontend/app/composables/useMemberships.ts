import type { components } from '~/api/generated/admin'

export type MembershipDto = components['schemas']['MembershipDto']

/** The roles a membership can hold. Mirrors Organisation's SystemRole. */
export const membershipRoles = ['Administrator', 'Supervisor', 'Agent'] as const

export function useMemberships() {
  const { clients, call } = useApi()

  function listForUser(userId: Ref<string>) {
    return useAsyncData(
      'admin-user-memberships',
      async () => {
        const result = await call(
          clients.admin.GET('/api/Admin/Memberships', { params: { query: { userId: userId.value } } }),
          { toastOnError: false },
        )
        if (!result.ok) throw result.problem
        return result.data
      },
      { watch: [userId] },
    )
  }

  function add(body: { userId: string; departmentId: string; role: string }) {
    return call(clients.admin.POST('/api/Admin/Memberships', { body }))
  }

  function changeRole(id: string, body: { role: string; rowVersion: string }) {
    return call(clients.admin.PUT('/api/Admin/Memberships/{id}/role', { params: { path: { id } }, body }))
  }

  function remove(id: string) {
    return call(clients.admin.DELETE('/api/Admin/Memberships/{id}', { params: { path: { id } } }))
  }

  return { listForUser, add, changeRole, remove }
}
