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
} as const
export type Permission = (typeof Permissions)[keyof typeof Permissions]

/** Every /api/Agent/Auth/* call (login, refresh, logout): a 401 there never triggers a refresh. */
export const authPathPrefix = '/api/Agent/Auth/'
