import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession, type Role } from '../auth/session'
import { DepotsPage } from './DepotsPage'
import { AllocationsPage } from './AllocationsPage'
import Analytics from './Analytics'

// WEB-P8: depot management (8.01), allocation proposals review (8.02), analytics per shelter (8.17).

type Handler = (url: URL, init?: RequestInit) => { status: number; body: unknown }

function mockApi(handler: Handler) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const { status, body } = handler(new URL(url), init)
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 })

function renderAs(role: Role, ui: JSX.Element) {
  saveSession({
    token: `token-${role}`,
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 3, name: 'Test', email: 'test@floodlink.lk', role, phone: '+94770000000', createdAt: '' },
  })
  return render(
    <AppProvider>
      <AuthProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </AuthProvider>
    </AppProvider>,
  )
}

afterEach(() => sessionStorage.clear())

const DEPOT = {
  id: 1, name: 'Colombo Central Depot', latitude: 6.9355, longitude: 79.8487, managerId: 3, managerName: 'Kamal Perera',
  itemCount: 2, createdAt: '2026-09-25T00:00:00Z', updatedAt: '2026-10-15T00:00:00Z',
}
const MANAGERS = [{ id: 3, name: 'Kamal Perera', email: 'depot@floodlink.lk' }, { id: 7, name: 'Nimal Silva', email: 'nimal@floodlink.lk' }]

describe('DepotsPage', () => {
  const depotApi = (onWrite?: (url: URL, init?: RequestInit) => { status: number; body: unknown }) =>
    mockApi((url, init) => {
      if (url.pathname === '/api/depots/managers') return { status: 200, body: MANAGERS }
      if (init?.method === 'PUT' || init?.method === 'POST') return onWrite?.(url, init) ?? { status: 200, body: DEPOT }
      return { status: 200, body: page([DEPOT]) }
    })

  it('lists depots with their manager and stock lines', async () => {
    depotApi()
    renderAs('DepotManager', <DepotsPage />)

    const row = (await screen.findByText('Colombo Central Depot')).closest('tr')!
    expect(within(row).getByText('Kamal Perera')).toBeInTheDocument()
    expect(within(row).getByText('2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add depot' })).toBeInTheDocument()
  })

  it('is read-only for a coordinator', async () => {
    const fetchMock = depotApi()
    renderAs('Coordinator', <DepotsPage />)

    await screen.findByText('Colombo Central Depot')
    expect(screen.queryByRole('button', { name: 'Add depot' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit Colombo Central Depot' })).not.toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/depots/managers'))).toBe(false)
  })

  it('reassigns the manager and saves the depot', async () => {
    let sent: unknown = null
    depotApi((url, init) => {
      sent = { path: url.pathname, method: init?.method, body: JSON.parse(String(init?.body)) }
      return { status: 200, body: { ...DEPOT, managerId: 7, managerName: 'Nimal Silva' } }
    })
    renderAs('DepotManager', <DepotsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Edit Colombo Central Depot' }))
    await screen.findByRole('option', { name: 'Nimal Silva (nimal@floodlink.lk)' })
    await userEvent.selectOptions(screen.getByLabelText('Manager'), '7')
    await userEvent.click(screen.getByRole('button', { name: 'Save depot' }))

    expect(await screen.findByText('Depot “Colombo Central Depot” saved.')).toBeInTheDocument()
    expect(sent).toEqual({
      path: '/api/depots/1',
      method: 'PUT',
      body: { name: 'Colombo Central Depot', latitude: 6.9355, longitude: 79.8487, managerId: 7 },
    })
  })

  it('a new depot needs a name and is not sent without one', async () => {
    const fetchMock = depotApi()
    renderAs('DepotManager', <DepotsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Add depot' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save depot' }))

    expect(await screen.findByText('Enter the depot name.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('shows the API reason when a save is refused', async () => {
    depotApi(() => ({ status: 400, body: { error: 'BAD_REQUEST', message: 'User 2 is not a DepotManager account.' } }))
    renderAs('DepotManager', <DepotsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Edit Colombo Central Depot' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save depot' }))

    expect(await screen.findByText(/User 2 is not a DepotManager account\./)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save depot' })).toBeInTheDocument() // dialog stays open
  })
})

describe('AllocationsPage', () => {
  const ALLOCATION = {
    id: 9, workflowRunId: '741984fa-7614-4107-9871-1f77126cdc5d', depotId: 2, depotName: 'Kaduwela Supply Depot', shelterId: 2,
    shelterName: 'Kaduwela Community Hall', itemName: 'Medical', quantity: 5, status: 'Reserved', createdAt: '2026-10-15T06:00:00Z',
  }

  it('lists allocation lines and links coordinators to the trace', async () => {
    mockApi(() => ({ status: 200, body: page([ALLOCATION]) }))
    renderAs('Coordinator', <AllocationsPage />)

    const row = (await screen.findByText('Kaduwela Community Hall')).closest('tr')!
    expect(within(row).getByText('Reserved')).toBeInTheDocument()
    expect(within(row).getByRole('link', { name: 'Open trace' })).toHaveAttribute('href', `/workflows/${ALLOCATION.workflowRunId}`)
  })

  it('depot staff see the run id but no trace link', async () => {
    mockApi(() => ({ status: 200, body: page([ALLOCATION]) }))
    renderAs('DepotManager', <AllocationsPage />)

    await screen.findByText('Kaduwela Community Hall')
    expect(screen.queryByRole('link', { name: 'Open trace' })).not.toBeInTheDocument()
    expect(screen.getByText('741984fa')).toBeInTheDocument()
  })

  it('filters by status on the server and shows the empty state', async () => {
    const fetchMock = mockApi((url) => ({ status: 200, body: url.searchParams.get('status') ? page([]) : page([ALLOCATION]) }))
    renderAs('Coordinator', <AllocationsPage />)
    await screen.findByText('Kaduwela Community Hall')

    await userEvent.selectOptions(screen.getByLabelText('Status'), 'Committed')

    expect(await screen.findByText('No committed allocations.')).toBeInTheDocument()
    await waitFor(() => {
      const urls = fetchMock.mock.calls.map(([url]) => new URL(url))
      expect(urls[urls.length - 1].searchParams.get('status')).toBe('Committed')
    })
  })
})

describe('Analytics', () => {
  it('shows time to approval and decisions per shelter', async () => {
    mockApi(() => ({
      status: 200,
      body: {
        total: 4, approved: 2, rejected: 1, revisionsRequested: 1, avgApprovalMinutes: 60, medianApprovalMinutes: 45,
        topRejectionReasons: [{ reason: 'Road closed', count: 1 }],
        dispatchesPerShelter: [{ shelterId: 1, shelterName: 'Central Colombo Relief Center', approved: 2, rejected: 1, revisionsRequested: 0 }],
      },
    }))
    renderAs('Coordinator', <Analytics />)

    expect(await screen.findByText('1h 0m')).toBeInTheDocument()
    expect(screen.getByText('45 min')).toBeInTheDocument()
    const table = screen.getByRole('table', { name: 'Decisions per shelter' })
    const row = within(table).getByText('Central Colombo Relief Center').closest('tr')!
    expect(within(row).getAllByRole('cell').map((c) => c.textContent)).toEqual(['Central Colombo Relief Center', '2', '1', '0'])
  })
})
