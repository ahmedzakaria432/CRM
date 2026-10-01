import { readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'

// Every i18n/locales/en/<domain>.json is loaded for both locales (Probe 8), so a feature story adds a file pair
// and never edits this list. scripts/check-locales.ts guarantees the ar/ twin exists with the same keys.
const domains = readdirSync(fileURLToPath(new URL('./i18n/locales/en', import.meta.url)))
  .filter((file) => file.endsWith('.json'))
  .sort()

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@nuxt/eslint', '@pinia/nuxt', '@nuxtjs/i18n'],
  css: [
    '@fontsource/ibm-plex-sans-arabic/400.css',
    '@fontsource/ibm-plex-sans-arabic/500.css',
    '@fontsource/ibm-plex-sans-arabic/600.css',
    '@fontsource/ibm-plex-sans-arabic/700.css',
    '@fontsource/noto-kufi-arabic/400.css',
    '@fontsource/noto-kufi-arabic/500.css',
    '@fontsource/noto-kufi-arabic/600.css',
    '@fontsource/noto-kufi-arabic/700.css',
    '@fontsource/tajawal/400.css', // Tajawal has no 600 weight: the browser uses 700
    '@fontsource/tajawal/500.css',
    '@fontsource/tajawal/700.css',
    '~/assets/css/main.css',
  ],
  vite: { plugins: [tailwindcss()] },
  typescript: { strict: true, typeCheck: false }, // typecheck is its own command; build stays fast
  runtimeConfig: {
    apiBase: 'http://localhost:5187', // server-side (SSR) API origin; NUXT_API_BASE overrides
    // browser: same-origin /api (dev proxy in dev, reverse proxy in production). organisationCode (Story 58, D5) is the
    // organisation this host brands before sign-in; NUXT_PUBLIC_ORGANISATION_CODE overrides.
    public: { apiBase: '', organisationCode: 'DEMO' },
  },
  routeRules: {
    '/agent/**': { ssr: false }, // agent workspace is an SPA (Probe 10)
    '/admin/**': { ssr: false }, // Story 06: the admin screens are an SPA too, like /agent/**
    '/_dev/**': { ssr: false },
  },
  nitro: {
    // Probe 9: the browser calls /api on the Nuxt origin, so the API needs no CORS policy in development.
    devProxy: { '/api': { target: 'http://localhost:5187/api', changeOrigin: true } },
  },
  i18n: {
    strategy: 'no_prefix', // one URL per page; the locale lives in the crm-locale cookie
    defaultLocale: 'en',
    locales: [
      { code: 'en', language: 'en', dir: 'ltr', name: 'English', files: domains.map((d) => `en/${d}`) },
      { code: 'ar', language: 'ar', dir: 'rtl', name: 'العربية', files: domains.map((d) => `ar/${d}`) },
    ],
    // Probe 6: cookie first, Accept-Language next, then 'en'. Story 07 adds the profile preference via setLocale.
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'crm-locale',
      redirectOn: 'no prefix',
      alwaysRedirect: false,
      fallbackLocale: 'en',
    },
  },
})
