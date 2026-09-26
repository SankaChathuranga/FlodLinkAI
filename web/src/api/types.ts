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