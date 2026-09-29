export type Theme = 'light' | 'dark'

export function useTheme() {
  const cookie = useCookie<Theme>('crm-theme', {
    default: () => 'light',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 365,
  })
  // Edge Cases & Failure Modes: a tampered cookie value ("purple") coerces to light, never crashes.
  const theme = computed<Theme>(() => (cookie.value === 'dark' ? 'dark' : 'light'))
  function setTheme(next: Theme): void {
    cookie.value = next
  }
  function toggleTheme(): void {
    setTheme(theme.value === 'dark' ? 'light' : 'dark')
  }
  return { theme, setTheme, toggleTheme }
}
