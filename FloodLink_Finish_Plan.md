# FloodLink AI — Finish Plan (8 → 12 October 2026)

Deadline extended to **Monday 12 October 2026**. Submit by early evening, not at 11:50 PM.
Repository, demo video and all deployed services must stay live until **21 October** (spec §15).

This plan is written against the SE3090 spec and the rubric (§16.1), in order of marks at risk.
Everything in "Cut" is deliberately dropped; do not start it.

---

## 0. Where we are (verified in code, 8 Oct)

| Area | State |
|---|---|
| DB, CRUD, migrations, seed | Done |
| Four agents | All plain C#, **no LLM**. Triage = rule score + template text; Matching = greedy loop; Route = Mapbox (first allocation only); Validation = rules (correct as is) |
| Orchestrator + state machine | Built, tested only with fake agents |
| **Pipeline** | **Broken**: nothing advances a run past Triage; Validation expects a `PlanDocument` that nothing builds, so every run would fail |
| Approve | Writes Dispatch + audit row only. Stock is never reserved or decremented |
| Auth | **None**: no Users table, no login, no password hashing. Dev-only fake coordinator |
| React | Tab-switch navigation, no router, no login, no workflow monitor |
| Flutter | No login, no workflow status screen |
| Tests | Backend + CI fine. No agent evals, injection test, React/Flutter tests, performance test |
| Docs / deploy | Dockerfile + hosting config exist. Report is a draft. No ADR, no demo video |

## 1. Rubric priorities

| Criterion | Marks | What decides it |
|---|---|---|
| Agentic AI contribution | 12 (individual) | Real prompt, contract, allow-listed tools, validation, tests, viva explanation |
| Orchestration, agents, state | 10 (group) | Planning + delegation, persisted state, safe failure, approval |
| Documentation + deployment | 10 (group) | Consolidated PDF, ADRs, AI logs, live URLs, APK |
| API integration, security, cross-platform | 10 | Same API for both clients, login by role, approvals |
| Component design / business logic | 10 | Working end-to-end workflow |
| Testing, CI, Git | 8 | Tests across layers + agent evals, CI green, regular commits |

Demo checklist (§17.1) starts with "login using different roles", so auth is not optional.

## 2. Design decisions

### 2.1 LLM: Gemini via plain `HttpClient`
- One `ILlmClient` in Infrastructure; `GeminiClient` calls the REST `generateContent` endpoint. No SDK.
- JSON mode with a response schema for every call, temperature 0, timeout 20 s, max 2 retries.
- Model name in config (`Gemini:Model`, use a Flash-class model that your AI Studio key lists), key in `Gemini__ApiKey`.
- A fake `ILlmClient` is used in all CI tests. Live Gemini runs are a separate, excluded test category.
- **The Gemini AI Pro student subscription is not the API.** Create an API key in Google AI Studio and check its free-tier rate limits. The key goes in user-secrets / env vars only. **Never paste it into any AI tool or commit it** (spec §18.2).
- Free-tier prompts may be used by Google to improve its products (check current terms). Send only IDs, need types, quantities and short sanitised notes. No names, phone numbers or photos (spec §11 data minimisation).
- Orchestration framework for the ADR: **custom C# orchestrator** (state machine + invoker interfaces + execution log). "Custom orchestration" is explicitly allowed in §2.

### 2.2 Which agents use the LLM

| Agent | LLM? | Tools (allow-list) | Output contract | Deterministic guardrail | Fallback |
|---|---|---|---|---|---|
| **Planner** (coordinator step, new, in orchestrator) | Yes | none | `PlanSteps[]`: agent, purpose, rationale | Agents must be exactly Triage, Matching, Routing, Validation in order; max 4 steps; rationale ≤ 200 chars | Default fixed plan |
| **Triage** | Yes | `get_shelter_context(shelterId)` read-only | per report: `reportId`, `adjustment` (−15..+15), `justification` | `reportId` must be in input, final score clamped 0–100, adjustment bounded | Rule score + template text |
| **Matching** | Yes | `get_inventory(itemName)` read-only | allocations: depot, shelter, item, qty | qty ≤ available − reserved, depot/item must exist, every need allocated or listed as unfulfillable | Existing greedy matcher |
| **Route/ETA** | No | `mapbox_directions` (validated coordinates) | distance, ETA, polyline per route | Plausibility checks, timeout/retry, safe failure | n/a |
| **Validation** | **No, by design** | none | check list + overall pass/fail | It *is* the guardrail (spec §9, §12) | n/a |

