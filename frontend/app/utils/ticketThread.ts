import type { TicketThreadEntryDto, TicketThreadPageDto } from '~/composables/tickets/useTicketThread'
import { localizedName, type LocalizedTextLike } from '~/utils/scope'

/** The lookups a thread page carries, keyed by id. */
export interface ThreadLookups {
  people: ReadonlyMap<string, string>
  categories: ReadonlyMap<string, LocalizedTextLike>
  departments: ReadonlyMap<string, LocalizedTextLike>
}

export interface ThreadText {
  key: string
  params: Record<string, string>
}

type Translate = (key: string) => string
type Te = (key: string) => boolean

const statusKinds: ReadonlySet<string> = new Set([
  'Created',
  'Opened',
  'AwaitingCustomer',
  'PutOnHold',
  'Resolved',
  'Closed',
  'Reopened',
  'Cancelled',
])
const personKinds: ReadonlySet<string> = new Set(['Assigned', 'Claimed', 'Unassigned'])
const knownKinds: ReadonlySet<string> = new Set([...statusKinds, ...personKinds, 'Transferred', 'PriorityChanged', 'CategoryChanged'])

/** Builds the three lookup maps from every loaded page. */
export function mergeLookups(pages: ReadonlyArray<TicketThreadPageDto>): ThreadLookups {
  const people = new Map<string, string>()
  const categories = new Map<string, LocalizedTextLike>()
  const departments = new Map<string, LocalizedTextLike>()
  for (const page of pages) {
    for (const person of page.people ?? []) {
      if (person.id && person.displayName) people.set(person.id, person.displayName)
    }
    for (const category of page.categories ?? []) {
      if (category.id && category.name) categories.set(category.id, { ar: category.name.ar, en: category.name.en })
    }
    for (const department of page.departments ?? []) {
      if (department.id) departments.set(department.id, { ar: department.nameAr, en: department.nameEn })
    }
  }
  return { people, categories, departments }
}

/** The staff member's display name for an id, or "a colleague". */
export function personName(id: string | null | undefined, lookups: ThreadLookups, t: Translate): string {
  return (id ? lookups.people.get(id) : undefined) ?? t('tickets.thread.colleague')
}

/** The locale key and resolved parameters of an activity entry (D9): tickets.activity.<templateKey>. */
export function activityText(
  entry: TicketThreadEntryDto,
  lookups: ThreadLookups,
  meId: string | undefined,
  locale: string,
  t: Translate,
  te: Te,
): ThreadText {
  const kind = entry.templateKey ?? ''
  const actor = meId !== undefined && meId === entry.authorId ? t('tickets.thread.you') : personName(entry.authorId, lookups, t)
  const from = entry.parameters?.from ?? null
  const to = entry.parameters?.to ?? null

  if (!knownKinds.has(kind)) return { key: 'tickets.activity.unknown', params: { actor } }

  const params: Record<string, string> = { actor }
  if (statusKinds.has(kind) && kind !== 'Created') {
    params.from = translated(`tickets.status.${from ?? ''}`, from, t, te)
    params.to = translated(`tickets.status.${to ?? ''}`, to, t, te)
  } else if (kind === 'PriorityChanged') {
    params.from = translated(`tickets.priority.${from ?? ''}`, from, t, te)
    params.to = translated(`tickets.priority.${to ?? ''}`, to, t, te)
  } else if (personKinds.has(kind)) {
    params.from = from ? personName(from, lookups, t) : t('tickets.thread.nobody')
    params.to = to ? personName(to, lookups, t) : t('tickets.thread.nobody')
  } else if (kind === 'Transferred') {
    params.from = nameOf(lookups.departments, from, locale, t('tickets.thread.otherDepartment'))
    params.to = nameOf(lookups.departments, to, locale, t('tickets.thread.otherDepartment'))
  } else if (kind === 'CategoryChanged') {
    params.from = nameOf(lookups.categories, from, locale, t('tickets.thread.aCategory'))
    params.to = nameOf(lookups.categories, to, locale, t('tickets.thread.aCategory'))
  }
  return { key: `tickets.activity.${kind}`, params }
}

function translated(key: string, raw: string | null, t: Translate, te: Te): string {
  return raw !== null && te(key) ? t(key) : (raw ?? '')
}

function nameOf(map: ReadonlyMap<string, LocalizedTextLike>, id: string | null, locale: string, fallback: string): string {
  const name = id ? localizedName(map.get(id), locale) : ''
  return name === '' ? fallback : name
}
