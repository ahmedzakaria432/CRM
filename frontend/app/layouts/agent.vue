<template>
  <div>
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary-600 focus:px-4 focus:py-2 focus:text-fg-inverse"
    >
      {{ t('shell.skipToContent') }}
    </a>
    <div class="flex min-h-dvh">
      <aside class="hidden w-64 shrink-0 border-e border-border md:block">
        <nav :aria-label="t('shell.nav.main')" class="flex flex-col gap-1 p-4">
          <NuxtLink
            v-for="item in agentNav"
            :key="item.to"
            :to="item.to"
            class="rounded-md px-3 py-2 text-sm font-medium text-fg ui-focus:focus-ring ui-hover:bg-surface-raised"
          >
            {{ item.label }}
          </NuxtLink>
        </nav>
      </aside>
      <div class="flex min-w-0 flex-1 flex-col">
        <header class="flex items-center justify-between gap-4 border-b border-border px-4 py-3">
          <div class="flex min-w-0 items-center gap-3">
            <button
              type="button"
              class="rounded-md p-2 ui-focus:focus-ring ui-hover:bg-surface-raised md:hidden"
              :aria-expanded="drawerOpen"
              aria-controls="agent-nav-drawer"
              :aria-label="t('shell.nav.openMenu')"
              @click="drawerOpen = true"
            >
              <span aria-hidden="true">☰</span>
            </button>
            <span class="truncate text-lg font-semibold">{{ t('shell.appName') }}</span>
          </div>
          <div class="flex items-center gap-3">
            <ShellScopeSwitcher />
            <ShellLocaleSwitcher />
            <UiThemeToggle :light-label="t('shell.theme.toLight')" :dark-label="t('shell.theme.toDark')" />
            <div class="shrink-0"><UiAvatar :name="auth.me?.displayName ?? '—'" size="sm" /></div>
            <UiButton variant="ghost" size="sm" class="max-sm:hidden" @click="auth.logout()">
              {{ t('identity.menu.signOut') }}
            </UiButton>
          </div>
        </header>
        <main id="main" tabindex="-1" class="flex-1 overflow-y-auto p-4">
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
      </div>
    </div>
    <UiDrawer
      id="agent-nav-drawer"
      v-model:open="drawerOpen"
      :title="t('shell.appName')"
      :close-label="t('shell.nav.closeMenu')"
      side="start"
    >
      <nav :aria-label="t('shell.nav.main')" class="flex flex-col gap-1">
        <NuxtLink
          v-for="item in agentNav"
          :key="item.to"
          :to="item.to"
          class="rounded-md px-3 py-2 text-sm font-medium text-fg ui-focus:focus-ring ui-hover:bg-surface-raised"
          @click="drawerOpen = false"
        >
          {{ item.label }}
        </NuxtLink>
        <UiButton variant="ghost" class="justify-start" @click="signOutFromDrawer()">
          {{ t('identity.menu.signOut') }}
        </UiButton>
      </nav>
    </UiDrawer>
  </div>
</template>

<script setup lang="ts">
import { Permissions } from '~/utils/permissions'

const { t } = useI18n()
useMyScope()
const auth = useAuthStore()
const { can } = useCan()

interface AgentNavItem {
  to: string
  label: string
}
// Computed so the label follows a locale switch and the admin nav items follow the signed-in user's permissions.
const agentNav = computed<ReadonlyArray<AgentNavItem>>(() => [
  { to: '/agent', label: t('shell.nav.home') },
  ...(can(Permissions.CustomersRead) ? [{ to: '/agent/customers', label: t('customers.nav.customers') }] : []),
  ...(can(Permissions.AdminOrganisation)
    ? [
        { to: '/admin/branches', label: t('organisation.nav.branches') },
        { to: '/admin/departments', label: t('organisation.nav.departments') },
      ]
    : []),
  ...(can(Permissions.AdminUsers) ? [{ to: '/admin/users', label: t('identity.nav.users') }] : []),
  { to: '/agent/account', label: t('identity.menu.account') },
])

const drawerOpen = ref(false)

function signOutFromDrawer(): void {
  drawerOpen.value = false
  void auth.logout()
}

function errorMessage(error: unknown): string | undefined {
  if (import.meta.dev && error instanceof Error) return error.message
  return undefined
}
</script>
