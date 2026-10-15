import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { WorkflowMonitor } from './WorkflowMonitor'
import { WorkflowTrace } from './WorkflowTrace'

// WEB-WF: workflow monitor (4.01/4.03) and execution trace (4.02) with fetch mocked per URL.

type Handler = (url: string, init?: RequestInit) => { status: number; body: unknown }

function mockApi(handler: Handler) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const { status, body } = handler(url, init)
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const RUN_ID = '741984fa-7614-4107-9871-1f77126cdc5d'

const summary = (over: Record<string, unknown> = {}) => ({
  id: RUN_ID,
  objective: 'Water for Kaduwela',
  currentState: 'PendingApproval',
  progressPercent: 90,
  failedAtState: null,
  failureReason: null,
  reportCount: 2,
  retryCount: 0,
  retryLimit: 3,
  createdAt: '2026-10-15T06:00:00Z',
  updatedAt: '2026-10-15T06:01:00Z',
  ...over,
})

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 })

const renderMonitor = () =>
  render(
    <AppProvider>
      <MemoryRouter>
        <WorkflowMonitor />
      </MemoryRouter>
    </AppProvider>,
  )

describe('WorkflowMonitor', () => {
  it('lists runs with stage and progress', async () => {
    mockApi(() => ({ status: 200, body: page([summary()]) }))
    renderMonitor()

    expect(await screen.findByRole('link', { name: 'Water for Kaduwela' })).toHaveAttribute('href', `/workflows/${RUN_ID}`)
    expect(within(screen.getByRole('table')).getByText('Awaiting approval')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '90')
  })

  it('shows the empty state', async () => {
    mockApi(() => ({ status: 200, body: page([]) }))
    renderMonitor()
    expect(await screen.findByText('No workflow runs yet.')).toBeInTheDocument()
  })

  it('shows the API error with a way to try again', async () => {
    mockApi(() => ({ status: 500, body: { error: 'SERVER_ERROR', message: 'Database unavailable' } }))
    renderMonitor()
    expect(await screen.findByRole('alert')).toHaveTextContent('Database unavailable')
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('failed view retries a run and disables Retry at the limit', async () => {
    const fetchMock = mockApi((url, init) => {
      if (url.includes('/retry') && init?.method === 'POST') return { status: 202, body: { id: RUN_ID, state: 'Matching', retryCount: 1 } }
      return {
        status: 200,
        body: page([
          summary({ currentState: 'Failed', failedAtState: 'Matching', failureReason: 'NO_STOCK_AVAILABLE: none', progressPercent: 100 }),
          summary({ id: 'b2', objective: 'Food run', currentState: 'Failed', failedAtState: 'Routing', retryCount: 3 }),
        ]),
      }
    })
    renderMonitor()

    await userEvent.click(await screen.findByRole('tab', { name: 'Failed runs' }))
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('state=Failed'))).toBe(true)
    expect(await screen.findByText(/NO_STOCK_AVAILABLE/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Limit reached' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(await screen.findByRole('status')).toHaveTextContent('attempt 1 of 3')
  })
})

const PLAN_JSON = JSON.stringify({
  plan: {
    Source: 'llm',
    Steps: [
      { Agent: 'Triage', Purpose: 'Rank needs.', Rationale: 'Infants first.' },
      { Agent: 'Matching', Purpose: 'Allocate.', Rationale: 'Free stock.' },
      { Agent: 'Routing', Purpose: 'Route.', Rationale: 'Two depots.' },
      { Agent: 'Validation', Purpose: 'Check.', Rationale: 'Safety.' },
    ],
  },
  triagePlan: {
    ScoringMethod: 'llm-adjusted',
    PriorityItems: [{ ReportId: 9, ShelterId: 2, NeedType: 'Water', Quantity: 60, PriorityScore: 95, RuleScore: 80, LlmAdjustment: 15, Justification: 'Contaminated well, infants.' }],
  },
  allocationProposal: { AllocationProposalId: 1, Allocations: [{ DepotId: 2, ShelterId: 2, ItemName: 'Water', Quantity: 60 }], Unfulfillable: [] },
  route: { DistanceKm: 12.5, EtaMinutes: 25, Legs: [{ DepotId: 2, ShelterId: 2, DistanceKm: 12.5, EtaMinutes: 25 }] },
  validationResults: { OverallPassed: true, Checks: [{ CheckName: 'StockAvailability', Passed: true }] },
})

const LOGS = [
  { id: 'l1', agentName: 'PlannerAgent', status: 'Success', isRetry: false, durationMs: 3100, createdAt: '2026-10-15T06:00:03Z', errorMessage: null, output: null,
    toolCalls: [{ Tool: 'llm.generate', Succeeded: true, Input: { model: 'gemini-3.8-flash', promptVersion: 'planner.v1' }, Output: { usedFallback: false, latencyMs: 3000, attempts: 1, promptTokens: 388, outputTokens: 181 } }] },
  { id: 'l2', agentName: 'MatchingAgent', status: 'Success', isRetry: false, durationMs: 13600, createdAt: '2026-10-15T06:00:20Z', errorMessage: null, output: null,
    toolCalls: [
      { Tool: 'db.inventory.read', Succeeded: true, Input: { itemNames: ['Water'] }, Output: { stockLines: 4 }, DurationMs: 4 },
      { Tool: 'llm.generate', Succeeded: false, Input: { model: 'gemini-3.8-flash', promptVersion: 'matching.v1' }, Output: { usedFallback: true, attempts: 3 }, Error: 'LLM_HTTP_ERROR: Gemini returned HTTP 503.' },
    ] },
]

const DISPATCH = {
  workflowRunId: RUN_ID, state: 'Approved', dispatchId: 'd1', decision: 'Approved', notes: 'Go', decidedBy: 'Jane Smith',
  decidedAt: '2026-10-15T06:05:00Z', dispatchedAt: '2026-10-15T06:05:00Z', isDelivered: false, deliveredAt: null,
  decisions: [{ dispatchId: 'd1', decision: 'Approved', notes: 'Go', decidedById: 2, decidedBy: 'Jane Smith', decidedAt: '2026-10-15T06:05:00Z', dispatchedAt: null }],
}

const renderTrace = () =>
  render(
    <AppProvider>
      <MemoryRouter initialEntries={[`/workflows/${RUN_ID}`]}>
        <Routes>
          <Route path="/workflows/:id" element={<WorkflowTrace />} />
        </Routes>
      </MemoryRouter>
    </AppProvider>,
  )

describe('WorkflowTrace', () => {
  it('shows the plan, LLM calls with fallback flags, triage, routes and the approver', async () => {
    mockApi((url) => {
      if (url.endsWith('/logs')) return { status: 200, body: LOGS }
      if (url.includes('/by-workflow/')) return { status: 200, body: DISPATCH }
      if (url.includes('/api/audit/')) return { status: 200, body: { dispatchId: 'd1', decision: 'Approved', events: [] } }
      return {
        status: 200,
        body: { id: RUN_ID, objective: 'Water for Kaduwela', currentState: 'Approved', failedAtState: null, failureReason: null, reportIds: [8, 9], retryCount: 0, createdAt: '2026-10-15T06:00:00Z', updatedAt: '2026-10-15T06:05:00Z', planJson: PLAN_JSON },
      }
    })
    renderTrace()

    expect(await screen.findByRole('heading', { name: 'Water for Kaduwela' })).toBeInTheDocument()
    expect(screen.getByText('Written by the LLM')).toBeInTheDocument()
    expect(screen.getByText('LLM output accepted')).toBeInTheDocument()
    expect(screen.getByText('LLM not used — rule-based fallback')).toBeInTheDocument()
    expect(screen.getByText(/Gemini returned HTTP 503/)).toBeInTheDocument()
    expect(screen.getByText('+15')).toBeInTheDocument()
    expect(screen.getByText(/Total 12.5 km/)).toBeInTheDocument()
    expect(screen.getByText('Jane Smith')).toBeInTheDocument()
  })

  it('shows a not-found message for an unknown run', async () => {
    mockApi(() => ({ status: 404, body: { error: 'NOT_FOUND', message: 'nope' } }))
    renderTrace()
    expect(await screen.findByText(/No workflow run with id/)).toBeInTheDocument()
  })

  it('shows the loading state, then a retryable error', async () => {
    mockApi(() => ({ status: 500, body: { error: 'SERVER_ERROR', message: 'Database unavailable' } }))
    renderTrace()
    expect(screen.getByText('Loading execution trace…')).toBeInTheDocument()
    expect(await screen.findByRole('alert')).toHaveTextContent('Database unavailable')
  })
})
