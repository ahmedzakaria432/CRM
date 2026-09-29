import type { Problem } from '~/api/problem'
import { ProblemTypes } from '~/api/problem'

export function useFormErrors() {
  const fieldErrors = ref<Record<string, string[]>>({})
  const formError = ref<string | null>(null)

  /** Returns true if `problem` was a validation problem and its fields (if any) were applied. */
  function apply(problem: Problem): boolean {
    if (problem.type !== ProblemTypes.Validation) return false

    const entries = Object.entries(problem.fieldErrors)
    const formMessages = entries.find(([key]) => key === '_form')?.[1] ?? []
    const rest = Object.fromEntries(entries.filter(([key]) => key !== '_form'))

    fieldErrors.value = rest
    formError.value = formMessages[0] ?? (Object.keys(rest).length === 0 ? problem.detail : null)
    return true
  }

  function errorFor(field: string): string | undefined {
    return fieldErrors.value[field]?.[0]
  }

  function clear(field?: string): void {
    if (field === undefined) {
      fieldErrors.value = {}
      formError.value = null
      return
    }
    fieldErrors.value = Object.fromEntries(
      Object.entries(fieldErrors.value).filter(([key]) => key !== field),
    )
  }

  return { fieldErrors, formError, apply, errorFor, clear }
}
