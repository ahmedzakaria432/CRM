import type { components } from '~/api/generated/public'
import type { Theme } from '~/composables/useTheme'

export type PublicBrandingDto = components['schemas']['PublicBrandingDto']
export type BrandTokens = components['schemas']['BrandTokensDto']

/** Mirrors backend ArabicFont (Story 58, D9). The CSS family is built from this map only, never from a response (S3). */
export const arabicFontFamilies = {
  IbmPlexSansArabic: "'IBM Plex Sans Arabic'",
  NotoKufiArabic: "'Noto Kufi Arabic'",
  Tajawal: "'Tajawal'",
} as const
export type ArabicFontKey = keyof typeof arabicFontFamilies
export const arabicFontKeys = Object.keys(arabicFontFamilies) as ArabicFontKey[]

const fallbackStack = "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif"

const tokenName = /^--crm-(primary|accent)-(50|[1-9]00|950)$/
const hexValue = /^#[0-9a-fA-F]{6}$/

/** Logos are shown only through <img src>, never inline: a script cannot run in an image context (S2). */
export function brandLogoUrl(id: string | null | undefined): string | null {
  return id ? `/api/Public/Branding/logos/${id}` : null
}

export function isArabicFontKey(value: string | null | undefined): value is ArabicFontKey {
  return typeof value === 'string' && Object.hasOwn(arabicFontFamilies, value)
}

/** Only well-formed token entries survive: a bad name or value is dropped, never written to CSS (S3). */
export function safeTokens(map: Record<string, string> | null | undefined): Record<string, string> {
  const safe: Record<string, string> = {}
  for (const [name, value] of Object.entries(map ?? {})) {
    if (tokenName.test(name) && hexValue.test(value)) safe[name] = value
  }
  return safe
}

function declarations(map: Record<string, string> | null | undefined): string {
  return Object.entries(safeTokens(map))
    .map(([name, value]) => `${name}:${value}`)
    .join(';')
}

/** The text of <style id="crm-brand">. Doubled selectors (0,2,0) outrank tokens.css's :root / [data-theme] (0,1,0)
 * whatever the order of the two style elements; the dark block comes after the light one. */
export function buildBrandCss(branding: PublicBrandingDto | null): string {
  if (!branding) return ''
  const parts: string[] = []
  if (branding.tokens) {
    parts.push(`:root:root,[data-theme='light'][data-theme='light']{${declarations(branding.tokens.light)}}`)
    parts.push(`[data-theme='dark'][data-theme='dark']{${declarations(branding.tokens.dark)}}`)
  }
  const font = branding.arabicFont
  if (isArabicFontKey(font) && font !== 'IbmPlexSansArabic') {
    parts.push(`html[lang='ar'][lang='ar']{--crm-font-sans:${arabicFontFamilies[font]}, ${fallbackStack}}`)
  }
  return parts.join('')
}

/** Inline style for a preview frame: the token overrides (and the Arabic font) on one subtree. */
export function previewStyle(
  tokens: BrandTokens | null | undefined,
  theme: Theme,
  font: string | null | undefined,
): Record<string, string> {
  const style = safeTokens(theme === 'dark' ? tokens?.dark : tokens?.light)
  if (isArabicFontKey(font)) style['--crm-font-sans'] = `${arabicFontFamilies[font]}, ${fallbackStack}`
  return style
}
