import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession, type Role } from '../auth/session'
import { a11yViolations } from '../test/a11y'
import { Navbar } from './Navbar'
import { LoginPage } from './LoginPage'
import { ReportsQueue } from './ReportsQueue'
import { InventoryDashboard } from './InventoryDashboard'
import ApprovalQueue from './ApprovalQueue'
import { WorkflowMonitor } from './WorkflowMonitor'
import { WorkflowTrace } from './WorkflowTrace'
import Analytics from './Analytics'
import DispatchHistory from './DispatchHistory'
import { DepotsPage } from './DepotsPage'
import { AllocationsPage } from './AllocationsPage'
import { StockHistoryPage } from './StockHistoryPage'
import { UsersAdmin } from './UsersAdmin'
import { TriagePlanViewer } from './TriagePlanViewer'
import { ShelterDetailModal } from './ShelterDetailModal'

// 8.19: every portal screen passes axe-core's WCAG 2.1 A/AA checks in its loaded state, and the
// header is keyboard-usable (skip link, grouped menus, sign-out with an accessible name).

vi.mock('qrcode', () => ({ default: { toDataURL: vi.fn(async () => 'data:image/png;base64,AA==') } }))

const RUN = '741984fa-7614-4107-9871-1f77126cdc5d'
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 })

const SHELTER = { id: 1, name: 'Central Colombo Relief Center', latitude: 6.9271, longitude: 79.8612, capacity: 200, currentOccupancy: 190, status: 'Active', createdAt: '2026-09-25T00:00:00Z', updatedAt: '2026-10-18T00:00:00Z' }
const REPORT = { id: 3, shelterId: 1, shelter: SHELTER, reportedBy: 1, reporter: { id: 1, name: 'John Doe' }, needType: 'Medical', quantityNeeded: 10, urgencyLevel: 85, notes: 'Insulin needed.', status: 'New', createdAt: '2026-10-18T06:00:00Z', gpsLat: 6.93, gpsLng: 79.86 }
const STOCK = { id: 1, depotId: 1, depot: { id: 1, name: 'Colombo Central Depot' }, itemName: 'Water', unit: 'bottles', quantityAvailable: 600, quantityReserved: 40, reorderThreshold: 100, updatedAt: '2026-10-18T00:00:00Z' }
const PLAN = JSON.stringify({
  plan: { Source: 'llm', Steps: [{ Agent: 'Triage', Purpose: 'Rank.', Rationale: 'Insulin first.' }, { Agent: 'Matching', Purpose: 'Allocate.', Rationale: 'r' }, { Agent: 'Routing', Purpose: 'Route.', Rationale: 'r' }, { Agent: 'Validation', Purpose: 'Check.', Rationale: 'r' }] },
  triagePlan: { ScoringMethod: 'llm-adjusted', PriorityItems: [{ ReportId: 3, ShelterId: 1, NeedType: 'Medical', Quantity: 10, PriorityScore: 100, RuleScore: 85, LlmAdjustment: 15, Justification: 'Insulin.' }] },
  allocationProposal: { AllocationProposalId: 1, Allocations: [{ DepotId: 2, ShelterId: 1, ItemName: 'Medical', Quantity: 10 }], Unfulfillable: [{ ShelterId: 1, ItemName: 'Food', Reason: 'Short' }] },
  route: { DistanceKm: 12.5, EtaMinutes: 25, Legs: [{ DepotId: 2, ShelterId: 1, DistanceKm: 12.5, EtaMinutes: 25, Polyline: 'czci@ox_gN{oDcbD' }] },
  validationResults: { OverallPassed: true, Checks: [{ CheckName: 'StockAvailability', Passed: true }] },
})

