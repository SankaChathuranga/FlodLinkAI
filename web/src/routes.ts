import type { Role } from './auth/session'

/**
 * Portal pages and the roles allowed to open each one (Admin is always allowed).
 * Mirrors the backend access matrix; the Navbar and ProtectedRoute both read this list.
 */
export interface PageDef {
  path: string
  label: string
  roles: Role[]
}

export const PAGES: PageDef[] = [
  { path: '/shelters', label: 'Shelters', roles: ['Coordinator', 'DepotManager'] },
  { path: '/reports', label: 'Reports', roles: ['Coordinator'] },
  { path: '/triage', label: 'Triage', roles: ['Coordinator'] },
  { path: '/workflows', label: 'Workflows', roles: ['Coordinator'] },
  { path: '/inventory', label: 'Inventory', roles: ['Coordinator', 'DepotManager'] },
  { path: '/approvals', label: 'Approvals', roles: ['Coordinator'] },
  { path: '/dispatches', label: 'Dispatches', roles: ['Coordinator'] },
  { path: '/analytics', label: 'Analytics', roles: ['Coordinator'] },
  { path: '/users', label: 'Users', roles: [] },
]

export function canOpen(page: PageDef, role: Role | null): boolean {
  return role !== null && (role === 'Admin' || page.roles.includes(role))
}

/** Where a role lands after signing in. */
export function homePathFor(role: Role | null): string {
  if (role === 'DepotManager') return '/inventory'
  return PAGES.find((p) => canOpen(p, role))?.path ?? '/login'
}
