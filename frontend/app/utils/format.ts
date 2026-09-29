export type CrmLocale = 'en' | 'ar'

/** Pinned BCP 47 tags (Probe 12). Gregorian only (Hijri is out of scope); Arabic-Indic digits, the ar-SA CLDR
 * default. Switching Arabic to Latin digits is a one-line change here: 'ar-SA-u-ca-gregory-nu-latn'. */
export const intlLocale: Readonly<Record<CrmLocale, string>> = { en: 'en-GB', ar: 'ar-SA-u-ca-gregory-nu-arab' }

/** One zone for SSR and browser, so server and client render the same text. Tenant/branch time zones: Story 06+. */
export const crmTimeZone = 'Asia/Riyadh'

/** LEFT-TO-RIGHT ISOLATE ... POP DIRECTIONAL ISOLATE - wraps an identifier placed inside RTL text (title
 * attributes, toasts) so its own script's direction is isolated from the surrounding paragraph. */
const LeftToRightIsolate = '⁦'
const PopDirectionalIsolate = '⁩'

export function isolateLtr(text: string): string {
  return `${LeftToRightIsolate}${text}${PopDirectionalIsolate}`
}

export function toCrmLocale(value: string): CrmLocale {
  return value === 'ar' ? 'ar' : 'en'
}

function toDate(value: Date | string): Date | null {
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function cacheKey(locale: CrmLocale, options: unknown): string {
  return `${intlLocale[locale]}:${JSON.stringify(options)}`
}

const dateTimeFormatCache = new Map<string, Intl.DateTimeFormat>()

function getDateTimeFormat(locale: CrmLocale, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  const key = cacheKey(locale, options)
  const cached = dateTimeFormatCache.get(key)
  if (cached !== undefined) return cached
  const format = new Intl.DateTimeFormat(intlLocale[locale], { timeZone: crmTimeZone, ...options })
  dateTimeFormatCache.set(key, format)
  return format
}

const numberFormatCache = new Map<string, Intl.NumberFormat>()

function getNumberFormat(locale: CrmLocale, options?: Intl.NumberFormatOptions): Intl.NumberFormat {
  const key = cacheKey(locale, options)
  const cached = numberFormatCache.get(key)
  if (cached !== undefined) return cached
  const format = new Intl.NumberFormat(intlLocale[locale], options)
  numberFormatCache.set(key, format)
  return format
}

const relativeTimeFormatCache = new Map<string, Intl.RelativeTimeFormat>()

function getRelativeTimeFormat(locale: CrmLocale): Intl.RelativeTimeFormat {
  const key = intlLocale[locale]
  const cached = relativeTimeFormatCache.get(key)
  if (cached !== undefined) return cached
  const format = new Intl.RelativeTimeFormat(intlLocale[locale], { numeric: 'auto' })
  relativeTimeFormatCache.set(key, format)
  return format
}

export function formatDate(value: Date | string, locale: CrmLocale, style: 'short' | 'medium' | 'long' = 'medium'): string {
  const date = toDate(value)
  if (date === null) return ''
  return getDateTimeFormat(locale, { dateStyle: style }).format(date)
}

export function formatTime(value: Date | string, locale: CrmLocale): string {
  const date = toDate(value)
  if (date === null) return ''
  return getDateTimeFormat(locale, { timeStyle: 'short' }).format(date)
}

export function formatDateTime(value: Date | string, locale: CrmLocale): string {
  const date = toDate(value)
  if (date === null) return ''
  return getDateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

interface RelativeUnit {
  unit: Intl.RelativeTimeFormatUnit
  seconds: number
}

// Largest unit first; the loop below falls through to 'second' when nothing bigger fits.
const RelativeUnits: ReadonlyArray<RelativeUnit> = [
  { unit: 'year', seconds: 31_536_000 },
  { unit: 'month', seconds: 2_592_000 },
  { unit: 'week', seconds: 604_800 },
  { unit: 'day', seconds: 86_400 },
  { unit: 'hour', seconds: 3_600 },
  { unit: 'minute', seconds: 60 },
]
const SecondUnit: RelativeUnit = { unit: 'second', seconds: 1 }

function pickRelativeUnit(absSeconds: number): RelativeUnit {
  for (const entry of RelativeUnits) {
    if (absSeconds >= entry.seconds) return entry
  }
  return SecondUnit
}

/** `now` is a parameter: a relative time must be computed on the client, never during SSR. */
export function formatRelativeTime(value: Date | string, locale: CrmLocale, now: Date): string {
  const date = toDate(value)
  if (date === null) return ''
  const diffSeconds = (date.getTime() - now.getTime()) / 1000
  const unit = pickRelativeUnit(Math.abs(diffSeconds))
  return getRelativeTimeFormat(locale).format(Math.round(diffSeconds / unit.seconds), unit.unit)
}

export function formatNumber(value: number, locale: CrmLocale, options?: Intl.NumberFormatOptions): string {
  return getNumberFormat(locale, options).format(value)
}

export function formatCurrency(amount: number, locale: CrmLocale, currency = 'SAR'): string {
  return getNumberFormat(locale, { style: 'currency', currency }).format(amount)
}

interface FileSizeUnit {
  unit: 'byte' | 'kilobyte' | 'megabyte' | 'gigabyte'
  threshold: number
}

// Decimal (1000), largest unit first; falls through to 'byte' when nothing bigger fits.
const FileSizeUnits: ReadonlyArray<FileSizeUnit> = [
  { unit: 'gigabyte', threshold: 1_000_000_000 },
  { unit: 'megabyte', threshold: 1_000_000 },
  { unit: 'kilobyte', threshold: 1_000 },
]
const ByteUnit: FileSizeUnit = { unit: 'byte', threshold: 1 }

function pickFileSizeUnit(absBytes: number): FileSizeUnit {
  for (const entry of FileSizeUnits) {
    if (absBytes >= entry.threshold) return entry
  }
  return ByteUnit
}

export function formatFileSize(bytes: number, locale: CrmLocale): string {
  const unit = pickFileSizeUnit(Math.abs(bytes))
  return getNumberFormat(locale, {
    style: 'unit',
    unit: unit.unit,
    unitDisplay: 'short',
    maximumFractionDigits: 1,
  }).format(bytes / unit.threshold)
}
