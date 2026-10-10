import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AppProvider } from '../context/AppContext'
import ApprovalQueue from './ApprovalQueue'

// WEB-001..WEB-007: Approval Queue states and coordinator actions, with fetch mocked per URL.

const RUN = {
  workflowRunId: '7d2db5ee-b775-4f65-91a2-ada398f8b3ac',
  objective: 'Water for Relief Camp 1',
  state: 'PendingApproval',
  createdAt: '2026-10-08T10:00:00Z',
}

const REPORT = {
  workflowRunId: RUN.workflowRunId,
  hasRunValidation: true,
  overallPassed: true,
  checks: [{ checkName: 'StockAvailability', passed: true, violationDetail: null }],
}

type Handler = (url: string, init?: RequestInit) => { status: number; body: unknown }

function mockApi(handler: Handler) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const { status, body } = handler(url, init)
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const renderQueue = () =>
  render(
    <AppProvider>
      <ApprovalQueue />
    </AppProvider>,
  )

describe('ApprovalQueue', () => {
  it('WEB-001 lists runs waiting for approval', async () => {
    mockApi(() => ({ status: 200, body: { items: [RUN] } }))
    renderQueue()

    expect(screen.getByText('Loading queue…')).toBeInTheDocument()
    expect(await screen.findByText(RUN.objective)).toBeInTheDocument()
    expect(screen.getByText('PendingApproval')).toBeInTheDocument()
  })

  it('WEB-002 shows the empty state when nothing is pending', async () => {
    mockApi(() => ({ status: 200, body: { items: [] } }))
    renderQueue()

    expect(await screen.findByText('No runs are pending approval right now.')).toBeInTheDocument()
  })

  it('WEB-003 shows the API error instead of an empty queue', async () => {
    mockApi(() => ({ status: 500, body: { error: 'SERVER_ERROR', message: 'Database unavailable' } }))
    renderQueue()

    expect(await screen.findByText(/Request failed \(500 SERVER_ERROR\): Database unavailable/)).toBeInTheDocument()
  })

  it('WEB-004 explains a 401 so the coordinator knows auth is missing', async () => {
    mockApi(() => ({ status: 401, body: null }))
    renderQueue()

    expect(await screen.findByText(/401 Unauthorized/)).toBeInTheDocument()
  })

  it('WEB-005 approve posts the decision and shows a success notice', async () => {
    let approved = false
    const fetchMock = mockApi((url, init) => {
      if (url.endsWith('/approve') && init?.method === 'POST') {
        approved = true
        return { status: 200, body: { dispatchId: 'd1', workflowRunId: RUN.workflowRunId, state: 'Approved' } }
      }
      if (url.includes('/api/validations/')) return { status: 200, body: REPORT }
      return { status: 200, body: { items: approved ? [] : [RUN] } }
    })
    renderQueue()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Inspect & decide' }))
    await user.click(screen.getByRole('button', { name: 'Approve' }))
    await user.click(screen.getByRole('button', { name: 'Submit decision' }))

    expect(await screen.findByText(/Decision recorded for run 7d2db5ee/)).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(`/api/dispatches/${RUN.workflowRunId}/approve`),
      expect.objectContaining({ method: 'POST' }),
    )
    await waitFor(() => expect(screen.getByText('No runs are pending approval right now.')).toBeInTheDocument())
  })

  it('WEB-006 reject without a reason is blocked and never calls the API', async () => {
    const fetchMock = mockApi((url) =>
      url.includes('/api/validations/') ? { status: 200, body: REPORT } : { status: 200, body: { items: [RUN] } },
    )
    renderQueue()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Inspect & decide' }))
    await user.click(screen.getByRole('button', { name: 'Reject' }))
    await user.click(screen.getByRole('button', { name: 'Submit decision' }))

    expect(await screen.findByText('Rejection reason is required.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/reject'))).toBe(false)
  })

  it('WEB-007 a failed approval shows the error and no success notice', async () => {
    mockApi((url, init) => {
      if (url.endsWith('/approve') && init?.method === 'POST')
        return { status: 409, body: { error: 'INSUFFICIENT_STOCK', message: 'Depot 1 no longer has 40 Water available.' } }
      if (url.includes('/api/validations/')) return { status: 200, body: REPORT }
      return { status: 200, body: { items: [RUN] } }
    })
    renderQueue()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Inspect & decide' }))
    await user.click(screen.getByRole('button', { name: 'Approve' }))
    await user.click(screen.getByRole('button', { name: 'Submit decision' }))

    expect(await screen.findByText(/409 INSUFFICIENT_STOCK/)).toBeInTheDocument()
    expect(screen.queryByText(/Decision recorded/)).not.toBeInTheDocument()
  })
})
