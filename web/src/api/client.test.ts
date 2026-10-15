import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, approveRun, listDispatches } from './client'
import { describeApiError } from '../lib/apiError'
import { getToken, onUnauthorized, saveSession } from '../auth/session'

// 6.21: the JSON client wrapper with a mocked fetch — headers, query strings, error mapping and 401 handling.

function mockFetch(status: number, body: unknown) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) =>
    new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function signIn() {
  saveSession({
    token: 'token-coordinator',
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 2, name: 'Jane Smith', email: 'coordinator@floodlink.lk', role: 'Coordinator', phone: '+94770000000', createdAt: '' },
  })
}

afterEach(() => sessionStorage.clear())

describe('API client wrapper', () => {
  it('sends JSON with the bearer token and returns the parsed body', async () => {
    signIn()
    const fetchMock = mockFetch(200, { dispatchId: 'd1', decision: 'Approved' })

    const result = await approveRun('http://api.test', 'run-1', 'Go')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(url).toBe('http://api.test/api/dispatches/run-1/approve')
    expect(init?.method).toBe('POST')
    expect(headers.get('Authorization')).toBe('Bearer token-coordinator')
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(JSON.parse(String(init?.body))).toEqual({ notes: 'Go' })
    expect(result).toEqual({ dispatchId: 'd1', decision: 'Approved' })
  })

  it('builds the query string for list filters', async () => {
    signIn()
    const fetchMock = mockFetch(200, { page: 2, pageSize: 10, total: 0, items: [] })

    await listDispatches('http://api.test', { status: 'Rejected', dateFrom: '2026-10-01', page: 2, pageSize: 10 })

    const url = new URL(fetchMock.mock.calls[0][0])
    expect(Object.fromEntries(url.searchParams)).toEqual({ status: 'Rejected', dateFrom: '2026-10-01', page: '2', pageSize: '10' })
  })

  it('turns an error response into an ApiError with the API code and message', async () => {
    signIn()
    mockFetch(409, { error: 'INVALID_STATE', message: 'Only a PendingApproval plan can be approved.' })

    const error = await approveRun('http://api.test', 'run-1').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, code: 'INVALID_STATE', message: 'Only a PendingApproval plan can be approved.' })
    expect(describeApiError(error)).toBe('Request failed (409 INVALID_STATE): Only a PendingApproval plan can be approved.')
  })

  it('a non-JSON error still becomes an ApiError', async () => {
    signIn()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('Bad gateway', { status: 502, statusText: 'Bad Gateway' })))

    const error = await approveRun('http://api.test', 'run-1').catch((e: unknown) => e)

    expect(error).toMatchObject({ status: 502, code: 'REQUEST_FAILED', message: '502 Bad Gateway' })
  })

  it('a 401 signs the user out and reads as an expired session', async () => {
    signIn()
    const signedOut = vi.fn()
    const stop = onUnauthorized(signedOut)
    mockFetch(401, undefined)

    const error = await approveRun('http://api.test', 'run-1').catch((e: unknown) => e)
    stop()

    expect(signedOut).toHaveBeenCalledOnce()
    expect(getToken()).toBeNull()
    expect(describeApiError(error)).toBe('Your session has expired. Sign in again.')
  })
})
