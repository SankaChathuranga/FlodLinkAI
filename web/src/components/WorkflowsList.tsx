import { useCallback, useEffect, useState } from 'react'
import { apiFetch } from '../api/http'
import { useAppContext } from '../context/AppContext'

interface WorkflowRunSummary {
  id: string
  objective: string
  currentState: string
  progressPercent: number
  failedAtState: string | null
  failureReason: string | null
  reportCount: number
  retryCount: number
  createdAt: string
  updatedAt: string
}

interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

/**
 * Workflow runs, newest first (GET /api/workflows). A basic list for now; the live monitor,
 * execution trace and retry view are FloodLink_Project_Finalization.md tasks 4.01–4.03.
 */
export function WorkflowsList() {
  const { apiBaseUrl } = useAppContext()
  const [data, setData] = useState<Page<WorkflowRunSummary> | null>(null)
  const [page, setPage] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await apiFetch(`${apiBaseUrl}/api/workflows?page=${page}&pageSize=20`)
      if (!res.ok) throw new Error(`Could not load workflow runs (${res.status}).`)
      setData((await res.json()) as Page<WorkflowRunSummary>)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load workflow runs.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl, page])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Workflow runs</h1>
        <button
          onClick={() => void load()}
          disabled={loading}
          className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-60"
        >
          Refresh
        </button>
      </div>

      <section className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-x-auto">
        {loading && !data ? (
          <p className="p-5 text-sm text-slate-500">Loading workflow runs…</p>
        ) : error ? (
          <p role="alert" className="p-5 text-sm text-red-700">{error}</p>
        ) : !data || data.items.length === 0 ? (
          <p className="p-5 text-sm text-slate-500">No workflow runs yet.</p>
        ) : (
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-2 font-medium">Objective</th>
                <th className="px-4 py-2 font-medium">Stage</th>
                <th className="px-4 py-2 font-medium">Progress</th>
                <th className="px-4 py-2 font-medium">Reports</th>
                <th className="px-4 py-2 font-medium">Updated</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.items.map((run) => (
                <tr key={run.id}>
                  <td className="px-4 py-2 text-slate-900">{run.objective}</td>
                  <td className="px-4 py-2 text-slate-700">
                    {run.currentState}
                    {run.failureReason && <span className="block text-xs text-red-700">{run.failureReason}</span>}
                  </td>
                  <td className="px-4 py-2 text-slate-700">{run.progressPercent}%</td>
                  <td className="px-4 py-2 text-slate-700">{run.reportCount}</td>
                  <td className="px-4 py-2 text-slate-500">{new Date(run.updatedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {data && data.totalPages > 1 && (
        <div className="flex items-center gap-3 text-sm">
          <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="underline disabled:opacity-40">Previous</button>
          <span>Page {data.page} of {data.totalPages}</span>
          <button disabled={page >= data.totalPages} onClick={() => setPage(page + 1)} className="underline disabled:opacity-40">Next</button>
        </div>
      )}
    </div>
  )
}
