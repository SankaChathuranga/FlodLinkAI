import type {
  ApprovalActionResult,
  AuditResponse,
  DispatchListResponse,
  DispatchSummary,
  ValidationReport,
  WorkflowRunQueueItem,
} from './types'

/**
 * Minimal JSON API client for the FloodLink backend (Member D — approval queue).
 * Non-2xx responses throw an ApiError carrying the backend's { error, message }.
 */

export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

const isOk = (status: number) => status >= 200 && status < 300

async function request<T>(baseUrl: string, path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })

  const body = (await res.json().catch(() => null)) as
    | { error?: string; message?: string }
    | null

  if (!isOk(res.status)) {
    const code = body?.error ?? 'REQUEST_FAILED'
    const message = body?.message ?? `${res.status} ${res.statusText}`
    throw new ApiError(res.status, code, message)
  }

  return body as T
}

/** Queue of workflow runs awaiting a coordinator decision. */
export function getApprovalQueue(
  baseUrl: string,
  signal?: AbortSignal,
): Promise<{ items: WorkflowRunQueueItem[] }> {
  return request(baseUrl, '/api/dispatches/approval-queue', { signal })
}

/** Persisted safety-check report for a workflow run. */
export function getValidationReport(
  baseUrl: string,
  workflowRunId: string,
  signal?: AbortSignal,
): Promise<ValidationReport> {
  return request(baseUrl, `/api/validations/${workflowRunId}`, { signal })
}

/** Approve a plan. Notes are optional. */
export function approveRun(baseUrl: string, workflowRunId: string, notes?: string): Promise<ApprovalActionResult> {
  return request(baseUrl, `/api/dispatches/${workflowRunId}/approve`, {
    method: 'POST',
    body: JSON.stringify({ notes: notes ?? null }),
  })
}

/** Reject a plan. Reason is mandatory (enforced server-side too). */
export function rejectRun(baseUrl: string, workflowRunId: string, reason: string): Promise<ApprovalActionResult> {
  return request(baseUrl, `/api/dispatches/${workflowRunId}/reject`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })
}

/** Send a plan back for re-matching. Notes are mandatory. */
export function requestRevision(baseUrl: string, workflowRunId: string, notes: string): Promise<ApprovalActionResult> {
  return request(baseUrl, `/api/dispatches/${workflowRunId}/request-revision`, {
    method: 'POST',
    body: JSON.stringify({ notes }),
  })
}

/** Paginated, filterable dispatch history. */
export function listDispatches(
  baseUrl: string,
  params: { status?: string; dateFrom?: string; dateTo?: string; page?: number; pageSize?: number },
): Promise<DispatchListResponse> {
  const qs = new URLSearchParams()
  if (params.status) qs.set('status', params.status)
  if (params.dateFrom) qs.set('dateFrom', params.dateFrom)
  if (params.dateTo) qs.set('dateTo', params.dateTo)
  qs.set('page', String(params.page ?? 1))
  qs.set('pageSize', String(params.pageSize ?? 20))

  return request<DispatchListResponse>(baseUrl, `/api/dispatches?${qs.toString()}`)
}

/** Reporting/analytics: decision totals, turnaround and rejection reasons. */
export function getDispatchSummary(baseUrl: string): Promise<DispatchSummary> {
  return request(baseUrl, '/api/dispatches/summary')
}

/** Append-only compliance trail for one dispatch. */
export function getAuditTrail(baseUrl: string, dispatchId: string): Promise<AuditResponse> {
  return request(baseUrl, `/api/audit/${dispatchId}`)
}