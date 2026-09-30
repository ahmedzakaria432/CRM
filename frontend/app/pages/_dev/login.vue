<template>
  <div class="mx-auto flex max-w-md flex-col gap-4 p-6">
    <h1 class="text-2xl font-bold text-fg">Development sign-in</h1>
    <p class="text-sm text-fg-muted">
      Story 06: X-Dev-User, honoured only while the API runs in Development. Story 07 replaces this page with
      real sign-in.
    </p>
    <p class="text-sm text-fg-muted">Current user: <code>{{ devUserCookie ?? '(none)' }}</code></p>

    <div class="flex flex-col gap-2">
      <UiButton v-for="user in users" :key="user.id" variant="secondary" @click="signIn(user.id)">
        {{ user.label }}
      </UiButton>
    </div>

    <UiButton variant="ghost" @click="signOut()">Sign out</UiButton>
  </div>
</template>

<script setup lang="ts">
import { scopeCookieName, devUserCookieName } from '~/utils/scope'

definePageMeta({ layout: 'portal' })

const devUserCookie = useCookie<string | null>(devUserCookieName, { sameSite: 'lax' })
const scopeCookie = useCookie<string | null>(scopeCookieName, { sameSite: 'lax' })

// Well-known dev-seed ids (Crm.Modules.Organisation.Contracts.DevSeed.OrganisationDevSeed). Dev-only English
// labels, exempt from the bare-string-in-template lint rule (this page never ships to production).
const users = [
  { id: '06000000-0000-0000-0003-000000000001', label: 'Admin — every department' },
  { id: '06000000-0000-0000-0003-000000000002', label: 'Supervisor — Riyadh Care & Billing' },
  { id: '06000000-0000-0000-0003-000000000003', label: 'Agent A — Riyadh Customer Care' },
  { id: '06000000-0000-0000-0003-000000000004', label: 'Agent B — Jeddah Customer Care' },
]

function signIn(id: string): void {
  devUserCookie.value = id
  scopeCookie.value = null
  navigateTo('/agent')
}

function signOut(): void {
  devUserCookie.value = null
  scopeCookie.value = null
}
</script>
