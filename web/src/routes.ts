import type { Role } from './auth/session'

/**
 * Portal pages and the roles allowed to open each one (Admin is always allowed).
 * Mirrors the backend access matrix; the Navbar and ProtectedRoute both read this list.
 */
export type PageGroup = 'Operations' | 'Stock' | 'Insights' | 'Admin'

export interface PageDef {
  path: string
  label: string
  roles: Role[]
  /** Menu the page sits under in the header (and side navigation on small screens). */
  group: PageGroup
}

export const PAGE_GROUPS: PageGroup[] = ['Operations', 'Stock', 'Insights', 'Admin']

export const PAGES: PageDef[] = [
  { path: '/shelters', label: 'Shelters', roles: ['Coordinator', 'DepotManager'], group: 'Operations' },
  { path: '/reports', label: 'Reports', roles: ['Coordinator'], group: 'Operations' },
  { path: '/triage', label: 'Triage', roles: ['Coordinator'], group: 'Operations' },
  { path: '/workflows', label: 'Workflows', roles: ['Coordinator'], group: 'Operations' },
  { path: '/approvals', label: 'Approvals', roles: ['Coordinator'], group: 'Operations' },
  { path: '/dispatches', label: 'Dispatches', roles: ['Coordinator'], group: 'Operations' },
  { path: '/inventory', label: 'Inventory', roles: ['Coordinator', 'DepotManager'], group: 'Stock' },
  { path: '/depots', label: 'Depots', roles: ['Coordinator', 'DepotManager'], group: 'Stock' },
  { path: '/allocations', label: 'Allocations', roles: ['Coordinator', 'DepotManager'], group: 'Stock' },
  { path: '/stock-history', label: 'Stock history', roles: ['Coordinator', 'DepotManager'], group: 'Stock' },
  { path: '/analytics', label: 'Analytics', roles: ['Coordinator'], group: 'Insights' },
  { path: '/users', label: 'Users', roles: [], group: 'Admin' },
]

export function canOpen(page: PageDef, role: Role | null): boolean {
  return role !== null && (role === 'Admin' || page.roles.includes(role))
}

/** Where a role lands after signing in. */
export function homePathFor(role: Role | null): string {
  if (role === 'DepotManager') return '/inventory'
  return PAGES.find((p) => canOpen(p, role))?.path ?? '/login'
}
