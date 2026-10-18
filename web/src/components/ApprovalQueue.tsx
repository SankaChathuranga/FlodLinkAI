import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button, InlineLoading, InlineNotification, Stack, Tag, TextArea, Tile } from '@carbon/react'
import { useAppContext } from '../context/AppContext'
import { useAuth } from '../context/AuthContext'
import {
  approveRun,
  getApprovalQueue,
  getValidationReport,
  rejectRun,
  requestRevision,
} from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { ValidationReport, WorkflowRunQueueItem } from '../api/types'

/**
 * Approval Queue – Member D (Ijini) coordinator screen.
 *
 * Shows every workflow run that has passed the Validation/Safety Agent and is
 * waiting for a human decision. The coordinator can inspect the safety checks,
 * then Approve, Reject (reason required) or send back for Revision (notes required).
 *
 * Each card shows a one-line plan summary (reports, allocations, route distance/ETA, unmet needs,
 * whether the LLM was used); the full trace is one click away. Reject and revision notes are
 * mandatory here and on the server. After a decision the notice names the approver.
 */

type ActionMode = 'approve' | 'reject' | 'revision'

interface QueueState {
  items: WorkflowRunQueueItem[]
  expandedId: string | null
  reports: Record<string, ValidationReport>
  mode: ActionMode | null
  notes: string
  busyId: string | null
  error: string | null
  notice: string | null
}

function ApprovalQueue() {
  const { apiBaseUrl } = useAppContext()
  const { user } = useAuth()

  const [state, setState] = useState<QueueState>({
    items: [],
    expandedId: null,
    reports: {},
    mode: null,
    notes: '',
    busyId: null,
    error: null,
    notice: null,
  })

  const [loading, setLoading] = useState(true)
  const mountedRef = useRef(true)

  const refresh = useCallback(async (): Promise<void> => {
    try {
      const { items } = await getApprovalQueue(apiBaseUrl)
      if (!mountedRef.current) return
      setState((s) => ({ ...s, items, error: null }))
    } catch (err) {
      if (!mountedRef.current) return
      setState((s) => ({ ...s, error: describeApiError(err) }))
    } finally {
      if (mountedRef.current) setLoading(false)
    }
  }, [apiBaseUrl])

  useEffect(() => {
    mountedRef.current = true
    void refresh()
    return () => {
      mountedRef.current = false
    }
  }, [refresh])

  const toggleExpand = async (item: WorkflowRunQueueItem): Promise<void> => {
    if (state.expandedId === item.workflowRunId) {
      setState((s) => ({ ...s, expandedId: null, mode: null, notes: '' }))
      return
    }
    setState((s) => ({
      ...s,
      expandedId: item.workflowRunId,
      mode: null,
      notes: '',
      error: null,
    }))
    if (!state.reports[item.workflowRunId]) {
      try {
        const report = await getValidationReport(apiBaseUrl, item.workflowRunId)
        if (!mountedRef.current) return
        setState((s) => ({
          ...s,
          reports: { ...s.reports, [item.workflowRunId]: report },
        }))
      } catch {
        /* report fetch failure is non-fatal; UI shows a placeholder */
      }
    }
  }

  const submitAction = async (item: WorkflowRunQueueItem): Promise<void> => {
    const { mode, notes } = state
    if (!mode) return
    if ((mode === 'reject' || mode === 'revision') && !notes.trim()) {
      setState((s) => ({
        ...s,
        error: mode === 'reject' ? 'Rejection reason is required.' : 'Revision notes are required.',
      }))
      return
    }
    setState((s) => ({ ...s, busyId: item.workflowRunId, error: null, notice: null }))
    try {
      if (mode === 'approve') await approveRun(apiBaseUrl, item.workflowRunId, notes.trim() || undefined)
      else if (mode === 'reject') await rejectRun(apiBaseUrl, item.workflowRunId, notes.trim())
      else await requestRevision(apiBaseUrl, item.workflowRunId, notes.trim())
      setState((s) => ({
        ...s,
        busyId: null,
        expandedId: null,
        mode: null,
        notes: '',
        notice: `${mode === 'approve' ? 'Approved' : mode === 'reject' ? 'Rejected' : 'Sent back for revision'}: “${item.objective}” — by ${user?.name ?? 'you'} at ${new Date().toLocaleTimeString()}.`,
      }))
      void refresh()
    } catch (err) {
      if (!mountedRef.current) return
      setState((s) => ({ ...s, busyId: null, error: describeApiError(err) }))
    }
  }

  return (
    <section aria-labelledby="approvals-heading">
      <Stack gap={5}>
        <div>
          <h1 id="approvals-heading" className="cds--heading-04">Approval Queue</h1>
          <p className="cds--body-compact-01 fl-text-muted">
            Workflow runs awaiting coordinator decision — all have passed safety validation.
          </p>
        </div>

        {state.notice && (
          <InlineNotification kind="success" role="status" title={state.notice} lowContrast
            onCloseButtonClick={() => setState((s) => ({ ...s, notice: null }))} />
        )}
        {state.error && (
          <InlineNotification kind="error" role="alert" title={state.error} lowContrast hideCloseButton />
        )}

        {loading ? (
          <InlineLoading description="Loading queue…" />
        ) : state.items.length === 0 ? (
          <Tile className="fl-empty"><p className="cds--body-compact-01">No runs are pending approval right now.</p></Tile>
        ) : (
          <ul className="fl-report-list" aria-label="Runs awaiting approval">
            {state.items.map((item) => (
              <QueueCard
                key={item.workflowRunId}
                item={item}
                expanded={state.expandedId === item.workflowRunId}
                report={state.reports[item.workflowRunId]}
                mode={state.mode}
                notes={state.notes}
                busy={state.busyId === item.workflowRunId}
                onToggle={() => void toggleExpand(item)}
                onModeChange={(mode) => setState((s) => ({ ...s, mode, error: null }))}
                onCancel={() => setState((s) => ({ ...s, mode: null, error: null }))}
                onNotesChange={(notes) => setState((s) => ({ ...s, notes }))}
                onSubmit={() => void submitAction(item)}
              />
            ))}
          </ul>
        )}
      </Stack>
    </section>
  )
}

