import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  Button,
  InlineLoading,
  InlineNotification,
  ProgressIndicator,
  ProgressStep,
  Stack,
  Table as CarbonTable,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
  Tag,
  Tile,
} from '@carbon/react'
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
  stateTagType,
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
      <Stack gap={4}>
        <BackLink />
        <Tile role="alert">
          No workflow run with id <span className="fl-mono">{id}</span>.
        </Tile>
      </Stack>
    )
  }
  if (!data) {
    return (
      <Stack gap={4}>
        <BackLink />
        {error ? (
          <div role="alert" className="fl-error-block">
            <InlineNotification kind="error" title="The trace could not be loaded" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void load()}>Try again</Button>
          </div>
        ) : (
          <InlineLoading description="Loading execution trace…" />
        )}
      </Stack>
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
    <Stack gap={5} as="article" aria-labelledby="trace-heading">
      <BackLink />

      {/* Header */}
      <Tile>
        <div className="fl-plan-banner">
          <div>
            <h1 id="trace-heading" className="cds--heading-04">{run.objective}</h1>
            <p className="fl-mono fl-text-muted cds--label-01">{run.id}</p>
            <p className="cds--helper-text-01">
              Reports {run.reportIds.join(', ') || '—'} · started {new Date(run.createdAt).toLocaleString()} · updated{' '}
              {new Date(run.updatedAt).toLocaleString()} · retries {run.retryCount}
            </p>
          </div>
          <div className="fl-toolbar-inline">
            {active && <InlineLoading description="Processing…" />}
            <Tag type={stateTagType(run.currentState)}>{stateLabel(run.currentState)}</Tag>
          </div>
        </div>
        <PipelineSteps state={run.currentState} failedAt={run.failedAtState} />
        {run.currentState === 'Failed' && (
          <div role="alert" className="fl-error-block fl-spaced">
            <InlineNotification kind="error" title={`Stopped at ${run.failedAtState ?? 'unknown stage'}`}
              subtitle={run.failureReason ?? 'No reason recorded.'} lowContrast hideCloseButton />
            <div className="fl-toolbar-inline">
              <Button size="sm" onClick={() => void retry()} disabled={retrying}>{retrying ? 'Retrying…' : 'Retry stage'}</Button>
              {retryMessage && <span className="cds--helper-text-01" role="status">{retryMessage}</span>}
            </div>
          </div>
        )}
        {error && <InlineNotification kind="warning" title="Refresh failed" subtitle={error} lowContrast hideCloseButton />}
      </Tile>

      {/* Planner */}
      <Section title="Plan" subtitle="From the Planner step. The orchestrator always runs these four agents in this order.">
        {plan.plan ? (
          <>
            <SourceBadge llm={plan.plan.Source === 'llm'} reason={plan.plan.FallbackReason} />
            <ol className="fl-plan-steps">
              {plan.plan.Steps.map((step, i) => (
                <li key={step.Agent}>
                  <strong>{i + 1}. {step.Agent}</strong> — {step.Purpose}
                  <span className="cds--helper-text-01 fl-block">{step.Rationale}</span>
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
          <ol className="fl-log">
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
              <TableRow key={item.ReportId}>
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
                <Td className="fl-justification">{item.Justification}</Td>
              </TableRow>
            ))}
          </Table>
        </Section>
      )}

      {/* Allocations */}
      {plan.allocationProposal && (
        <Section title="Allocations" subtitle={plan.revision ? `Re-planned after revision: “${plan.revision.Notes}”` : undefined}>
          <Table headers={['Depot', 'Shelter', 'Item', 'Quantity']}>
            {plan.allocationProposal.Allocations.map((a, i) => (
              <TableRow key={i}>
                <Td>{a.DepotId}</Td>
                <Td>{a.ShelterId}</Td>
                <Td>{a.ItemName}</Td>
                <Td>{a.Quantity}</Td>
              </TableRow>
            ))}
          </Table>
          {plan.allocationProposal.Unfulfillable.length > 0 && (
            <div className="fl-callout-warning">
              <strong>Not fully covered:</strong>
              <ul>
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
            <div className="fl-stack-sm">
              <RouteMap legs={mapLegs} />
              <Table headers={['Depot', 'Shelter', 'Distance', 'ETA']}>
                {legRows.map((leg) => (
                  <TableRow key={leg.key}>
                    <Td>{leg.depot}</Td>
                    <Td>{leg.shelter}</Td>
                    <Td>{leg.distanceKm.toFixed(1)} km</Td>
                    <Td>{Math.round(leg.etaMinutes)} min</Td>
                  </TableRow>
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
          <ul className="fl-checks">
            {plan.validationResults.Checks.map((c) => (
              <li key={c.CheckName}>
                <span aria-hidden="true" className={c.Passed ? 'fl-text-success' : 'fl-text-error'}>
                  {c.Passed ? '✔' : '✖'}
                </span>
                <span>
                  <strong>{c.CheckName}</strong>
                  <span className="sr-only">{c.Passed ? ' passed' : ' failed'}</span>
                  {c.ViolationDetail && <span className="fl-text-error"> — {c.ViolationDetail}</span>}
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
          <ul className="fl-decisions">
            {dispatch.decisions.map((d) => (
              <li key={d.dispatchId}>
                <Tag type={stateTagType(d.decision)} size="sm">{stateLabel(d.decision)}</Tag>
                by <strong>{d.decidedBy ?? `user #${d.decidedById ?? '?'}`}</strong> on {new Date(d.decidedAt).toLocaleString()}
                {d.notes && <span className="fl-quote">“{d.notes}”</span>}
              </li>
            ))}
            {dispatch.isDelivered && (
              <li className="fl-text-success">
                Delivery confirmed {dispatch.deliveredAt ? new Date(dispatch.deliveredAt).toLocaleString() : ''}.
                {deliveryPhoto && (
                  <a href={`${apiBaseUrl}${deliveryPhoto}`} target="_blank" rel="noreferrer" className="fl-delivery-photo">
                    <img src={`${apiBaseUrl}${deliveryPhoto}`} alt="Delivery photo" />
                  </a>
                )}
              </li>
            )}
          </ul>
        )}
        {audit && audit.events.length > 0 && (
          <details className="fl-details">
            <summary>Audit trail for the latest decision ({audit.events.length} events)</summary>
            <ol>
              {audit.events.map((e) => (
                <li key={e.id} className="fl-mono">
                  {new Date(e.createdAt).toLocaleString()} · {e.eventType} · actor {e.actorId ?? '—'}
                </li>
              ))}
            </ol>
          </details>
        )}
      </Section>
    </Stack>
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
    <div className="fl-explainer" aria-live="polite">
      {!explanation ? (
        <Button kind="tertiary" size="sm" onClick={() => void explain()} disabled={loading}>
          {loading ? 'Explaining…' : 'Explain these results'}
        </Button>
      ) : (
        <div className="fl-stack-sm">
          <div className="fl-plan-banner">
            <span className="cds--label-01">
              {explanation.source === 'llm' ? 'Explanation written by the LLM' : 'Explanation from the rule results (LLM not used)'}
              {' · '}display only — the checks above decide pass or fail
            </span>
            <Button kind="ghost" size="sm" onClick={() => void explain(true)} disabled={loading}>
              {loading ? 'Explaining…' : 'Regenerate'}
            </Button>
          </div>
          <p className="cds--body-compact-01">{explanation.summary}</p>
          <ul className="fl-checks">
            {explanation.points.map((p) => (
              <li key={p.checkName}>
                <strong className={p.passed ? 'fl-text-success' : 'fl-text-error'}>{p.checkName}</strong>
                <span className="sr-only">{p.passed ? ' passed' : ' failed'}</span>: {p.explanation}
              </li>
            ))}
          </ul>
          {explanation.source !== 'llm' && explanation.fallbackReason && (
            <p className="cds--helper-text-01">Why no LLM text: {explanation.fallbackReason}</p>
          )}
        </div>
      )}
      {error && <p role="alert" className="fl-text-error cds--body-compact-01">{error}</p>}
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
    <ProgressIndicator aria-label="Pipeline progress" currentIndex={state === 'Approved' ? PIPELINE.length : Math.max(current, 0)}
      spaceEqually className="fl-pipeline">
      {PIPELINE.map((step, i) => {
        const failed = i === current && (state === 'Failed' || state === 'Rejected')
        return (
          <ProgressStep key={step.label} label={step.label} invalid={failed}
            secondaryLabel={failed ? (state === 'Rejected' ? 'Rejected' : 'Failed') : undefined}
            complete={i < current || state === 'Approved'} />
        )
      })}
    </ProgressIndicator>
  )
}

function LogStep({ entry }: { entry: WorkflowLogEntry }) {
  const llm = llmCallOf(entry)
  const tools = (entry.toolCalls ?? []).filter((t) => t.Tool !== 'llm.generate')
  const ok = entry.status === 'Success'
  return (
    <li className="fl-log__step">
      <div className="fl-plan-banner">
        <div className="fl-tag-row">
          <strong>{entry.agentName}</strong>
          <Tag type={ok ? 'green' : 'red'} size="sm">{entry.status}</Tag>
          {entry.isRetry && <Tag type="warm-gray" size="sm">retry</Tag>}
        </div>
        <span className="cds--helper-text-01">
          {formatDuration(entry.durationMs)} · {new Date(entry.createdAt).toLocaleTimeString()}
        </span>
      </div>
      {entry.errorMessage && <p className="fl-text-error cds--helper-text-01">{entry.errorMessage}</p>}
      {llm && <LlmCall call={llm} />}
      {tools.length > 0 && (
        <details className="fl-details">
          <summary>Tool calls ({tools.length})</summary>
          <ul>
            {tools.map((t, i) => (
              <li key={i} className="fl-tool-call fl-mono">
                <span className={t.Succeeded ? 'fl-text-success' : 'fl-text-error'}>{t.Tool}</span>
                {t.DurationMs != null && ` · ${t.DurationMs} ms`}
                {t.Input && <span className="fl-block">in: {compact(t.Input)}</span>}
                {t.Output && <span className="fl-block">out: {compact(t.Output)}</span>}
                {t.Error && <span className="fl-block fl-text-error">error: {t.Error}</span>}
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
    <div className={`fl-llm-call ${fallback ? 'fl-llm-call--fallback' : ''}`}>
      <Tag type={fallback ? 'warm-gray' : 'purple'} size="sm">{fallback ? 'LLM not used — rule-based fallback' : 'LLM output accepted'}</Tag>
      <span className="cds--helper-text-01">
        {String(call.Input?.model ?? '?')} · prompt {String(call.Input?.promptVersion ?? '?')} · {String(out.latencyMs ?? '?')} ms · attempts{' '}
        {String(out.attempts ?? '?')} · tokens {String(out.promptTokens ?? '—')}/{String(out.outputTokens ?? '—')}
      </span>
      {call.Error && <span className="fl-block cds--helper-text-01">Reason: {call.Error}</span>}
    </div>
  )
}

function SourceBadge({ llm, reason }: { llm: boolean; reason?: string | null }) {
  return llm ? (
    <Tag type="purple">Written by the LLM</Tag>
  ) : (
    <span>
      <Tag type="warm-gray">Default plan</Tag>
      {reason && <span className="cds--helper-text-01"> {reason}</span>}
    </span>
  )
}

function Section({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <section aria-label={title}>
      <Tile className="fl-section">
        <h2 className="cds--heading-03">{title}</h2>
        {subtitle && <p className="cds--helper-text-01">{subtitle}</p>}
        <div className="fl-section__body">{children}</div>
      </Tile>
    </section>
  )
}

function Table({ headers, children }: { headers: string[]; children: ReactNode }) {
  return (
    <div className="fl-table-scroll">
      <CarbonTable size="sm" useZebraStyles>
        <TableHead>
          <TableRow>
            {headers.map((h) => <TableHeader key={h}>{h}</TableHeader>)}
          </TableRow>
        </TableHead>
        <TableBody>{children}</TableBody>
      </CarbonTable>
    </div>
  )
}

function Td({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <TableCell className={className}>{children}</TableCell>
}

function Empty({ children }: { children: ReactNode }) {
  return <p className="cds--body-compact-01 fl-text-muted">{children}</p>
}

function BackLink() {
  return (
    <Link to="/workflows" className="cds--link">
      ← All workflow runs
    </Link>
  )
}

function compact(value: unknown): string {
  const text = JSON.stringify(value)
  return text.length > 300 ? `${text.slice(0, 300)}…` : text
}
