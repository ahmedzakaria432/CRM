import type { PublicBrandingDto } from '~/utils/branding'

// Server only. Public data, no user state - safe to share across requests; without it every SSR render spends the
// Public Area's per-IP rate-limit budget (Story 58, D13, R6). A saved change reaches the portal within the TTL.
const ttlMs = 15_000
const entries = new Map<string, { expires: number; value: PublicBrandingDto | null }>()

export async function cachedServerBranding(
  code: string,
  load: () => Promise<PublicBrandingDto | null>,
): Promise<PublicBrandingDto | null> {
  const hit = entries.get(code)
  if (hit && hit.expires > Date.now()) return hit.value
  const value = await load()
  // A failed load (null) is cached for the same short time: a down API is not hammered once per page render.
  entries.set(code, { expires: Date.now() + ttlMs, value })
  return value
}
