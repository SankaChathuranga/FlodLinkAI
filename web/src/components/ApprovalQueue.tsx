import { useCallback, useEffect, useRef, useState } from 'react'
import { useAppContext } from '../context/AppContext'
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
 * Auth note: the backend endpoints carry [Authorize(Roles="Coordinator")]. Until
 * JWT auth is wired (Week 2+), they return 401 – this component surfaces that
 * clearly so a demo is not silently broken.
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
        notice: `Decision recorded for run ${item.workflowRunId.slice(0, 8)}…`,
      }))
      void refresh()
    } catch (err) {
      if (!mountedRef.current) return
      setState((s) => ({ ...s, busyId: null, error: describeApiError(err) }))
    }
  }

  return (
    <div>
      <h1 className="fl-section-title">Approval Queue</h1>
      <p className="fl-section-desc">
        Workflow runs awaiting coordinator decision — all have passed safety validation.
      </p>

      {state.notice && (
        <div className="fl-notification fl-notification-success" style={{ marginBottom: '1rem' }}>
          {state.notice}
        </div>
      )}
      {state.error && (
        <div className="fl-notification fl-notification-error" style={{ marginBottom: '1rem' }}>
          {state.error}
        </div>
      )}

      {loading ? (
        <p style={{ fontSize: '0.875rem', color: 'var(--text-muted)' }}>Loading queue…</p>
      ) : state.items.length === 0 ? (
        <div className="fl-card" style={{ padding: '2rem', textAlign: 'center', color: 'var(--text-muted)', fontSize: '0.875rem' }}>
          No runs are pending approval right now.
        </div>
      ) : (
        <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
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
    </div>
  )
}

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
  return (
    <li className="fl-card" style={{ overflow: 'hidden' }}>
      {/* Card header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', padding: '0.875rem 1rem', gap: '0.75rem' }}>
        <div style={{ minWidth: 0 }}>
          <p style={{ fontWeight: 600, color: 'var(--text-primary)', margin: '0 0 0.25rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {item.objective}
          </p>
          <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: 0 }}>
            Created {new Date(item.createdAt).toLocaleString()}
          </p>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem', flexShrink: 0 }}>
          {/* PendingApproval → warning token per spec */}
          <span className="fl-badge fl-badge-warning">{item.state}</span>
          <button type="button" onClick={onToggle} disabled={busy} className="fl-btn-ghost">
            {expanded ? 'Collapse' : 'Inspect & decide'}
          </button>
        </div>
      </div>

      {expanded && (
        <div style={{ borderTop: '1px solid var(--border-default)', padding: '1rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <ValidationChecks report={report} />

          {/* Action buttons */}
          <div>
            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
              <button
                type="button"
                onClick={() => onModeChange('approve')}
                disabled={busy}
                style={{
                  background: mode === 'approve' ? 'var(--state-success)' : 'var(--bg-surface)',
                  color: mode === 'approve' ? '#fff' : 'var(--state-success)',
                  border: `1px solid var(--state-success)`,
                  borderRadius: '4px',
                  padding: '0.375rem 0.875rem',
                  fontSize: '0.875rem',
                  fontWeight: 500,
                  cursor: busy ? 'not-allowed' : 'pointer',
                  opacity: busy ? 0.5 : 1,
                  fontFamily: 'var(--font-sans)',
                }}
              >
                Approve
              </button>
              <button
                type="button"
                onClick={() => onModeChange('reject')}
                disabled={busy}
                style={{
                  background: mode === 'reject' ? 'var(--state-error)' : 'var(--bg-surface)',
                  color: mode === 'reject' ? '#fff' : 'var(--state-error)',
                  border: `1px solid var(--state-error)`,
                  borderRadius: '4px',
                  padding: '0.375rem 0.875rem',
                  fontSize: '0.875rem',
                  fontWeight: 500,
                  cursor: busy ? 'not-allowed' : 'pointer',
                  opacity: busy ? 0.5 : 1,
                  fontFamily: 'var(--font-sans)',
                }}
              >
                Reject
              </button>
              <button
                type="button"
                onClick={() => onModeChange('revision')}
                disabled={busy}
                style={{
                  background: mode === 'revision' ? 'var(--accent-primary)' : 'var(--bg-surface)',
                  color: mode === 'revision' ? '#fff' : 'var(--accent-primary)',
                  border: `1px solid var(--accent-primary)`,
                  borderRadius: '4px',
                  padding: '0.375rem 0.875rem',
                  fontSize: '0.875rem',
                  fontWeight: 500,
                  cursor: busy ? 'not-allowed' : 'pointer',
                  opacity: busy ? 0.5 : 1,
                  fontFamily: 'var(--font-sans)',
                }}
              >
                Request revision
              </button>
            </div>

            {mode && (
              <div style={{ marginTop: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                  {mode === 'approve'
                    ? 'Optional approval notes'
                    : mode === 'reject'
                      ? 'Rejection reason (required)'
                      : 'Revision notes (required)'}
                </label>
                <textarea
                  value={notes}
                  onChange={(e) => onNotesChange(e.target.value)}
                  disabled={busy}
                  rows={3}
                  className="fl-input"
                  style={{ resize: 'vertical' }}
                  placeholder={
                    mode === 'approve'
                      ? 'e.g. Supply route confirmed – good to go.'
                      : mode === 'reject'
                        ? 'e.g. Shelter #12 already serviced by another run.'
                        : 'e.g. Vehicle capacity underestimated; re-match with bigger truck.'
                  }
                />
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button type="button" onClick={onSubmit} disabled={busy} className="fl-btn-primary">
                    {busy ? 'Submitting…' : 'Submit decision'}
                  </button>
                  <button type="button" onClick={onCancel} disabled={busy} className="fl-btn-secondary">
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </li>
  )
}

function ValidationChecks({ report }: { report: ValidationReport | undefined }) {
  if (!report) {
    return <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Loading safety checks…</p>
  }

  if (!report.hasRunValidation) {
    return (
      <div className="fl-notification fl-notification-warning">
        No safety checks recorded for this run yet.
      </div>
    )
  }

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
        <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)' }}>Safety checks</span>
        <span className={`fl-badge ${report.overallPassed ? 'fl-badge-success' : 'fl-badge-error'}`}>
          {report.overallPassed ? 'Overall PASS' : 'Overall FAIL'}
        </span>
      </div>
      <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
        {report.checks.map((check) => (
          <li
            key={check.checkName}
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              justifyContent: 'space-between',
              gap: '0.5rem',
              fontSize: '0.875rem',
              backgroundColor: check.passed ? 'var(--bg-base)' : '#FFF1F1',
              border: `1px solid ${check.passed ? 'var(--border-default)' : '#FFBDBD'}`,
              borderRadius: '4px',
              padding: '0.5rem 0.75rem',
            }}
          >
            <span style={{ color: check.passed ? 'var(--text-primary)' : 'var(--state-error)' }}>
              {check.passed ? '✓' : '✗'} {check.checkName}
            </span>
            {!check.passed && check.violationDetail && (
              <span style={{ fontSize: '0.75rem', color: 'var(--state-error)', textAlign: 'right', maxWidth: '18rem' }}>
                {check.violationDetail}
              </span>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

export default ApprovalQueue
