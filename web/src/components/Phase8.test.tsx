import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession, type Role } from '../auth/session'
import { DepotsPage } from './DepotsPage'
import { AllocationsPage } from './AllocationsPage'
import Analytics from './Analytics'
import { WorkflowTrace } from './WorkflowTrace'

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

// 8.10 route map, 8.15 validation explanation and 8.16 delivery photo in the execution trace.
describe('WorkflowTrace additions', () => {
  const RUN = '741984fa-7614-4107-9871-1f77126cdc5d'
  const PLAN = JSON.stringify({
    route: { DistanceKm: 6.5, EtaMinutes: 15, Legs: [{ DepotId: 2, ShelterId: 2, DistanceKm: 6.5, EtaMinutes: 15, Polyline: '_p~iF~ps|U_ulLnnqC' }] },
    validationResults: { OverallPassed: false, Checks: [{ CheckName: 'StockAvailability', Passed: false, ViolationDetail: 'Requested 60 exceeds 40.' }] },
  })
  const EXPLANATION = {
    source: 'llm', summary: 'Blocked: depot 2 has only 40 kits free.', overallPassed: false, generatedAt: '2026-10-18T00:00:00Z',
    points: [{ checkName: 'StockAvailability', passed: false, explanation: 'Restock or use another depot.' }],
  }

  const traceApi = (opts: { explain?: { status: number; body: unknown }; delivered?: boolean } = {}) =>
    mockApi((url, init) => {
      const path = url.pathname
      if (path === `/api/validations/${RUN}/explain` && init?.method === 'POST') return opts.explain ?? { status: 200, body: EXPLANATION }
      if (path === '/api/routes') {
        return { status: 200, body: [{ id: 'r1', depotId: 2, depotName: 'Kaduwela Supply Depot', shelterId: 2, shelterName: 'Kaduwela Community Hall', distanceKm: 6.5, etaMinutes: 15, polyline: '_p~iF~ps|U_ulLnnqC' }] }
      }
      if (path.endsWith('/logs')) return { status: 200, body: [] }
      if (path.includes('/by-workflow/')) {
        return {
          status: 200,
          body: {
            workflowRunId: RUN, state: 'Approved', dispatchId: opts.delivered ? 'd1' : null, decision: opts.delivered ? 'Approved' : null,
            isDelivered: !!opts.delivered, deliveredAt: opts.delivered ? '2026-10-18T08:00:00Z' : null,
            decisions: opts.delivered ? [{ dispatchId: 'd1', decision: 'Approved', notes: null, decidedById: 2, decidedBy: 'Jane Smith', decidedAt: '2026-10-18T06:00:00Z', dispatchedAt: null }] : [],
          },
        }
      }
      if (path.startsWith('/api/audit/')) {
        return { status: 200, body: { dispatchId: 'd1', decision: 'Approved', events: [
          { id: 'e1', eventType: 'DeliveryConfirmed', eventDetailJson: JSON.stringify({ photoReference: '/uploads/deliveries/abc.jpg' }), actorId: '1', createdAt: '2026-10-18T08:00:00Z' },
        ] } }
      }
      return { status: 200, body: { id: RUN, objective: 'Insulin for Kaduwela', currentState: 'Approved', failedAtState: null, failureReason: null, reportIds: [1], retryCount: 0, createdAt: '2026-10-18T05:00:00Z', updatedAt: '2026-10-18T06:00:00Z', planJson: PLAN } }
    })

  // The trace reads the run id from the URL, so mount it at the run's route.
  const renderTraceAt = () => {
    saveSession({ token: 'token-Coordinator', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user: { id: 2, name: 'Jane', email: 'c@floodlink.lk', role: 'Coordinator', phone: '+94770000000', createdAt: '' } })
    return render(
      <AppProvider>
        <AuthProvider>
          <MemoryRouter initialEntries={[`/workflows/${RUN}`]}>
            <Routes><Route path="/workflows/:id" element={<WorkflowTrace />} /></Routes>
          </MemoryRouter>
        </AuthProvider>
      </AppProvider>,
    )
  }
  it('draws the route legs and names depot and shelter', async () => {
    traceApi()
    renderTraceAt()

    expect(await screen.findByRole('img', { name: 'Route map with 1 leg' })).toBeInTheDocument()
    expect(screen.getAllByTestId('route-leg')).toHaveLength(1)
    expect(screen.getByRole('list', { name: 'Map legend' })).toHaveTextContent('Kaduwela Supply Depot → Kaduwela Community Hall')
  })

  it('explains the safety checks on request, keeping the rule verdict', async () => {
    traceApi()
    renderTraceAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Explain these results' }))

    expect(await screen.findByText('Blocked: depot 2 has only 40 kits free.')).toBeInTheDocument()
    expect(screen.getByText(/Explanation written by the LLM/)).toBeInTheDocument()
    expect(screen.getByText(/display only/)).toBeInTheDocument()
  })

  it('shows why an explanation could not be loaded', async () => {
    traceApi({ explain: { status: 409, body: { error: 'NOT_VALIDATED', message: 'The safety checks have not run.' } } })
    renderTraceAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Explain these results' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The safety checks have not run.')
  })

  it('shows the delivery photo recorded with the confirmation', async () => {
    traceApi({ delivered: true })
    renderTraceAt()

    const photo = await screen.findByRole('img', { name: 'Delivery photo' })
    expect(photo).toHaveAttribute('src', 'http://localhost:5000/uploads/deliveries/abc.jpg')
  })
})
