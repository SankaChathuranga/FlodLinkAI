import { useState, useEffect, useCallback } from 'react'
import { useAppContext } from '../context/AppContext'
import { Button, InlineNotification, RadioTile, SkeletonText, Stack, Tag, Tile, TileGroup } from '@carbon/react'
import { apiFetch } from '../api/http'
import { Link } from 'react-router-dom'

interface TriagePriorityItemDTO {
  reportId: number
  shelterId: number
  needType: string
  quantity: number
  priorityScore: number
  justification: string
  /** Rule-based urgency score before any LLM adjustment (plans made before Phase 3 lack it). */
  ruleScore?: number | null
  /** Accepted LLM adjustment, -15..+15; absent when the score is rule-based only. */
  llmAdjustment?: number | null
}

interface TriagePlanDTO {
  workflowRunId: string
  priorityItems: TriagePriorityItemDTO[]
  /** "llm-adjusted" or "rule-based". */
  scoringMethod?: string
}

interface TriagePlanEntityRecord {
  id: number
  generatedFromReportIds: number[]
  priorityRank: number
  planSummaryJson: string
  createdByAgentRunId?: string
  createdAt: string
}

export function TriagePlanViewer() {
  const { apiBaseUrl } = useAppContext()

  const [triagePlanRecords, setTriagePlanRecords] = useState<TriagePlanEntityRecord[]>([])
  const [selectedPlanRecord, setSelectedPlanRecord] = useState<TriagePlanEntityRecord | null>(null)
  const [parsedPlan, setParsedPlan] = useState<TriagePlanDTO | null>(null)


  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  const fetchTriagePlans = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await apiFetch(`${apiBaseUrl}/api/reports/triage-plans`)
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


  const urgencyTag = (score: number) => (score >= 80 ? 'red' : score >= 50 ? 'magenta' : 'green')

  return (
    <section aria-labelledby="triage-heading">
      <Stack gap={5}>
        <Tile>
          <div className="inventory-dashboard__header">
            <div>
              <h2 id="triage-heading" className="cds--heading-04">Triage Plan Viewer</h2>
              <p className="cds--body-compact-01 fl-text-muted">
                Every need gets a rule-based urgency score (need type, shelter occupancy, days since resupply). The
                LLM may then adjust it by at most ±15 after reading the volunteer&apos;s notes, with a one-line reason.
                If the LLM is unavailable or its output fails the checks, the rule score and a template reason are used.
              </p>
            </div>
            <Link to="/reports" className="cds--btn cds--btn--primary cds--btn--md">Start a workflow from Reports</Link>
          </div>
        </Tile>

        {error && (
          <div>
            <InlineNotification kind="error" role="alert" title="Could not load triage plans" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void fetchTriagePlans()}>Retry</Button>
          </div>
        )}

        {loading && <Tile aria-busy="true" aria-label="Loading triage plans"><SkeletonText paragraph lineCount={4} /></Tile>}

        {!loading && !error && triagePlanRecords.length === 0 && (
          <Tile className="fl-empty">
            <h3 className="cds--heading-03">No Triage Plans Generated Yet</h3>
            <p className="cds--body-compact-01 fl-text-muted">Plans appear here once a workflow has been started from the Reports page.</p>
          </Tile>
        )}

        {!loading && !error && triagePlanRecords.length > 0 && (
          <div className="fl-split">
            <Tile className="fl-split__side">
              <TileGroup
                legend="Generated Triage Runs"
                name="triage-run"
                valueSelected={String(selectedPlanRecord?.id ?? '')}
                onChange={(value) => setSelectedPlanRecord(triagePlanRecords.find((r) => String(r.id) === String(value)) ?? null)}
              >
                {triagePlanRecords.map((rec) => (
                  <RadioTile key={rec.id} id={`triage-run-${rec.id}`} value={String(rec.id)}>
                    <span className="cds--label-01">Run #{rec.id} · {new Date(rec.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                    <span className="fl-mono fl-text-muted fl-truncate">ID: {rec.createdByAgentRunId ?? 'N/A'}</span>
                  </RadioTile>
                ))}
              </TileGroup>
            </Tile>

            <Stack gap={4}>
              <Tile className="fl-plan-banner">
                <div>
                  <span className="cds--label-01">Active Triage Plan Contract</span>
                  <h3 className="cds--heading-03">
                    Workflow Run ID: <span className="fl-mono">{parsedPlan?.workflowRunId ?? 'N/A'}</span>
                  </h3>
                </div>
                <Tag type={parsedPlan?.scoringMethod === 'llm-adjusted' ? 'purple' : 'gray'}>
                  {parsedPlan?.scoringMethod === 'llm-adjusted' ? 'Scores: rules + LLM adjustment' : 'Scores: rule-based only'}
                </Tag>
              </Tile>

              {parsedPlan?.priorityItems && parsedPlan.priorityItems.length > 0 ? (
                <ol className="fl-report-list" aria-label="Ranked needs">
                  {parsedPlan.priorityItems.map((item, idx) => (
                    <li key={`${item.reportId}-${idx}`}>
                      <Tile className="fl-timeline__item">
                        <div className="fl-tag-row">
                          <strong>#{idx + 1}</strong>
                          <span className="cds--heading-02">Shelter #{item.shelterId} · Need: {item.needType}</span>
                          <span className="cds--label-01">Qty: {item.quantity}</span>
                          <Tag type={urgencyTag(item.priorityScore)} size="sm">Priority Score: {item.priorityScore.toFixed(0)} / 100</Tag>
                          {item.ruleScore != null && (
                            <span className="cds--label-01">
                              rule {item.ruleScore.toFixed(0)}
                              {item.llmAdjustment != null && ` ${item.llmAdjustment >= 0 ? '+' : '−'} ${Math.abs(item.llmAdjustment)} LLM`}
                            </span>
                          )}
                        </div>
                        <div className="fl-reason">
                          <span className="cds--label-01">{item.llmAdjustment != null ? 'LLM justification' : 'Rule-based justification'}</span>
                          <p className="cds--body-compact-01">{item.justification}</p>
                        </div>
                      </Tile>
                    </li>
                  ))}
                </ol>
              ) : (
                <Tile className="fl-empty"><p className="cds--body-compact-01">No priority items found in this plan.</p></Tile>
              )}
            </Stack>
          </div>
        )}
      </Stack>
    </section>
  )
}
