import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAppContext } from '../context/AppContext'
import {
  WORKFLOW_STATES,
  listWorkflows,
  retryWorkflow,
  stateBadgeClass,
  stateLabel,
  type Page,
  type WorkflowRunSummary,
} from '../api/workflows'
import { describeApiError } from '../lib/apiError'

const REFRESH_MS = 5000
type View = 'all' | 'failed'

/**
 * Workflow monitor (4.01) and failed-runs view (4.03): every run with its stage and progress,
 * filtered by state or objective, paged, and refreshed every 5 s so stage changes show up live.
 * Failed runs show why they stopped and a Retry button (within the run's retry limit).
 */
export function WorkflowMonitor() {
  const { apiBaseUrl } = useAppContext()
  const [view, setView] = useState<View>('all')
  const [stateFilter, setStateFilter] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [autoRefresh, setAutoRefresh] = useState(true)

  const [data, setData] = useState<Page<WorkflowRunSummary> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [retrying, setRetrying] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const inFlight = useRef<AbortController | null>(null)

  const effectiveState = view === 'failed' ? 'Failed' : stateFilter

  const load = useCallback(
    async (quiet = false) => {
      inFlight.current?.abort()
      const controller = new AbortController()
      inFlight.current = controller
      if (!quiet) setLoading(true)
      try {
        const result = await listWorkflows(apiBaseUrl, { state: effectiveState, search, page }, controller.signal)
        setData(result)
        setError(null)
        setLastUpdated(new Date())
      } catch (err) {
        if (controller.signal.aborted) return
        setError(describeApiError(err))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    },
    [apiBaseUrl, effectiveState, search, page],
  )

  useEffect(() => {
    void load()
    return () => inFlight.current?.abort()
  }, [load])

  // Live updates: poll quietly while auto-refresh is on and the tab is visible.
  useEffect(() => {
    if (!autoRefresh) return
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') void load(true)
    }, REFRESH_MS)
    return () => clearInterval(timer)
  }, [autoRefresh, load])

  // Debounce the search box.
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim())
      setPage(1)
    }, 300)
    return () => clearTimeout(timer)
  }, [searchInput])

  const retry = async (run: WorkflowRunSummary) => {
    setRetrying(run.id)
    setNotice(null)
    try {
      const result = await retryWorkflow(apiBaseUrl, run.id)
      setNotice({ kind: 'ok', text: `Retrying "${run.objective}" from ${result.state} (attempt ${result.retryCount} of ${run.retryLimit}).` })
      void load(true)
    } catch (err) {
      setNotice({ kind: 'error', text: describeApiError(err) })
    } finally {
      setRetrying(null)
    }
  }

  const switchView = (next: View) => {
    setView(next)
    setPage(1)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Workflow runs</h1>
          <p className="text-sm text-slate-500">
            Start a run from the Reports page. Click a run to see its full execution trace.
          </p>
        </div>
        <div className="flex items-center gap-3 text-sm">
          <label className="flex items-center gap-2 text-slate-700">
            <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} />
            Auto-refresh
          </label>
          <span className="text-xs text-slate-500" aria-live="polite">
            {lastUpdated ? `Updated ${lastUpdated.toLocaleTimeString()}` : ''}
          </span>
          <button
            onClick={() => void load()}
            disabled={loading}
            className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-slate-700 hover:bg-slate-50 disabled:opacity-60"
          >
            Refresh
          </button>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div role="tablist" aria-label="Run views" className="inline-flex rounded-lg border border-slate-300 bg-white p-0.5">
          {(['all', 'failed'] as View[]).map((v) => (
            <button
              key={v}
              role="tab"
              aria-selected={view === v}
              onClick={() => switchView(v)}
              className={`px-3 py-1.5 text-sm rounded-md ${view === v ? 'bg-slate-900 text-white' : 'text-slate-700 hover:bg-slate-100'}`}
            >
              {v === 'all' ? 'All runs' : 'Failed runs'}
            </button>
          ))}
        </div>
        {view === 'all' && (
          <label className="text-sm text-slate-700">
            <span className="sr-only">Filter by stage</span>
            <select
              value={stateFilter}
              onChange={(e) => {
                setStateFilter(e.target.value)
                setPage(1)
              }}
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5"
            >
              <option value="">All stages</option>
              {WORKFLOW_STATES.map((s) => (
                <option key={s} value={s}>
                  {stateLabel(s)}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="text-sm">
          <span className="sr-only">Search objectives</span>
          <input
            type="search"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search objectives…"
            className="w-56 rounded-md border border-slate-300 bg-white px-3 py-1.5"
          />
        </label>
      </div>

      {notice && (
        <p
          role={notice.kind === 'error' ? 'alert' : 'status'}
          className={`rounded-md border px-3 py-2 text-sm ${
            notice.kind === 'error' ? 'border-rose-200 bg-rose-50 text-rose-800' : 'border-emerald-200 bg-emerald-50 text-emerald-800'
          }`}
        >
          {notice.text}
        </p>
      )}

      <section className="overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm">
        {loading && !data ? (
          <p className="p-5 text-sm text-slate-500">Loading workflow runs…</p>
        ) : error && !data ? (
          <div role="alert" className="p-5 text-sm text-rose-700">
            {error}{' '}
            <button className="underline" onClick={() => void load()}>
              Try again
            </button>
          </div>
        ) : !data || data.items.length === 0 ? (
          <p className="p-5 text-sm text-slate-500">
            {view === 'failed' ? 'No failed runs.' : search || stateFilter ? 'No runs match these filters.' : 'No workflow runs yet.'}
          </p>
        ) : (
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-2 font-medium">Objective</th>
                <th className="px-4 py-2 font-medium">Stage</th>
                <th className="px-4 py-2 font-medium w-40">Progress</th>
                <th className="px-4 py-2 font-medium">Reports</th>
                <th className="px-4 py-2 font-medium">Retries</th>
                <th className="px-4 py-2 font-medium">Updated</th>
                {view === 'failed' && <th className="px-4 py-2 font-medium">Action</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.items.map((run) => {
                const canRetry = run.currentState === 'Failed' && run.retryCount < run.retryLimit
                return (
                  <tr key={run.id} className="align-top">
                    <td className="px-4 py-2">
                      <Link to={`/workflows/${run.id}`} className="font-medium text-blue-700 hover:underline">
                        {run.objective}
                      </Link>
                      <span className="block font-mono text-xs text-slate-400">{run.id.slice(0, 8)}</span>
                    </td>
                    <td className="px-4 py-2">
                      <span className={`inline-block rounded-full border px-2 py-0.5 text-xs font-semibold ${stateBadgeClass(run.currentState)}`}>
                        {stateLabel(run.currentState)}
                      </span>
                      {run.failureReason && (
                        <span className="mt-1 block max-w-xs text-xs text-rose-700">
                          {run.failedAtState && <strong>{run.failedAtState}: </strong>}
                          {run.failureReason}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2">
                      <div
                        role="progressbar"
                        aria-valuenow={run.progressPercent}
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-label={`Progress of ${run.objective}`}
                        className="h-2 w-full rounded-full bg-slate-200"
                      >
                        <div
                          className={`h-2 rounded-full ${run.currentState === 'Failed' || run.currentState === 'Rejected' ? 'bg-rose-500' : 'bg-blue-600'}`}
                          style={{ width: `${run.progressPercent}%` }}
                        />
                      </div>
                      <span className="text-xs text-slate-500">{run.progressPercent}%</span>
                    </td>
                    <td className="px-4 py-2 text-slate-700">{run.reportCount}</td>
                    <td className="px-4 py-2 text-slate-700">
                      {run.retryCount} / {run.retryLimit}
                    </td>
                    <td className="px-4 py-2 text-slate-500">{new Date(run.updatedAt).toLocaleString()}</td>
                    {view === 'failed' && (
                      <td className="px-4 py-2">
                        <button
                          onClick={() => void retry(run)}
                          disabled={!canRetry || retrying === run.id}
                          title={canRetry ? `Re-run the ${run.failedAtState} stage` : 'Retry limit reached'}
                          className="rounded-md bg-blue-600 px-3 py-1 text-xs font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          {retrying === run.id ? 'Retrying…' : canRetry ? 'Retry' : 'Limit reached'}
                        </button>
                      </td>
                    )}
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </section>

      {error && data && (
        <p role="alert" className="text-xs text-rose-700">
          Live refresh failed: {error}
        </p>
      )}

      {data && data.totalPages > 1 && (
        <nav aria-label="Pagination" className="flex items-center gap-3 text-sm">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="underline disabled:opacity-40">
            Previous
          </button>
          <span>
            Page {data.page} of {data.totalPages} ({data.totalItems} runs)
          </span>
          <button disabled={page >= data.totalPages} onClick={() => setPage(page + 1)} className="underline disabled:opacity-40">
            Next
          </button>
        </nav>
      )}
    </div>
  )
}
