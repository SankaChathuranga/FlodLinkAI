import { useEffect, useState } from 'react'
import {
  InlineLoading,
  InlineNotification,
  ProgressBar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
  Tile,
} from '@carbon/react'
import { useAppContext } from '../context/AppContext'
import { getDispatchSummary } from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { DispatchSummary } from '../api/types'

/**
 * Analytics – Member D (Ijini). Reporting view over the coordinator decisions:
 * decision totals, approval turnaround and top rejection reasons.
 */
function Analytics() {
  const { apiBaseUrl } = useAppContext()

  const [summary, setSummary] = useState<DispatchSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getDispatchSummary(apiBaseUrl)
      .then((res) => {
        if (!cancelled) { setSummary(res); setLoading(false) }
      })
      .catch((err) => {
        if (!cancelled) { setError(describeApiError(err)); setLoading(false) }
      })

    return () => { cancelled = true }
  }, [apiBaseUrl])

  if (loading) {
    return (
      <section aria-labelledby="analytics-heading">
        <h1 id="analytics-heading" className="cds--heading-04">Analytics</h1>
        <InlineLoading description="Loading analytics…" />
      </section>
    )
  }

  if (error || !summary) {
    return (
      <section aria-labelledby="analytics-heading">
        <h1 id="analytics-heading" className="cds--heading-04">Analytics</h1>
        <InlineNotification kind="error" role="alert" title={error ?? 'No summary available.'} lowContrast hideCloseButton />
      </section>
    )
  }

  // Metric cards — tones follow semantic mapping from ui-context.md
  const cards = [
    { label: 'Total decisions',    value: String(summary.total),               color: 'var(--text-primary)' },
    { label: 'Approved',           value: String(summary.approved),             color: 'var(--state-success)' },
    { label: 'Rejected',           value: String(summary.rejected),             color: 'var(--state-error)' },
    { label: 'Revisions requested',value: String(summary.revisionsRequested),   color: 'var(--text-primary)' /* warning on bg only, not text */ },
    {
      label: 'Avg time to approval',
      value: summary.avgApprovalMinutes === null ? '–' : formatMinutes(summary.avgApprovalMinutes),
      color: 'var(--accent-primary)',
    },
    {
      label: 'Median time to approval',
      value: summary.medianApprovalMinutes == null ? '–' : formatMinutes(summary.medianApprovalMinutes),
      color: 'var(--accent-primary)',
    },
  ]
  const perShelter = summary.dispatchesPerShelter ?? []

  return (
    <section aria-labelledby="analytics-heading">
      <Stack gap={5}>
        <div>
          <h1 id="analytics-heading" className="cds--heading-04">Analytics</h1>
          <p className="cds--body-compact-01 fl-text-muted">
            Coordinator decision metrics derived from the dispatch log. Time to approval runs from starting a workflow to the approval.
          </p>
        </div>

        <ul className="fl-metric-grid" aria-label="Decision metrics">
          {cards.map((card) => (
            <li key={card.label}>
              <Tile className="fl-metric">
                <span className="fl-metric__value" style={{ color: card.color }}>{card.value}</span>
                <span className="cds--label-01">{card.label}</span>
              </Tile>
            </li>
          ))}
        </ul>

        <Tile>
          <h2 className="cds--heading-03">Decisions per shelter</h2>
          {perShelter.length === 0 ? (
            <p className="cds--body-compact-01 fl-text-muted">No decisions recorded yet.</p>
          ) : (
            <div className="fl-table-scroll">
              <Table aria-label="Decisions per shelter" size="sm">
                <TableHead>
                  <TableRow>
                    <TableHeader>Shelter</TableHeader>
                    <TableHeader>Approved</TableHeader>
                    <TableHeader>Rejected</TableHeader>
                    <TableHeader>Revisions</TableHeader>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {perShelter.map((sh) => (
                    <TableRow key={sh.shelterId}>
                      <TableCell>{sh.shelterName}</TableCell>
                      <TableCell>{sh.approved}</TableCell>
                      <TableCell>{sh.rejected}</TableCell>
                      <TableCell>{sh.revisionsRequested}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Tile>

        <Tile>
          <h2 className="cds--heading-03">Top rejection reasons</h2>
          {summary.topRejectionReasons.length === 0 ? (
            <p className="cds--body-compact-01 fl-text-muted">No rejections recorded yet.</p>
          ) : (
            <ul className="fl-stack-sm fl-plain-list">
              {summary.topRejectionReasons.map((reason) => (
                <li key={reason.reason}>
                  <ProgressBar
                    label={reason.reason}
                    helperText={`${reason.count}×`}
                    value={reason.count}
                    max={summary.topRejectionReasons[0].count}
                    size="small"
                  />
                </li>
              ))}
            </ul>
          )}
        </Tile>
      </Stack>
    </section>
  )
}

function formatMinutes(minutes: number): string {
  if (minutes < 60) return `${Math.round(minutes)} min`
  const hours = Math.floor(minutes / 60)
  const mins = Math.round(minutes % 60)
  return `${hours}h ${mins}m`
}

export default Analytics
