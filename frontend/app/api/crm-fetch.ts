import type { AuthSession } from './auth-session'
import { CorrelationHeader, newCorrelationId } from './correlation'

export interface CrmFetchOptions {
  baseFetch: (input: Request) => Promise<Response>
  /** Story 47: which session a request belongs to (staff or portal customer). */
  session: (request: Request) => AuthSession
  onUnauthenticated: (request: Request) => void | Promise<void>
}

/**
 * Wraps `baseFetch` with the correlation id, bearer token, and the 401 -> one refresh -> one retry ->
 * login contract (Task 6d). Story 07 must not change this retry contract.
 *
 * The single-flight refresh guard lives in this closure, one per `createCrmFetch` call, not at true module
 * scope: `/_dev/api`'s fake-transport panel builds its own private `createCrmFetch` with a scripted
 * transport specifically to exercise this logic in isolation (scenarios A-E), and it must not share
 * in-flight state with the real app instance built in `app/plugins/api.ts`.
 *
 * Story 47: the guard is per *session object*, so a portal refresh never joins a staff one (two audiences, two
 * sessions, in one browser). The 401 -> one refresh -> one retry -> login contract itself is unchanged.
 */
export function createCrmFetch(options: CrmFetchOptions): (input: Request) => Promise<Response> {
  const { baseFetch, session, onUnauthenticated } = options
  const inflightRefreshes = new WeakMap<AuthSession, Promise<boolean>>()

  function withHeaders(request: Request, correlationId: string, token: string | null): Request {
    const headers = new Headers(request.headers)
    headers.set(CorrelationHeader, correlationId)
    if (token !== null) {
      headers.set('Authorization', `Bearer ${token}`)
    } else {
      headers.delete('Authorization')
    }
    return new Request(request, { headers })
  }

  function refreshOnce(current: AuthSession): Promise<boolean> {
    let inflight = inflightRefreshes.get(current)
    if (inflight === undefined) {
      inflight = current.refresh().finally(() => {
        inflightRefreshes.delete(current)
      })
      inflightRefreshes.set(current, inflight)
    }
    return inflight
  }

  return async function crmFetch(input: Request): Promise<Response> {
    // Clone before sending: a body stream can be read once, and the retry (if any) needs an unread copy.
    const retryClone = input.clone()

    const correlationId = input.headers.get(CorrelationHeader) ?? newCorrelationId()
    const current = session(input)
    const initial = withHeaders(input, correlationId, current.accessToken())
    const response = await baseFetch(initial)

    if (response.status !== 401 || current.isRefreshRequest(input)) {
      return response
    }

    const refreshed = await refreshOnce(current)
    if (refreshed) {
      const retryRequest = withHeaders(retryClone, correlationId, current.accessToken())
      const retryResponse = await baseFetch(retryRequest)
      if (retryResponse.status !== 401) {
        return retryResponse
      }
      // Retry also came back 401: no second refresh, no loop.
      current.clear()
      await onUnauthenticated(input)
      return retryResponse
    }

    // Refresh failed outright: never retry, go straight to login.
    current.clear()
    await onUnauthenticated(input)
    return response
  }
}
