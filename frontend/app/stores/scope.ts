import type { components } from '~/api/generated/agent'
import { scopeCookieName, allDepartments } from '~/utils/scope'

export type MyScopeDto = components['schemas']['MyScopeDto']

export const useScopeStore = defineStore('scope', () => {
  const myScope = ref<MyScopeDto | null>(null)
  const status = ref<'idle' | 'loading' | 'ready' | 'error'>('idle')

  const activeDepartments = computed(() => (myScope.value?.departments ?? []).filter((d) => d.isActive))

  function setActive(value: string): void {
    const cookie = useCookie<string | null>(scopeCookieName, { sameSite: 'lax', maxAge: 60 * 60 * 24 * 365 })
    cookie.value = value === allDepartments ? null : value
    // Effective permissions follow the active scope, so the profile is re-read with the new X-Crm-Scope header.
    void useAuthStore().loadMe()
    // Every list that reads through useAsyncData/useFetch refetches with the new X-Crm-Scope header.
    void refreshNuxtData()
  }

  /** Signing in or out as another user: forget the previous user's scope. */
  function reset(): void {
    myScope.value = null
    status.value = 'idle'
  }

  return { myScope, status, activeDepartments, setActive, reset }
})
