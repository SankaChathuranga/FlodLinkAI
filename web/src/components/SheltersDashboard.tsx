import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import { ShelterMap } from './ShelterMap'
import { ShelterDetailModal } from './ShelterDetailModal'
import type { Shelter, PaginatedResult } from '../types'

export function SheltersDashboard() {
  const { apiBaseUrl } = useAppContext()

  const [shelters, setShelters] = useState<Shelter[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Filters & Pagination
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [searchTerm, setSearchTerm] = useState<string>('')
  const [selectedShelter, setSelectedShelter] = useState<Shelter | null>(null)
  const [detailShelterId, setDetailShelterId] = useState<number | null>(null)
  const [page, setPage] = useState<number>(1)
  const [totalPages, setTotalPages] = useState<number>(1)

  const fetchShelters = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const queryParams = new URLSearchParams()
      if (statusFilter) queryParams.append('status', statusFilter)
      if (searchTerm) queryParams.append('search', searchTerm)
      queryParams.append('page', page.toString())
      queryParams.append('pageSize', '10')

      const response = await fetch(`${apiBaseUrl}/api/shelters?${queryParams.toString()}`)
      if (!response.ok) {
        throw new Error(`Failed to fetch shelters (HTTP ${response.status})`)
      }

      const data: PaginatedResult<Shelter> = await response.json()
      setShelters(data.items || [])
      setTotalPages(data.totalPages || 1)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to connect to backend server.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl, statusFilter, searchTerm, page])

  useEffect(() => {
    fetchShelters()
  }, [fetchShelters])

  return (
    <div className="space-y-6">
      {/* Top Banner / Controls */}
      <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Shelters Overview Dashboard</h2>
          <p className="text-sm text-slate-500">Monitor shelter occupancy, location coordinates, and volunteer contact details.</p>
        </div>

        {/* Filter Controls */}
        <div className="flex flex-wrap items-center gap-3">
          {/* Search */}
          <div className="relative">
            <input
              type="text"
              placeholder="Search shelter name..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value)
                setPage(1)
              }}
              className="pl-9 pr-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 w-48 sm:w-64"
            />
            <svg className="w-4 h-4 text-slate-400 absolute left-3 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>

          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value)
              setPage(1)
            }}
            className="px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-sm text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 font-medium"
          >
            <option value="">All Statuses</option>
            <option value="Active">Active Only</option>
            <option value="Closed">Closed Only</option>
          </select>

          <button
            onClick={fetchShelters}
            className="px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-sm font-medium transition flex items-center space-x-1"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* Interactive GIS Map View */}
      <ShelterMap
        shelters={shelters}
        selectedShelterId={selectedShelter?.id}
        onSelectShelter={(shelter) => setSelectedShelter(shelter)}
      />

      {/* ERROR STATE */}
      {error && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 p-4 rounded-xl flex items-center justify-between shadow-sm">
          <div className="flex items-center space-x-3">
            <svg className="w-6 h-6 text-rose-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <h4 className="font-semibold text-sm">Failed to Load Shelters</h4>
              <p className="text-xs text-rose-600">{error}</p>
            </div>
          </div>
          <button
            onClick={fetchShelters}
            className="px-3 py-1.5 bg-rose-600 text-white rounded-lg text-xs font-semibold hover:bg-rose-700 transition"
          >
            Retry
          </button>
        </div>
      )}

      {/* LOADING STATE */}
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[1, 2, 3].map((n) => (
            <div key={n} className="bg-white p-6 rounded-xl border border-slate-200 animate-pulse space-y-4">
              <div className="h-5 bg-slate-200 rounded w-3/4"></div>
              <div className="h-4 bg-slate-200 rounded w-1/2"></div>
              <div className="h-3 bg-slate-200 rounded w-full"></div>
            </div>
          ))}
        </div>
      )}

      {/* EMPTY STATE */}
      {!loading && !error && shelters.length === 0 && (
        <div className="bg-white p-12 text-center rounded-xl border border-slate-200 shadow-sm space-y-3">
          <svg className="w-12 h-12 text-slate-300 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
          </svg>
          <h3 className="text-lg font-bold text-slate-800">No Shelters Found</h3>
          <p className="text-sm text-slate-500 max-w-sm mx-auto">No emergency shelters match the specified search query or status filter.</p>
          <button
            onClick={() => {
              setSearchTerm('')
              setStatusFilter('')
              setPage(1)
            }}
            className="px-4 py-2 bg-blue-50 text-blue-600 font-semibold rounded-lg text-xs hover:bg-blue-100 transition"
          >
            Clear Filters
          </button>
        </div>
      )}

      {/* SHELTERS LIST GRID */}
      {!loading && !error && shelters.length > 0 && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {shelters.map((shelter) => {
              const occupancyPct = shelter.capacity > 0 ? Math.round((shelter.currentOccupancy / shelter.capacity) * 100) : 0
              const isOvercrowded = occupancyPct >= 100
              const isHigh = occupancyPct >= 80

              return (
                <div
                  key={shelter.id}
                  onClick={() => setSelectedShelter(shelter)}
                  className={`bg-white p-6 rounded-xl border transition-all cursor-pointer shadow-sm hover:shadow-md ${
                    selectedShelter?.id === shelter.id ? 'ring-2 ring-blue-500 border-blue-500' : 'border-slate-200'
                  }`}
                >
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <h3 className="font-bold text-slate-900 text-lg leading-snug">{shelter.name}</h3>
                      <p className="text-xs text-slate-500 font-mono mt-0.5">
                        Lat: {shelter.latitude.toFixed(4)}, Lng: {shelter.longitude.toFixed(4)}
                      </p>
                    </div>
                    <span
                      className={`px-2.5 py-1 rounded-full text-xs font-semibold ${
                        shelter.status.toLowerCase() === 'active'
                          ? 'bg-emerald-100 text-emerald-800'
                          : 'bg-slate-100 text-slate-600'
                      }`}
                    >
                      {shelter.status}
                    </span>
                  </div>

                  {/* Occupancy % Calculation & Progress Bar */}
                  <div className="space-y-1.5 mt-4">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-slate-600 font-medium">Occupancy Ratio</span>
                      <span
                        className={`font-bold ${
                          isOvercrowded ? 'text-rose-600' : isHigh ? 'text-amber-600' : 'text-emerald-600'
                        }`}
                      >
                        {shelter.currentOccupancy} / {shelter.capacity} ({occupancyPct}%)
                      </span>
                    </div>

                    <div className="w-full bg-slate-100 h-2.5 rounded-full overflow-hidden">
                      <div
                        style={{ width: `${Math.min(occupancyPct, 100)}%` }}
                        className={`h-full transition-all duration-500 ${
                          isOvercrowded ? 'bg-rose-600' : isHigh ? 'bg-amber-500' : 'bg-emerald-500'
                        }`}
                      ></div>
                    </div>
                  </div>

                  {/* Volunteer Contact Information & Timeline Button */}
                  <div className="mt-4 pt-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
                    <div className="flex items-center space-x-1.5">
                      <svg className="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      <span>{shelter.contactVolunteer?.name ?? `Volunteer #${shelter.contactVolunteerId ?? 1}`}</span>
                    </div>

                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        setDetailShelterId(shelter.id)
                      }}
                      className="px-2.5 py-1 bg-blue-50 hover:bg-blue-100 text-blue-700 font-semibold rounded text-xs transition border border-blue-200 flex items-center space-x-1"
                    >
                      <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      <span>Report History</span>
                    </button>
                  </div>
                </div>
              )
            })}
          </div>

          {/* Pagination Controls */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between bg-white px-4 py-3 rounded-xl border border-slate-200 text-sm">
              <button
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-md font-medium disabled:opacity-50 transition"
              >
                Previous
              </button>
              <span className="text-slate-600 text-xs font-semibold">
                Page {page} of {totalPages}
              </span>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
                className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-md font-medium disabled:opacity-50 transition"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}

      {/* Shelter Detail Modal with Full Report History Timeline */}
      <ShelterDetailModal
        shelterId={detailShelterId}
        onClose={() => setDetailShelterId(null)}
      />
    </div>
  )
}
