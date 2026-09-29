import { defineNuxtModule } from 'nuxt/kit'
import type { NuxtPage } from 'nuxt/schema'

function prune(pages: NuxtPage[]): void {
  for (let i = pages.length - 1; i >= 0; i--) {
    const page = pages[i]
    if (page === undefined) continue
    if (page.path.startsWith('/_dev')) pages.splice(i, 1)
    else if (page.children) prune(page.children)
  }
}

/** /_dev/** is the review surface for a project without component tests. It never ships. */
export default defineNuxtModule({
  meta: { name: 'crm-dev-only-pages' },
  setup(_options, nuxt) {
    if (nuxt.options.dev) return
    nuxt.hook('pages:extend', prune)
  },
})
