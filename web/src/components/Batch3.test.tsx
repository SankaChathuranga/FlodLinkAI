import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession, type Role } from '../auth/session'
import { StockHistoryPage } from './StockHistoryPage'
import { InventoryDashboard } from './InventoryDashboard'
import ApprovalQueue from './ApprovalQueue'

// WEB-B3: stock history (8.03), QR stock labels (8.06), depot stock check in the approval queue (8.05).

// jsdom has no canvas; the real library draws the QR code on one.
vi.mock('qrcode', () => ({ default: { toDataURL: vi.fn(async (text: string) => `data:image/png;base64,${btoa(text)}`) } }))

type Handler = (url: URL) => { status: number; body: unknown }

function mockApi(handler: Handler) {
  const fetchMock = vi.fn(async (url: string) => {
    const { status, body } = handler(new URL(url))
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 })
const lastUrl = (fetchMock: ReturnType<typeof mockApi>, path: string) => {
  const urls = fetchMock.mock.calls.map(([u]) => new URL(u)).filter((u) => u.pathname === path)
  return urls[urls.length - 1]
}

function renderAs(role: Role, ui: JSX.Element, path = '/') {
  saveSession({
    token: `token-${role}`,
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 3, name: 'Test', email: 't@floodlink.lk', role, phone: '+94770000000', createdAt: '' },
  })
  return render(
    <AppProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
      </AuthProvider>
    </AppProvider>,
  )
}

afterEach(() => sessionStorage.clear())

const MOVEMENT = (over: Record<string, unknown> = {}) => ({
  id: 1, inventoryItemId: 1, itemName: 'Water', unit: 'bottles', depotId: 1, depotName: 'Colombo Central Depot',
  kind: 'PlanCommit', quantity: 40, availableAfter: 560, reservedAfter: 0, workflowRunId: 'run-1', userId: 2,
  userName: 'Jane Smith', note: null, createdAt: '2026-10-18T06:00:00Z', ...over,
})

describe('StockHistoryPage', () => {
  const historyApi = (items: unknown[]) =>
    mockApi((url) => (url.pathname === '/api/depots' ? { status: 200, body: page([{ id: 1, name: 'Colombo Central Depot' }]) } : { status: 200, body: page(items) }))

  it('lists changes with the stock left afterwards and who made them', async () => {
    historyApi([MOVEMENT(), MOVEMENT({ id: 2, kind: 'PlanReserve', userId: null, userName: null, availableAfter: 600, reservedAfter: 40 })])
    renderAs('Coordinator', <StockHistoryPage />)

    const table = await screen.findByRole('table', { name: 'Stock changes' })
    const dispatched = within(table).getByText('Dispatched').closest('tr')!
    expect(within(dispatched).getByText('−40 bottles')).toBeInTheDocument()
    expect(within(dispatched).getByText('560')).toBeInTheDocument()
    expect(within(dispatched).getByText(/Jane Smith/)).toBeInTheDocument()
    expect(within(dispatched).getByRole('link', { name: 'workflow' })).toHaveAttribute('href', '/workflows/run-1')
    const reserved = within(table).getByText('Reserved for plan').closest('tr')!
    expect(within(reserved).getByText(/System \(validated plan\)/)).toBeInTheDocument()
  })

  it('opens on one stock line from the URL and filters by change on the server', async () => {
    const fetchMock = historyApi([MOVEMENT({ kind: 'CheckIn', quantity: 25, workflowRunId: null })])
    renderAs('DepotManager', <StockHistoryPage />, '/stock-history?itemId=1')

    expect(await screen.findByText(/Showing one stock line: Water at Colombo Central Depot/)).toBeInTheDocument()
    expect(lastUrl(fetchMock, '/api/inventory/movements')?.searchParams.get('inventoryItemId')).toBe('1')
    expect(screen.queryByRole('link', { name: 'workflow' })).not.toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Change'), 'CheckIn')
    await waitFor(() => expect(lastUrl(fetchMock, '/api/inventory/movements')?.searchParams.get('kind')).toBe('CheckIn'))
  })

  it('shows empty and error states', async () => {
    historyApi([])
    const { unmount } = renderAs('Coordinator', <StockHistoryPage />)
    expect(await screen.findByText('No stock changes match these filters.')).toBeInTheDocument()
    unmount()

    mockApi(() => ({ status: 500, body: { error: 'X', message: 'Database down' } }))
    renderAs('Coordinator', <StockHistoryPage />)
    expect(await screen.findByText(/Database down/)).toBeInTheDocument()
  })
})

describe('Inventory stock labels', () => {
  it('draws a QR label that the app can scan, with a history link', async () => {
    mockApi(() => ({ status: 200, body: page([{ id: 4, depotId: 2, depot: { id: 2, name: 'Kaduwela Supply Depot' }, itemName: 'Medical', unit: 'kits', quantityAvailable: 50, quantityReserved: 0, reorderThreshold: 10, updatedAt: '2026-10-18T00:00:00Z' }]) }))
    renderAs('DepotManager', <InventoryDashboard />)

    expect(await screen.findByRole('link', { name: 'Stock history of Medical at Kaduwela Supply Depot' })).toHaveAttribute('href', '/stock-history?itemId=4')
    await userEvent.click(screen.getByRole('button', { name: 'QR label for Medical at Kaduwela Supply Depot' }))

    const image = await screen.findByRole('img', { name: 'QR code FLI:4' })
    expect(image).toHaveAttribute('src', `data:image/png;base64,${btoa('FLI:4')}`)
    expect(screen.getByRole('link', { name: 'Download label' })).toHaveAttribute('download', 'floodlink-medical-4.png')
  })
})

describe('Approval queue stock check', () => {
  const queueWith = (stockCheck: unknown) =>
    mockApi((url) =>
      url.pathname.startsWith('/api/validations/')
        ? { status: 200, body: { workflowRunId: 'r1', state: 'PendingApproval', hasRunValidation: true, overallPassed: true, checks: [] } }
        : { status: 200, body: { items: [{ workflowRunId: 'r1', objective: 'Water for Colombo', state: 'PendingApproval', createdAt: '2026-10-18T06:00:00Z', reportCount: 1, shelterCount: 1, allocationCount: 1, totalQuantity: 40, validationPassed: true, stockCheck }] } },
    )

  it('shows that the depot confirmed the reserved stock', async () => {
    queueWith({ reservedLines: 2, confirmedLines: 2, shortfalls: [] })
    renderAs('Coordinator', <ApprovalQueue />)
    expect(await screen.findByText('Depot confirmed stock')).toBeInTheDocument()
  })

  it('warns about a shortfall the depot reported', async () => {
    queueWith({ reservedLines: 1, confirmedLines: 1, shortfalls: [{ depotName: 'Colombo Central Depot', itemName: 'Water', reserved: 40, counted: 25, note: 'Damaged' }] })
    renderAs('Coordinator', <ApprovalQueue />)
    expect(await screen.findByText('Shortfall reported by depot')).toBeInTheDocument()
    expect(screen.getByText('Colombo Central Depot: 25 of 40 Water on the shelf (Damaged)')).toBeVisible()
  })
})
