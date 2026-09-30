import { ProblemTypes } from '~/api/problem'
import { scopeCookieName } from '~/utils/scope'

/** Loads the caller's department scope into the shared store. Self-heals a stale `crm-scope` cookie: if the
 * active department was removed since the last visit, the API answers 403 Organisation.ActiveScopeRejected -
 * reset to "all" and retry once, rather than showing an error screen for a cookie the user never touched. */
export function useMyScope() {
  const scope = useScopeStore()
  const { clients, call } = useApi()

  async function load(): Promise<void> {
    scope.status = 'loading'

    const result = await call(clients.agent.GET('/api/Agent/MyScope'), { toastOnError: false })

    if (result.ok) {
      scope.myScope = result.data
      scope.status = 'ready'
      return
    }

    if (result.problem.type === ProblemTypes.Forbidden && result.problem.code === 'Organisation.ActiveScopeRejected') {
      const cookie = useCookie<string | null>(scopeCookieName, { sameSite: 'lax' })
      cookie.value = null

      const retry = await call(clients.agent.GET('/api/Agent/MyScope'), { toastOnError: false })
      if (retry.ok) {
        scope.myScope = retry.data
        scope.status = 'ready'
        return
      }
    }

    scope.status = 'error'
  }

  onMounted(() => {
    if (scope.status === 'idle') void load()
  })

  return { scope, reload: load }
}
