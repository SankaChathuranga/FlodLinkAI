import { useState, useEffect, useCallback, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAppContext } from '../context/AppContext'
import { apiFetch } from '../api/http'
import { startWorkflow } from '../api/workflows'
import { describeApiError } from '../lib/apiError'
import type { Report, Shelter } from '../types'

export function ReportsQueue() {
  const { apiBaseUrl } = useAppContext()

  const [reports, setReports] = useState<Report[]>([])
  const [shelters, setShelters] = useState<Shelter[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Filters & Sorting
  const [selectedShelterId, setSelectedShelterId] = useState<string>('')
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [urgencyThreshold, setUrgencyThreshold] = useState<number>(0)
  const [sortOrder, setSortOrder] = useState<string>('urgency_desc')

  // Start workflow (4.04): pick reports, describe the objective, start a run.
  const navigate = useNavigate()
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [objective, setObjective] = useState('')
  const [starting, setStarting] = useState(false)
  const [startError, setStartError] = useState<string | null>(null)

  // Only reports that are not yet planned can join a new run.
  const canSelect = (report: Report) => report.status === 'New' || report.status === 'Triaged'

  const toggleSelected = (id: number) =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const onStart = async (e: FormEvent) => {
    e.preventDefault()
    setStartError(null)
    if (selected.size === 0) {
      setStartError('Select at least one report.')
      return
    }
    if (!objective.trim()) {
      setStartError('Describe what this workflow should achieve.')
      return
    }
    setStarting(true)
    try {
      const run = await startWorkflow(apiBaseUrl, objective.trim(), [...selected])
      navigate(`/workflows/${run.id}`)
    } catch (err) {
      setStartError(describeApiError(err))
      setStarting(false)
    }
  }

  // Fetch shelters dropdown items
  useEffect(() => {
    apiFetch(`${apiBaseUrl}/api/shelters?pageSize=100`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data?.items) setShelters(data.items)
      })
      .catch(() => {})
  }, [apiBaseUrl])

  const fetchReports = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const queryParams = new URLSearchParams()
      if (selectedShelterId) queryParams.append('shelterId', selectedShelterId)
      if (statusFilter) queryParams.append('status', statusFilter)
      if (urgencyThreshold > 0) queryParams.append('urgency', urgencyThreshold.toString())
      if (sortOrder) queryParams.append('sort', sortOrder)

      const response = await apiFetch(`${apiBaseUrl}/api/reports?${queryParams.toString()}`)
      if (!response.ok) {
        throw new Error(`Failed to fetch report queue (HTTP ${response.status})`)
      }

      const data: Report[] = await response.json()
      setReports(data || [])
      // Drop selections that are no longer listed or no longer eligible.
      setSelected((prev) => new Set([...prev].filter((id) => data?.some((r) => r.id === id && canSelect(r)))))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to connect to backend server.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl, selectedShelterId, statusFilter, urgencyThreshold, sortOrder])

  useEffect(() => {
    fetchReports()
  }, [fetchReports])

  const getUrgencyBadge = (score: number) => {
    if (score >= 80) return 'bg-rose-100 text-rose-800 border-rose-300 ring-rose-500'
    if (score >= 50) return 'bg-amber-100 text-amber-800 border-amber-300 ring-amber-500'
    return 'bg-emerald-100 text-emerald-800 border-emerald-300 ring-emerald-500'
  }

  const getNeedTypeBadge = (need: string) => {
    switch (need?.toLowerCase()) {
      case 'medical':
        return 'bg-purple-100 text-purple-800 border-purple-200'
      case 'water':
        return 'bg-blue-100 text-blue-800 border-blue-200'
      case 'food':
        return 'bg-amber-100 text-amber-800 border-amber-200'
      case 'shelter-repair':
        return 'bg-orange-100 text-orange-800 border-orange-200'
      default:
        return 'bg-slate-100 text-slate-700 border-slate-200'
    }
  }

  return (
    <div className="space-y-6">
      {/* Header Controls */}
      <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Report Review Queue</h2>
          <p className="text-sm text-slate-500">Review field reports, then select New or Triaged ones and start a workflow.</p>
        </div>

        {/* Filters Bar */}
        <div className="flex flex-wrap items-center gap-3">
          {/* Shelter Filter */}
          <select
            value={selectedShelterId}
            onChange={(e) => setSelectedShelterId(e.target.value)}
            className="px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All Shelters</option>
            {shelters.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>

          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 font-medium"
          >
            <option value="">All Statuses</option>
            <option value="New">New</option>
            <option value="Triaged">Triaged</option>
            <option value="InPlan">In Plan</option>
            <option value="Resolved">Resolved</option>
          </select>

          {/* Urgency Filter */}
          <select
            value={urgencyThreshold}
            onChange={(e) => setUrgencyThreshold(Number(e.target.value))}
            className="px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="0">All Urgencies</option>
            <option value="80">Critical (80+)</option>
            <option value="50">High (50+)</option>
            <option value="20">Moderate (20+)</option>
          </select>

          {/* Sort Order */}
          <select
            value={sortOrder}
            onChange={(e) => setSortOrder(e.target.value)}
            className="px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="urgency_desc">Highest Urgency First</option>
            <option value="urgency_asc">Lowest Urgency First</option>
            <option value="newest">Newest First</option>
            <option value="oldest">Oldest First</option>
          </select>

          <button
            onClick={fetchReports}
            className="px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-sm font-medium transition flex items-center space-x-1"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* START WORKFLOW */}
      <form
        onSubmit={(e) => void onStart(e)}
        aria-label="Start workflow"
        className="sticky top-2 z-10 flex flex-col gap-3 rounded-xl border border-blue-200 bg-blue-50 p-4 shadow-sm md:flex-row md:items-center"
      >
        <span className="text-sm font-semibold text-blue-900" aria-live="polite">
          {selected.size} report{selected.size === 1 ? '' : 's'} selected
        </span>
        <label className="flex-1 text-sm">
          <span className="sr-only">Objective</span>
          <input
            value={objective}
            onChange={(e) => setObjective(e.target.value)}
            maxLength={1000}
            placeholder="Objective, e.g. Water and medical supplies for Kaduwela shelters"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
          />
        </label>
        <div className="flex items-center gap-2">
          {selected.size > 0 && (
            <button type="button" onClick={() => setSelected(new Set())} className="text-xs text-slate-600 underline">
              Clear
            </button>
          )}
          <button
            type="submit"
            disabled={starting}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {starting ? 'Starting…' : 'Start workflow'}
          </button>
        </div>
        {startError && (
          <p role="alert" className="text-sm text-rose-700 md:basis-full">
            {startError}
          </p>
        )}
      </form>

      {/* ERROR STATE */}
      {error && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 p-4 rounded-xl flex items-center justify-between shadow-sm">
          <div className="flex items-center space-x-3">
            <svg className="w-6 h-6 text-rose-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <h4 className="font-semibold text-sm">Failed to Load Queue</h4>
              <p className="text-xs text-rose-600">{error}</p>
            </div>
          </div>
          <button
            onClick={fetchReports}
            className="px-3 py-1.5 bg-rose-600 text-white rounded-lg text-xs font-semibold hover:bg-rose-700 transition"
          >
            Retry
          </button>
        </div>
      )}

      {/* LOADING STATE */}
      {loading && (
        <div className="space-y-4">
          {[1, 2, 3].map((n) => (
            <div key={n} className="bg-white p-6 rounded-xl border border-slate-200 animate-pulse space-y-3">
              <div className="h-4 bg-slate-200 rounded w-1/4"></div>
              <div className="h-4 bg-slate-200 rounded w-1/2"></div>
              <div className="h-3 bg-slate-200 rounded w-full"></div>
            </div>
          ))}
        </div>
      )}

      {/* EMPTY STATE */}
      {!loading && !error && reports.length === 0 && (
        <div className="bg-white p-12 text-center rounded-xl border border-slate-200 shadow-sm space-y-3">
          <svg className="w-12 h-12 text-slate-300 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          <h3 className="text-lg font-bold text-slate-800">No Field Reports in Queue</h3>
          <p className="text-sm text-slate-500 max-w-sm mx-auto">No reports match your active filter criteria or shelter selection.</p>
          <button
            onClick={() => {
              setSelectedShelterId('')
              setStatusFilter('')
              setUrgencyThreshold(0)
              setSortOrder('urgency_desc')
            }}
            className="px-4 py-2 bg-blue-50 text-blue-600 font-semibold rounded-lg text-xs hover:bg-blue-100 transition"
          >
            Clear All Filters
          </button>
        </div>
      )}

      {/* REPORT QUEUE ITEMS LIST */}
      {!loading && !error && reports.length > 0 && (
        <div className="space-y-4">
          {reports.map((report) => (
            <div
              key={report.id}
              className="bg-white p-6 rounded-xl border border-slate-200 shadow-sm hover:shadow-md transition-shadow flex flex-col md:flex-row md:items-center justify-between gap-6"
            >
              {/* Selection */}
              <div className="md:self-start">
                <input
                  type="checkbox"
                  aria-label={`Select report ${report.id} for a workflow`}
                  checked={selected.has(report.id)}
                  disabled={!canSelect(report)}
                  title={canSelect(report) ? undefined : `Report is ${report.status} and cannot join a new workflow`}
                  onChange={() => toggleSelected(report.id)}
                  className="h-5 w-5 disabled:opacity-30"
                />
              </div>

              {/* Left Details */}
              <div className="space-y-2 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs font-mono font-bold text-slate-400">#R-{report.id}</span>
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-bold border ${getNeedTypeBadge(report.needType)}`}>
                    {report.needType}
                  </span>
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-bold border ${getUrgencyBadge(report.urgencyLevel)}`}>
                    Urgency Score: {report.urgencyLevel} / 100
                  </span>
                  <span className="px-2 py-0.5 bg-slate-100 text-slate-700 text-xs font-medium rounded">
                    Status: {report.status}
                  </span>
                </div>

                <h3 className="text-lg font-bold text-slate-900">
                  Quantity Required: <span className="text-blue-600">{report.quantityNeeded} units</span>
                </h3>

                {report.notes && (
                  <p className="text-sm text-slate-700 bg-slate-50 border border-slate-200 rounded px-3 py-2 whitespace-pre-line break-words">
                    <span className="font-semibold text-slate-800">Field notes:</span> {report.notes}
                  </p>
                )}

                <div className="text-xs text-slate-600 space-y-1">
                  <p>
                    <span className="font-semibold text-slate-800">Shelter:</span>{' '}
                    {report.shelter?.name ?? `Shelter #${report.shelterId}`}
                  </p>
                  <p>
                    <span className="font-semibold text-slate-800">Reported By:</span>{' '}
                    {report.reporter?.name ?? `User #${report.reportedBy}`} &bull;{' '}
                    <span className="text-slate-400">{new Date(report.createdAt).toLocaleString()}</span>
                  </p>

                  {(report.gpsLat || report.gpsLng) && (
                    <p className="font-mono text-slate-500">
                      GPS Coords: Lat {report.gpsLat?.toFixed(4)}, Lng {report.gpsLng?.toFixed(4)}
                    </p>
                  )}
                </div>
              </div>

              {/* Right Side Photo Preview if present */}
              {report.photoUrl && (
                <div className="flex-shrink-0">
                  <a
                    href={report.photoUrl.startsWith('http') ? report.photoUrl : `${apiBaseUrl}${report.photoUrl}`}
                    target="_blank"
                    rel="noreferrer"
                    className="block group relative"
                  >
                    <img
                      src={report.photoUrl.startsWith('http') ? report.photoUrl : `${apiBaseUrl}${report.photoUrl}`}
                      alt="Report evidence"
                      className="w-24 h-24 object-cover rounded-lg border border-slate-300 group-hover:opacity-90 transition"
                      onError={(e) => {
                        ;(e.target as HTMLElement).style.display = 'none'
                      }}
                    />
                    <span className="absolute bottom-1 right-1 bg-slate-900/80 text-white text-[10px] px-1.5 py-0.5 rounded backdrop-blur font-mono">
                      Photo Evidence
                    </span>
                  </a>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
