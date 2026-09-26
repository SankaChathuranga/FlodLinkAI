import { useCallback, useEffect, useState } from 'react'
import { useAppContext } from '../context/AppContext'
import { getAuditTrail, listDispatches } from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { AuditEvent, DispatchListResponse } from '../api/types'

const DECISIONS = ['Approved', 'Rejected', 'RevisionRequested'] as const

/**
 * Dispatch History — Member D (Ijini). Filterable, paginated table of every
 * coordinator decision, with an append-only audit timeline per dispatch.
 */
function DispatchHistory() {
  const { apiBaseUrl } = useAppContext()

  const [draft, setDraft] = useState({ status: '', dateFrom: '', dateTo: '' })
  const [filters, setFilters] = useState({ status: '', dateFrom: '', dateTo: '' })
  const [page, setPage] = useState(1)
  const [data, setData] = useState<DispatchListResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [audits, setAudits] = useState<Record<string, AuditEvent[]>>({})
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await listDispatches(apiBaseUrl, {
        status: filters.status || undefined,
        dateFrom: filters.dateFrom || undefined,
        dateTo: filters.dateTo || undefined,
        page,
        pageSize: 10,
      })
      setData(res)
      setLoading(false)
    } catch (err) {
      setError(describeApiError(err))
      setLoading(false)
    }
  }, [apiBaseUrl, filters.status, filters.dateFrom, filters.dateTo, page])

  useEffect(() => {
    void load()
  }, [load])

  const applyFilters = () => {
    setFilters(draft)
    setPage(1)
  }

  const resetFilters = () => {
    setDraft({ status: '', dateFrom: '', dateTo: '' })
    setFilters({ status: '', dateFrom: '', dateTo: '' })
    setPage(1)
  }

  const toggleAudit = async (dispatchId: string) => {
    if (expandedId === dispatchId) {
      setExpandedId(null)
      return
    }
    setExpandedId(dispatchId)
    if (!audits[dispatchId]) {
      try {
        const res = await getAuditTrail(apiBaseUrl, dispatchId)
        setAudits((s) => ({ ...s, [dispatchId]: res.events }))
      } catch (err) {
        setError(describeApiError(err))
      }
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / 10)) : 1

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-1">Dispatch History</h1>
      <p className="text-sm text-gray-500 mb-6">
        Every coordinator decision with its append-only audit timeline.
      </p>

      {error && <Banner text={error} />}

      <div className="bg-white border border-gray-200 rounded-xl p-4 mb-6 flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs text-gray-500">
          Decision
          <select
            value={draft.status}
            onChange={(e) => setDraft({ ...draft, status: e.target.value })}
            className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm bg-white"
          >
            <option value="">All</option>
            {DECISIONS.map((d) => (
              <option key={d} value={d}>
                {d}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-1 text-xs text-gray-500">
          From
          <input
            type="date"
            value={draft.dateFrom}
            onChange={(e) => setDraft({ ...draft, dateFrom: e.target.value })}
            className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm"
          />
        </label>

        <label className="flex flex-col gap-1 text-xs text-gray-500">
          To
          <input
            type="date"
            value={draft.dateTo}
            onChange={(e) => setDraft({ ...draft, dateTo: e.target.value })}
            className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm"
          />
        </label>

        <div className="flex gap-2">
          <button
            type="button"
            onClick={applyFilters}
            className="px-3 py-1.5 rounded-lg text-sm font-medium bg-gray-900 text-white hover:bg-gray-700"
          >
            Apply
          </button>
          <button
            type="button"
            onClick={resetFilters}
            className="px-3 py-1.5 rounded-lg text-sm font-medium bg-white text-gray-600 border border-gray-300 hover:bg-gray-50"
          >
            Reset
          </button>
        </div>
      </div>

      {loading ? (
        <p className="text-gray-500 text-sm">Loading history…</p>
      ) : !data || data.items.length === 0 ? (
        <div className="bg-white rounded-xl shadow p-8 text-center text-sm text-gray-500">
          No dispatch decisions match the current filters.
        </div>
      ) : (
        <>
          <div className="bg-white rounded-xl shadow border border-gray-100 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
                  <th className="px-4 py-3">Decision</th>
                  <th className="px-4 py-3">Workflow run</th>
                  <th className="px-4 py-3">Notes / reason</th>
                  <th className="px-4 py-3">Dispatched at</th>
                  <th className="px-4 py-3">Created at</th>
                  <th className="px-4 py-3" />
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

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600">
            <span>
              Page {data.page} of {totalPages} · {data.total} total
            </span>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="px-3 py-1.5 rounded-lg border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40"
              >
                Previous
              </button>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
                className="px-3 py-1.5 rounded-lg border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40"
              >
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
  dispatchId,
  decision,
  workflowRunId,
  notes,
  dispatchedAt,
  createdAt,
  expanded,
  events,
  onToggle,
}: {
  dispatchId: string
  decision: string
  workflowRunId: string
  notes: string | null
  dispatchedAt: string | null
  createdAt: string
  expanded: boolean
  events: AuditEvent[] | undefined
  onToggle: () => void
}) {
  return (
    <>
      <tr className="border-b border-gray-50 hover:bg-gray-50">
        <td className="px-4 py-3">
          <DecisionBadge decision={decision} />
        </td>
        <td className="px-4 py-3 font-mono text-xs text-gray-600">{workflowRunId}</td>
        <td className="px-4 py-3 max-w-[16rem] truncate text-gray-700">{notes ?? '—'}</td>
        <td className="px-4 py-3 text-gray-600">
          {dispatchedAt ? new Date(dispatchedAt).toLocaleString() : '—'}
        </td>
        <td className="px-4 py-3 text-gray-600">{new Date(createdAt).toLocaleString()}</td>
        <td className="px-4 py-3 text-right">
          <button
            type="button"
            onClick={onToggle}
            className="text-sm text-blue-700 hover:underline"
          >
            {expanded ? 'Hide audit' : 'Audit'}
          </button>
        </td>
      </tr>
      {expanded && (
        <tr className="bg-gray-50">
          <td colSpan={6} className="px-4 py-4">
            <AuditTimeline events={events} dispatchId={dispatchId} />
          </td>
        </tr>
      )}
    </>
  )
}

function AuditTimeline({ events, dispatchId }: { events: AuditEvent[] | undefined; dispatchId: string }) {
  if (!events) {
    return <p className="text-xs text-gray-400">Loading audit trail…</p>
  }

  if (events.length === 0) {
    return (
      <p className="text-xs text-gray-400">
        No audit events recorded for dispatch {dispatchId}.
      </p>
    )
  }

  return (
    <ul className="space-y-2">
      {events.map((event) => (
        <li key={event.id} className="flex items-start gap-3 text-sm">
          <span className="mt-1.5 h-2 w-2 rounded-full bg-blue-600 shrink-0" />
          <div className="min-w-0">
            <p className="font-medium text-gray-800">{event.eventType}</p>
            {event.eventDetailJson && (
              <pre className="text-xs text-gray-500 bg-white border border-gray-200 rounded px-2 py-1 mt-1 overflow-x-auto whitespace-pre-wrap">
                {formatDetail(event.eventDetailJson)}
              </pre>
            )}
            <p className="text-xs text-gray-400 mt-1">
              {new Date(event.createdAt).toLocaleString()}
              {event.actorId ? ` · actor ${event.actorId}` : ''}
            </p>
          </div>
        </li>
      ))}
    </ul>
  )
}

function DecisionBadge({ decision }: { decision: string }) {
  const styles: Record<string, string> = {
    Approved: 'bg-emerald-100 text-emerald-700',
    Rejected: 'bg-red-100 text-red-700',
    RevisionRequested: 'bg-amber-100 text-amber-800',
  }
  return (
    <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${styles[decision] ?? 'bg-gray-100 text-gray-600'}`}>
      {decision}
    </span>
  )
}

function Banner({ text }: { text: string }) {
  return (
    <div className="mb-4 border border-red-300 bg-red-50 text-red-800 rounded-lg px-3 py-2 text-sm">
      {text}
    </div>
  )
}

function formatDetail(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

export default DispatchHistory