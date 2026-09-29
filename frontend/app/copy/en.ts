// Shell copy, hand-off to Story 05 (i18n + RTL). Layouts, error.vue, and the login placeholder read
// strings only from here — the UI kit itself contains no copy at all (every label comes from a prop or
// slot). Story 05 replaces `shellCopy.x` with `t('shell.x')` mechanically.
export const shellCopy = {
  appName: 'Customer Support CRM',
  skipToContent: 'Skip to main content',
  nav: {
    home: 'Home',
    main: 'Main navigation',
    openMenu: 'Open navigation menu',
    closeMenu: 'Close navigation menu',
  },
  theme: {
    toLight: 'Switch to light theme',
    toDark: 'Switch to dark theme',
  },
  error: {
    title: 'Something went wrong',
    retry: 'Try again',
    home: 'Go home',
    reference: 'Reference',
  },
  toast: {
    dismiss: 'Dismiss',
  },
  pagination: {
    label: 'Pagination',
    previous: 'Previous',
    next: 'Next',
    page: 'Page',
  },
  login: {
    title: 'Sign in',
    pending: 'Sign-in arrives with Story 07.',
  },
} as const
