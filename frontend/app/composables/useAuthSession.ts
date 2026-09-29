import { anonymousSession, type AuthSession } from '~/api/auth-session'

// Module-level, not useState: an AuthSession holds functions, which cannot be serialised into the SSR
// payload. Shared across SSR requests, so it must only ever hold anonymousSession on the server — Story
// 07's plugin that calls setAuthSession with a real session is a *.client.ts plugin for that reason.
const session = shallowRef<AuthSession>(anonymousSession)

export function useAuthSession() {
  function setAuthSession(next: AuthSession): void {
    session.value = next
  }
  return { session, setAuthSession }
}
