// ── FloodLink AI — Navigation Types ──────────────────────────────────────────
//
// Central type definitions for the app shell's navigation model.
// Any new top-level section must be added here first.
// ─────────────────────────────────────────────────────────────────────────────

export type PageId =
  | 'dashboard'
  | 'incidents'
  | 'inventory'
  | 'depots'
  | 'approvals'
  | 'routes'
  | 'logs';

export interface NavItem {
  id: PageId;
  label: string;
  section: 'operations' | 'management' | 'audit';
  /** Numeric badge (e.g. pending approval count). Omit or set 0 to hide. */
  badge?: number;
}

export const NAV_ITEMS: NavItem[] = [
  // Operations
  { id: 'dashboard',  label: 'Dashboard',        section: 'operations' },
  { id: 'incidents',  label: 'Incident Reports',  section: 'operations' },
  // Management
  { id: 'inventory',  label: 'Inventory',         section: 'management' },
  { id: 'depots',     label: 'Depots',            section: 'management' },
  // Approvals & Audit
  { id: 'approvals',  label: 'Approvals Queue',   section: 'audit', badge: 3 },
  { id: 'routes',     label: 'Routes',            section: 'audit' },
  { id: 'logs',       label: 'Execution Logs',    section: 'audit' },
];

export const SECTION_LABELS: Record<NavItem['section'], string> = {
  operations: 'Operations',
  management: 'Management',
  audit:      'Approvals & Audit',
};