const MODES: { mode: ActionMode; label: string; kind: 'primary' | 'danger' | 'secondary' }[] = [
  { mode: 'approve', label: 'Approve', kind: 'primary' },
  { mode: 'reject', label: 'Reject', kind: 'danger' },
  { mode: 'revision', label: 'Request revision', kind: 'secondary' },
]

function QueueCard({
  item,
  expanded,
  report,
  mode,
  notes,
  busy,
  onToggle,
  onModeChange,
  onCancel,
  onNotesChange,
  onSubmit,
}: {
  item: WorkflowRunQueueItem
  expanded: boolean
  report: ValidationReport | undefined
  mode: ActionMode | null
  notes: string
  busy: boolean
  onToggle: () => void
  onModeChange: (mode: ActionMode) => void
  onCancel: () => void
  onNotesChange: (notes: string) => void
  onSubmit: () => void
}) {
  const panelId = `decide-${item.workflowRunId}`
  return (
    <li>
      <Tile className="fl-queue-card">
        <div className="fl-plan-banner">
          <div className="fl-queue-card__main">
            <h2 className="cds--heading-03 fl-truncate-heading">{item.objective}</h2>
            <p className="cds--helper-text-01">
              Created {new Date(item.createdAt).toLocaleString()} ·{' '}
              <Link to={`/workflows/${item.workflowRunId}`} className="cds--link">View trace</Link>
            </p>
            <PlanSummary item={item} />
          </div>
          <div className="fl-toolbar-inline">
            <Tag type="purple" size="sm">{item.state}</Tag>
            <Button kind="ghost" size="sm" onClick={onToggle} disabled={busy} aria-expanded={expanded} aria-controls={panelId}>
              {expanded ? 'Collapse' : 'Inspect & decide'}
            </Button>
          </div>
        </div>

        {expanded && (
          <div id={panelId} className="fl-queue-card__panel">
            <ValidationChecks report={report} />

            <div className="fl-toolbar-inline" role="group" aria-label="Decision">
              {MODES.map((m) => (
                <Button key={m.mode} size="sm" kind={mode === m.mode ? m.kind : 'tertiary'} aria-pressed={mode === m.mode}
                  onClick={() => onModeChange(m.mode)} disabled={busy}>
                  {m.label}
                </Button>
              ))}
            </div>

            {mode && (
              <div className="fl-stack-sm">
                <TextArea
                  id={`${panelId}-notes`}
                  labelText={mode === 'approve' ? 'Optional approval notes' : mode === 'reject' ? 'Rejection reason (required)' : 'Revision notes (required)'}
                  value={notes}
                  onChange={(e) => onNotesChange(e.target.value)}
                  disabled={busy}
                  rows={3}
                  maxCount={2000}
                  placeholder={
                    mode === 'approve'
                      ? 'e.g. Supply route confirmed – good to go.'
                      : mode === 'reject'
                        ? 'e.g. Shelter #12 already serviced by another run.'
                        : 'e.g. Vehicle capacity underestimated; re-match with bigger truck.'
                  }
                />
                <div className="fl-toolbar-inline">
                  <Button size="sm" onClick={onSubmit} disabled={busy}>{busy ? 'Submitting…' : 'Submit decision'}</Button>
                  <Button size="sm" kind="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
                </div>
              </div>
            )}
          </div>
        )}
      </Tile>
    </li>
  )
}

