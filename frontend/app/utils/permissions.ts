/** Mirrors backend CrmPermissions.cs. Never rename a value. */
export const Permissions = {
  TicketsRead: 'tickets.read',
  TicketsWrite: 'tickets.write',
  TicketsAssign: 'tickets.assign',
  CustomersRead: 'customers.read',
  CustomersWrite: 'customers.write',
  AdminOrganisation: 'admin.organisation',
  AdminUsers: 'admin.users',
  AdminBranding: 'admin.branding',
  PortalTickets: 'portal.tickets',
  PortalAccount: 'portal.account',
} as const
export type Permission = (typeof Permissions)[keyof typeof Permissions]

/** Every /api/Agent/Auth/* call (login, refresh, logout): a 401 there never triggers a refresh. */
export const authPathPrefix = '/api/Agent/Auth/'

/** Every /api/Portal/Auth/* call (register, login, refresh, logout): a 401 there never triggers a refresh. */
export const portalAuthPathPrefix = '/api/Portal/Auth/'

/** Every /api/Portal/* call belongs to the portal customer session, not the staff one (Story 47). */
export const portalApiPrefix = '/api/Portal/'

export function isPortalApiRequest(request: Request): boolean {
  return new URL(request.url, 'http://local').pathname.startsWith(portalApiPrefix)
}
