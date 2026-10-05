import { useEffect, useRef, useState } from 'react'
import { useAppContext } from '../context/AppContext'
import { getAuditTrail, listDispatches } from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { AuditEvent, DispatchListResponse } from '../api/types'

/**
 * Dispatch History – Member D (Ijini).
 * Paginated, filterable table of all coordinator dispatch decisions,
 * with expandable per-dispatch audit timeline.
 */

const DECISIONS = ['Approved', 'Rejected', 'RevisionRequested']

interface Filters { status: string; dateFrom: string; dateTo: string }

function DispatchHistory() {
  const { apiBaseUrl } = useAppContext()

  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<Filters>({ status: '', dateFrom: '', dateTo: '' })
  const [draft, setDraft] = useState<Filters>({ status: '', dateFrom: '', dateTo: '' })
  const [data, setData] = useState<DispatchListResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [audits, setAudits] = useState<Record<string, AuditEvent[]>>({})
  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true
    setLoading(true)
    listDispatches(apiBaseUrl, { ...filters, page })
      .then((res: DispatchListResponse) => {
        if (!mountedRef.current) return
        setData(res)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!mountedRef.current) return
        setError(describeApiError(err))
      })
      .finally(() => {
        if (mountedRef.current) setLoading(false)
      })
    return () => { mountedRef.current = false }
  }, [apiBaseUrl, filters, page])

  const applyFilters = () => { setFilters(draft); setPage(1) }
  const resetFilters = () => {
    const empty: Filters = { status: '', dateFrom: '', dateTo: '' }
    setDraft(empty); setFilters(empty); setPage(1)
  }

  const toggleAudit = async (dispatchId: string): Promise<void> => {
    if (expandedId === dispatchId) { setExpandedId(null); return }
    setExpandedId(dispatchId)
    if (!audits[dispatchId]) {
      try {
        const result = await getAuditTrail(apiBaseUrl, dispatchId)
        if (!mountedRef.current) return
        setAudits((prev) => ({ ...prev, [dispatchId]: result.events }))
      } catch { /* non-fatal */ }
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <div>
      <h1 className="fl-section-title">Dispatch History</h1>
      <p className="fl-section-desc">All coordinator decisions with full audit trail.</p>

      {error && (
        <div className="fl-notification fl-notification-error" style={{ marginBottom: '1rem' }}>
          {error}
        </div>
      )}

      {/* Filter bar */}
      <div className="fl-card" style={{ padding: '0.875rem 1rem', marginBottom: '1rem', display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'flex-end' }}>
        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
          Decision
          <select
            value={draft.status}
            onChange={(e) => setDraft({ ...draft, status: e.target.value })}
            className="fl-input"
            style={{ width: '10rem' }}
          >
            <option value="">All</option>
            {DECISIONS.map((d) => (
              <option key={d} value={d}>{d}</option>
            ))}
          </select>
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
          From
          <input type="date" value={draft.dateFrom} onChange={(e) => setDraft({ ...draft, dateFrom: e.target.value })} className="fl-input" style={{ width: '10rem' }} />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
          To
          <input type="date" value={draft.dateTo} onChange={(e) => setDraft({ ...draft, dateTo: e.target.value })} className="fl-input" style={{ width: '10rem' }} />
        </label>

        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button type="button" onClick={applyFilters} className="fl-btn-primary">Apply</button>
          <button type="button" onClick={resetFilters} className="fl-btn-secondary">Reset</button>
        </div>
      </div>

      {loading ? (
        <p style={{ fontSize: '0.875rem', color: 'var(--text-muted)' }}>Loading history…</p>
      ) : !data || data.items.length === 0 ? (
        <div className="fl-card" style={{ padding: '2rem', textAlign: 'center', fontSize: '0.875rem', color: 'var(--text-muted)' }}>
          No dispatch decisions match the current filters.
        </div>
      ) : (
        <>
          <div className="fl-card" style={{ overflow: 'hidden' }}>
            <div style={{ overflowX: 'auto' }}>
              <table className="fl-table">
                <thead>
                  <tr>
                    <th>Decision</th>
                    <th>Workflow run</th>
                    <th>Notes / reason</th>
                    <th>Dispatched at</th>
                    <th>Created at</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((item) => (
                    <AuditRow
                      key={item.id}
                      dispatchId={item.id}
                      decision={item.decision}
                      workflowRunId={item.workflowRunId}
                      notes={item.approvalNotes}
                      dispatchedAt={item.dispatchedAt}
                      createdAt={item.createdAt}
                      expanded={expandedId === item.id}
                      events={audits[item.id]}
                      onToggle={() => void toggleAudit(item.id)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Pagination */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '1rem', fontSize: '0.875rem', color: 'var(--text-muted)' }}>
            <span>Page {data.page} of {totalPages} · {data.total} total</span>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} className="fl-btn-secondary">
                Previous
              </button>
              <button type="button" disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="fl-btn-secondary">
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

function AuditRow({
  dispatchId, decision, workflowRunId, notes, dispatchedAt, createdAt, expanded, events, onToggle,
}: {
  dispatchId: string; decision: string; workflowRunId: string; notes: string | null
  dispatchedAt: string | null; createdAt: string; expanded: boolean
  events: AuditEvent[] | undefined; onToggle: () => void
}) {
  return (
    <>
      <tr>
        <td><DecisionBadge decision={decision} /></td>
        {/* Monospace for IDs per spec: reserved for IDs and coordinates */}
        <td className="fl-mono" style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{workflowRunId}</td>
        <td style={{ maxWidth: '16rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{notes ?? '–'}</td>
        <td style={{ color: 'var(--text-muted)' }}>{dispatchedAt ? new Date(dispatchedAt).toLocaleString() : '–'}</td>
        <td style={{ color: 'var(--text-muted)' }}>{new Date(createdAt).toLocaleString()}</td>
        <td style={{ textAlign: 'right' }}>
          <button type="button" onClick={onToggle} className="fl-btn-ghost">
            {expanded ? 'Hide audit' : 'Audit'}
          </button>
        </td>
      </tr>
      {expanded && (
        <tr>
          <td colSpan={6} style={{ backgroundColor: 'var(--bg-base)', padding: '1rem' }}>
            <AuditTimeline events={events} dispatchId={dispatchId} />
          </td>
        </tr>
      )}
    </>
  )
}

function AuditTimeline({ events, dispatchId }: { events: AuditEvent[] | undefined; dispatchId: string }) {
  if (!events) {
    return <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Loading audit trail…</p>
  }
  if (events.length === 0) {
    return <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>No audit events recorded for dispatch {dispatchId}.</p>
  }
  return (
    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
      {events.map((event) => (
        <li key={event.id} style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem', fontSize: '0.875rem' }}>
          <span className="fl-timeline-dot" />
          <div style={{ minWidth: 0 }}>
            <p style={{ fontWeight: 500, color: 'var(--text-primary)', margin: '0 0 0.125rem' }}>{event.eventType}</p>
            {event.eventDetailJson && (
              /* Monospace for raw JSON values per spec */
              <pre className="fl-mono" style={{ fontSize: '0.75rem', color: 'var(--text-muted)', backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-default)', borderRadius: '4px', padding: '0.375rem 0.5rem', marginTop: '0.25rem', overflowX: 'auto', whiteSpace: 'pre-wrap' }}>
                {formatDetail(event.eventDetailJson)}
              </pre>
            )}
            <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.25rem' }}>
              {new Date(event.createdAt).toLocaleString()}
              {event.actorId ? ` · actor ${event.actorId}` : ''}
            </p>
          </div>
        </li>
      ))}
    </ul>
  )
}

/** Status badge – exact WorkflowState → color mapping from ui-context.md */
function DecisionBadge({ decision }: { decision: string }) {
  const cls: Record<string, string> = {
    Approved: 'fl-badge-success',
    Rejected: 'fl-badge-error',
    RevisionRequested: 'fl-badge-warning',
  }
  return (
    <span className={`fl-badge ${cls[decision] ?? 'fl-badge-neutral'}`}>
      {decision}
    </span>
  )
}

function formatDetail(json: string): string {
  try { return JSON.stringify(JSON.parse(json), null, 2) }
  catch { return json }
}

export default DispatchHistory