function PlanSummary({ item }: { item: WorkflowRunQueueItem }) {
  if (item.allocationCount === undefined) return null
  const parts = [
    `${item.reportCount ?? 0} report${item.reportCount === 1 ? '' : 's'}`,
    `${item.allocationCount} allocation${item.allocationCount === 1 ? '' : 's'} (${item.totalQuantity ?? 0} units) to ${item.shelterCount ?? 0} shelter${item.shelterCount === 1 ? '' : 's'}`,
    item.distanceKm != null && item.etaMinutes != null
      ? `${item.distanceKm.toFixed(1)} km · ETA ${Math.round(item.etaMinutes)} min`
      : 'no route',
  ]
  return (
    <div className="fl-plan-summary">
      <p className="cds--body-compact-01">{parts.join(' · ')}</p>
      <div className="fl-tag-row">
        {(item.unfulfillableCount ?? 0) > 0 && (
          <Tag type="warm-gray" size="sm">
            {item.unfulfillableCount} need{item.unfulfillableCount === 1 ? '' : 's'} not fully covered
          </Tag>
        )}
        {item.isRevision && <Tag type="blue" size="sm">Revised plan</Tag>}
        <StockCheckBadge check={item.stockCheck} />
      </div>
      <p className="cds--helper-text-01">
        Plan: {item.planSource === 'llm' ? 'LLM' : 'default'} · Triage: {item.scoringMethod === 'llm-adjusted' ? 'rules + LLM' : 'rules only'} ·
        Safety checks: {item.validationPassed ? 'passed' : `${item.failedChecks ?? 0} failed`}
      </p>
    </div>
  )
}

/** Whether depot staff confirmed the reserved stock is on the shelf (8.05). */
function StockCheckBadge({ check }: { check?: WorkflowRunQueueItem['stockCheck'] }) {
  if (!check || check.reservedLines === 0) return null
  if (check.shortfalls.length > 0) {
    const detail = check.shortfalls
      .map((s) => `${s.depotName ?? 'Depot'}: ${s.counted} of ${s.reserved} ${s.itemName} on the shelf${s.note ? ` (${s.note})` : ''}`)
      .join('; ')
    return (
      <>
        <Tag type="red" size="sm">Shortfall reported by depot</Tag>
        <span className="cds--helper-text-01 fl-text-error fl-block">{detail}</span>
      </>
    )
  }
  const all = check.confirmedLines === check.reservedLines
  return (
    <Tag type={all ? 'green' : 'blue'} size="sm">
      {all ? 'Depot confirmed stock' : `Depot confirmed ${check.confirmedLines} of ${check.reservedLines} lines`}
    </Tag>
  )
}

function ValidationChecks({ report }: { report: ValidationReport | undefined }) {
  if (!report) {
    return <InlineLoading description="Loading safety checks…" />
  }

  if (!report.hasRunValidation) {
    return <InlineNotification kind="warning" title="No safety checks recorded for this run yet." lowContrast hideCloseButton />
  }

  return (
    <div className="fl-stack-sm">
      <div className="fl-tag-row">
        <strong>Safety checks</strong>
        <Tag type={report.overallPassed ? 'green' : 'red'} size="sm">{report.overallPassed ? 'Overall PASS' : 'Overall FAIL'}</Tag>
      </div>
      <ul className="fl-check-list">
        {report.checks.map((check) => (
          <li key={check.checkName} className={check.passed ? '' : 'fl-check-list__failed'}>
            <span>
              <span aria-hidden="true">{check.passed ? '✓' : '✗'}</span> {check.checkName}
              <span className="sr-only">{check.passed ? ' passed' : ' failed'}</span>
            </span>
            {!check.passed && check.violationDetail && <span className="cds--helper-text-01">{check.violationDetail}</span>}
          </li>
        ))}
      </ul>
    </div>
  )
}

export default ApprovalQueue
