import { anonymousSession, type AuthSession } from '~/api/auth-session'

// Module-level, not useState: an AuthSession holds functions, which cannot be serialised into the SSR
// payload. Shared across SSR requests, so it must only ever hold anonymousSession on the server - the
// plugin that calls setAuthSession / setPortalSession with a real session is a *.client.ts plugin for that reason.
// Story 47: one session per audience - the staff one (Agent/Admin) and the portal customer one (Portal).
const session = shallowRef<AuthSession>(anonymousSession)
const portalSession = shallowRef<AuthSession>(anonymousSession)

export function useAuthSession() {
  function setAuthSession(next: AuthSession): void {
    session.value = next
  }
  function setPortalSession(next: AuthSession): void {
    portalSession.value = next
  }
  return { session, portalSession, setAuthSession, setPortalSession }
}
