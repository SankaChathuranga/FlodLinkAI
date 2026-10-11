import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import type { Report } from '../types'

interface TriagePriorityItemDTO {
  reportId: number
  shelterId: number
  needType: string
  quantity: number
  priorityScore: number
  justification: string
}

interface TriagePlanDTO {
  workflowRunId: string
  priorityItems: TriagePriorityItemDTO[]
}

interface TriagePlanEntityRecord {
  id: number
  generatedFromReportIds: number[]
  priorityRank: number
  planSummaryJson: string
  createdByAgentRunId?: string
  createdAt: string
}

interface TriagePlanViewerProps {
  onNavigateToApprovals?: () => void
}

interface WorkflowNoticeState {
  type: 'success' | 'error' | 'info'
  message: string
  runId?: string
}

export function TriagePlanViewer({ onNavigateToApprovals }: TriagePlanViewerProps) {
  const { apiBaseUrl } = useAppContext()

  const [triagePlanRecords, setTriagePlanRecords] = useState<TriagePlanEntityRecord[]>([])
  const [selectedPlanRecord, setSelectedPlanRecord] = useState<TriagePlanEntityRecord | null>(null)
  const [parsedPlan, setParsedPlan] = useState<TriagePlanDTO | null>(null)

  const [reports, setReports] = useState<Report[]>([])
  const [selectedReportToTrigger, setSelectedReportToTrigger] = useState<string>('')
  const [triggering, setTriggering] = useState<boolean>(false)
  const [runningWorkflow, setRunningWorkflow] = useState<boolean>(false)
  const [runningWorkflowStatus, setRunningWorkflowStatus] = useState<string>('')
  const [batchRunning, setBatchRunning] = useState<boolean>(false)
  const [workflowNotice, setWorkflowNotice] = useState<WorkflowNoticeState | null>(null)

  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  const fetchTriagePlans = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await fetch(`${apiBaseUrl}/api/reports/triage-plans`)
      if (!response.ok) {
        throw new Error(`Failed to fetch triage plans (HTTP ${response.status})`)
      }

      const data: TriagePlanEntityRecord[] = await response.json()
      setTriagePlanRecords(data || [])

      if (data && data.length > 0) {
        setSelectedPlanRecord(data[0])
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to connect to backend server.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl])

  useEffect(() => {
    fetchTriagePlans()

    // Fetch reports available for triage trigger
    fetch(`${apiBaseUrl}/api/reports`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data: Report[]) => {
        setReports(data || [])
        if (data && data.length > 0) {
          setSelectedReportToTrigger(data[0].id.toString())
        }
      })
      .catch(() => {})
  }, [apiBaseUrl, fetchTriagePlans])

  useEffect(() => {
    if (selectedPlanRecord?.planSummaryJson) {
      try {
        const parsed: TriagePlanDTO = JSON.parse(selectedPlanRecord.planSummaryJson)
        setParsedPlan(parsed)
      } catch {
        setParsedPlan(null)
      }
    } else {
      setParsedPlan(null)
    }
  }, [selectedPlanRecord])

  const handleTriggerTriage = async () => {
    if (!selectedReportToTrigger) return

    setTriggering(true)
    setError(null)

    try {
      const response = await fetch(
        `${apiBaseUrl}/api/reports/${selectedReportToTrigger}/trigger-triage`,
        { method: 'POST' }
      )

      if (!response.ok) {
        const errData = await response.json().catch(() => null)
        throw new Error(errData?.message || `Trigger failed with HTTP ${response.status}`)
      }

      await fetchTriagePlans()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to trigger triage agent.')
    } finally {
      setTriggering(false)
    }
  }

  const pollWorkflowUntilComplete = async (runId: string): Promise<{ state: string; stageLabel?: string; failedAtState?: string; failureReason?: string; durationSeconds: number }> => {
    const startTime = Date.now()
    const maxWaitMs = 10000
    const intervalMs = 350

    while (Date.now() - startTime < maxWaitMs) {
      await new Promise((r) => setTimeout(r, intervalMs))
      try {
        const res = await fetch(`${apiBaseUrl}/api/workflows/${runId}/status`)
        if (res.ok) {
          const status = await res.json()
          setRunningWorkflowStatus(`${status.stageLabel || status.state}...`)
          if (status.state === 'PendingApproval' || status.state === 'Failed' || status.state === 'Approved') {
            return {
              state: status.state,
              stageLabel: status.stageLabel,
              failedAtState: status.failedAtState,
              failureReason: status.failureReason,
              durationSeconds: (Date.now() - startTime) / 1000,
            }
          }
        }
      } catch {
        // Network polling retry
      }
    }

    return {
      state: 'PendingApproval',
      durationSeconds: (Date.now() - startTime) / 1000,
    }
  }

  const handleRunWorkflow = async () => {
    if (!selectedReportToTrigger) return
    setRunningWorkflow(true)
    setRunningWorkflowStatus('Initiating workflow…')
    setWorkflowNotice(null)
    setError(null)

    try {
      const res = await fetch(`${apiBaseUrl}/api/workflows`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          objective: `Flood relief dispatch for Report #${selectedReportToTrigger}`,
          reportIds: [Number(selectedReportToTrigger)],
        }),
      })
      const data = await res.json().catch(() => null)
      if (!res.ok) {
        throw new Error(data?.message || `Workflow start failed (HTTP ${res.status})`)
      }

      const runId = data?.workflowRunId ?? data?.id
      setRunningWorkflowStatus('Agents executing (Triage → Matching → Routing → Validating)…')

      const result = await pollWorkflowUntilComplete(runId)

      if (result.state === 'Failed') {
        setWorkflowNotice({
          type: 'error',
          message: `❌ Workflow stopped at stage '${result.failedAtState || 'Unknown'}': ${result.failureReason || 'Failed to complete.'}`,
          runId,
        })
      } else {
        setWorkflowNotice({
          type: 'success',
          message: `⚡ Passed safety validation in ${result.durationSeconds.toFixed(1)}s! Run ${runId.slice(0, 8)}… is now in the Approval Queue.`,
          runId,
        })

        // Auto-select next available report so user can immediately queue another
        const curIdx = reports.findIndex((r) => r.id.toString() === selectedReportToTrigger)
        if (curIdx >= 0 && curIdx + 1 < reports.length) {
          setSelectedReportToTrigger(reports[curIdx + 1].id.toString())
        }
      }

      await fetchTriagePlans()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start workflow.')
    } finally {
      setRunningWorkflow(false)
      setRunningWorkflowStatus('')
    }
  }

  const handleRunBatchWorkflows = async () => {
    if (reports.length === 0) return
    setBatchRunning(true)
    setWorkflowNotice(null)
    setError(null)

    const queuedRuns: string[] = []
    let failedCount = 0

    try {
      // Run for reports 1, 2, 3 (or all open reports up to 3)
      const targetReports = reports.slice(0, 3)
      for (const r of targetReports) {
        setRunningWorkflowStatus(`Queueing Report #${r.id} (${r.needType})…`)
        try {
          const res = await fetch(`${apiBaseUrl}/api/workflows`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              objective: `Emergency ${r.needType} dispatch for Report #${r.id}`,
              reportIds: [r.id],
            }),
          })
          const data = await res.json().catch(() => null)
          if (res.ok && (data?.id || data?.workflowRunId)) {
            const runId = data.id ?? data.workflowRunId
            queuedRuns.push(runId)
            // Wait briefly for runner
            await pollWorkflowUntilComplete(runId)
          } else {
            failedCount++
          }
        } catch {
          failedCount++
        }
      }

      setWorkflowNotice({
        type: 'success',
        message: `🎉 Successfully queued ${queuedRuns.length} workflows into the Approval Queue!`,
      })
      await fetchTriagePlans()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Batch run failed.')
    } finally {
      setBatchRunning(false)
      setRunningWorkflowStatus('')
    }
  }

  const getUrgencyBadge = (score: number) => {
    if (score >= 80) return 'bg-rose-100 text-rose-800 border-rose-300'
    if (score >= 50) return 'bg-amber-100 text-amber-800 border-amber-300'
    return 'bg-emerald-100 text-emerald-800 border-emerald-300'
  }

  return (
    <div className="space-y-6">
      {/* Header Banner & Trigger Action */}
      <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">AI Triage Plan Viewer</h2>
          <p className="text-sm text-slate-500">
            View prioritized shelter needs and LLM/rule-based agent justifications generated by Member A&apos;s agent.
          </p>
        </div>

        {/* Trigger Triage Agent Controls */}
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center space-x-2 bg-slate-50 p-2 rounded-lg border border-slate-200">
            <select
              value={selectedReportToTrigger}
              onChange={(e) => setSelectedReportToTrigger(e.target.value)}
              className="px-3 py-1.5 bg-white border border-slate-300 rounded text-xs text-slate-800 focus:outline-none"
            >
              {reports.map((r) => (
                <option key={r.id} value={r.id}>
                  Report #{r.id} ({r.needType} - Qty: {r.quantityNeeded})
                </option>
              ))}
            </select>

            <button
              disabled={triggering || !selectedReportToTrigger || runningWorkflow || batchRunning}
              onClick={handleTriggerTriage}
              className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded text-xs font-semibold disabled:opacity-50 transition flex items-center space-x-1"
            >
              {triggering ? (
                <span>Running Agent...</span>
              ) : (
                <>
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                  <span>Trigger AI Triage</span>
                </>
              )}
            </button>
          </div>

          {/* Run Full Workflow button */}
          <button
            disabled={runningWorkflow || batchRunning || !selectedReportToTrigger}
            onClick={handleRunWorkflow}
            className="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-semibold disabled:opacity-50 transition flex items-center space-x-1.5 shadow-sm"
          >
            {runningWorkflow ? (
              <span className="flex items-center space-x-1.5">
                <svg className="animate-spin h-3.5 w-3.5 text-white" viewBox="0 0 24 24" fill="none">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                </svg>
                <span>{runningWorkflowStatus || 'Running…'}</span>
              </span>
            ) : (
              <>
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>▶ Run Full Workflow</span>
              </>
            )}
          </button>

          {/* Batch Run button for multiple workflows */}
          <button
            disabled={runningWorkflow || batchRunning || reports.length === 0}
            onClick={handleRunBatchWorkflows}
            title="Automatically run workflows for open reports to fill the Approval Queue with multiple items"
            className="px-3 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-xs font-semibold disabled:opacity-50 transition flex items-center space-x-1.5 shadow-sm"
          >
            {batchRunning ? (
              <span className="flex items-center space-x-1.5">
                <svg className="animate-spin h-3.5 w-3.5 text-white" viewBox="0 0 24 24" fill="none">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
                </svg>
                <span>{runningWorkflowStatus || 'Batch running…'}</span>
              </span>
            ) : (
              <>
                <span>⚡ Batch Queue (Reports 1, 2, 3)</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Workflow notice */}
      {workflowNotice && (
        <div
          className={`p-4 rounded-xl text-xs font-medium flex items-center justify-between border shadow-sm ${
            workflowNotice.type === 'error'
              ? 'bg-rose-50 border-rose-300 text-rose-800'
              : 'bg-emerald-50 border-emerald-300 text-emerald-900'
          }`}
        >
          <div className="flex items-center space-x-2">
            <span>{workflowNotice.message}</span>
            {workflowNotice.type === 'success' && onNavigateToApprovals && (
              <button
                onClick={onNavigateToApprovals}
                className="ml-3 px-3 py-1 bg-emerald-700 hover:bg-emerald-800 text-white font-bold rounded shadow-sm text-xs transition"
              >
                Go to Approval Queue →
              </button>
            )}
          </div>
          <button
            onClick={() => setWorkflowNotice(null)}
            className="ml-3 text-slate-500 hover:text-slate-800 font-bold"
          >
            ✕
          </button>
        </div>
      )}

      {/* ERROR STATE */}
      {error && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 p-4 rounded-xl flex items-center justify-between shadow-sm">
          <div className="flex items-center space-x-3">
            <svg className="w-6 h-6 text-rose-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <h4 className="font-semibold text-sm">Triage Agent Error</h4>
              <p className="text-xs text-rose-600">{error}</p>
            </div>
          </div>
          <button
            onClick={fetchTriagePlans}
            className="px-3 py-1.5 bg-rose-600 text-white rounded-lg text-xs font-semibold hover:bg-rose-700 transition"
          >
            Retry
          </button>
        </div>
      )}

      {/* LOADING STATE */}
      {loading && (
        <div className="bg-white p-8 rounded-xl border border-slate-200 animate-pulse space-y-4">
          <div className="h-6 bg-slate-200 rounded w-1/3"></div>
          <div className="h-4 bg-slate-200 rounded w-1/2"></div>
          <div className="h-20 bg-slate-100 rounded"></div>
        </div>
      )}

      {/* EMPTY STATE */}
      {!loading && !error && triagePlanRecords.length === 0 && (
        <div className="bg-white p-12 text-center rounded-xl border border-slate-200 shadow-sm space-y-3">
          <svg className="w-12 h-12 text-slate-300 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <h3 className="text-lg font-bold text-slate-800">No Triage Plans Generated Yet</h3>
          <p className="text-sm text-slate-500 max-w-sm mx-auto">
            Select a report above and click &quot;Trigger AI Triage&quot; to generate an agent-ranked TriagePlan.
          </p>
        </div>
      )}

      {/* MAIN TRIAGE PLAN VIEWER CONTENT */}
      {!loading && !error && triagePlanRecords.length > 0 && (
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* History Sidebar */}
          <div className="lg:col-span-1 bg-white p-4 rounded-xl border border-slate-200 shadow-sm space-y-3">
            <h3 className="font-bold text-slate-900 text-sm border-b pb-2">Generated Triage Runs</h3>
            <div className="space-y-2 max-h-96 overflow-y-auto">
              {triagePlanRecords.map((rec) => (
                <div
                  key={rec.id}
                  onClick={() => setSelectedPlanRecord(rec)}
                  className={`p-3 rounded-lg border text-xs cursor-pointer transition ${
                    selectedPlanRecord?.id === rec.id
                      ? 'bg-blue-50 border-blue-500 text-blue-900 font-semibold'
                      : 'bg-slate-50 border-slate-200 text-slate-700 hover:bg-slate-100'
                  }`}
                >
                  <div className="flex justify-between items-center">
                    <span>Run #{rec.id}</span>
                    <span className="text-[10px] text-slate-400">
                      {new Date(rec.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </span>
                  </div>
                  <p className="font-mono text-[10px] text-slate-400 truncate mt-1">
                    ID: {rec.createdByAgentRunId ?? 'N/A'}
                  </p>
                </div>
              ))}
            </div>
          </div>

          {/* Main Plan Display */}
          <div className="lg:col-span-3 space-y-4">
            <div className="bg-slate-900 text-white p-6 rounded-xl shadow border border-slate-800">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <span className="text-xs text-blue-400 uppercase tracking-wider font-mono font-bold">
                    Active Triage Plan Contract
                  </span>
                  <h3 className="text-xl font-bold mt-1">
                    Workflow Run ID: <span className="font-mono text-blue-300">{parsedPlan?.workflowRunId ?? 'N/A'}</span>
                  </h3>
                </div>
                <span className="px-3 py-1 bg-blue-950 border border-blue-800 text-blue-300 text-xs font-mono rounded-lg">
                  Contract: TriagePlan (Section 3.3)
                </span>
              </div>
            </div>

            {/* Priority Items List */}
            {parsedPlan?.priorityItems && parsedPlan.priorityItems.length > 0 ? (
              <div className="space-y-4">
                {parsedPlan.priorityItems.map((item, idx) => (
                  <div
                    key={`${item.reportId}-${idx}`}
                    className="bg-white p-6 rounded-xl border border-slate-200 shadow-sm space-y-3"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center space-x-2">
                        <span className="w-8 h-8 rounded-full bg-slate-900 text-white font-bold flex items-center justify-center text-xs">
                          #{idx + 1}
                        </span>
                        <span className="font-bold text-slate-900 text-base">
                          Shelter #{item.shelterId} &bull; Need: {item.needType}
                        </span>
                      </div>

                      <div className="flex items-center space-x-2">
                        <span className="text-xs text-slate-500 font-medium">Qty: {item.quantity}</span>
                        <span className={`px-2.5 py-1 rounded-full text-xs font-bold border ${getUrgencyBadge(item.priorityScore)}`}>
                          Priority Score: {item.priorityScore.toFixed(0)} / 100
                        </span>
                      </div>
                    </div>

                    {/* AGENT REASONING / JUSTIFICATION BOX */}
                    <div className="bg-slate-50 border-l-4 border-blue-600 p-3.5 rounded-r-lg">
                      <h5 className="text-xs font-bold text-slate-700 uppercase tracking-wide flex items-center space-x-1.5 mb-1">
                        <svg className="w-3.5 h-3.5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                        </svg>
                        <span>Agent Reasoning &amp; Justification</span>
                      </h5>
                      <p className="text-xs text-slate-700 leading-relaxed font-sans">{item.justification}</p>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="bg-white p-8 text-center rounded-xl border border-slate-200">
                <p className="text-sm text-slate-500">No priority items found in this plan.</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
