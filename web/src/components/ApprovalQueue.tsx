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
 * Approval Queue — Member D (Ijini) coordinator screen.
 *
 * Shows every workflow run that has passed the Validation/Safety Agent and is
 * waiting for a human decision. The coordinator can inspect the safety checks,
 * then Approve, Reject (reason required) or send back for Revision (notes required).
 *
 * Auth note: the backend endpoints carry [Authorize(Roles="Coordinator")]. Until
 * JWT auth is wired (Week 2+), they return 401 — this component surfaces that
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

    setState((s) => ({ ...s, expandedId: item.workflowRunId, mode: null, notes: '' }))

    if (!state.reports[item.workflowRunId]) {
      try {
        const report = await getValidationReport(apiBaseUrl, item.workflowRunId)
        if (!mountedRef.current) return
        setState((s) => ({
          ...s,
          reports: { ...s.reports, [item.workflowRunId]: report },
        }))
      } catch (err) {
        if (!mountedRef.current) return
        setState((s) => ({ ...s, error: describeApiError(err) }))
      }
    }
  }

  const submitAction = async (item: WorkflowRunQueueItem): Promise<void> => {
    const { mode, notes } = state
    if (mode === null) return

    if (mode !== 'approve' && notes.trim().length === 0) {
      setState((s) => ({ ...s, error: mode === 'reject' ? 'A rejection reason is required.' : 'Revision notes are required.' }))
      return
    }

    setState((s) => ({ ...s, busyId: item.workflowRunId, error: null, notice: null }))

    try {
      switch (mode) {
        case 'approve':
          await approveRun(apiBaseUrl, item.workflowRunId, notes.trim() || undefined)
          break
        case 'reject':
          await rejectRun(apiBaseUrl, item.workflowRunId, notes.trim())
          break
        case 'revision':
          await requestRevision(apiBaseUrl, item.workflowRunId, notes.trim())
          break
      }

      if (!mountedRef.current) return
      const label = mode === 'approve' ? 'approved' : mode === 'reject' ? 'rejected' : 'sent for revision'
      setState((s) => ({
        ...s,
        busyId: null,
        mode: null,
        notes: '',
        expandedId: null,
        notice: `Plan for "${item.objective}" ${label}.`,
      }))
      await refresh()
    } catch (err) {
      if (!mountedRef.current) return
      setState((s) => ({ ...s, busyId: null, error: describeApiError(err) }))
    }
  }

  return (
    <div className="max-w-3xl mx-auto px-4 py-8">
      {state.notice && <Banner tone="ok" text={state.notice} />}
      {state.error && <Banner tone="error" text={state.error} />}

      {loading ? (
        <p className="text-gray-500 text-sm">Loading approval queue…</p>
      ) : state.items.length === 0 ? (
        <div className="bg-white rounded-xl shadow p-8 text-center text-sm text-gray-500">
          No plans are awaiting approval.
          <p className="text-xs text-gray-400 mt-2">
            Push a workflow run to PendingApproval (via the API) and it will appear here.
          </p>
        </div>
      ) : (
        <ul className="space-y-4">
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

function Banner({ tone, text }: { tone: 'ok' | 'error'; text: string }) {
  const styles =
    tone === 'ok'
      ? 'bg-emerald-50 border-emerald-300 text-emerald-800'
      : 'bg-red-50 border-red-300 text-red-800'

  return (
    <div className={`mb-4 border rounded-lg px-3 py-2 text-sm ${styles}`}>
      {text}
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
    <li className="bg-white rounded-xl shadow border border-gray-100">
      <div className="flex items-start justify-between px-4 py-3">
        <div className="min-w-0">
          <p className="font-semibold text-gray-900 truncate">{item.objective}</p>
          <p className="text-xs text-gray-400 mt-1">
            Created {new Date(item.createdAt).toLocaleString()}
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <span className="text-xs font-mono bg-amber-100 text-amber-800 px-2 py-1 rounded-full">
            {item.state}
          </span>
          <button
            type="button"
            onClick={onToggle}
            disabled={busy}
            className="text-sm text-blue-700 hover:underline disabled:opacity-50"
          >
            {expanded ? 'Collapse' : 'Inspect & decide'}
          </button>
        </div>
      </div>

      {expanded && (
        <div className="border-t border-gray-100 px-4 py-4 space-y-4">
          <ValidationChecks report={report} />

          <div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => onModeChange('approve')}
                disabled={busy}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors disabled:opacity-50 ${
                  mode === 'approve'
                    ? 'bg-emerald-600 text-white border-emerald-600'
                    : 'bg-white text-emerald-700 border-emerald-300 hover:bg-emerald-50'
                }`}
              >
                Approve
              </button>
              <button
                type="button"
                onClick={() => onModeChange('reject')}
                disabled={busy}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors disabled:opacity-50 ${
                  mode === 'reject'
                    ? 'bg-red-600 text-white border-red-600'
                    : 'bg-white text-red-700 border-red-300 hover:bg-red-50'
                }`}
              >
                Reject
              </button>
              <button
                type="button"
                onClick={() => onModeChange('revision')}
                disabled={busy}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors disabled:opacity-50 ${
                  mode === 'revision'
                    ? 'bg-sky-600 text-white border-sky-600'
                    : 'bg-white text-sky-700 border-sky-300 hover:bg-sky-50'
                }`}
              >
                Request revision
              </button>
            </div>

            {mode && (
              <div className="mt-3 space-y-2">
                <label className="block text-xs text-gray-500">
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
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
                  placeholder={
                    mode === 'approve'
                      ? 'e.g. Supply route confirmed — good to go.'
                      : mode === 'reject'
                        ? 'e.g. Shelter #12 already serviced by another run.'
                        : 'e.g. Vehicle capacity underestimated; re-match with bigger truck.'
                  }
                />
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={onSubmit}
                    disabled={busy}
                    className="px-4 py-1.5 rounded-lg text-sm font-medium bg-gray-900 text-white hover:bg-gray-700 disabled:opacity-50"
                  >
                    {busy ? 'Submitting…' : 'Submit decision'}
                  </button>
                  <button
                    type="button"
                    onClick={onCancel}
                    disabled={busy}
                    className="px-4 py-1.5 rounded-lg text-sm font-medium bg-white text-gray-600 border border-gray-300 hover:bg-gray-50 disabled:opacity-50"
                  >
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
    return <p className="text-xs text-gray-400">Loading safety checks…</p>
  }

  if (!report.hasRunValidation) {
    return (
      <p className="text-xs text-amber-700">
        No safety checks recorded for this run yet.
      </p>
    )
  }

  return (
    <div>
      <div className="flex items-center gap-2 mb-2">
        <span className="text-sm font-medium text-gray-700">Safety checks</span>
        <span
          className={`text-xs font-medium px-2 py-0.5 rounded-full ${
            report.overallPassed
              ? 'bg-emerald-100 text-emerald-700'
              : 'bg-red-100 text-red-700'
          }`}
        >
          {report.overallPassed ? 'Overall PASS' : 'Overall FAIL'}
        </span>
      </div>
      <ul className="space-y-1.5">
        {report.checks.map((check) => (
          <li
            key={check.checkName}
            className="flex items-start justify-between gap-2 text-sm bg-gray-50 rounded-lg px-3 py-2"
          >
            <span className={check.passed ? 'text-gray-800' : 'text-red-800'}>
              {check.passed ? '✓' : '✗'} {check.checkName}
            </span>
            {!check.passed && check.violationDetail && (
              <span className="text-xs text-red-600 text-right max-w-[18rem]">
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