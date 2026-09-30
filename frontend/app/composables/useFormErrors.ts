import type { Problem } from '~/api/problem'
import { ProblemTypes } from '~/api/problem'
import { problemMessage } from '~/api/problem-message'

export function useFormErrors() {
  const { $i18n } = useNuxtApp()
  const translate = { t: (key: string) => $i18n.t(key), te: (key: string) => $i18n.te(key) }
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

  /** A problem whose `code` names a field in `fieldForCode` puts the localized errors.code.* text on that field
   * (keeping any other field errors the problem carried). Any other 422 domain rule goes to the form. Returns false
   * for everything else - the caller decides (apply() for a plain 400, a toast otherwise). */
  function applyCode(problem: Problem, fieldForCode: Readonly<Record<string, string>>): boolean {
    const field: string | undefined = problem.code === null ? undefined : fieldForCode[problem.code]
    const message = problemMessage(problem, translate).description ?? problem.title
    if (field !== undefined) {
      fieldErrors.value = { ...problem.fieldErrors, [field]: [message] }
      formError.value = null
      return true
    }
    if (problem.type === ProblemTypes.DomainRule) {
      fieldErrors.value = {}
      formError.value = message
      return true
    }
    return false
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

  return { fieldErrors, formError, apply, applyCode, errorFor, clear }
}
