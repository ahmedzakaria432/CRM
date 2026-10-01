// Story 58: the organisation's --crm-* overrides and Arabic font, in one <style> in the head. SSR routes (/, /login,
// /portal/**) get it server-rendered; SPA routes (/agent/**, /admin/**) load it before mount. Never throws: a failed
// call keeps tokens.css (D10).
export default defineNuxtPlugin(async (nuxtApp) => {
  const branding = useBrandingStore()
  const i18n = nuxtApp.$i18n
  // After an SSR render Pinia already holds the server's answer. If the server had none, keep it that way during
  // hydration: the HTML was rendered with the defaults, so loading now would mismatch.
  if (branding.branding === null && !(import.meta.client && nuxtApp.payload.serverRendered)) {
    await branding.loadPublic()
  }
  useHead({
    style: [{ key: 'crm-brand', id: 'crm-brand', textContent: () => branding.css }],
    title: () => branding.displayName(i18n.locale.value) ?? i18n.t('shell.appName'),
  })
})
