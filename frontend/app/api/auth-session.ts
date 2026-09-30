export interface AuthSession {
  /** Current access token, or null when signed out. */
  accessToken(): string | null
  /** Exchange the refresh credential for a new access token. Resolves true on success. Must not throw. */
  refresh(): Promise<boolean>
  /** Forget all credentials (called before redirecting to login). */
  clear(): void
  /** True for the refresh call itself, which must never trigger another refresh. */
  isRefreshRequest(request: Request): boolean
}

/** Server side and before the client auth plugin runs: nobody is signed in and refresh always fails. */
export const anonymousSession: AuthSession = {
  accessToken: () => null,
  refresh: async () => false,
  clear: () => {},
  isRefreshRequest: () => false,
}
