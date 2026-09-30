/** The portal customer's own profile and password (Story 47). A 400 goes to useFormErrors(), never a toast. */
export function usePortalProfile() {
  const { clients, call } = useApi()
  const quiet = { toastOnError: false }

  return {
    update: (displayName: string, preferredLocale: string) =>
      call(clients.portal.PUT('/api/Portal/Me', { body: { displayName, preferredLocale } }), quiet),
    changePassword: (currentPassword: string, newPassword: string) =>
      call(clients.portal.POST('/api/Portal/Me/change-password', { body: { currentPassword, newPassword } }), quiet),
  }
}
