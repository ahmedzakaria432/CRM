import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type { BadgeTone } from '~/utils/tickets'

/** The customer portal's ticket list state (Story 47, D9). The URL is the state, like the agent inbox. */
export const portalTabs = ['open', 'closed'] as const
export type PortalTab = (typeof portalTabs)[number]

/** The customer-friendly statuses the API returns (never the internal ticket statuses). */
export const portalStatuses = ['Received', 'InProgress', 'AwaitingYourReply', 'Resolved', 'Closed'] as const
export type PortalStatusName = (typeof portalStatuses)[number]

/** Which portal statuses each tab lists: open = Received, InProgress, AwaitingYourReply; closed = Resolved, Closed. */
export const openStatuses: ReadonlyArray<PortalStatusName> = ['Received', 'InProgress', 'AwaitingYourReply']
export const closedStatuses: ReadonlyArray<PortalStatusName> = ['Resolved', 'Closed']

export const portalPageSize = 20

export interface PortalTicketFilters {
  tab: PortalTab
  status: PortalStatusName | ''
  q: string
  page: number
}

function first(value: LocationQuery[string] | undefined): string {
  const item = Array.isArray(value) ? value[0] : value
  return typeof item === 'string' ? item.trim() : ''
}

export function statusesOfTab(tab: PortalTab): ReadonlyArray<PortalStatusName> {
  return tab === 'closed' ? closedStatuses : openStatuses
}

export function parsePortalFilters(query: LocationQuery): PortalTicketFilters {
  const tab: PortalTab = first(query.tab) === 'closed' ? 'closed' : 'open'
  const rawStatus = first(query.status)
  const status = statusesOfTab(tab).find((candidate) => candidate === rawStatus) ?? '' // a status must belong to the tab
  const page = Number.parseInt(first(query.page), 10)
  return {
    tab,
    status,
    q: first(query.q).slice(0, 200),
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

/** Only non-default values reach the URL. */
export function portalFiltersToQuery(f: PortalTicketFilters): LocationQueryRaw {
  const query: LocationQueryRaw = {}
  if (f.tab !== 'open') query.tab = f.tab
  if (f.status !== '') query.status = f.status
  if (f.q !== '') query.q = f.q
  if (f.page !== 1) query.page = String(f.page)
  return query
}

export function portalStatusTone(status: string | null | undefined): BadgeTone {
  switch (status) {
    case 'Received': return 'info'
    case 'InProgress': return 'primary'
    case 'AwaitingYourReply': return 'warning'
    case 'Resolved': return 'success'
    default: return 'neutral' // Closed
  }
}

/** The portal status name as a translation key, or null for a value a newer backend might add. */
export function portalStatusKey(status: string | null | undefined): string | null {
  return portalStatuses.find((candidate) => candidate === status) === undefined ? null : `portal.status.${status}`
}

/** code -> field for the new request form (useFormErrors().applyCode). */
export const submitFieldForCode: Readonly<Record<string, string>> = {
  'Tickets.CategoryNotFound': 'categoryId',
  'Tickets.SubjectRequired': 'subject',
  'Tickets.SubjectTooLong': 'subject',
  'Tickets.DescriptionRequired': 'description',
  'Tickets.DescriptionTooLong': 'description',
}
