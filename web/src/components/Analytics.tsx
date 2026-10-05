import { useEffect, useState } from 'react'
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
      <div>
        <h1 className="fl-section-title">Analytics</h1>
        <p style={{ fontSize: '0.875rem', color: 'var(--text-muted)' }}>Loading analytics…</p>
      </div>
    )
  }

  if (error || !summary) {
    return (
      <div>
        <h1 className="fl-section-title">Analytics</h1>
        <div className="fl-notification fl-notification-error" style={{ marginTop: '1rem' }}>
          {error ?? 'No summary available.'}
        </div>
      </div>
    )
  }

  // Metric cards — tones follow semantic mapping from ui-context.md
  const cards = [
    { label: 'Total decisions',    value: String(summary.total),               color: 'var(--text-primary)' },
    { label: 'Approved',           value: String(summary.approved),             color: 'var(--state-success)' },
    { label: 'Rejected',           value: String(summary.rejected),             color: 'var(--state-error)' },
    { label: 'Revisions requested',value: String(summary.revisionsRequested),   color: 'var(--text-primary)' /* warning on bg only, not text */ },
    {
      label: 'Avg approval time',
      value: summary.avgApprovalMinutes === null ? '–' : formatMinutes(summary.avgApprovalMinutes),
      color: 'var(--accent-primary)',
    },
  ]

  return (
    <div>
      <h1 className="fl-section-title">Analytics</h1>
      <p className="fl-section-desc">Coordinator decision metrics derived from the dispatch log.</p>

      {/* Metric tiles */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(9rem, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
        {cards.map((card) => (
          <div key={card.label} className="fl-card" style={{ padding: '1rem' }}>
            <p style={{ fontSize: '1.75rem', fontWeight: 700, color: card.color, margin: '0 0 0.25rem', lineHeight: 1 }}>
              {card.value}
            </p>
            <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: 0 }}>{card.label}</p>
          </div>
        ))}
      </div>

      {/* Top rejection reasons panel */}
      <div className="fl-card" style={{ padding: '1.25rem' }}>
        <h2 style={{ fontSize: '1rem', fontWeight: 600, color: 'var(--text-primary)', margin: '0 0 0.875rem' }}>
          Top rejection reasons
        </h2>
        {summary.topRejectionReasons.length === 0 ? (
          <p style={{ fontSize: '0.875rem', color: 'var(--text-muted)' }}>No rejections recorded yet.</p>
        ) : (
          <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {summary.topRejectionReasons.map((reason) => {
              const width = Math.max(6, (reason.count / summary.topRejectionReasons[0].count) * 100)
              return (
                <li key={reason.reason} style={{ fontSize: '0.875rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.25rem' }}>
                    <span style={{ color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{reason.reason}</span>
                    <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginLeft: '0.5rem', flexShrink: 0 }}>{reason.count}×</span>
                  </div>
                  <div className="fl-bar-track">
                    <div className="fl-bar-fill" style={{ width: `${width}%` }} />
                  </div>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </div>
  )
}

function formatMinutes(minutes: number): string {
  if (minutes < 60) return `${Math.round(minutes)} min`
  const hours = Math.floor(minutes / 60)
  const mins = Math.round(minutes % 60)
  return `${hours}h ${mins}m`
}

export default Analytics
