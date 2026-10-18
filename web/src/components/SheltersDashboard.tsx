import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import { Button, InlineNotification, ProgressBar, Search, Select, SelectItem, SkeletonText, Stack, Tag, Tile } from '@carbon/react'
import { Renew, Time } from '@carbon/icons-react'
import { apiFetch } from '../api/http'
import { useDebouncedValue } from '../lib/useDebouncedValue'
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
  const debouncedSearch = useDebouncedValue(searchTerm.trim())
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
      if (debouncedSearch) queryParams.append('search', debouncedSearch)
      queryParams.append('page', page.toString())
      queryParams.append('pageSize', '10')

      const response = await apiFetch(`${apiBaseUrl}/api/shelters?${queryParams.toString()}`)
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
  }, [apiBaseUrl, statusFilter, debouncedSearch, page])

  useEffect(() => {
    fetchShelters()
  }, [fetchShelters])

  return (
    <section aria-labelledby="shelters-heading">
      <Stack gap={5}>
        <Tile>
          <div className="inventory-dashboard__header">
            <div>
              <h2 id="shelters-heading" className="cds--heading-04">Shelters Overview Dashboard</h2>
              <p className="cds--body-compact-01 fl-text-muted">Monitor shelter occupancy, location coordinates, and volunteer contact details.</p>
            </div>
            <Button kind="secondary" renderIcon={Renew} onClick={() => void fetchShelters()} disabled={loading}>Refresh</Button>
          </div>
          <div className="fl-filter-bar">
            <Search size="md" labelText="Search shelters" placeholder="Search shelter name..." value={searchTerm}
              onChange={(e) => { setSearchTerm(e.target.value); setPage(1) }} closeButtonLabelText="Clear search" />
            <Select id="shelters-status" labelText="Status" value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1) }}>
              <SelectItem value="" text="All Statuses" />
              <SelectItem value="Active" text="Active Only" />
              <SelectItem value="Closed" text="Closed Only" />
            </Select>
          </div>
        </Tile>

        <ShelterMap
          shelters={shelters}
          selectedShelterId={selectedShelter?.id}
          onSelectShelter={(shelter) => setSelectedShelter(shelter)}
        />

        {error && (
          <div>
            <InlineNotification kind="error" role="alert" title="Failed to Load Shelters" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void fetchShelters()}>Retry</Button>
          </div>
        )}

        {loading && (
          <div className="fl-card-grid" aria-busy="true" aria-label="Loading shelters">
            {[1, 2, 3].map((n) => <Tile key={n}><SkeletonText paragraph lineCount={3} /></Tile>)}
          </div>
        )}

        {!loading && !error && shelters.length === 0 && (
          <Tile className="fl-empty">
            <h3 className="cds--heading-03">No Shelters Found</h3>
            <p className="cds--body-compact-01 fl-text-muted">No emergency shelters match the specified search query or status filter.</p>
            <Button kind="tertiary" size="sm" onClick={() => { setSearchTerm(''); setStatusFilter(''); setPage(1) }}>
              Clear Filters
            </Button>
          </Tile>
        )}

        {!loading && !error && shelters.length > 0 && (
          <>
            <ul className="fl-card-grid" aria-label="Shelters">
              {shelters.map((shelter) => {
                const occupancyPct = shelter.capacity > 0 ? Math.round((shelter.currentOccupancy / shelter.capacity) * 100) : 0
                const status = occupancyPct >= 100 ? 'error' : occupancyPct >= 80 ? 'warning' : 'success'
                const selected = selectedShelter?.id === shelter.id
                return (
                  <li key={shelter.id}>
                    <Tile className={`fl-shelter-card${selected ? ' fl-shelter-card--selected' : ''}`}>
                      <div className="fl-shelter-card__head">
                        <div>
                          <h3 className="cds--heading-03">{shelter.name}</h3>
                          <p className="fl-mono fl-text-muted cds--label-01">
                            Lat: {shelter.latitude.toFixed(4)}, Lng: {shelter.longitude.toFixed(4)}
                          </p>
                        </div>
                        <Tag type={shelter.status.toLowerCase() === 'active' ? 'green' : 'gray'} size="sm">{shelter.status}</Tag>
                      </div>
                      <ProgressBar
                        label="Occupancy Ratio"
                        helperText={`${shelter.currentOccupancy} / ${shelter.capacity} (${occupancyPct}%)${occupancyPct > 100 ? ' — over capacity' : ''}`}
                        value={Math.min(occupancyPct, 100)}
                        max={100}
                        status={status === 'error' ? 'error' : 'active'}
                        className={`fl-occupancy fl-occupancy--${status}`}
                      />
                      <div className="fl-shelter-card__foot">
                        <span className="cds--label-01">
                          Contact: {shelter.contactVolunteer?.name ?? `Volunteer #${shelter.contactVolunteerId ?? 1}`}
                        </span>
                        <div className="fl-shelter-card__actions">
                          <Button kind="ghost" size="sm" aria-pressed={selected} onClick={() => setSelectedShelter(selected ? null : shelter)}>
                            {selected ? 'Shown on map' : 'Show on map'}
                          </Button>
                          <Button kind="tertiary" size="sm" renderIcon={Time} onClick={() => setDetailShelterId(shelter.id)}>
                            Report History
                          </Button>
                        </div>
                      </div>
                    </Tile>
                  </li>
                )
              })}
            </ul>

            {totalPages > 1 && (
              <nav aria-label="Shelter pages" className="fl-pager">
                <Button kind="secondary" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <span className="cds--body-compact-01">Page {page} of {totalPages}</span>
                <Button kind="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </nav>
            )}
          </>
        )}

        <ShelterDetailModal shelterId={detailShelterId} onClose={() => setDetailShelterId(null)} />
      </Stack>
    </section>
  )
}
