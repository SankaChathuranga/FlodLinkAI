import { useEffect, useState } from 'react'
import { useAppContext } from '../context/AppContext'
import { getDispatchSummary } from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { DispatchSummary } from '../api/types'

/**
 * Analytics — Member D (Ijini). Reporting view over the coordinator decisions:
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
        if (!cancelled) {
          setSummary(res)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(describeApiError(err))
          setLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [apiBaseUrl])

  if (loading) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-8">
        <p className="text-gray-500 text-sm">Loading analytics…</p>
      </div>
    )
  }

  if (error || !summary) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-8">
        <h1 className="text-2xl font-bold text-gray-900 mb-1">Analytics</h1>
        <div className="mt-4 border border-red-300 bg-red-50 text-red-800 rounded-lg px-3 py-2 text-sm">
          {error ?? 'No summary available.'}
        </div>
      </div>
    )
  }

  const cards = [
    { label: 'Total decisions', value: String(summary.total), tone: 'text-gray-900' },
    { label: 'Approved', value: String(summary.approved), tone: 'text-emerald-700' },
    { label: 'Rejected', value: String(summary.rejected), tone: 'text-red-700' },
    { label: 'Revisions requested', value: String(summary.revisionsRequested), tone: 'text-amber-700' },
    {
      label: 'Avg approval time',
      value:
        summary.avgApprovalMinutes === null
          ? '—'
          : `${formatMinutes(summary.avgApprovalMinutes)}`,
      tone: 'text-blue-700',
    },
  ]

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-1">Analytics</h1>
      <p className="text-sm text-gray-500 mb-6">
        Coordinator decision metrics derived from the dispatch log.
      </p>

      <div className="grid grid-cols-2 sm:grid-cols-5 gap-4 mb-8">
        {cards.map((card) => (
          <div key={card.label} className="bg-white rounded-xl shadow border border-gray-100 px-4 py-4">
            <p className={`text-2xl font-bold ${card.tone}`}>{card.value}</p>
            <p className="text-xs text-gray-500 mt-1">{card.label}</p>
          </div>
        ))}
      </div>

      <div className="bg-white rounded-xl shadow border border-gray-100 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-3">Top rejection reasons</h2>
        {summary.topRejectionReasons.length === 0 ? (
          <p className="text-sm text-gray-400">No rejections recorded yet.</p>
        ) : (
          <ul className="space-y-3">
            {summary.topRejectionReasons.map((reason) => {
              const width = Math.max(6, (reason.count / summary.topRejectionReasons[0].count) * 100)
              return (
                <li key={reason.reason} className="text-sm">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-gray-800 truncate">{reason.reason}</span>
                    <span className="text-gray-400 text-xs">{reason.count}×</span>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-red-500 rounded-full"
                      style={{ width: `${width}%` }}
                    />
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