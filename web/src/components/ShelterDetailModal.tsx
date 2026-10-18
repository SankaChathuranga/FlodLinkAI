import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import { Button, InlineNotification, Modal, ProgressBar, Select, SelectItem, SkeletonText, Stack, Tag, Tile } from '@carbon/react'
import { apiFetch } from '../api/http'
import type { Shelter, Report } from '../types'

interface ShelterDetailModalProps {
  shelterId: number | null
  onClose: () => void
}

/** A shelter's occupancy, report counts and full report history, in a dialog (Esc or Close returns). */
export function ShelterDetailModal({ shelterId, onClose }: ShelterDetailModalProps) {
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
      const response = await apiFetch(`${apiBaseUrl}/api/shelters/${shelterId}`)
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

  const urgencyTag = (urgency: number) => (urgency >= 80 ? 'red' : urgency >= 50 ? 'magenta' : 'green')

  return (
    <Modal
      open
      passiveModal
      size="lg"
      modalHeading={shelter?.name ?? `Shelter #${shelterId}`}
      modalLabel={shelter ? `${shelter.status} · Lat ${shelter.latitude.toFixed(4)}, Lng ${shelter.longitude.toFixed(4)}` : 'Shelter'}
      onRequestClose={onClose}
      closeButtonLabel="Close"
    >
      <Stack gap={5}>
        {error && (
          <div>
            <InlineNotification kind="error" role="alert" title="Failed to Load Shelter Timeline" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void fetchShelterDetail()}>Retry</Button>
          </div>
        )}

        {loading && <SkeletonText paragraph lineCount={6} />}

        {!loading && shelter && (
          <>
            <div className="fl-detail-summary">
              <div>
                <ProgressBar
                  label="Occupancy & Capacity"
                  helperText={`${shelter.currentOccupancy} / ${shelter.capacity} (${occupancyPct}%)`}
                  value={Math.min(occupancyPct, 100)}
                  max={100}
                  status={occupancyPct >= 100 ? 'error' : 'active'}
                />
                <p className="cds--body-compact-01">
                  Contact Volunteer: <strong>{shelter.contactVolunteer?.name ?? `ID #${shelter.contactVolunteerId ?? 'N/A'}`}</strong>
                  {shelter.contactVolunteer?.phone && ` (${shelter.contactVolunteer.phone})`}
                </p>
              </div>
              <dl className="fl-stat-list">
                <div><dt>Total Reports</dt><dd>{reports.length}</dd></div>
                <div><dt>Critical (≥80 Urgency)</dt><dd className="fl-text-error">{highUrgencyCount}</dd></div>
                <div><dt>Resolved Needs</dt><dd className="fl-text-success">{resolvedCount}</dd></div>
              </dl>
            </div>

            <div className="fl-filter-bar">
              <div>
                <h3 className="cds--heading-03">Full Report History Timeline</h3>
                <p className="cds--helper-text-01">Chronological list of emergency field reports submitted for this shelter.</p>
              </div>
              <Select id="timeline-status" labelText="Status" size="sm" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                <SelectItem value="" text="All Statuses" />
                <SelectItem value="New" text="New" />
                <SelectItem value="Triaged" text="Triaged" />
                <SelectItem value="InPlan" text="InPlan" />
                <SelectItem value="Resolved" text="Resolved" />
              </Select>
              <Select id="timeline-need" labelText="Need type" size="sm" value={needTypeFilter} onChange={(e) => setNeedTypeFilter(e.target.value)}>
                <SelectItem value="" text="All Need Types" />
                <SelectItem value="Water" text="Water" />
                <SelectItem value="Food" text="Food" />
                <SelectItem value="Medical" text="Medical" />
                <SelectItem value="Shelter-Repair" text="Shelter Repair" />
                <SelectItem value="Other" text="Other" />
              </Select>
            </div>

            {filteredReports.length === 0 ? (
              <Tile className="fl-empty">
                <p className="cds--heading-02">No Report History Found</p>
                <p className="cds--helper-text-01">No field reports match the selected filters for this shelter.</p>
              </Tile>
            ) : (
              <ol className="fl-timeline" aria-label="Report history">
                {filteredReports.map((report: Report) => (
                  <li key={report.id}>
                    <Tile className="fl-timeline__item">
                      <div className="fl-tag-row">
                        <Tag type="blue" size="sm">{report.needType}</Tag>
                        <strong>Quantity: {report.quantityNeeded} units</strong>
                        <Tag type={urgencyTag(report.urgencyLevel)} size="sm">Urgency: {report.urgencyLevel}/100</Tag>
                        <Tag type="cool-gray" size="sm">{report.status}</Tag>
                      </div>
                      <p className="cds--helper-text-01">
                        Report #{report.id} · Reported at: {new Date(report.createdAt).toLocaleString()}
                      </p>
                      {report.photoUrl && (
                        <img
                          className="fl-timeline__photo"
                          src={report.photoUrl.startsWith('http') ? report.photoUrl : `${apiBaseUrl}/${report.photoUrl.replace(/^\//, '')}`}
                          alt={`Evidence for report ${report.id}`}
                          onError={(e) => { (e.target as HTMLElement).style.display = 'none' }}
                        />
                      )}
                    </Tile>
                  </li>
                ))}
              </ol>
            )}
          </>
        )}
      </Stack>
    </Modal>
  )
}
