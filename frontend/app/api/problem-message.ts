import type { Problem } from './problem'

type Translate = { t: (key: string) => string; te: (key: string) => boolean }

const ProblemUrnPrefix = 'urn:crm:problem:'

/** Codes whose server detail is already in the request language (Accept-Language) and names what a static key cannot:
 * the agent who won a claim, the ticket's current status (Story 15 TicketRefusals). */
const serverWordedCodes: ReadonlySet<string> = new Set([
  'Tickets.AlreadyClaimed',
  'Tickets.TransitionNotAllowed',
  'Organisation.BrandingContrastTooLow',
])

/** Toast/alert text in the UI locale: a known code wins, then the URN's title, then the body's own title. */
export function problemMessage(problem: Problem, i18n: Translate): { title: string; description: string | null } {
  const slug = problem.type.startsWith(ProblemUrnPrefix) ? problem.type.slice(ProblemUrnPrefix.length) : ''
  const titleKey = `errors.problem.${slug}`
  const codeKey = problem.code === null ? '' : `errors.code.${problem.code}`
  return {
    title: slug !== '' && i18n.te(titleKey) ? i18n.t(titleKey) : problem.title,
    description:
      problem.code !== null && serverWordedCodes.has(problem.code) && problem.detail
        ? problem.detail
        : codeKey !== '' && i18n.te(codeKey)
          ? i18n.t(codeKey)
          : problem.detail,
  }
}
