import type { LocationQuery, LocationQueryRaw } from 'vue-router'

export const customerStatuses = ['Active', 'Inactive'] as const
export type CustomerStatusName = (typeof customerStatuses)[number]
export const customerTypes = ['Individual', 'Organisation'] as const
export type CustomerTypeName = (typeof customerTypes)[number]
export const customerSorts = ['createdAt', 'number', 'nameEn', 'nameAr'] as const
export type CustomerSortKey = (typeof customerSorts)[number]
export const customersPageSize = 20

export interface CustomerFilters {
  q: string
  status: CustomerStatusName | ''
  type: CustomerTypeName | ''
  branchId: string
  departmentId: string
  sort: CustomerSortKey
  descending: boolean
  page: number
}

export const defaultCustomerFilters: Readonly<CustomerFilters> = {
  q: '', status: '', type: '', branchId: '', departmentId: '', sort: 'createdAt', descending: true, page: 1,
}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
export function isGuid(value: string): boolean { return guidPattern.test(value) }

function oneOf<T extends string>(values: ReadonlyArray<T>, value: string): T | undefined {
  return values.find((candidate) => candidate === value)
}

function first(value: LocationQuery[string] | undefined): string {
  const item = Array.isArray(value) ? value[0] : value
  return typeof item === 'string' ? item.trim() : ''
}

export function parseCustomerFilters(query: LocationQuery): CustomerFilters {
  const page = Number.parseInt(first(query.page), 10)
  const branch = first(query.branch)
  const department = first(query.department)
  return {
    q: first(query.q).slice(0, 200),
    status: oneOf(customerStatuses, first(query.status)) ?? '',
    type: oneOf(customerTypes, first(query.type)) ?? '',
    branchId: isGuid(branch) ? branch : '',
    departmentId: isGuid(department) ? department : '',
    sort: oneOf(customerSorts, first(query.sort)) ?? 'createdAt',
    descending: first(query.dir) !== 'asc',
    page: Number.isFinite(page) && page >= 1 ? page : 1,
  }
}

/** Only non-default values reach the URL. */
export function customerFiltersToQuery(filters: CustomerFilters): LocationQueryRaw {
  const query: LocationQueryRaw = {}
  if (filters.q !== '') query.q = filters.q
  if (filters.status !== '') query.status = filters.status
  if (filters.type !== '') query.type = filters.type
  if (filters.branchId !== '') query.branch = filters.branchId
  if (filters.departmentId !== '') query.department = filters.departmentId
  if (filters.sort !== 'createdAt') query.sort = filters.sort
  if (!filters.descending) query.dir = 'asc'
  if (filters.page !== 1) query.page = String(filters.page)
  return query
}

/** code -> field key maps for applyCode (F1b). Field keys are the camelCase request paths useFormErrors uses. */
export const contactFieldForCode: Readonly<Record<string, string>> = {
  'Customers.ContactNameRequired': 'name',
  'Customers.ContactNameTooLong': 'name',
  'Customers.JobTitleTooLong': 'jobTitle',
  'Customers.EmailInvalid': 'email',
  'Customers.PhoneInvalid': 'phone',
  'Customers.ContactChannelRequired': 'email',
  'Customers.PortalAccessRequiresEmail': 'hasPortalAccess',
  'Customers.DuplicateContactEmail': 'email',
  'Customers.DuplicateContactPhone': 'phone',
}

export const profileFieldForCode: Readonly<Record<string, string>> = {
  'Customers.NameRequired': 'displayNameEn',
  'Customers.NameTooLong': 'displayNameEn',
  'Customers.TypeUnsupported': 'type',
  'Customers.LegalNameRequired': 'legalName',
  'Customers.LegalNameNotAllowed': 'legalName',
  'Customers.LegalNameTooLong': 'legalName',
  'Customers.LanguageUnsupported': 'preferredLanguage',
}

export const createFieldForCode: Readonly<Record<string, string>> = {
  ...profileFieldForCode,
  ...Object.fromEntries(Object.entries(contactFieldForCode).map(([code, field]) => [code, `contacts[0].${field}`])),
  'Organisation.DepartmentRequired': 'departmentId',
  'Organisation.OutOfScope': 'departmentId',
}

export const noteFieldForCode: Readonly<Record<string, string>> = {
  'Customers.NoteBodyRequired': 'body',
  'Customers.NoteBodyTooLong': 'body',
}

export function statusTone(status: string | null | undefined): 'success' | 'neutral' {
  return status === 'Active' ? 'success' : 'neutral'
}
