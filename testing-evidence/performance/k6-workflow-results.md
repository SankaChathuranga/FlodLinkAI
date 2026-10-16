# Load test — authenticated API with workflow starts (6.28)

**Run:** 15 Oct 2026, `testing/performance/floodlink-workflow-load.js` with k6 0.54.0 (Docker), against the API
built in Release and running locally (`ASPNETCORE_ENVIRONMENT=Development`) on a fresh, migrated database
(demo seed only). Real Mapbox. Gemini switched off for this run (the key's free-tier quota was used up and its
429 retries would distort agent timings), so Planner, Triage and Matching ran their rule-based paths.
Raw output: `k6-workflow-output.txt`, `k6-workflow-summary.json`.

**Not yet against the deployed API** — nothing is deployed until Phase 7. Re-run the same script against the
deployed URL (`BASE_URL`, `DEMO_PASSWORD`) and add those numbers here; expect higher latency from the network
and the hosting tier.

## Load
- 20 concurrent users for 60 s on read endpoints (inventory, shelters, depots; coordinators also reports,
  workflows and the approval queue), 1 s think time.
- 5 workflows started at the same moment (each for its own new report), followed to PendingApproval/Failed.

## Results

| Measure | p50 | p95 | max | Notes |
|---|---|---|---|---|
| Read endpoints (HTTP, `db_read_ms`) | 11.6 ms | 30.4 ms | 358.5 ms | 5,334 requests, **0 % failed** |
| Workflow start → PendingApproval | 1.65 s | 2.05 s | 2.15 s | **5 / 5** reached PendingApproval |
| Agent step (all agents, `agent_duration_ms`) | 4 ms | 166 ms | 537 ms | from `AgentExecutionLog.duration_ms` |

Overall: 5367 requests, 87.5 req/s, checks 100 % (5349 / 5349); all thresholds passed.

**Agentic AI latency per agent** (5 runs, `duration_ms` from the execution log):

| Agent | avg | p50 | p95 | max |
|---|---|---|---|---|
| RoutingAgent (Mapbox) | 206 ms | 110 ms | 472 ms | 537 ms |
| TriageAgent | 14 ms | 4 ms | 44 ms | 54 ms |
| ValidationAgent | 13 ms | 5 ms | 39 ms | 47 ms |
| StockReservation | 11 ms | 4 ms | 33 ms | 40 ms |
| MatchingAgent | 9 ms | 2 ms | 28 ms | 34 ms |
| PlannerAgent | 4 ms | 0 ms | 15 ms | 19 ms |

Routing (the external Mapbox call) dominates the rule-based pipeline. With Gemini on, add the model latency
measured in the live evaluation (`testing-evidence/agents/`): about 15–18 s per call on the free tier for
Planner and Triage, plus Matching.

## Finding: workflow list read every run's full plan
A first attempt ran by mistake against an old `floodlink_perf` database left by the Oct 9 ZAP scan (52,616
junk reports). Reads there had p95 5.8 s. Timing each endpoint showed `GET /api/workflows` at ~0.5 s and the
approval queue at 1.1–1.7 s with tiny responses: three old runs (from before runs were scoped to their reports)
each held ~22 MB of plan JSON, and the list loaded every run's `PlanJson` only to show summary rows. Fixed —
the list now selects summary columns only (0.5 s → ~0.02 s on that database). The approval queue still reads
plans by design (it summarises them); runs are now scoped to their own reports so plans stay small. Those
invalid results were discarded; the numbers above are from the clean run.
