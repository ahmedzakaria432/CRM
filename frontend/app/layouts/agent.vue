<template>
  <div>
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary-600 focus:px-4 focus:py-2 focus:text-fg-inverse"
    >
      {{ shellCopy.skipToContent }}
    </a>
    <div class="flex min-h-dvh">
      <aside class="hidden w-64 shrink-0 border-e border-border lg:block">
        <nav :aria-label="shellCopy.nav.main" class="flex flex-col gap-1 p-4">
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
          <div class="flex items-center gap-3">
            <button
              type="button"
              class="rounded-md p-2 ui-focus:focus-ring ui-hover:bg-surface-raised lg:hidden"
              :aria-expanded="drawerOpen"
              aria-controls="agent-nav-drawer"
              :aria-label="shellCopy.nav.openMenu"
              @click="drawerOpen = true"
            >
              <span aria-hidden="true">☰</span>
            </button>
            <span class="text-lg font-semibold">{{ shellCopy.appName }}</span>
          </div>
          <div class="flex items-center gap-3">
            <UiThemeToggle :light-label="shellCopy.theme.toLight" :dark-label="shellCopy.theme.toDark" />
            <UiAvatar name="—" size="sm" />
          </div>
        </header>
        <main id="main" tabindex="-1" class="flex-1 overflow-y-auto p-4">
          <NuxtErrorBoundary>
            <slot />
            <template #error="{ error, clearError }">
              <UiErrorState
                :title="shellCopy.error.title"
                :description="errorMessage(error)"
                :retry-label="shellCopy.error.retry"
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
      :title="shellCopy.appName"
      :close-label="shellCopy.nav.closeMenu"
      side="start"
    >
      <nav :aria-label="shellCopy.nav.main" class="flex flex-col gap-1">
        <NuxtLink
          v-for="item in agentNav"
          :key="item.to"
          :to="item.to"
          class="rounded-md px-3 py-2 text-sm font-medium text-fg ui-focus:focus-ring ui-hover:bg-surface-raised"
          @click="drawerOpen = false"
        >
          {{ item.label }}
        </NuxtLink>
      </nav>
    </UiDrawer>
  </div>
</template>

<script setup lang="ts">
import { shellCopy } from '~/copy/en'

interface AgentNavItem {
  to: string
  label: string
}
const agentNav: ReadonlyArray<AgentNavItem> = [{ to: '/agent', label: shellCopy.nav.home }]

const drawerOpen = ref(false)

function errorMessage(error: unknown): string | undefined {
  if (import.meta.dev && error instanceof Error) return error.message
  return undefined
}
</script>