/** One fake backend for every screen. */
function mockApi() {
  vi.stubGlobal('fetch', vi.fn(async (input: string) => {
    const url = new URL(input)
    const p = url.pathname
    const body = (() => {
      if (p === '/api/reports') return page([REPORT])
      if (p === '/api/shelters') return page([SHELTER])
      if (p === '/api/shelters/1') return { ...SHELTER, reports: [REPORT] }
      if (p === '/api/inventory') return page([STOCK])
      if (p === '/api/inventory/movements') return page([{ id: 1, inventoryItemId: 1, itemName: 'Water', unit: 'bottles', depotId: 1, depotName: 'Colombo Central Depot', kind: 'CheckIn', quantity: 25, availableAfter: 625, reservedAfter: 0, workflowRunId: null, userId: 3, userName: 'Kamal Perera', note: null, createdAt: '2026-10-18T06:00:00Z' }])
      if (p === '/api/depots') return page([{ id: 1, name: 'Colombo Central Depot', latitude: 6.9355, longitude: 79.8487, managerId: 3, managerName: 'Kamal Perera', itemCount: 2, createdAt: '2026-09-25T00:00:00Z', updatedAt: '2026-10-18T00:00:00Z' }])
      if (p === '/api/depots/managers') return [{ id: 3, name: 'Kamal Perera', email: 'depot@floodlink.lk' }]
      if (p === '/api/allocations') return page([{ id: 9, workflowRunId: RUN, depotId: 2, depotName: 'Kaduwela Supply Depot', shelterId: 1, shelterName: 'Central Colombo Relief Center', itemName: 'Medical', quantity: 10, status: 'Reserved', createdAt: '2026-10-18T06:00:00Z' }])
      if (p === '/api/dispatches/approval-queue') return { items: [{ workflowRunId: RUN, objective: 'Insulin for Colombo', state: 'PendingApproval', createdAt: '2026-10-18T06:00:00Z', reportCount: 1, shelterCount: 1, allocationCount: 1, totalQuantity: 10, validationPassed: true, stockCheck: { reservedLines: 1, confirmedLines: 1, shortfalls: [] } }] }
      if (p.startsWith('/api/validations/')) return { workflowRunId: RUN, state: 'PendingApproval', hasRunValidation: true, overallPassed: true, checks: [{ checkName: 'StockAvailability', passed: true, violationDetail: null }] }
      if (p === '/api/dispatches/summary') return { total: 2, approved: 1, rejected: 1, revisionsRequested: 0, avgApprovalMinutes: 30, medianApprovalMinutes: 30, topRejectionReasons: [{ reason: 'Road closed', count: 1 }], dispatchesPerShelter: [{ shelterId: 1, shelterName: 'Central Colombo Relief Center', approved: 1, rejected: 1, revisionsRequested: 0 }] }
      if (p === '/api/dispatches') return { page: 1, pageSize: 20, total: 1, items: [{ id: 'd1', workflowRunId: RUN, decision: 'Approved', approvalNotes: 'Go', approvedById: 2, dispatchedAt: '2026-10-18T07:00:00Z', createdAt: '2026-10-18T07:00:00Z' }] }
      if (p === '/api/workflows') return page([{ id: RUN, objective: 'Insulin for Colombo', currentState: 'PendingApproval', progressPercent: 90, failedAtState: null, failureReason: null, reportCount: 1, retryCount: 0, retryLimit: 3, createdAt: '2026-10-18T06:00:00Z', updatedAt: '2026-10-18T06:01:00Z' }])
      if (p === `/api/workflows/${RUN}`) return { id: RUN, objective: 'Insulin for Colombo', currentState: 'Approved', failedAtState: null, failureReason: null, reportIds: [3], retryCount: 0, createdAt: '2026-10-18T06:00:00Z', updatedAt: '2026-10-18T07:00:00Z', planJson: PLAN }
      if (p.endsWith('/logs')) return [{ id: 'l1', agentName: 'TriageAgent', status: 'Success', isRetry: false, durationMs: 40, createdAt: '2026-10-18T06:00:01Z', errorMessage: null, output: null, toolCalls: [{ Tool: 'db.reports.read', Succeeded: true, Input: { ids: [3] }, Output: { rows: 1 }, DurationMs: 3 }] }]
      if (p.includes('/by-workflow/')) return { workflowRunId: RUN, state: 'Approved', dispatchId: 'd1', decision: 'Approved', isDelivered: false, decisions: [{ dispatchId: 'd1', decision: 'Approved', notes: 'Go', decidedById: 2, decidedBy: 'Jane Smith', decidedAt: '2026-10-18T07:00:00Z', dispatchedAt: null }] }
      if (p === '/api/routes') return [{ id: 'r1', depotId: 2, depotName: 'Kaduwela Supply Depot', shelterId: 1, shelterName: 'Central Colombo Relief Center', distanceKm: 12.5, etaMinutes: 25, polyline: 'czci@ox_gN{oDcbD' }]
      if (p.startsWith('/api/audit/')) return { dispatchId: 'd1', decision: 'Approved', events: [{ id: 'e1', eventType: 'DispatchApproved', eventDetailJson: '{}', actorId: '2', createdAt: '2026-10-18T07:00:00Z' }] }
      if (p === '/api/reports/triage-plans') return [{ id: 1, generatedFromReportIds: [3], priorityRank: 1, planSummaryJson: JSON.stringify({ workflowRunId: RUN, scoringMethod: 'llm-adjusted', priorityItems: [{ reportId: 3, shelterId: 1, needType: 'Medical', quantity: 10, priorityScore: 100, justification: 'Insulin.', ruleScore: 85, llmAdjustment: 15 }] }), createdAt: '2026-10-18T06:00:00Z' }]
      if (p === '/api/users') return [{ id: 2, name: 'Jane Smith', email: 'coordinator@floodlink.lk', role: 'Coordinator', phone: '+94779876543', createdAt: '2026-09-25T00:00:00Z' }]
      return {}
    })()
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
  }))
}

