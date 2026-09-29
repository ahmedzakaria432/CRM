import {
  toCrmLocale,
  formatDate,
  formatTime,
  formatDateTime,
  formatRelativeTime,
  formatNumber,
  formatCurrency,
  formatFileSize,
} from '~/utils/format'

/** Binds app/utils/format.ts to the current UI locale. Reads `locale.value` on each call (not once), so a
 * locale switch is reflected without re-creating the composable. */
export function useFormat(): {
  date: (value: Date | string, style?: 'short' | 'medium' | 'long') => string
  time: (value: Date | string) => string
  dateTime: (value: Date | string) => string
  relativeTime: (value: Date | string, now: Date) => string
  number: (value: number, options?: Intl.NumberFormatOptions) => string
  currency: (amount: number, currency?: string) => string
  fileSize: (bytes: number) => string
} {
  const { locale } = useI18n()

  return {
    date: (value, style) => formatDate(value, toCrmLocale(locale.value), style),
    time: (value) => formatTime(value, toCrmLocale(locale.value)),
    dateTime: (value) => formatDateTime(value, toCrmLocale(locale.value)),
    relativeTime: (value, now) => formatRelativeTime(value, toCrmLocale(locale.value), now),
    number: (value, options) => formatNumber(value, toCrmLocale(locale.value), options),
    currency: (amount, currency) => formatCurrency(amount, toCrmLocale(locale.value), currency),
    fileSize: (bytes) => formatFileSize(bytes, toCrmLocale(locale.value)),
  }
}
