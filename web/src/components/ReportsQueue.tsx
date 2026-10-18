import { useState, useEffect, useCallback, useRef, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Button,
  Checkbox,
  Form,
  InlineNotification,
  Search,
  Select,
  SelectItem,
  SkeletonText,
  Stack,
  Tag,
  TextInput,
  Tile,
} from '@carbon/react'
import { Renew } from '@carbon/icons-react'
import { useAppContext } from '../context/AppContext'
import { apiFetch } from '../api/http'
import { startWorkflow } from '../api/workflows'
import { describeApiError } from '../lib/apiError'
import { useDebouncedValue } from '../lib/useDebouncedValue'
import type { PaginatedResult, Report, Shelter } from '../types'

const PAGE_SIZE = 10

export function ReportsQueue() {
  const { apiBaseUrl } = useAppContext()

  const [reports, setReports] = useState<Report[]>([])
  const [paging, setPaging] = useState({ page: 1, totalPages: 1, totalItems: 0 })
  const [shelters, setShelters] = useState<Shelter[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Filters & Sorting
  const [selectedShelterId, setSelectedShelterId] = useState<string>('')
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [urgencyThreshold, setUrgencyThreshold] = useState<number>(0)
  const [sortOrder, setSortOrder] = useState<string>('urgency_desc')
  const [search, setSearch] = useState<string>('')
  const debouncedSearch = useDebouncedValue(search.trim())
  const [page, setPage] = useState<number>(1)

  // A new filter, sort or search starts again from the first page.
  useEffect(() => {
    setPage(1)
  }, [selectedShelterId, statusFilter, urgencyThreshold, sortOrder, debouncedSearch])

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

  // Only the latest request may update the list (a filter change and its page reset both fetch).
  const latestRequest = useRef(0)

  const fetchReports = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)

    try {
      const queryParams = new URLSearchParams()
      if (selectedShelterId) queryParams.append('shelterId', selectedShelterId)
      if (statusFilter) queryParams.append('status', statusFilter)
      if (urgencyThreshold > 0) queryParams.append('urgency', urgencyThreshold.toString())
      if (sortOrder) queryParams.append('sort', sortOrder)
      if (debouncedSearch) queryParams.append('search', debouncedSearch)
      queryParams.append('page', String(page))
      queryParams.append('pageSize', String(PAGE_SIZE))

      const response = await apiFetch(`${apiBaseUrl}/api/reports?${queryParams.toString()}`)
      if (!response.ok) {
        throw new Error(`Failed to fetch report queue (HTTP ${response.status})`)
      }

      const data: PaginatedResult<Report> = await response.json()
      if (requestId !== latestRequest.current) return
      const items = data?.items ?? []
      setReports(items)
      setPaging({ page: data.page, totalPages: data.totalPages, totalItems: data.totalItems })
      // Selections on other pages are kept; drop listed ones that can no longer join a run.
      setSelected((prev) => new Set([...prev].filter((id) => items.every((r) => r.id !== id || canSelect(r)))))
    } catch (err) {
      if (requestId !== latestRequest.current) return
      setError(err instanceof Error ? err.message : 'Failed to connect to backend server.')
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [apiBaseUrl, selectedShelterId, statusFilter, urgencyThreshold, sortOrder, debouncedSearch, page])

  useEffect(() => {
    fetchReports()
  }, [fetchReports])

  return (
    <section aria-labelledby="reports-heading">
      <Stack gap={5}>
        {/* Header and filters */}
        <Tile>
          <div className="inventory-dashboard__header">
            <div>
              <h2 id="reports-heading" className="cds--heading-04">Report Review Queue</h2>
              <p className="cds--body-compact-01 fl-text-muted">Review field reports, then select New or Triaged ones and start a workflow.</p>
            </div>
            <Button kind="secondary" size="md" renderIcon={Renew} onClick={() => void fetchReports()} disabled={loading}>Refresh</Button>
          </div>
          <div className="fl-filter-bar">
            <Search size="md" labelText="Search reports" placeholder="Search need, notes, shelter or #id" value={search}
              onChange={(e) => setSearch(e.target.value)} closeButtonLabelText="Clear search" />
            <Select id="reports-shelter" labelText="Shelter" size="md" value={selectedShelterId} onChange={(e) => setSelectedShelterId(e.target.value)}>
              <SelectItem value="" text="All Shelters" />
              {shelters.map((s) => <SelectItem key={s.id} value={String(s.id)} text={s.name} />)}
            </Select>
            <Select id="reports-status" labelText="Status" size="md" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
              <SelectItem value="" text="All Statuses" />
              <SelectItem value="New" text="New" />
              <SelectItem value="Triaged" text="Triaged" />
              <SelectItem value="InPlan" text="In Plan" />
              <SelectItem value="Resolved" text="Resolved" />
            </Select>
            <Select id="reports-urgency" labelText="Urgency" size="md" value={String(urgencyThreshold)} onChange={(e) => setUrgencyThreshold(Number(e.target.value))}>
              <SelectItem value="0" text="All Urgencies" />
              <SelectItem value="80" text="Critical (80+)" />
              <SelectItem value="50" text="High (50+)" />
              <SelectItem value="20" text="Moderate (20+)" />
            </Select>
            <Select id="reports-sort" labelText="Sort by" size="md" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)}>
              <SelectItem value="urgency_desc" text="Highest Urgency First" />
              <SelectItem value="urgency_asc" text="Lowest Urgency First" />
              <SelectItem value="newest" text="Newest First" />
              <SelectItem value="oldest" text="Oldest First" />
              <SelectItem value="quantity_desc" text="Largest Quantity First" />
              <SelectItem value="quantity_asc" text="Smallest Quantity First" />
            </Select>
          </div>
        </Tile>

        {/* Start workflow */}
        <Tile className="fl-sticky-bar">
          <Form onSubmit={(e) => void onStart(e)} aria-label="Start workflow">
            <div className="fl-start-bar">
              <span className="cds--label-01" aria-live="polite">
                {selected.size} report{selected.size === 1 ? '' : 's'} selected
              </span>
              <TextInput id="workflow-objective" labelText="Objective" hideLabel maxLength={1000} value={objective}
                placeholder="Objective, e.g. Water and medical supplies for Kaduwela shelters"
                onChange={(e) => setObjective(e.target.value)} />
              {selected.size > 0 && (
                <Button kind="ghost" size="md" type="button" onClick={() => setSelected(new Set())}>Clear</Button>
              )}
              <Button type="submit" size="md" disabled={starting}>{starting ? 'Starting…' : 'Start workflow'}</Button>
            </div>
            {startError && (
              <InlineNotification kind="error" role="alert" title="Workflow not started" subtitle={startError} lowContrast hideCloseButton />
            )}
          </Form>
        </Tile>

        {/* Error */}
        {error && (
          <div>
            <InlineNotification kind="error" role="alert" title="Failed to Load Queue" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void fetchReports()}>Retry</Button>
          </div>
        )}

        {/* Loading */}
        {loading && (
          <div aria-busy="true" aria-label="Loading reports">
            {[1, 2, 3].map((n) => (
              <Tile key={n} className="fl-report-card"><SkeletonText paragraph lineCount={3} /></Tile>
            ))}
          </div>
        )}

        {/* Empty */}
        {!loading && !error && reports.length === 0 && (
          <Tile className="fl-empty">
            <h3 className="cds--heading-03">No Field Reports in Queue</h3>
            <p className="cds--body-compact-01 fl-text-muted">No reports match your active filter criteria or shelter selection.</p>
            <Button kind="tertiary" size="sm" onClick={() => {
              setSearch('')
              setSelectedShelterId('')
              setStatusFilter('')
              setUrgencyThreshold(0)
              setSortOrder('urgency_desc')
            }}>
              Clear All Filters
            </Button>
          </Tile>
        )}

        {/* Reports */}
        {!loading && !error && reports.length > 0 && (
          <ul className="fl-report-list" aria-label="Field reports">
            {reports.map((report) => (
              <li key={report.id}>
                <Tile className="fl-report-card">
                  <Checkbox
                    id={`select-report-${report.id}`}
                    labelText={`Select report ${report.id} for a workflow`}
                    hideLabel
                    checked={selected.has(report.id)}
                    disabled={!canSelect(report)}
                    title={canSelect(report) ? undefined : `Report is ${report.status} and cannot join a new workflow`}
                    onChange={() => toggleSelected(report.id)}
                  />
                  <div className="fl-report-card__body">
                    <div className="fl-tag-row">
                      <span className="fl-mono fl-text-muted">#R-{report.id}</span>
                      <Tag type={needTagType(report.needType)} size="sm">{report.needType}</Tag>
                      <Tag type={urgencyTagType(report.urgencyLevel)} size="sm">Urgency Score: {report.urgencyLevel} / 100</Tag>
                      <Tag type="cool-gray" size="sm">Status: {report.status}</Tag>
                    </div>
                    <h3 className="cds--heading-02">
                      Quantity Required: <span className="fl-accent">{report.quantityNeeded} units</span>
                    </h3>
                    {report.notes && (
                      <p className="fl-notes"><strong>Field notes:</strong> {report.notes}</p>
                    )}
                    <dl className="fl-report-meta">
                      <div><dt>Shelter</dt><dd>{report.shelter?.name ?? `Shelter #${report.shelterId}`}</dd></div>
                      <div><dt>Reported by</dt><dd>{report.reporter?.name ?? `User #${report.reportedBy}`} · {new Date(report.createdAt).toLocaleString()}</dd></div>
                      {(report.gpsLat || report.gpsLng) && (
                        <div><dt>GPS</dt><dd className="fl-mono">Lat {report.gpsLat?.toFixed(4)}, Lng {report.gpsLng?.toFixed(4)}</dd></div>
                      )}
                    </dl>
                  </div>
                  {report.photoUrl && (
                    <a className="fl-report-photo" href={photoSrc(report.photoUrl)} target="_blank" rel="noreferrer">
                      <img src={photoSrc(report.photoUrl)} alt={`Photo for report ${report.id}`}
                        onError={(e) => { (e.target as HTMLElement).style.display = 'none' }} />
                    </a>
                  )}
                </Tile>
              </li>
            ))}
          </ul>
        )}

        {/* Paging */}
        {!loading && !error && paging.totalItems > 0 && (
          <nav aria-label="Report pages" className="fl-pager">
            <span className="cds--body-compact-01">
              Page {paging.page} of {paging.totalPages} ({paging.totalItems} report{paging.totalItems === 1 ? '' : 's'})
            </span>
            <div>
              <Button kind="secondary" size="sm" onClick={() => setPage((p) => p - 1)} disabled={paging.page <= 1}>Previous</Button>
              <Button kind="secondary" size="sm" onClick={() => setPage((p) => p + 1)} disabled={paging.page >= paging.totalPages}>Next</Button>
            </div>
          </nav>
        )}
      </Stack>
    </section>
  )

  function photoSrc(url: string) {
    return url.startsWith('http') ? url : `${apiBaseUrl}${url}`
  }
}

type TagType = 'red' | 'magenta' | 'green' | 'purple' | 'blue' | 'teal' | 'warm-gray' | 'gray' | 'cool-gray'

/** Urgency colour: critical (80+) red, high (50+) magenta, otherwise green. */
function urgencyTagType(score: number): TagType {
  if (score >= 80) return 'red'
  if (score >= 50) return 'magenta'
  return 'green'
}

function needTagType(need: string): TagType {
  switch (need?.toLowerCase()) {
    case 'medical': return 'purple'
    case 'water': return 'blue'
    case 'food': return 'teal'
    case 'shelter-repair': return 'warm-gray'
    default: return 'gray'
  }
}