Every LLM call is written to `AgentExecutionLog.tool_calls_json` (prompt version, model, latency, tokens, `usedFallback`). The trace screen shows `usedFallback` honestly; do not hide it.

Prompt-injection stance: report notes are untrusted data, passed inside delimiters, never as instructions; outputs are schema-checked; adjustments are bounded; Validation re-checks stock regardless of what the LLM says. If `Report` has no free-text `Notes` field, add one (max 500 chars) so the injection test is real.

### 2.3 Pipeline shape
- `POST /api/workflows` (Coordinator/Admin) takes `objective` + `reportIds`, creates the run, returns **202** with the run id.
- A hosted background runner (channel-backed `BackgroundService`) advances the run: Planner → Triage → Matching → Routing → Validating → **PendingApproval** (or Failed with reason). Flutter/React poll status.
- `PlanJson` is merged by key only (`plan`, `triagePlan`, `allocationProposal`, `routes`, `validationResults`). Triage must stop overwriting it.
- A `PlanDocumentBuilder` (before Validation) builds the `PlanDocument` from the merged plan + live inventory + depot/shelter coordinates + configured vehicle capacity.
- Route agent routes **every distinct depot → shelter pair**, not just the first.
- Stock lifecycle (Member B's transaction): validation passes → **reserve** stock (`QuantityReserved += qty`); approve → **commit** (`QuantityAvailable −= qty`, reserved −= qty) in one DB transaction; reject / fail → **release**. Revision re-runs Matching with the coordinator's note as input.
- Every state change and decision writes `AgentExecutionLog` / `AuditTrail`.

### 2.4 Auth
- `Users` table (Id, Name, Email unique, PasswordHash, Role, CreatedAt). Hash with ASP.NET `PasswordHasher<T>`.
- `POST /api/auth/register` (Volunteer only), `POST /api/auth/login` (JWT), `GET /api/auth/me`.
- Roles: Volunteer, Coordinator, DepotManager, Admin. Apply `[Authorize(Roles=…)]` per controller; `Report.ReportedBy` comes from the token.
- Remove the Development-only fake-coordinator fallback. Fix `TestAppFactory` to issue real tokens.
- Seed four documented test accounts (passwords from config; list them in the README, spec §14.1).
- React: Context API (`AuthContext`) + React Router + `ProtectedRoute` + role-based nav. Flutter: Provider + `flutter_secure_storage`.

## 3. Work split (everyone keeps their own agent, spec §3)

| Member | Owns this week |
|---|---|
| **A – Sharani** | Triage LLM agent + prompt + golden tests; Flutter login/logout/secure storage; link reports to workflow (`Report.WorkflowRunId`) and `GET /api/reports/mine`; Flutter status screen |
| **B – Eshini** | Matching LLM agent + tool + fallback; reserve / commit / release transaction + endpoints; concurrency test (two plans, one stock); inventory authorization |
| **C – Sanka** | `ILlmClient`/Gemini, Planner step, background runner, workflow endpoints (list, status, logs, retry), Route agent for all pairs, React workflow monitor + start-workflow, CI, deployment |
| **D – Ijini** | Users/auth backend, `PlanDocumentBuilder`, approval roles + revision notes, React login/router/protected routes, approval UI polish, end-to-end test, validation rule additions |

Each member must still have ≥ 4 endpoints and one business operation beyond CRUD (spec §5). Commit under your own name, in small commits, with PRs reviewed by a teammate.

## 4. Day-by-day

### Thu 8 Oct — Foundation (merge to `main` by tonight)
- C: `ILlmClient` + `GeminiClient` + fake; background runner; `POST /api/workflows` → 202; fix Triage `PlanJson` overwrite. 
- D: Users table + migration + login/register/me + role policies; `PlanDocumentBuilder`.
- A: Triage prompt + schema draft; add `Notes` / `WorkflowRunId` to `Report` if missing.
- B: reserve / commit / release service with transaction (no LLM yet).
- **Gate:** with rule-based fallbacks only, one run reaches PendingApproval through the API and can be approved with stock changing.

### Fri 9 Oct — Agents + UI
- A: Triage LLM live; Flutter login.
- B: Matching LLM live with tool and fallback.
- C: Planner step; Route all pairs; React workflow monitor + trace viewer + start-workflow button.
- D: React login, router, protected routes, role nav; approval UI with revision notes.
- **Gate:** React → real Gemini → PendingApproval → approve, with the trace visible.

### Sat 10 Oct — Close the loop, tests, deploy
- A + C: Flutter report → status tracker (poll) → shows Approved/Rejected/Revision with the coordinator note.
- All: agent golden cases (≥ 3 each, one failure), prompt-injection test, unauthorized-approval test, LLM-down fallback test; React (≈ 6) and Flutter (≈ 6) tests; CI green.
- C/D/A: deploy API + Postgres, React, build APK. Set env vars: `ConnectionStrings__DefaultConnection`, `Jwt__*`, `Mapbox__ApiKey`, `Gemini__ApiKey`, `Cors__AllowedOrigins`, `Database__MigrateOnStartup`.
- **Gate:** APK on a phone logs in against the deployed API; React approves; phone sees the status.

### Sun 11 Oct — Evidence + documents (feature freeze at noon)
- Run live Gemini golden cases and record the real results; run the performance test and record real numbers (§18.2: no invented results).
- Performance: k6 (or similar) with ~20 concurrent users on read endpoints + 5 concurrent workflow starts; record p50/p95, success rate, agent latency from `duration_ms`.
- Write: ER diagram, README (§14.1), 5 ADRs, group report sections, individual sections.
- Each member: AI usage log (what was actually used and changed) and their own one-page reflection.
- Record a rehearsal of the demo.

### Mon 12 Oct — Buffer and submit
- Fix only blockers. Final PDF (one file, group + 4 individual sections), demo video (10 min, "anyone with link"), APK, links.
- Open every link in a private window. Name items `SE3090_G<number>`. Group leader submits by early evening.

## 5. Tests that must exist (spec §12)

- **Backend:** auth (login, wrong password, role denied), approve only by Coordinator, state-machine transitions, reserve/commit/release, concurrent reservation, orchestrator end-to-end with fake LLM on Postgres.
- **Agent evaluation:** golden case per agent (planning/delegation correct, structured output valid, over-allocation blocked by Validation, approval enforced); prompt-injection (hostile report note + hostile LLM output cannot change stock or skip validation); LLM failure → fallback → run still completes; Mapbox failure → safe Failed state.
- **React:** `ProtectedRoute`, login validation, approval queue empty/error/loading, workflow trace render.
- **Flutter:** login validation, report form validation, status screen states, navigation guard.
- **Performance:** as above.
- **CI:** backend job runs everything except the `Live` category; web build + `flutter analyze`/`flutter test` jobs if not already present.

## 6. Documents checklist

- [ ] ADRs: React state (Context API), Flutter state (Provider), agent framework (custom C# orchestrator + Gemini), workflow-state schema (`WorkflowRuns.PlanJson` jsonb + `AgentExecutionLog`), deployment platform
- [ ] ER diagram + schema
- [ ] README with install, env var names, test accounts, startup order, live URLs
- [ ] Agentic AI evaluation report (real results)
- [ ] Performance report (real numbers)
- [ ] Testing report, deployment report, security notes
- [ ] Individual sections ×4: contribution statement, commits/PRs/tests, challenges, AI log, ~1-page reflection in own words, signed declaration
- [ ] Consolidated group AI declaration
- [ ] Demo video (10 min), APK + install notes, links verified in incognito

## 7. Cut (do not build)

Photo/camera work beyond what exists, local push notifications, route polyline map preview, LLM-written coordinator brief, QR scanning, analytics polish, extra React Router depth, full performance suite.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Gemini free-tier rate limit or outage during demo | Fallbacks to rule-based logic, `usedFallback` visible; rehearse; keep a recorded successful run |
| Tool-calling loop takes too long to build | Fall back to inventory/shelter snapshot in the prompt; still log it as a tool result |
| Deployment surprises on Sat | Start deployment Thursday with a "hello" health check |
| Merge conflicts across four people | Small PRs into `main`, rebase daily, one owner per file area |
| Report numbers not matching reality | Only use results produced by running the tests/scripts |

## 9. Definition of done for the demo

1. Log in as each role (Volunteer in Flutter; Coordinator and DepotManager in React).
2. Volunteer submits a report in Flutter.
3. Coordinator starts a workflow in React; trace shows Planner → Triage → Matching → Routing → Validation with timings, tool calls and fallback flags.
4. Plan stops at PendingApproval; an unsafe plan is shown being blocked.
5. Coordinator approves; stock changes in Postgres; audit trail shows the actor.
6. Flutter shows the updated status.
7. Show Swagger, CI green, tests, deployed URLs and GitHub history.
