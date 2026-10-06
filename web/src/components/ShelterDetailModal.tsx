import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import type { Shelter, Report } from '../types'

interface ShelterDetailModalProps {
  shelterId: number | null
  onClose: () => void
  onTriggerTriage?: (reportId: number) => void
}

export function ShelterDetailModal({ shelterId, onClose, onTriggerTriage }: ShelterDetailModalProps) {
  const { apiBaseUrl } = useAppContext()

  const [shelter, setShelter] = useState<Shelter | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [needTypeFilter, setNeedTypeFilter] = useState<string>('')

  const fetchShelterDetail = useCallback(async () => {
    if (!shelterId) return

    setLoading(true)
    setError(null)

    try {
      const response = await fetch(`${apiBaseUrl}/api/shelters/${shelterId}`)
      if (!response.ok) {
        throw new Error(`Failed to load shelter details (HTTP ${response.status})`)
      }
      const data: Shelter = await response.json()
      setShelter(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error retrieving shelter details.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl, shelterId])

  useEffect(() => {
    fetchShelterDetail()
  }, [fetchShelterDetail])

  if (!shelterId) return null

  // Filter timeline
  const reports = shelter?.reports ?? []
  const filteredReports = reports.filter((r) => {
    if (statusFilter && r.status.toLowerCase() !== statusFilter.toLowerCase()) return false
    if (needTypeFilter && r.needType.toLowerCase() !== needTypeFilter.toLowerCase()) return false
    return true
  })

  // Calculate summary metrics
  const occupancyPct = shelter && shelter.capacity > 0 ? Math.round((shelter.currentOccupancy / shelter.capacity) * 100) : 0
  const highUrgencyCount = reports.filter((r) => r.urgencyLevel >= 80).length
  const resolvedCount = reports.filter((r) => r.status.toLowerCase() === 'resolved').length

  const getUrgencyBadge = (urgency: number) => {
    if (urgency >= 80) return 'bg-rose-100 text-rose-800 border-rose-300'
    if (urgency >= 50) return 'bg-amber-100 text-amber-800 border-amber-300'
    return 'bg-emerald-100 text-emerald-800 border-emerald-300'
  }

  const getStatusDot = (status: string) => {
    switch (status.toLowerCase()) {
      case 'new':
        return 'bg-blue-500'
      case 'triaged':
        return 'bg-purple-500'
      case 'inplan':
        return 'bg-amber-500'
      case 'resolved':
        return 'bg-emerald-500'
      default:
        return 'bg-slate-400'
    }
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6">
      <div className="bg-white w-full max-w-4xl rounded-2xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col max-h-[90vh] animate-in fade-in zoom-in duration-150">
        
        {/* Modal Header */}
        <div className="bg-slate-900 text-white p-6 flex items-start justify-between">
          <div>
            <div className="flex items-center space-x-3">
              <h2 className="text-2xl font-bold">{shelter?.name ?? `Shelter #${shelterId}`}</h2>
              {shelter && (
                <span
                  className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                    shelter.status.toLowerCase() === 'active' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-slate-700 text-slate-300'
                  }`}
                >
                  {shelter.status}
                </span>
              )}
            </div>
            {shelter && (
              <p className="text-xs text-slate-400 font-mono mt-1">
                Coordinates: Lat {shelter.latitude.toFixed(4)}, Lng {shelter.longitude.toFixed(4)}
              </p>
            )}
          </div>

          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
            aria-label="Close modal"
          >
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Modal Content Scroll Area */}
        <div className="p-6 overflow-y-auto space-y-6 flex-1 bg-slate-50/50">

          {/* ERROR STATE */}
          {error && (
            <div className="bg-rose-50 border border-rose-200 text-rose-800 p-4 rounded-xl flex items-center justify-between">
              <div>
                <h4 className="font-bold text-sm">Failed to Load Shelter Timeline</h4>
                <p className="text-xs text-rose-600">{error}</p>
              </div>
              <button
                onClick={fetchShelterDetail}
                className="px-3 py-1.5 bg-rose-600 text-white text-xs font-semibold rounded-lg hover:bg-rose-700"
              >
                Retry
              </button>
            </div>
          )}

          {/* LOADING STATE */}
          {loading && (
            <div className="space-y-4 animate-pulse">
              <div className="h-24 bg-slate-200 rounded-xl"></div>
              <div className="h-40 bg-slate-200 rounded-xl"></div>
            </div>
          )}

          {!loading && shelter && (
            <>
              {/* Shelter Overview Metrics Card */}
              <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm grid grid-cols-1 sm:grid-cols-3 gap-4">
                
                {/* Capacity & Occupancy Bar */}
                <div className="sm:col-span-2 space-y-2">
                  <div className="flex items-center justify-between text-xs font-semibold text-slate-700">
                    <span>Occupancy & Capacity</span>
                    <span className={occupancyPct >= 100 ? 'text-rose-600 font-bold' : occupancyPct >= 80 ? 'text-amber-600' : 'text-emerald-600'}>
                      {shelter.currentOccupancy} / {shelter.capacity} ({occupancyPct}%)
                    </span>
                  </div>
                  <div className="w-full bg-slate-100 h-3 rounded-full overflow-hidden">
                    <div
                      style={{ width: `${Math.min(occupancyPct, 100)}%` }}
                      className={`h-full transition-all duration-500 ${
                        occupancyPct >= 100 ? 'bg-rose-600' : occupancyPct >= 80 ? 'bg-amber-500' : 'bg-emerald-500'
                      }`}
                    ></div>
                  </div>
                  <p className="text-xs text-slate-500">
                    Contact Volunteer: <strong className="text-slate-700">{shelter.contactVolunteer?.name ?? `ID #${shelter.contactVolunteerId ?? 'N/A'}`}</strong>
                    {shelter.contactVolunteer?.phone && ` (${shelter.contactVolunteer.phone})`}
                  </p>
                </div>

                {/* Report Statistics Badges */}
                <div className="bg-slate-50 p-3 rounded-lg border border-slate-200 flex flex-col justify-between text-xs">
                  <div className="flex items-center justify-between">
                    <span className="text-slate-500">Total Reports:</span>
                    <span className="font-bold text-slate-800">{reports.length}</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-slate-500">Critical (&ge;80 Urgency):</span>
                    <span className="font-bold text-rose-600">{highUrgencyCount}</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-slate-500">Resolved Needs:</span>
                    <span className="font-bold text-emerald-600">{resolvedCount}</span>
                  </div>
                </div>
              </div>

              {/* Report History Timeline Header & Filters */}
              <div className="space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
                  <div>
                    <h3 className="text-lg font-bold text-slate-900">Full Report History Timeline</h3>
                    <p className="text-xs text-slate-500">Chronological list of emergency field reports submitted for this shelter.</p>
                  </div>

                  {/* Filters */}
                  <div className="flex items-center space-x-2">
                    <select
                      value={statusFilter}
                      onChange={(e) => setStatusFilter(e.target.value)}
                      className="px-2.5 py-1.5 bg-white border border-slate-300 rounded-lg text-xs text-slate-800 focus:ring-2 focus:ring-blue-500"
                    >
                      <option value="">All Statuses</option>
                      <option value="New">New</option>
                      <option value="Triaged">Triaged</option>
                      <option value="InPlan">InPlan</option>
                      <option value="Resolved">Resolved</option>
                    </select>

                    <select
                      value={needTypeFilter}
                      onChange={(e) => setNeedTypeFilter(e.target.value)}
                      className="px-2.5 py-1.5 bg-white border border-slate-300 rounded-lg text-xs text-slate-800 focus:ring-2 focus:ring-blue-500"
                    >
                      <option value="">All Need Types</option>
                      <option value="Water">Water</option>
                      <option value="Food">Food</option>
                      <option value="Medical">Medical</option>
                      <option value="Shelter-Repair">Shelter Repair</option>
                      <option value="Other">Other</option>
                    </select>
                  </div>
                </div>

                {/* EMPTY TIMELINE STATE */}
                {filteredReports.length === 0 ? (
                  <div className="bg-white p-8 text-center rounded-xl border border-slate-200">
                    <svg className="w-10 h-10 text-slate-300 mx-auto mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <p className="text-sm font-semibold text-slate-700">No Report History Found</p>
                    <p className="text-xs text-slate-500">No field reports match the selected filters for this shelter.</p>
                  </div>
                ) : (
                  /* Vertical Timeline Layout */
                  <div className="relative border-l-2 border-slate-200 ml-4 space-y-6 pl-6 py-2">
                    {filteredReports.map((report: Report) => (
                      <div key={report.id} className="relative group">
                        
                        {/* Status Dot */}
                        <div
                          className={`absolute -left-[31px] top-1.5 w-4 h-4 rounded-full border-2 border-white shadow ${getStatusDot(
                            report.status
                          )}`}
                        ></div>

                        {/* Report Timeline Card */}
                        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm hover:border-slate-300 transition space-y-3">
                          
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="flex items-center space-x-2">
                              <span className="px-2.5 py-0.5 bg-blue-100 text-blue-800 font-bold text-xs rounded-md">
                                {report.needType}
                              </span>
                              <span className="font-bold text-slate-900 text-sm">
                                Quantity: {report.quantityNeeded} units
                              </span>
                            </div>

                            <div className="flex items-center space-x-2">
                              <span className={`px-2 py-0.5 text-xs font-bold rounded border ${getUrgencyBadge(report.urgencyLevel)}`}>
                                Urgency: {report.urgencyLevel}/100
                              </span>

                              <span className="px-2 py-0.5 bg-slate-100 text-slate-700 text-xs font-semibold rounded">
                                {report.status}
                              </span>
                            </div>
                          </div>

                          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between text-xs text-slate-500 pt-2 border-t border-slate-100 gap-2">
                            <div>
                              <span>Report #{report.id}</span> &bull;{' '}
                              <span>Reported at: {new Date(report.createdAt).toLocaleString()}</span>
                            </div>

                            {onTriggerTriage && report.status.toLowerCase() !== 'resolved' && (
                              <button
                                onClick={() => onTriggerTriage(report.id)}
                                className="px-2.5 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-semibold rounded text-xs transition border border-indigo-200 flex items-center space-x-1"
                              >
                                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                                </svg>
                                <span>Trigger Triage</span>
                              </button>
                            )}
                          </div>

                          {/* Photo evidence preview if present */}
                          {report.photoUrl && (
                            <div className="mt-2">
                              <p className="text-xs font-semibold text-slate-600 mb-1">Attached Photo Evidence:</p>
                              <img
                                src={report.photoUrl.startsWith('http') ? report.photoUrl : `${apiBaseUrl}/${report.photoUrl.replace(/^\//, '')}`}
                                alt={`Evidence for report ${report.id}`}
                                className="h-24 w-auto object-cover rounded-lg border border-slate-200 shadow-xs"
                                onError={(e) => {
                                  (e.target as HTMLImageElement).style.display = 'none'
                                }}
                              />
                            </div>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        {/* Modal Footer */}
        <div className="bg-slate-100 p-4 border-t border-slate-200 flex justify-end">
          <button
            onClick={onClose}
            className="px-5 py-2 bg-slate-800 hover:bg-slate-900 text-white rounded-lg text-xs font-bold transition"
          >
            Close Timeline
          </button>
        </div>
      </div>
    </div>
  )
}
