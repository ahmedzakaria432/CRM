import type { Permission } from '~/utils/permissions'

/** Hides what the caller may not do. The API still enforces every permission - this is presentation only. */
export function useCan() {
  const auth = useAuthStore()
  return { can: (permission: Permission): boolean => auth.permissions.has(permission) }
}
