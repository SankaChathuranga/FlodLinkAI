import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAppContext } from '../context/AppContext'
import { getAuditTrail } from '../api/client'
import type { AuditResponse } from '../api/types'
import {
  ACTIVE_STATES,
  formatDuration,
  getDispatchStatus,
  getWorkflow,
  getWorkflowLogs,
  llmCallOf,
  parsePlan,
  retryWorkflow,
  stateBadgeClass,
  stateLabel,
  type DispatchStatus,
  type ToolCall,
  type WorkflowLogEntry,
  type WorkflowRun,
  type WorkflowState,
} from '../api/workflows'
import { ApiError } from '../api/client'
import { describeApiError } from '../lib/apiError'
import { explainValidation, getRouteLegs, type StoredRouteLeg, type ValidationExplanation } from '../api/workflows'
import { RouteMap, type RouteMapLeg } from './RouteMap'

const POLL_MS = 3000

interface TraceData {
  run: WorkflowRun
  logs: WorkflowLogEntry[]
  dispatch: DispatchStatus | null
  audit: AuditResponse | null
  /** Stored route legs with depot and shelter names (null until the run is routed or if unavailable). */
  routes: StoredRouteLeg[] | null
}

/**
 * Execution trace (4.02) for one workflow run: the Planner's plan, every agent step with its
 * timing, tool calls and LLM call (model, prompt version, tokens, fallback flag), the triage
 * scores, allocations, routes, validation checks, the coordinator's decisions and the audit
 * trail. Polls while the run is still being processed.
 */
