/** Story 59 (D2): non-secret hints that a session probably exists, so `/` can route a returning user without calling
 * refresh (HttpOnly, path-scoped and rate-limited). They grant nothing - the API and the guards still decide. */
export const staffSessionHintCookie = 'crm-staff-session'
export const portalSessionHintCookie = 'crm-portal-session'

/** 14 days = Identity:Sessions:RefreshTokenLifetime. Rewritten on every successful restore. */
export const sessionHintCookieOptions = { sameSite: 'lax', path: '/', maxAge: 60 * 60 * 24 * 14 } as const
