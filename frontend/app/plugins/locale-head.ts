// <html lang dir> follow the active locale, on the server and after a client-side switch. A plugin rather than
// app.vue, because error.vue renders outside app.vue and must be RTL too.
export default defineNuxtPlugin((nuxtApp) => {
  const i18n = nuxtApp.$i18n
  useHead({
    htmlAttrs: {
      lang: () => i18n.localeProperties.value.language ?? i18n.locale.value,
      dir: () => i18n.localeProperties.value.dir ?? 'ltr',
    },
  })
})
