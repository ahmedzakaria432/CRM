import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import { isGuid } from '~/utils/customers'
import { Permissions, type Permission } from '~/utils/permissions'

export const ticketViews = ['mine', 'unassigned', 'all'] as const
export type TicketView = (typeof ticketViews)[number]
export const ticketStatuses = ['New', 'Open', 'PendingCustomer', 'OnHold', 'Resolved', 'Closed', 'Cancelled'] as const
export type TicketStatusName = (typeof ticketStatuses)[number]
export const ticketPriorities = ['Low', 'Normal', 'High', 'Urgent'] as const
export type TicketPriorityName = (typeof ticketPriorities)[number]
export const ticketSorts = ['createdAt', 'modifiedAt', 'priority', 'number'] as const
export type TicketSortKey = (typeof ticketSorts)[number]
export const ticketsPageSize = 20

export interface TicketFilters {
  view: TicketView
  q: string
  status: TicketStatusName | ''
  priority: TicketPriorityName | ''
  categoryId: string
  assigneeId: string
  departmentId: string
  branchId: string
  customerId: string
  sort: TicketSortKey
  descending: boolean
  page: number
}

function oneOf<T extends string>(values: ReadonlyArray<T>, value: string): T | undefined {
  return values.find((candidate) => candidate === value)
}
function first(value: LocationQuery[string] | undefined): string {
  const item = Array.isArray(value) ? value[0] : value
  return typeof item === 'string' ? item.trim() : ''
}
function guid(value: LocationQuery[string] | undefined): string {
  const text = first(value)
  return isGuid(text) ? text : ''
}

export function parseTicketFilters(query: LocationQuery): TicketFilters {
  const page = Number.parseInt(first(query.page), 10)
  return {
    view: oneOf(ticketViews, first(query.view)) ?? 'mine',
    q: first(query.q).slice(0, 200),
    status: oneOf(ticketStatuses, first(query.status)) ?? '',
    priority: oneOf(ticketPriorities, first(query.priority)) ?? '',
    categoryId: guid(query.category),
    assigneeId: guid(query.assignee),
    departmentId: guid(query.department),
    branchId: guid(query.branch),
    customerId: guid(query.customer),
    sort: oneOf(ticketSorts, first(query.sort)) ?? 'createdAt',
    descending: first(query.dir) !== 'asc',
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

/** Only non-default values reach the URL (D2). */
export function ticketFiltersToQuery(f: TicketFilters): LocationQueryRaw {
  const query: LocationQueryRaw = {}
  if (f.view !== 'mine') query.view = f.view
  if (f.q !== '') query.q = f.q
  if (f.status !== '') query.status = f.status
  if (f.priority !== '') query.priority = f.priority
  if (f.categoryId !== '') query.category = f.categoryId
  if (f.assigneeId !== '') query.assignee = f.assigneeId
  if (f.departmentId !== '') query.department = f.departmentId
  if (f.branchId !== '') query.branch = f.branchId
  if (f.customerId !== '') query.customer = f.customerId
  if (f.sort !== 'createdAt') query.sort = f.sort
  if (!f.descending) query.dir = 'asc'
  if (f.page !== 1) query.page = String(f.page)
  return query
}

/** The seven actions of the status action bar (D5), in display order. */
export const ticketStatusActions = ['Open', 'AwaitCustomer', 'PutOnHold', 'Resolve', 'Close', 'Reopen', 'Cancel'] as const
export type TicketStatusAction = (typeof ticketStatusActions)[number]

const permissionFor: Readonly<Record<string, Permission>> = {
  Open: Permissions.TicketsWrite, AwaitCustomer: Permissions.TicketsWrite, PutOnHold: Permissions.TicketsWrite,
  Resolve: Permissions.TicketsWrite, Close: Permissions.TicketsWrite, Reopen: Permissions.TicketsWrite,
  Cancel: Permissions.TicketsWrite, Claim: Permissions.TicketsWrite, ChangePriority: Permissions.TicketsWrite,
  ChangeCategory: Permissions.TicketsWrite, Reply: Permissions.TicketsWrite, AddNote: Permissions.TicketsWrite,
  Assign: Permissions.TicketsAssign, Unassign: Permissions.TicketsAssign, Transfer: Permissions.TicketsAssign,
}

/** allowedActions (the aggregate's table) intersected with the caller's permissions. Unknown names are dropped. */
export function offeredActions(
  allowed: ReadonlyArray<string> | null | undefined,
  can: (permission: Permission) => boolean,
): ReadonlySet<string> {
  const offered = new Set<string>()
  for (const action of allowed ?? []) {
    const permission = permissionFor[action]
    if (permission !== undefined && can(permission)) offered.add(action)
  }
  return offered
}

export type BadgeTone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info'

export function ticketStatusTone(status: string | null | undefined): BadgeTone {
  switch (status) {
    case 'New': return 'info'
    case 'Open': return 'primary'
    case 'PendingCustomer': return 'warning'
    case 'Resolved': return 'success'
    default: return 'neutral' // OnHold, Closed, Cancelled
  }
}

export function ticketPriorityTone(priority: string | null | undefined): BadgeTone {
  switch (priority) {
    case 'Urgent': return 'danger'
    case 'High': return 'warning'
    case 'Normal': return 'info'
    default: return 'neutral' // Low
  }
}

/** code -> field for the new ticket drawer (useFormErrors().applyCode). */
export const createTicketFieldForCode: Readonly<Record<string, string>> = {
  'Customers.CustomerNotFound': 'customerId',
  'Organisation.OutOfScope': 'customerId',
  'Tickets.CustomerRequired': 'customerId',
  'Tickets.CustomerInactive': 'customerId',
  'Tickets.ContactNotOfCustomer': 'contactId',
  'Organisation.DepartmentRequired': 'departmentId',
  'Tickets.CategoryNotFound': 'categoryId',
  'Tickets.CategoryInactive': 'categoryId',
  'Tickets.CategoryDepartmentMismatch': 'categoryId',
  'Tickets.SubjectRequired': 'subject',
  'Tickets.SubjectTooLong': 'subject',
  'Tickets.DescriptionRequired': 'description',
  'Tickets.DescriptionTooLong': 'description',
  'Tickets.PriorityUnsupported': 'priority',
}

export const bodyFieldForCode: Readonly<Record<string, string>> = {
  'Tickets.BodyRequired': 'body',
  'Tickets.BodyTooLong': 'body',
}
