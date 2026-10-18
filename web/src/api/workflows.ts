import { request } from './client'

/**
 * Workflow endpoints (Member C): start a run, list and monitor runs, read the execution
 * trace, retry a failed stage, and read the coordinator decisions on a run.
 * Field names mirror the .NET DTOs (camelCase); PlanJson sections keep the C# PascalCase names.
 */

export type WorkflowState =
  | 'Triage'
  | 'Matching'
  | 'Routing'
  | 'Validating'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'RevisionRequested'
  | 'Failed'

export const WORKFLOW_STATES: WorkflowState[] = [
  'Triage', 'Matching', 'Routing', 'Validating', 'PendingApproval', 'Approved', 'Rejected', 'RevisionRequested', 'Failed',
]

/** States in which the background runner is still working on the run. */
export const ACTIVE_STATES: WorkflowState[] = ['Triage', 'Matching', 'Routing', 'Validating', 'RevisionRequested']

export interface WorkflowRunSummary {
  id: string
  objective: string
  currentState: WorkflowState
  progressPercent: number
  failedAtState: string | null
  failureReason: string | null
  reportCount: number
  retryCount: number
  retryLimit: number
  createdAt: string
  updatedAt: string
}

export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface WorkflowRun {
  id: string
  objective: string
  currentState: WorkflowState
  failedAtState: string | null
  failureReason: string | null
  reportIds: number[]
  retryCount: number
  createdAt: string
  updatedAt: string
  planJson: string | null
}

/** A tool call recorded by an agent (PascalCase: serialized by the orchestrator). */
export interface ToolCall {
  Tool: string
  Input?: Record<string, unknown> | null
  Output?: Record<string, unknown> | null
  Succeeded: boolean
  DurationMs?: number
  Error?: string | null
}

export interface WorkflowLogEntry {
  id: string
  agentName: string
  status: string
  isRetry: boolean
  durationMs: number
  createdAt: string
  errorMessage: string | null
  toolCalls: ToolCall[] | null
  output: unknown
  input?: unknown
}

export interface Decision {
  dispatchId: string
  decision: 'Approved' | 'Rejected' | 'RevisionRequested' | string
  notes: string | null
  decidedById: number | null
  decidedBy: string | null
  decidedAt: string
  dispatchedAt: string | null
}

export interface DispatchStatus {
  workflowRunId: string
  state: string
  dispatchId: string | null
  decision: string | null
  notes: string | null
  decidedBy: string | null
  decidedAt: string | null
  dispatchedAt: string | null
  isDelivered: boolean
  deliveredAt: string | null
  decisions: Decision[]
}

// ── PlanJson sections (C# PascalCase) ─────────────────────────────────────────

export interface PlanSections {
  plan?: {
    Steps: { Agent: string; Purpose: string; Rationale: string }[]
    Source: 'llm' | 'default' | string
    PromptVersion?: string | null
    FallbackReason?: string | null
  }
  triagePlan?: {
    ScoringMethod?: string
    PriorityItems: {
      ReportId: number
      ShelterId: number
      NeedType: string
      Quantity: number
      PriorityScore: number
      Justification: string
      RuleScore?: number | null
      LlmAdjustment?: number | null
    }[]
  }
  allocationProposal?: {
    AllocationProposalId: number
    Allocations: { DepotId: number; ShelterId: number; ItemName: string; Quantity: number }[]
    Unfulfillable: { ShelterId: number; ItemName: string; Reason: string }[]
  }
  route?: {
    DistanceKm: number
    EtaMinutes: number
    Legs?: { DepotId: number; ShelterId: number; DistanceKm: number; EtaMinutes: number; Polyline?: string }[]
  }
  validationResults?: {
    OverallPassed: boolean
    Checks: { CheckName: string; Passed: boolean; ViolationDetail?: string | null }[]
  }
  revision?: { Notes: string; RequestedById?: number | null; RequestedAt: string }
}

export function parsePlan(planJson: string | null | undefined): PlanSections {
  if (!planJson) return {}
  try {
    return JSON.parse(planJson) as PlanSections
  } catch {
    return {}
  }
}

