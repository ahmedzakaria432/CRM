<template>
  <div>
    <h1 class="text-2xl font-bold text-fg">{{ greeting }}</h1>
    <p v-if="scopeLabel" class="mt-1 text-sm text-fg-muted">{{ t('home.agent.scope', { scope: scopeLabel }) }}</p>
    <UiSkeleton v-else-if="scope.status === 'loading'" class="mt-2 w-48" />

    <section aria-labelledby="home-actions-title" class="mt-6">
      <h2 id="home-actions-title" class="sr-only">{{ t('home.agent.actions.title') }}</h2>
      <div class="flex flex-wrap gap-2">
        <UiButton v-if="can(Permissions.TicketsWrite)" @click="createOpen = true">{{ t('home.agent.actions.newTicket') }}</UiButton>
        <NuxtLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="inline-flex items-center justify-center rounded-md border border-border bg-surface-raised px-3 py-2 text-sm font-medium text-fg hover:bg-surface-sunken focus-visible:focus-ring"
        >
          {{ link.label }}
        </NuxtLink>
      </div>
    </section>

    <div v-if="can(Permissions.TicketsRead)" class="mt-8 flex flex-col gap-8">
      <HomeQueueCounts />
      <HomeRecentTickets />
    </div>

    <TicketCreateDrawer v-if="can(Permissions.TicketsWrite)" v-model:open="createOpen" @created="(ticket) => navigateTo(`/agent/tickets/${ticket.id}`)" />
  </div>
</template>

<script setup lang="ts">
import { Permissions } from '~/utils/permissions'
import { allDepartments, localizedName } from '~/utils/scope'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const auth = useAuthStore()
const scope = useScopeStore() // loaded by the layout's useMyScope()
const { can } = useCan()
const createOpen = ref(false)

const greeting = computed(() => {
  const name = auth.me?.displayName
  return name ? t('home.agent.greeting', { name }) : t('home.agent.greetingNoName')
})

// Same labels as the scope switcher (ScopeSwitcher.vue). One department and no explicit choice -> that one.
const scopeLabel = computed<string | null>(() => {
  if (scope.status !== 'ready') return null
  const active = scope.myScope?.activeDepartmentId ?? allDepartments
  const departments = scope.activeDepartments
  const chosen =
    active === allDepartments
      ? departments.length === 1
        ? departments[0]
        : undefined
      : departments.find((d) => d.departmentId === active)
  if (chosen === undefined) return t('organisation.scope.all')
  return t('organisation.scope.option', {
    department: localizedName(chosen.departmentName, locale.value),
    branch: localizedName(chosen.branchName, locale.value),
  })
})

interface QuickAction {
  to: string
  label: string
}
const links = computed<ReadonlyArray<QuickAction>>(() => [
  ...(can(Permissions.CustomersRead) ? [{ to: '/agent/customers', label: t('home.agent.actions.findCustomer') }] : []),
  ...(can(Permissions.AdminBranding) ? [{ to: '/admin/branding', label: t('branding.nav.branding') }] : []),
  ...(can(Permissions.AdminUsers) ? [{ to: '/admin/users', label: t('identity.nav.users') }] : []),
])
</script>