export function WorkflowTrace() {
  const { id = '' } = useParams<{ id: string }>()
  const { apiBaseUrl } = useAppContext()
  const [data, setData] = useState<TraceData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [retrying, setRetrying] = useState(false)
  const [retryMessage, setRetryMessage] = useState<string | null>(null)
  const mounted = useRef(true)

  const load = useCallback(async () => {
    try {
      const [run, logs, dispatch] = await Promise.all([
        getWorkflow(apiBaseUrl, id),
        getWorkflowLogs(apiBaseUrl, id),
        getDispatchStatus(apiBaseUrl, id).catch(() => null),
      ])
      const audit = dispatch?.dispatchId ? await getAuditTrail(apiBaseUrl, dispatch.dispatchId).catch(() => null) : null
      const routes = parsePlan(run.planJson).route
        ? await getRouteLegs(apiBaseUrl, id).then((r) => (Array.isArray(r) ? r : null)).catch(() => null)
        : null
      if (!mounted.current) return
      setData({ run, logs, dispatch, audit, routes })
      setError(null)
    } catch (err) {
      if (!mounted.current) return
      if (err instanceof ApiError && err.status === 404) setNotFound(true)
      else setError(describeApiError(err))
    }
  }, [apiBaseUrl, id])

  useEffect(() => {
    mounted.current = true
    void load()
    return () => {
      mounted.current = false
    }
  }, [load])

  const active = data ? ACTIVE_STATES.includes(data.run.currentState) : true
  useEffect(() => {
    if (!active || notFound) return
    const timer = setInterval(() => void load(), POLL_MS)
    return () => clearInterval(timer)
  }, [active, notFound, load])

  const retry = async () => {
    setRetrying(true)
    setRetryMessage(null)
    try {
      await retryWorkflow(apiBaseUrl, id)
      setRetryMessage('Retry queued.')
      await load()
    } catch (err) {
      setRetryMessage(describeApiError(err))
    } finally {
      setRetrying(false)
    }
  }

  if (notFound) {
    return (
      <div className="space-y-3">
        <BackLink />
        <p role="alert" className="rounded-lg border border-slate-200 bg-white p-6 text-slate-700">
          No workflow run with id <span className="font-mono">{id}</span>.
        </p>
      </div>
    )
  }
  if (!data) {
    return (
      <div className="space-y-3">
        <BackLink />
        {error ? (
          <div role="alert" className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">
            {error}{' '}
            <button className="underline" onClick={() => void load()}>
              Try again
            </button>
          </div>
        ) : (
          <p className="p-4 text-sm text-slate-500">Loading execution trace…</p>
        )}
      </div>
    )
  }

  const { run, logs, dispatch, audit, routes } = data
  const plan = parsePlan(run.planJson)
  // Legs to draw and list: stored legs carry names; the plan's legs are the fallback.
  const legRows = routes && routes.length > 0
    ? routes.map((r) => ({ key: r.id, depot: r.depotName ?? `Depot ${r.depotId}`, shelter: r.shelterName ?? `Shelter ${r.shelterId}`, distanceKm: r.distanceKm, etaMinutes: r.etaMinutes, polyline: r.polyline }))
    : (plan.route?.Legs ?? []).map((l, i) => ({ key: String(i), depot: `Depot ${l.DepotId}`, shelter: `Shelter ${l.ShelterId}`, distanceKm: l.DistanceKm, etaMinutes: l.EtaMinutes, polyline: l.Polyline ?? '' }))
  const mapLegs: RouteMapLeg[] = legRows.map((l) => ({ key: `leg-${l.key}`, label: `${l.depot} → ${l.shelter}`, polyline: l.polyline }))
  const deliveryPhoto = deliveryPhotoOf(audit)

  return (
    <div className="space-y-5">
      <BackLink />

      {/* Header */}
      <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold text-slate-900">{run.objective}</h1>
            <p className="font-mono text-xs text-slate-500">{run.id}</p>
            <p className="mt-1 text-xs text-slate-500">
              Reports {run.reportIds.join(', ') || '—'} · started {new Date(run.createdAt).toLocaleString()} · updated{' '}
              {new Date(run.updatedAt).toLocaleString()} · retries {run.retryCount}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {active && (
              <span className="flex items-center gap-1 text-xs text-blue-700" aria-live="polite">
                <span className="h-2 w-2 animate-pulse rounded-full bg-blue-600" /> Processing…
              </span>
            )}
            <span className={`rounded-full border px-3 py-1 text-sm font-semibold ${stateBadgeClass(run.currentState)}`}>
              {stateLabel(run.currentState)}
            </span>
          </div>
        </div>
        <PipelineSteps state={run.currentState} failedAt={run.failedAtState} />
        {run.currentState === 'Failed' && (
          <div role="alert" className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-rose-200 bg-rose-50 p-3">
            <p className="text-sm text-rose-800">
              <strong>Stopped at {run.failedAtState ?? 'unknown stage'}:</strong> {run.failureReason ?? 'No reason recorded.'}
            </p>
            <div className="flex items-center gap-2">
              {retryMessage && <span className="text-xs text-slate-700">{retryMessage}</span>}
              <button
                onClick={() => void retry()}
                disabled={retrying}
                className="rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {retrying ? 'Retrying…' : 'Retry stage'}
              </button>
            </div>
          </div>
        )}
        {error && <p className="mt-2 text-xs text-rose-700">Refresh failed: {error}</p>}
      </section>

      {/* Planner */}
      <Section title="Plan" subtitle="From the Planner step. The orchestrator always runs these four agents in this order.">
        {plan.plan ? (
          <>
            <SourceBadge llm={plan.plan.Source === 'llm'} reason={plan.plan.FallbackReason} />
            <ol className="mt-3 space-y-2">
              {plan.plan.Steps.map((step, i) => (
                <li key={step.Agent} className="text-sm">
                  <span className="font-semibold text-slate-900">
                    {i + 1}. {step.Agent}
                  </span>{' '}
                  — {step.Purpose}
                  <span className="block pl-4 text-xs text-slate-500">{step.Rationale}</span>
                </li>
              ))}
            </ol>
          </>
        ) : (
          <Empty>No plan recorded yet.</Empty>
        )}
      </Section>

      {/* Timeline */}
      <Section title="Execution timeline" subtitle="Every agent and orchestration step, in order.">
        {logs.length === 0 ? (
          <Empty>{active ? 'Waiting for the first step…' : 'No steps were logged.'}</Empty>
        ) : (
          <ol className="space-y-3">
            {logs.map((entry) => (
              <LogStep key={entry.id} entry={entry} />
            ))}
          </ol>
        )}
      </Section>

      {/* Triage */}
      {plan.triagePlan && (
        <Section
          title="Triage scores"
          subtitle={
            plan.triagePlan.ScoringMethod === 'llm-adjusted'
              ? 'Rule-based score plus an LLM adjustment (±15) from the field notes.'
              : 'Rule-based scores only (the LLM was not used or its output was rejected).'
          }
        >
          <Table headers={['Rank', 'Report', 'Shelter', 'Need', 'Qty', 'Rule', 'LLM', 'Final', 'Justification']}>
            {plan.triagePlan.PriorityItems.map((item, i) => (
              <tr key={item.ReportId} className="align-top">
                <Td>{i + 1}</Td>
                <Td>#{item.ReportId}</Td>
                <Td>{item.ShelterId}</Td>
                <Td>{item.NeedType}</Td>
                <Td>{item.Quantity}</Td>
                <Td>{item.RuleScore ?? '—'}</Td>
                <Td>{item.LlmAdjustment == null ? '—' : item.LlmAdjustment > 0 ? `+${item.LlmAdjustment}` : item.LlmAdjustment}</Td>
                <Td>
                  <strong>{item.PriorityScore.toFixed(0)}</strong>
                </Td>
                <Td className="max-w-md text-xs">{item.Justification}</Td>
              </tr>
            ))}
          </Table>
        </Section>
      )}

      {/* Allocations */}
      {plan.allocationProposal && (
        <Section title="Allocations" subtitle={plan.revision ? `Re-planned after revision: “${plan.revision.Notes}”` : undefined}>
          <Table headers={['Depot', 'Shelter', 'Item', 'Quantity']}>
            {plan.allocationProposal.Allocations.map((a, i) => (
              <tr key={i}>
                <Td>{a.DepotId}</Td>
                <Td>{a.ShelterId}</Td>
                <Td>{a.ItemName}</Td>
                <Td>{a.Quantity}</Td>
              </tr>
            ))}
          </Table>
          {plan.allocationProposal.Unfulfillable.length > 0 && (
            <div className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              <strong>Not fully covered:</strong>
              <ul className="mt-1 list-disc pl-5">
                {plan.allocationProposal.Unfulfillable.map((u, i) => (
                  <li key={i}>
                    Shelter {u.ShelterId} · {u.ItemName}: {u.Reason}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </Section>
      )}

      {/* Routes */}
      {plan.route && (
        <Section
          title="Routes"
          subtitle={`Total ${plan.route.DistanceKm.toFixed(1)} km · longest ETA ${Math.round(plan.route.EtaMinutes)} min (Mapbox driving)`}
        >
          {legRows.length > 0 ? (
            <div className="space-y-3">
              <RouteMap legs={mapLegs} />
              <Table headers={['Depot', 'Shelter', 'Distance', 'ETA']}>
                {legRows.map((leg) => (
                  <tr key={leg.key}>
                    <Td>{leg.depot}</Td>
                    <Td>{leg.shelter}</Td>
                    <Td>{leg.distanceKm.toFixed(1)} km</Td>
                    <Td>{Math.round(leg.etaMinutes)} min</Td>
                  </tr>
                ))}
              </Table>
            </div>
          ) : (
            <Empty>Single route, no per-leg detail.</Empty>
          )}
        </Section>
      )}

      {/* Validation */}
      {plan.validationResults && (
        <Section title="Safety checks" subtitle={plan.validationResults.OverallPassed ? 'All checks passed.' : 'At least one check failed — the plan was blocked.'}>
          <ul className="space-y-1 text-sm">
            {plan.validationResults.Checks.map((c) => (
              <li key={c.CheckName} className="flex gap-2">
                <span aria-hidden="true" className={c.Passed ? 'text-emerald-600' : 'text-rose-600'}>
                  {c.Passed ? '✔' : '✖'}
                </span>
                <span>
                  <span className="font-medium">{c.CheckName}</span>
                  <span className="sr-only">{c.Passed ? ' passed' : ' failed'}</span>
                  {c.ViolationDetail && <span className="text-rose-700"> — {c.ViolationDetail}</span>}
                </span>
              </li>
            ))}
          </ul>
          <ValidationExplainer runId={run.id} />
        </Section>
      )}

      {/* Decisions & audit */}
      <Section title="Decisions and audit">
        {!dispatch || dispatch.decisions.length === 0 ? (
          <Empty>{run.currentState === 'PendingApproval' ? 'Awaiting a coordinator decision.' : 'No coordinator decision yet.'}</Empty>
        ) : (
          <ul className="space-y-2 text-sm">
            {dispatch.decisions.map((d) => (
              <li key={d.dispatchId} className="rounded-md border border-slate-200 p-3">
                <span className={`mr-2 rounded-full border px-2 py-0.5 text-xs font-semibold ${stateBadgeClass(d.decision)}`}>
                  {stateLabel(d.decision)}
                </span>
                by <strong>{d.decidedBy ?? `user #${d.decidedById ?? '?'}`}</strong> on {new Date(d.decidedAt).toLocaleString()}
                {d.notes && <span className="mt-1 block text-slate-700">“{d.notes}”</span>}
              </li>
            ))}
            {dispatch.isDelivered && (
              <li className="text-emerald-700">
                Delivery confirmed {dispatch.deliveredAt ? new Date(dispatch.deliveredAt).toLocaleString() : ''}.
                {deliveryPhoto && (
                  <a href={`${apiBaseUrl}${deliveryPhoto}`} target="_blank" rel="noreferrer" className="mt-2 block w-fit">
                    <img src={`${apiBaseUrl}${deliveryPhoto}`} alt="Delivery photo" className="h-32 rounded-md border border-slate-200 object-cover" />
                  </a>
                )}
              </li>
            )}
          </ul>
        )}
        {audit && audit.events.length > 0 && (
          <details className="mt-3 text-sm">
            <summary className="cursor-pointer text-slate-700">Audit trail for the latest decision ({audit.events.length} events)</summary>
            <ol className="mt-2 space-y-1 text-xs">
              {audit.events.map((e) => (
                <li key={e.id} className="font-mono text-slate-600">
                  {new Date(e.createdAt).toLocaleString()} · {e.eventType} · actor {e.actorId ?? '—'}
                </li>
              ))}
            </ol>
          </details>
        )}
      </Section>
    </div>
  )
}

// ── Pieces ────────────────────────────────────────────────────────────────────

/** The photo URL recorded with the delivery confirmation, if it was an uploaded photo. */
function deliveryPhotoOf(audit: AuditResponse | null): string | null {
  const event = audit?.events.find((e) => e.eventType === 'DeliveryConfirmed')
  if (!event?.eventDetailJson) return null
  try {
    const detail = JSON.parse(event.eventDetailJson) as { photoReference?: string | null }
    return detail.photoReference?.startsWith('/uploads/') ? detail.photoReference : null
  } catch {
    return null
  }
}

/** On-demand plain-language explanation of the safety checks (8.15). Display only. */
function ValidationExplainer({ runId }: { runId: string }) {
  const { apiBaseUrl } = useAppContext()
  const [explanation, setExplanation] = useState<ValidationExplanation | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const explain = async (refresh = false) => {
    setLoading(true)
    setError(null)
    try {
      setExplanation(await explainValidation(apiBaseUrl, runId, refresh))
    } catch (err) {
      setError(describeApiError(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="mt-4 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm" aria-live="polite">
      {!explanation ? (
        <button type="button" onClick={() => void explain()} disabled={loading}
          className="rounded-md border border-slate-300 bg-white px-3 py-1.5 font-medium text-slate-800 hover:bg-slate-100 disabled:opacity-60">
          {loading ? 'Explaining…' : 'Explain these results'}
        </button>
      ) : (
        <div className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="text-xs font-semibold text-slate-600">
              {explanation.source === 'llm' ? 'Explanation written by the LLM' : 'Explanation from the rule results (LLM not used)'}
              {' · '}display only — the checks above decide pass or fail
            </span>
            <button type="button" onClick={() => void explain(true)} disabled={loading} className="text-xs text-blue-700 underline disabled:opacity-60">
              {loading ? 'Explaining…' : 'Regenerate'}
            </button>
          </div>
          <p className="text-slate-900">{explanation.summary}</p>
          <ul className="space-y-1">
            {explanation.points.map((p) => (
              <li key={p.checkName}>
                <span className={p.passed ? 'text-emerald-700' : 'text-rose-700'}>{p.checkName}</span>
                <span className="sr-only">{p.passed ? ' passed' : ' failed'}</span>: {p.explanation}
              </li>
            ))}
          </ul>
          {explanation.source !== 'llm' && explanation.fallbackReason && (
            <p className="text-xs text-slate-500">Why no LLM text: {explanation.fallbackReason}</p>
          )}
        </div>
      )}
      {error && <p role="alert" className="mt-2 text-rose-700">{error}</p>}
    </div>
  )
}

const PIPELINE: { label: string; states: WorkflowState[] }[] = [
  { label: 'Triage', states: ['Triage'] },
  { label: 'Matching', states: ['Matching', 'RevisionRequested'] },
  { label: 'Routing', states: ['Routing'] },
  { label: 'Validation', states: ['Validating'] },
  { label: 'Approval', states: ['PendingApproval', 'Approved', 'Rejected'] },
]

function PipelineSteps({ state, failedAt }: { state: WorkflowState; failedAt: string | null }) {
  const current = PIPELINE.findIndex((p) => p.states.includes(state === 'Failed' ? ((failedAt as WorkflowState) ?? 'Triage') : state))
  return (
    <ol aria-label="Pipeline progress" className="mt-4 flex flex-wrap gap-2 text-xs">
      {PIPELINE.map((step, i) => {
        const done = i < current || state === 'Approved'
        const here = i === current && state !== 'Approved'
        const failed = here && (state === 'Failed' || state === 'Rejected')
        return (
          <li
            key={step.label}
            aria-current={here ? 'step' : undefined}
            className={`rounded-full border px-3 py-1 ${
              failed
                ? 'border-rose-300 bg-rose-50 text-rose-800'
                : here
                  ? 'border-blue-400 bg-blue-50 font-semibold text-blue-800'
                  : done
                    ? 'border-emerald-300 bg-emerald-50 text-emerald-800'
                    : 'border-slate-200 text-slate-500'
            }`}
          >
            {done ? '✔ ' : ''}
            {step.label}
          </li>
        )
      })}
    </ol>
  )
}

function LogStep({ entry }: { entry: WorkflowLogEntry }) {
  const llm = llmCallOf(entry)
  const tools = (entry.toolCalls ?? []).filter((t) => t.Tool !== 'llm.generate')
  const ok = entry.status === 'Success'
  return (
    <li className="rounded-lg border border-slate-200 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <span aria-hidden="true" className={ok ? 'text-emerald-600' : 'text-rose-600'}>
            {ok ? '●' : '✖'}
          </span>
          <span className="font-semibold text-slate-900">{entry.agentName}</span>
          <span className={`text-xs ${ok ? 'text-emerald-700' : 'text-rose-700'}`}>{entry.status}</span>
          {entry.isRetry && <span className="rounded bg-amber-100 px-1.5 text-xs text-amber-800">retry</span>}
        </div>
        <span className="text-xs text-slate-500">
          {formatDuration(entry.durationMs)} · {new Date(entry.createdAt).toLocaleTimeString()}
        </span>
      </div>
      {entry.errorMessage && <p className="mt-1 text-xs text-rose-700">{entry.errorMessage}</p>}
      {llm && <LlmCall call={llm} />}
      {tools.length > 0 && (
        <details className="mt-2">
          <summary className="cursor-pointer text-xs text-slate-600">Tool calls ({tools.length})</summary>
          <ul className="mt-1 space-y-1">
            {tools.map((t, i) => (
              <li key={i} className="rounded bg-slate-50 px-2 py-1 font-mono text-xs text-slate-700">
                <span className={t.Succeeded ? 'text-emerald-700' : 'text-rose-700'}>{t.Tool}</span>
                {t.DurationMs != null && ` · ${t.DurationMs} ms`}
                {t.Input && <span className="block break-all">in: {compact(t.Input)}</span>}
                {t.Output && <span className="block break-all">out: {compact(t.Output)}</span>}
                {t.Error && <span className="block text-rose-700">error: {t.Error}</span>}
              </li>
            ))}
          </ul>
        </details>
      )}
    </li>
  )
}

function LlmCall({ call }: { call: ToolCall }) {
  const out = call.Output ?? {}
  const fallback = Boolean(out.usedFallback)
  return (
    <div className={`mt-2 rounded-md border px-3 py-2 text-xs ${fallback ? 'border-amber-200 bg-amber-50' : 'border-violet-200 bg-violet-50'}`}>
      <span className="font-semibold">{fallback ? 'LLM not used — rule-based fallback' : 'LLM output accepted'}</span>
      <span className="text-slate-600">
        {' '}
        · {String(call.Input?.model ?? '?')} · prompt {String(call.Input?.promptVersion ?? '?')} · {String(out.latencyMs ?? '?')} ms · attempts{' '}
        {String(out.attempts ?? '?')} · tokens {String(out.promptTokens ?? '—')}/{String(out.outputTokens ?? '—')}
      </span>
      {call.Error && <span className="block text-amber-900">Reason: {call.Error}</span>}
    </div>
  )
}

function SourceBadge({ llm, reason }: { llm: boolean; reason?: string | null }) {
  return llm ? (
    <span className="rounded-full border border-violet-300 bg-violet-50 px-2 py-0.5 text-xs font-semibold text-violet-800">Written by the LLM</span>
  ) : (
    <span className="text-xs text-amber-800">
      <span className="rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 font-semibold">Default plan</span>
      {reason && <span className="ml-2">{reason}</span>}
    </span>
  )
}

function Section({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      {subtitle && <p className="mb-3 text-xs text-slate-500">{subtitle}</p>}
      <div className={subtitle ? '' : 'mt-3'}>{children}</div>
    </section>
  )
}

function Table({ headers, children }: { headers: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead className="bg-slate-50 text-left text-slate-600">
          <tr>
            {headers.map((h) => (
              <th key={h} className="px-3 py-1.5 font-medium">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">{children}</tbody>
      </table>
    </div>
  )
}

function Td({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <td className={`px-3 py-1.5 text-slate-700 ${className}`}>{children}</td>
}

function Empty({ children }: { children: ReactNode }) {
  return <p className="text-sm text-slate-500">{children}</p>
}

function BackLink() {
  return (
    <Link to="/workflows" className="text-sm text-blue-700 hover:underline">
      ← All workflow runs
    </Link>
  )
}

function compact(value: unknown): string {
  const text = JSON.stringify(value)
  return text.length > 300 ? `${text.slice(0, 300)}…` : text
}
