import type { CrmClients } from '~/api/clients'
import { toProblem, networkProblem, ProblemTypes, type Problem } from '~/api/problem'

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }

export interface CallOptions {
  /** Default true. Non-validation, non-401 problems raise an error toast. */
  toastOnError?: boolean
}

interface OpenApiFetchResult<T> {
  data?: T
  error?: unknown
  response: Response
}

export function useApi(): {
  clients: CrmClients
  call: <T>(request: Promise<OpenApiFetchResult<T>>, options?: CallOptions) => Promise<ApiResult<T>>
} {
  const { $api } = useNuxtApp()
  const toasts = useToasts()

  function maybeToast(problem: Problem, options?: CallOptions): void {
    if (options?.toastOnError === false) return
    // Validation: the form shows it. Unauthorized: crm-fetch already redirected. Nothing else to add.
    if (problem.type === ProblemTypes.Validation) return
    if (problem.type === ProblemTypes.Unauthorized) return
    toasts.push({
      tone: 'danger',
      title: problem.title,
      description: problem.detail,
      correlationId: problem.correlationId,
    })
  }

  async function call<T>(
    request: Promise<OpenApiFetchResult<T>>,
    options?: CallOptions,
  ): Promise<ApiResult<T>> {
    let result: OpenApiFetchResult<T>
    try {
      result = await request
    } catch (error) {
      // A thrown fetch (offline, aborted) never becomes an unhandled rejection.
      const problem = networkProblem(error)
      maybeToast(problem, options)
      return { ok: false, problem }
    }

    if (result.error !== undefined) {
      const problem = toProblem(result.response.status, result.error, result.response.headers)
      maybeToast(problem, options)
      return { ok: false, problem }
    }

    return { ok: true, data: result.data as T }
  }

  return { clients: $api, call }
}
