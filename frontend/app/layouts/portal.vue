<template>
  <div class="flex min-h-dvh flex-col">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary-600 focus:px-4 focus:py-2 focus:text-fg-inverse"
    >
      {{ t('shell.skipToContent') }}
    </a>
    <header class="border-b border-t-4 border-border border-t-accent-600">
      <div
        class="mx-auto flex max-w-5xl flex-col items-start gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
      >
        <NuxtLink v-if="isPortalRoute" to="/portal" class="text-lg font-semibold text-fg focus-visible:focus-ring">
          <ShellBrandMark />
        </NuxtLink>
        <ShellBrandMark v-else />
        <div class="flex flex-wrap items-center gap-2">
          <nav v-if="isPortalRoute && portal.isAuthenticated" :aria-label="t('portal.nav.label')">
            <ul class="flex flex-wrap items-center gap-1">
              <li>
                <NuxtLink
                  to="/portal"
                  exact-active-class="bg-surface-sunken"
                  class="rounded-md px-2.5 py-1.5 text-sm text-fg hover:bg-surface-raised focus-visible:focus-ring"
                >
                  {{ t('portal.nav.tickets') }}
                </NuxtLink>
              </li>
              <li>
                <NuxtLink
                  to="/portal/tickets/new"
                  active-class="bg-surface-sunken"
                  class="rounded-md px-2.5 py-1.5 text-sm text-fg hover:bg-surface-raised focus-visible:focus-ring"
                >
                  {{ t('portal.nav.new') }}
                </NuxtLink>
              </li>
              <li>
                <NuxtLink
                  to="/portal/account"
                  active-class="bg-surface-sunken"
                  class="rounded-md px-2.5 py-1.5 text-sm text-fg hover:bg-surface-raised focus-visible:focus-ring"
                >
                  {{ t('portal.nav.account') }}
                </NuxtLink>
              </li>
              <li>
                <UiButton variant="ghost" size="sm" @click="portal.logout()">{{ t('portal.nav.signOut') }}</UiButton>
              </li>
            </ul>
          </nav>
          <ShellLocaleSwitcher />
          <UiThemeToggle :light-label="t('shell.theme.toLight')" :dark-label="t('shell.theme.toDark')" />
        </div>
      </div>
    </header>
    <main id="main" tabindex="-1" class="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
      <NuxtErrorBoundary>
        <slot />
        <template #error="{ error, clearError }">
          <UiErrorState
            :title="t('shell.error.title')"
            :description="errorMessage(error)"
            :retry-label="t('shell.error.retry')"
            @retry="clearError()"
          />
        </template>
      </NuxtErrorBoundary>
    </main>
    <footer class="border-t border-border">
      <div class="mx-auto max-w-5xl px-4 py-4 text-sm text-fg-muted">{{ brandName }}</div>
    </footer>
  </div>
</template>

<script setup lang="ts">
const { t, locale } = useI18n()
const route = useRoute()
const portal = usePortalAuthStore()
const branding = useBrandingStore()
const brandName = computed(() => branding.displayName(locale.value) ?? t('shell.appName'))

// The customer portal nav shows on /portal/** for a signed-in customer; the staff /login page shares this layout.
const isPortalRoute = computed(() => route.path === '/portal' || route.path.startsWith('/portal/'))

// Never render a 5xx's real message in production; import.meta.dev only, and never the stack.
function errorMessage(error: unknown): string | undefined {
  if (import.meta.dev && error instanceof Error) return error.message
  return undefined
}
</script>
