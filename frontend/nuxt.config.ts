import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@nuxt/eslint', '@pinia/nuxt'],
  css: ['~/assets/css/main.css'],
  vite: { plugins: [tailwindcss()] },
  typescript: { strict: true, typeCheck: false }, // typecheck is its own command; build stays fast
  runtimeConfig: {
    apiBase: 'http://localhost:5187', // server-side (SSR) API origin; NUXT_API_BASE overrides
    public: { apiBase: '' }, // browser: same-origin /api (dev proxy in dev, reverse proxy in production)
  },
  routeRules: {
    '/agent/**': { ssr: false }, // agent workspace is an SPA (Probe 10)
    '/_dev/**': { ssr: false },
  },
  nitro: {
    // Probe 9: the browser calls /api on the Nuxt origin, so the API needs no CORS policy in development.
    devProxy: { '/api': { target: 'http://localhost:5187/api', changeOrigin: true } },
  },
  app: { head: { htmlAttrs: { lang: 'en' } } }, // Story 05 makes lang/dir reactive
})
