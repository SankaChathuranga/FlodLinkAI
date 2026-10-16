# Load test — authenticated API with workflow starts (6.28)

**Run:** 15 Oct 2026, `testing/performance/floodlink-workflow-load.js` with k6 0.54.0 (Docker), against the API
built in Release and running locally (`ASPNETCORE_ENVIRONMENT=Development`) on a fresh, migrated database
(demo seed only). Real Mapbox. Gemini switched off for this run (the key's free-tier quota was used up and its
429 retries would distort agent timings), so Planner, Triage and Matching ran their rule-based paths.
Raw output: `k6-workflow-output.txt`, `k6-workflow-summary.json`.

The deployed run (16 Oct) is at the end of this file.

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

## Deployed run — 16 Oct 2026 (Render free tier API + Neon Postgres)

Same script and load against `https://flodlinkai.onrender.com` (warmed up first, so no cold start in the
numbers). Raw output: `k6-deployed-output.txt`, `k6-deployed-summary.json`. The script rejected its own five
runs afterwards (all five are `Rejected`), so no stock stayed reserved; the five `k6 load test VU …` reports
remain on the database.

| Measure | p50 | p95 | max | Notes |
|---|---|---|---|---|
| Read endpoints | 1.55 s | 2.49 s | 7.9 s | 1,962 requests, 2 failed (0.1 %); **p95 target 750 ms missed** |
| Workflow start → PendingApproval | 27.0 s | 36.2 s | 37.2 s | **5 / 5** reached PendingApproval |
| Agent step (all agents) | 385 ms | 1.96 s | 2.71 s | from `duration_ms` |

Overall 2,071 requests (31.7 req/s), checks 99.9 %.

| Agent | avg | p50 | max |
|---|---|---|---|
| StockReservation | 925 ms | 847 ms | 1,409 ms |
| RoutingAgent (Mapbox) | 893 ms | 552 ms | 2,712 ms |
| TriageAgent | 657 ms | 279 ms | 2,101 ms |
| ValidationAgent | 606 ms | 299 ms | 1,808 ms |
| PlannerAgent | 443 ms | 109 ms | 1,300 ms |
| MatchingAgent | 304 ms | 253 ms | 599 ms |

**Gemini was not used:** all 15 LLM calls returned `LLM_NOT_CONFIGURED` — `Gemini__ApiKey` is not set on
Render, so Planner, Triage and Matching ran their rule-based fallbacks. Set the key and re-run to measure the
LLM path (the live evaluation measured ~15–18 s per Gemini call on the free tier).

**Why it is slower than local:**
- Every database round trip costs ~90 ms (the Render startup log shows `SELECT 1` at 88 ms), which points to
  the Neon database being in a different region from the Render service; a list request makes several queries.
  Putting both in the same region is the biggest single improvement available.
- The Render free instance has a fraction of one CPU; 20 users with no caching queue behind it.
- Workflow end-to-end (27 s) is mostly waiting, not work: the agent steps total ~4 s per run, but the
  background runner processes one run at a time by design (so two plans never match the same stock at once),
  so with five started together the last waits for the four before it.
