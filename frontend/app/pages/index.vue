<template>
  <div>
    <section class="flex flex-col items-center gap-4 py-6 text-center sm:py-10">
      <ShellBrandMark />
      <h1 class="text-2xl font-bold text-fg sm:text-3xl">{{ t('home.landing.title', { name: brandName }) }}</h1>
      <p class="max-w-prose text-fg-muted">{{ t('home.landing.intro') }}</p>
    </section>

    <h2 class="sr-only">{{ t('home.landing.entryLabel') }}</h2>
    <ul class="mt-6 grid gap-4 sm:grid-cols-2">
      <li class="flex h-full flex-col gap-3 rounded-lg border border-border bg-surface p-6">
        <h3 class="text-lg font-semibold text-fg">{{ t('home.landing.staff.title') }}</h3>
        <p class="flex-1 text-sm text-fg-muted">{{ t('home.landing.staff.description') }}</p>
        <div>
          <NuxtLink
            to="/login"
            class="inline-flex items-center justify-center rounded-md bg-primary-600 px-4 py-2.5 text-base font-medium text-fg-inverse hover:bg-primary-700 focus-visible:focus-ring"
          >
            {{ t('home.landing.staff.action') }}
          </NuxtLink>
        </div>
      </li>
      <li class="flex h-full flex-col gap-3 rounded-lg border border-border bg-surface p-6">
        <h3 class="text-lg font-semibold text-fg">{{ t('home.landing.portal.title') }}</h3>
        <p class="flex-1 text-sm text-fg-muted">{{ t('home.landing.portal.description') }}</p>
        <div class="flex flex-wrap gap-2">
          <NuxtLink
            to="/portal/login"
            class="inline-flex items-center justify-center rounded-md bg-primary-600 px-4 py-2.5 text-base font-medium text-fg-inverse hover:bg-primary-700 focus-visible:focus-ring"
          >
            {{ t('home.landing.portal.signIn') }}
          </NuxtLink>
          <NuxtLink
            to="/portal/register"
            class="inline-flex items-center justify-center rounded-md border border-border bg-surface-raised px-4 py-2.5 text-base font-medium text-fg hover:bg-surface-sunken focus-visible:focus-ring"
          >
            {{ t('home.landing.portal.register') }}
          </NuxtLink>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
// The landing (Story 59): portal layout, SSR, no API call of its own - branding comes from plugins/branding.ts and
// falls back to the product defaults (Story 58 D10). A signed-in user never sees it (middleware/auth.global.ts).
definePageMeta({ layout: 'portal' })

const { t, locale } = useI18n()
const branding = useBrandingStore()
const brandName = computed(() => branding.displayName(locale.value) ?? t('shell.appName'))
</script>
