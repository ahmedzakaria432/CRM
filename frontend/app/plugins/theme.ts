// Reads the theme cookie (server and client), so SSR emits the right data-theme with no flash of the
// wrong theme. Default is 'light'; there is no 'system' option in this story (Story 58 may revisit it).
export default defineNuxtPlugin(() => {
  const { theme } = useTheme()
  useHead({ htmlAttrs: { 'data-theme': theme } })
})