function renderAs(role: Role | null, ui: ReactElement, path = '/') {
  if (role) {
    saveSession({
      token: `token-${role}`,
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      user: { id: 2, name: 'Jane Smith', email: 'coordinator@floodlink.lk', role, phone: '+94770000000', createdAt: '' },
    })
  }
  mockApi()
  return render(
    <AppProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes><Route path="*" element={ui} /><Route path="/workflows/:id" element={ui} /></Routes>
        </MemoryRouter>
      </AuthProvider>
    </AppProvider>,
  )
}

afterEach(() => sessionStorage.clear())

const screens: [string, Role | null, () => ReactElement, string, string?][] = [
  ['sign-in', null, () => <LoginPage />, 'Sign in'],
  ['reports queue', 'Coordinator', () => <ReportsQueue />, '#R-3'],
  ['triage viewer', 'Coordinator', () => <TriagePlanViewer />, 'Insulin.'],
  ['workflow monitor', 'Coordinator', () => <WorkflowMonitor />, 'Insulin for Colombo'],
  ['execution trace', 'Coordinator', () => <WorkflowTrace />, 'Insulin for Colombo', `/workflows/${RUN}`],
  ['approval queue', 'Coordinator', () => <ApprovalQueue />, 'Insulin for Colombo'],
  ['dispatch history', 'Coordinator', () => <DispatchHistory />, 'Go'],
  ['analytics', 'Coordinator', () => <Analytics />, 'Road closed'],
  ['inventory', 'DepotManager', () => <InventoryDashboard />, 'Water'],
  ['depots', 'DepotManager', () => <DepotsPage />, 'Colombo Central Depot'],
  ['allocations', 'Coordinator', () => <AllocationsPage />, 'Kaduwela Supply Depot'],
  ['stock history', 'DepotManager', () => <StockHistoryPage />, 'Kamal Perera'],
  ['user management', 'Admin', () => <UsersAdmin />, 'Jane Smith'],
  ['shelter detail dialog', 'Coordinator', () => <ShelterDetailModal shelterId={1} onClose={() => {}} />, 'Quantity: 10 units'],
]

describe('accessibility (axe-core, WCAG 2.1 AA)', () => {
  it.each(screens)('%s has no violations', async (_name, role, ui, loadedText, path) => {
    const { container } = renderAs(role, ui(), path)
    await screen.findAllByText(loadedText, { exact: false })
    expect(await a11yViolations(container)).toEqual([])
  })

  it('approval decision form has no violations once opened', async () => {
    const { container } = renderAs('Coordinator', <ApprovalQueue />)
    await userEvent.click(await screen.findByRole('button', { name: 'Inspect & decide' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
    expect(screen.getByLabelText('Rejection reason (required)')).toBeInTheDocument() // label tied to the field
    expect(await a11yViolations(container)).toEqual([])
  })
})

describe('header navigation', () => {
  it('offers a skip link, groups pages into menus and names the sign-out button', async () => {
    const { container } = renderAs('Coordinator', <Navbar />)

    expect(screen.getByRole('link', { name: /skip to main content/i })).toHaveAttribute('href', '#main-content')
    const nav = screen.getByRole('navigation', { name: 'Main' })
    expect(nav).toHaveTextContent('Operations')
    expect(nav).toHaveTextContent('Stock')
    expect(nav).not.toHaveTextContent('Users') // Admin only
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
    expect(await a11yViolations(container)).toEqual([])
  })

  it('opens a menu from the keyboard', async () => {
    renderAs('Coordinator', <Navbar />)
    const stock = screen.getAllByRole('link', { name: 'Stock' })[0]
    expect(stock).toHaveAttribute('aria-haspopup', 'menu')
    expect(stock).toHaveAttribute('aria-expanded', 'false')
    stock.focus()
    await userEvent.keyboard('{Enter}')
    expect(stock).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getAllByRole('link', { name: 'Stock history' })[0]).toHaveAttribute('href', '/stock-history')
  })

  it('shows a depot manager only the stock pages and shelters', async () => {
    renderAs('DepotManager', <Navbar />)
    const nav = screen.getByRole('navigation', { name: 'Main' })
    expect(nav).not.toHaveTextContent('Approvals')
    expect(nav).not.toHaveTextContent('Analytics')
    expect(nav).toHaveTextContent('Shelters')
  })
})
