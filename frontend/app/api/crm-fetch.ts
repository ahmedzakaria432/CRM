import type { AuthSession } from './auth-session'
import { CorrelationHeader, newCorrelationId } from './correlation'

export interface CrmFetchOptions {
  baseFetch: (input: Request) => Promise<Response>
  session: () => AuthSession
  onUnauthenticated: () => void | Promise<void>
}

/**
 * Wraps `baseFetch` with the correlation id, bearer token, and the 401 -> one refresh -> one retry ->
 * login contract (Task 6d). Story 07 must not change this retry contract.
 *
 * The single-flight refresh guard lives in this closure, one per `createCrmFetch` call, not at true module
 * scope: `/_dev/api`'s fake-transport panel builds its own private `createCrmFetch` with a scripted
 * transport specifically to exercise this logic in isolation (scenarios A-E), and it must not share
 * in-flight state with the real app instance built in `app/plugins/api.ts`.
 */
export function createCrmFetch(options: CrmFetchOptions): (input: Request) => Promise<Response> {
  const { baseFetch, session, onUnauthenticated } = options
  let inflightRefresh: Promise<boolean> | null = null

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

  function refreshOnce(): Promise<boolean> {
    if (inflightRefresh === null) {
      inflightRefresh = session()
        .refresh()
        .finally(() => {
          inflightRefresh = null
        })
    }
    return inflightRefresh
  }

  return async function crmFetch(input: Request): Promise<Response> {
    // Clone before sending: a body stream can be read once, and the retry (if any) needs an unread copy.
    const retryClone = input.clone()

    const correlationId = input.headers.get(CorrelationHeader) ?? newCorrelationId()
    const initial = withHeaders(input, correlationId, session().accessToken())
    const response = await baseFetch(initial)

    if (response.status !== 401 || session().isRefreshRequest(input)) {
      return response
    }

    const refreshed = await refreshOnce()
    if (refreshed) {
      const retryRequest = withHeaders(retryClone, correlationId, session().accessToken())
      const retryResponse = await baseFetch(retryRequest)
      if (retryResponse.status !== 401) {
        return retryResponse
      }
      // Retry also came back 401: no second refresh, no loop.
      session().clear()
      await onUnauthenticated()
      return retryResponse
    }

    // Refresh failed outright: never retry, go straight to login.
    session().clear()
    await onUnauthenticated()
    return response
  }
}