/** The LLM call recorded in a log entry's tool calls, if any. */
export function llmCallOf(entry: WorkflowLogEntry): ToolCall | undefined {
  return entry.toolCalls?.find((t) => t.Tool === 'llm.generate')
}

// ── Calls ─────────────────────────────────────────────────────────────────────

export function listWorkflows(
  baseUrl: string,
  params: { state?: string; search?: string; page?: number; pageSize?: number },
  signal?: AbortSignal,
): Promise<Page<WorkflowRunSummary>> {
  const qs = new URLSearchParams()
  if (params.state) qs.set('state', params.state)
  if (params.search) qs.set('search', params.search)
  qs.set('page', String(params.page ?? 1))
  qs.set('pageSize', String(params.pageSize ?? 20))
  qs.set('sort', '-updatedAt')
  return request(baseUrl, `/api/workflows?${qs.toString()}`, { signal })
}

export function getWorkflow(baseUrl: string, id: string, signal?: AbortSignal): Promise<WorkflowRun> {
  return request(baseUrl, `/api/workflows/${id}`, { signal })
}

export function getWorkflowLogs(baseUrl: string, id: string, signal?: AbortSignal): Promise<WorkflowLogEntry[]> {
  return request(baseUrl, `/api/workflows/${id}/logs`, { signal })
}

export function getDispatchStatus(baseUrl: string, id: string, signal?: AbortSignal): Promise<DispatchStatus> {
  return request(baseUrl, `/api/dispatches/by-workflow/${id}`, { signal })
}

export function startWorkflow(baseUrl: string, objective: string, reportIds: number[]): Promise<WorkflowRun> {
  return request(baseUrl, '/api/workflows', { method: 'POST', body: JSON.stringify({ objective, reportIds }) })
}

/** A stored route leg (GET /api/routes?workflowRunId=), with depot and shelter names. */
export interface StoredRouteLeg {
  id: string
  depotId: number | null
  depotName: string | null
  shelterId: number | null
  shelterName: string | null
  distanceKm: number
  etaMinutes: number
  polyline: string
}

export function getRouteLegs(baseUrl: string, id: string, signal?: AbortSignal): Promise<StoredRouteLeg[]> {
  return request(baseUrl, `/api/routes?workflowRunId=${id}`, { signal })
}

/** Plain-language explanation of the safety checks (display only; the rules decide pass/fail). */
export interface ValidationExplanation {
  source: 'llm' | 'template' | string
  promptVersion?: string | null
  fallbackReason?: string | null
  summary: string
  points: { checkName: string; passed: boolean; explanation: string }[]
  overallPassed: boolean
  generatedAt: string
}

export function explainValidation(baseUrl: string, id: string, refresh = false): Promise<ValidationExplanation> {
  return request(baseUrl, `/api/validations/${id}/explain${refresh ? '?refresh=true' : ''}`, { method: 'POST' })
}

export function retryWorkflow(baseUrl: string, id: string): Promise<{ id: string; state: string; retryCount: number }> {
  return request(baseUrl, `/api/workflows/${id}/retry`, { method: 'POST' })
}

// ── Display helpers ───────────────────────────────────────────────────────────

const STATE_LABELS: Record<string, string> = {
  Triage: 'Triage',
  Matching: 'Matching',
  Routing: 'Routing',
  Validating: 'Validating',
  PendingApproval: 'Awaiting approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
  RevisionRequested: 'Revision requested',
  Failed: 'Failed',
}

export const stateLabel = (state: string) => STATE_LABELS[state] ?? state

export function stateBadgeClass(state: string): string {
  switch (state) {
    case 'Approved':
      return 'bg-emerald-100 text-emerald-800 border-emerald-300'
    case 'Rejected':
    case 'Failed':
      return 'bg-rose-100 text-rose-800 border-rose-300'
    case 'PendingApproval':
      return 'bg-amber-100 text-amber-800 border-amber-300'
    default:
      return 'bg-blue-100 text-blue-800 border-blue-300'
  }
}

export function formatDuration(ms: number): string {
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`
}
