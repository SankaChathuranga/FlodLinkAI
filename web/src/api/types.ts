/**
 * Approval-queue types — mirror the JSON contract returned by Member D's
 * backend endpoints (DispatchController / ValidationController).
 * Keep property names in sync with the .NET anonymous-object responses.
 */

/** One workflow run awaiting a coordinator decision (GET /api/dispatches/approval-queue). */
export interface WorkflowRunQueueItem {
  workflowRunId: string
  objective: string
  state: string
  createdAt: string
}

/** A single deterministic safety check from the Validation/Safety Agent. */
export interface ValidationCheck {
  checkName: string
  passed: boolean
  violationDetail: string | null
}

/** Persisted validation report for one run (GET /api/validations/{id}). */
export interface ValidationReport {
  workflowRunId: string
  state: string
  hasRunValidation: boolean
  overallPassed: boolean
  checks: ValidationCheck[]
}

/** Result of an approve / reject / request-revision call. */
export interface ApprovalActionResult {
  dispatchId: string
  workflowRunId: string
  decision: string
  approvalNotes: string | null
  dispatchedAt: string | null
  state: string
  loopsBackTo?: string
}

/** One row of dispatch history (GET /api/dispatches). */
export interface DispatchListItem {
  id: string
  workflowRunId: string
  decision: string
  approvalNotes: string | null
  approvedById: string | null
  dispatchedAt: string | null
  createdAt: string
}

/** Paginated dispatch history envelope. */
export interface DispatchListResponse {
  page: number
  pageSize: number
  total: number
  items: DispatchListItem[]
}

/** One entry of the top-rejection-reasons breakdown. */
export interface RejectionReasonCount {
  reason: string
  count: number
}

/** Reporting/analytics payload (GET /api/dispatches/summary). */
export interface DispatchSummary {
  total: number
  approved: number
  rejected: number
  revisionsRequested: number
  avgApprovalMinutes: number | null
  topRejectionReasons: RejectionReasonCount[]
}

/** One append-only audit event (GET /api/audit/{dispatchId}). */
export interface AuditEvent {
  id: string
  eventType: string
  eventDetailJson: string | null
  actorId: string | null
  createdAt: string
}

/** Audit-trail envelope for one dispatch. */
export interface AuditResponse {
  dispatchId: string
  decision: string
  events: AuditEvent[]
}