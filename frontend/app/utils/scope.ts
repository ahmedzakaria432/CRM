/** Persists the agent's chosen department scope across reloads; read by every API call (app/plugins/api.ts). */
export const scopeCookieName = 'crm-scope'

/** The X-Crm-Scope header value meaning "every department the caller belongs to". */
export const allDepartments = 'all'

export interface LocalizedTextLike {
  ar?: string | null
  en?: string | null
}

/** `ar` -> the Arabic name, anything else -> the English name. Falls back to the other language if the
 * preferred one is missing (never renders an empty string). */
export function localizedName(text: LocalizedTextLike | undefined, locale: string): string {
  if (!text) return ''
  const preferred = locale === 'ar' ? text.ar : text.en
  const fallback = locale === 'ar' ? text.en : text.ar
  return preferred ?? fallback ?? ''
}
