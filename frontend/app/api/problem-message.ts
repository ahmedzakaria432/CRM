import type { Problem } from './problem'

type Translate = { t: (key: string) => string; te: (key: string) => boolean }

const ProblemUrnPrefix = 'urn:crm:problem:'

/** Toast/alert text in the UI locale: a known code wins, then the URN's title, then the body's own title. */
export function problemMessage(problem: Problem, i18n: Translate): { title: string; description: string | null } {
  const slug = problem.type.startsWith(ProblemUrnPrefix) ? problem.type.slice(ProblemUrnPrefix.length) : ''
  const titleKey = `errors.problem.${slug}`
  const codeKey = problem.code === null ? '' : `errors.code.${problem.code}`
  return {
    title: slug !== '' && i18n.te(titleKey) ? i18n.t(titleKey) : problem.title,
    description: codeKey !== '' && i18n.te(codeKey) ? i18n.t(codeKey) : problem.detail,
  }
}
