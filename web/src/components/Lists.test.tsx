import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession } from '../auth/session'
import { ReportsQueue } from './ReportsQueue'
import { InventoryDashboard } from './InventoryDashboard'

// WEB-LIST: server-side search, filters and paging on the Reports queue (5.01) and Inventory (5.02).

type Handler = (url: URL) => { status: number; body: unknown }

function mockApi(handler: Handler) {
  // `init` is kept so tests can read what was sent.
  const fetchMock = vi.fn(async (url: string, _init?: RequestInit) => {
    const { status, body } = handler(new URL(url))
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** The most recent request the component made to `path`. */
const lastRequest = (fetchMock: ReturnType<typeof mockApi>, path: string) => {
  const urls = fetchMock.mock.calls.map(([url]) => new URL(url)).filter((url) => url.pathname === path)
  return urls[urls.length - 1]
}

/** Every list request the component made to `path`. */
const requestsTo = (fetchMock: ReturnType<typeof mockApi>, path: string) =>
  fetchMock.mock.calls.map(([url]) => new URL(url)).filter((url) => url.pathname === path)

const page = (items: unknown[], over: Record<string, number> = {}) => ({
  items,
  page: 1,
  pageSize: 10,
  totalItems: items.length,
  totalPages: 1,
  ...over,
})

const report = (id: number, over: Record<string, unknown> = {}) => ({
  id,
  shelterId: 1,
  shelter: { id: 1, name: 'Kaduwela Community Hall' },
  reportedBy: 1,
  needType: 'Water',
  quantityNeeded: 100,
  urgencyLevel: 60,
  status: 'New',
  createdAt: '2026-10-15T06:00:00Z',
  ...over,
})

const stock = (id: number, over: Record<string, unknown> = {}) => ({
  id,
  depotId: 1,
  depot: { id: 1, name: 'Colombo Central Depot' },
  itemName: 'Water',
  unit: 'bottles',
  quantityAvailable: 600,
  quantityReserved: 0,
  reorderThreshold: 100,
  updatedAt: '2026-10-15T06:00:00Z',
  ...over,
})

afterEach(() => sessionStorage.clear())

describe('ReportsQueue', () => {
  const renderQueue = () =>
    render(
      <AppProvider>
        <MemoryRouter>
          <ReportsQueue />
        </MemoryRouter>
      </AppProvider>,
    )

  it('pages through reports on the server', async () => {
    const fetchMock = mockApi((url) => {
      if (url.pathname === '/api/shelters') return { status: 200, body: page([]) }
      const current = Number(url.searchParams.get('page'))
      return { status: 200, body: page([report(current === 2 ? 11 : 1)], { page: current, totalItems: 11, totalPages: 2 }) }
    })
    renderQueue()

    expect(await screen.findByText('Page 1 of 2 (11 reports)')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('#R-11')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(lastRequest(fetchMock, '/api/reports')?.searchParams.get('pageSize')).toBe('10')
  })

  it('sends the search term and starts again from page 1', async () => {
    const fetchMock = mockApi((url) =>
      url.pathname === '/api/shelters' ? { status: 200, body: page([]) } : { status: 200, body: page([report(3)]) },
    )
    renderQueue()
    await screen.findByText('#R-3')

    await userEvent.type(screen.getByRole('searchbox', { name: 'Search reports' }), 'insulin')

    await waitFor(() => {
      const last = lastRequest(fetchMock, '/api/reports')
      expect(last?.searchParams.get('search')).toBe('insulin')
      expect(last?.searchParams.get('page')).toBe('1')
    })
  })

  it('sends the shelter, status and urgency filters and the chosen sort', async () => {
    const fetchMock = mockApi((url) =>
      url.pathname === '/api/shelters'
        ? { status: 200, body: page([{ id: 2, name: 'Kaduwela Community Hall' }]) }
        : { status: 200, body: page([report(3)]) },
    )
    renderQueue()
    await screen.findByText('#R-3')
    await screen.findByRole('option', { name: 'Kaduwela Community Hall' })

    const [shelter, status, urgency, sort] = screen.getAllByRole('combobox')
    await userEvent.selectOptions(shelter, '2')
    await userEvent.selectOptions(status, 'Triaged')
    await userEvent.selectOptions(urgency, '80')
    await userEvent.selectOptions(sort, 'quantity_desc')

    await waitFor(() => {
      const params = lastRequest(fetchMock, '/api/reports')?.searchParams
      expect(params?.get('shelterId')).toBe('2')
      expect(params?.get('status')).toBe('Triaged')
      expect(params?.get('urgency')).toBe('80')
      expect(params?.get('sort')).toBe('quantity_desc')
      expect(params?.get('page')).toBe('1')
    })
  })

  it('shows the error state and retries', async () => {
    let fail = true
    mockApi((url) => {
      if (url.pathname === '/api/shelters') return { status: 200, body: page([]) }
      return fail ? { status: 500, body: { message: 'boom' } } : { status: 200, body: page([report(4)]) }
    })
    renderQueue()

    expect(await screen.findByText('Failed to Load Queue')).toBeInTheDocument()
    expect(screen.getByText('Failed to fetch report queue (HTTP 500)')).toBeInTheDocument()
    fail = false
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByText('#R-4')).toBeInTheDocument()
  })

  it('shows the empty state when nothing matches', async () => {
    mockApi((url) => ({ status: 200, body: url.pathname === '/api/shelters' ? page([]) : page([]) }))
    renderQueue()
    expect(await screen.findByText('No Field Reports in Queue')).toBeInTheDocument()
  })
})

describe('InventoryDashboard', () => {
  const renderInventory = () => {
    saveSession({
      token: 'token-depot',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 3, name: 'Kamal Perera', email: 'depot@floodlink.lk', role: 'DepotManager', phone: '+94770000000', createdAt: '' },
    })
    return render(
      <AppProvider>
        <AuthProvider>
          <InventoryDashboard />
        </AuthProvider>
      </AppProvider>,
    )
  }

  it('marks lines at or below the reorder threshold as low stock', async () => {
    mockApi(() => ({
      status: 200,
      body: page([
        stock(1),
        stock(4, { itemName: 'Medical', unit: 'kits', quantityAvailable: 50, quantityReserved: 45, reorderThreshold: 10 }),
      ]),
    }))
    renderInventory()

    expect(await screen.findByText('Medical')).toBeInTheDocument()
    expect(screen.getAllByText('Low stock')).toHaveLength(1)
    expect(screen.getByText('5 kits')).toBeInTheDocument() // free = 50 available − 45 reserved
  })

  it('asks the API for low stock only and shows the filtered empty state', async () => {
    const fetchMock = mockApi((url) => ({
      status: 200,
      body: url.searchParams.get('lowStock') === 'true' ? page([]) : page([stock(1)]),
    }))
    renderInventory()
    await screen.findByText('Water')

    await userEvent.click(screen.getByLabelText('Low stock only'))

    expect(await screen.findByText('No stock lines match these filters.')).toBeInTheDocument()
    expect(lastRequest(fetchMock, '/api/inventory')?.searchParams.get('lowStock')).toBe('true')
  })

  it('sends the chosen sort to the API', async () => {
    const fetchMock = mockApi(() => ({ status: 200, body: page([stock(1)]) }))
    renderInventory()
    await screen.findByText('Water')

    await userEvent.selectOptions(screen.getByLabelText('Sort by'), 'quantity_asc')

    await waitFor(() => expect(lastRequest(fetchMock, '/api/inventory')?.searchParams.get('sort')).toBe('quantity_asc'))
  })

  it('a refused hold shows the API reason and keeps the dialog open', async () => {
    const fetchMock = mockApi((url) =>
      url.pathname.endsWith('/reserve')
        ? { status: 409, body: { error: 'INSUFFICIENT_STOCK', message: 'Only 5 Medical is free to reserve.' } }
        : { status: 200, body: page([stock(4, { itemName: 'Medical', unit: 'kits', quantityAvailable: 50, quantityReserved: 45, reorderThreshold: 10 })]) },
    )
    renderInventory()
    await userEvent.click(await screen.findByRole('button', { name: 'Hold Medical at Colombo Central Depot' }))

    const quantity = screen.getByLabelText('Quantity to hold (kits)')
    await userEvent.clear(quantity)
    await userEvent.type(quantity, '20')
    await userEvent.click(screen.getByRole('button', { name: 'Hold stock' }))

    expect(await screen.findByText('Only 5 Medical is free to reserve.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Hold stock' })).toBeInTheDocument()
    const hold = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/api/inventory/4/reserve'))
    expect(JSON.parse(String(hold?.[1]?.body))).toEqual({ quantity: 20 })
  })

  it('a successful release reloads the stock', async () => {
    let released = false
    const fetchMock = mockApi((url) => {
      if (url.pathname.endsWith('/release')) {
        released = true
        return { status: 200, body: stock(1) }
      }
      return { status: 200, body: page([stock(1, { quantityReserved: released ? 0 : 40 })]) }
    })
    renderInventory()
    await userEvent.click(await screen.findByRole('button', { name: 'Release Water at Colombo Central Depot' }))
    await userEvent.click(screen.getByRole('button', { name: 'Release stock' }))

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Release Water at Colombo Central Depot' })).not.toBeInTheDocument())
    expect(requestsTo(fetchMock, '/api/inventory')).toHaveLength(2)
  })

  it('shows the API error', async () => {
    mockApi(() => ({ status: 500, body: { message: 'boom' } }))
    renderInventory()
    expect(await screen.findByText('Could not load stock (HTTP 500).')).toBeInTheDocument()
  })
})
