<template>
  <div class="flex min-h-dvh flex-col">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary-600 focus:px-4 focus:py-2 focus:text-fg-inverse"
    >
      {{ t('shell.skipToContent') }}
    </a>
    <header class="border-b border-border">
      <div
        class="mx-auto flex max-w-5xl flex-col items-start gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
      >
        <span class="text-lg font-semibold">{{ t('shell.appName') }}</span>
        <div class="flex items-center gap-2">
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
      <div class="mx-auto max-w-5xl px-4 py-4 text-sm text-fg-muted">{{ t('shell.appName') }}</div>
    </footer>
  </div>
</template>

<script setup lang="ts">
const { t } = useI18n()

// Never render a 5xx's real message in production; import.meta.dev only, and never the stack.
function errorMessage(error: unknown): string | undefined {
  if (import.meta.dev && error instanceof Error) return error.message
  return undefined
}
</script>
