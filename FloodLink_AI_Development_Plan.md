# FloodLink AI 

## AI-Coordinated Disaster & Flood Relief Resource Platform 

_Project Development Plan — SE3090 Assignment 1_ 

SLIIT — BSc (Hons) Information Technology, Software Engineering 

Duration: 31 July – 30 September 2026 (9 weeks) Group Size: 4 students — one primary component per student 

# 1. Project Overview 

FloodLink AI is a disaster relief resource coordination platform for Sri Lanka. Field volunteers report shelter needs (supplies, medical, capacity) through a Flutter mobile app, with GPS and photo evidence. A four-agent Agentic AI pipeline plans, matches, and routes supplies to shelters based on real inventory. A human coordinator reviews and approves every dispatch through a React web dashboard before any supplies are actually allocated or a truck is sent. The system closes the loop back to the field: volunteers see live status updates as their report moves from submission to approved dispatch. 

The system deliberately differs from a typical CRUD assignment because the AI's output — the response plan — changes shape with every disaster event, and the coordinator's approval step is a genuine high-impact decision, not a formality. 

## 1.1 Why this satisfies the assignment's core requirements 

- Four distinct agents, each with its own responsibility, input/output contract, and tool access — not four copies of the same prompt. 
- A real, defensible high-impact action requiring human approval: dispatching scarce relief supplies. 
- A cross-platform workflow that begins in Flutter, passes through ASP.NET Core, PostgreSQL, and all four agents, requires approval in React, and returns an updated status to the initiating Flutter user. 
- A meaningful third-party integration: a maps/routing API, used for a real purpose (ETA calculation), not a decorative widget. 
- React and Flutter serve genuinely different audiences — React is coordinator/admin-facing, Flutter is field/volunteer- facing. 

# 2. Team Structure & Component Ownership 

| **Student**  | **Primary Component**  | **Owned Agent**  | **Primary Client Focus**  |
| --- | --- | --- | --- |
| Member A - Sharani  | Field Intake & Shelter Reporting  | Triage / Planner Agent  | Flutter: report submission \| React: report review  |
| Member B - Eshini  | Inventory & Depot Management  | Logistics / Matching Agent  | React: inventory dashboard \| Flutter: stock check-in  |
| Member C - Sanka  | Agent Orchestration & Workflow Engine  | Route / ETA Agent  | React: workflow monitor \| Flutter: status tracker  |
| Member D - Ijini  | Validation, Approval & Dispatch Audit  | Validation / Safety Agent  | React: approval queue \| Flutter: delivery confirmation  |

 _Every member touches all five required layers (backend, database, React, Flutter, tests) for their own component, plus contributes one distinct agent to the shared pipeline — satisfying the 'no project-manager-only, testing-only, or documentation-only roles' rule._ 

# 3. Shared System Architecture (all members must understand this) 

- React and Flutter talk only to the ASP.NET Core Web API — never directly to each other, the database, or the AI agents. 
- The four agents run as an internal orchestrated pipeline, called by ASP.NET Core: Triage (A) -\> Matching (B) -\> Routing (C) -\> Validation (D) -\> human approval (React, Member D's endpoints). 
- All workflow and agent state is persisted in PostgreSQL (owned primarily by Member C's WorkflowRuns / AgentExecutionLog tables) so the process survives restarts and is fully auditable. 
- JWT authentication and role-based authorization (Volunteer, Coordinator, DepotManager, Admin) protect every endpoint — set up early, ideally in week 1-2, since every member's endpoints depend on it. 
- Agreed early integration points: the exact JSON shape of TriagePlan, AllocationProposal, Route, and ValidationResults must be agreed by the whole team before agent development starts in parallel (suggested: define these as shared DTOs in week 2). 

# 4. Individual Member Breakdown 

Each member's section below lists concrete, buildable sub-tasks across all required layers. 

## Member A — Field Intake & Shelter Reporting + Triage/Planner Agent 

_Owns everything related to how a need first enters the system: shelters, volunteer reports, photo/GPS evidence, and the Triage/Planner agent that turns raw reports into a prioritized, structured plan._ 

1. **Database — tables you own (PostgreSQL / EF Core)** 

- **Shelters** 

    - id (PK) 
    - name 
    - latitude, longitude 
    - capacity 
    - current\_occupancy 
    - contact\_volunteer\_id (FK -\> Users) 
    - status (Active/Closed) 
    - created\_at, updated\_at 

- **Reports** 

    - id (PK) 
    - shelter\_id (FK) 
    - reported\_by (FK -\> Users) 
    - need\_type (Water/Food/Medical/Shelter-Repair/Other) 
    - quantity\_needed 
    - urgency\_level (auto-set by agent) 
    - photo\_url 
    - gps\_lat, gps\_lng 
    - status (New/Triaged/InPlan/Resolved) 
    - created\_at 

- **TriagePlans** 

    - id (PK) 
    - generated\_from\_report\_ids (array/JSON) 
    - priority\_rank 
    - plan\_summary\_json 
    - created\_by\_agent\_run\_id (FK) 
    - created\_at 

2. **Backend API — endpoints you build (ASP.NET Core Web API)** 

- POST /api/shelters — create shelter record 
- GET /api/shelters?status=&search=&page= — list/search/filter/paginate shelters 
- GET /api/shelters/{id} — shelter detail incl. report history 
- PUT /api/shelters/{id} — update capacity/occupancy/status 
- POST /api/reports — submit a new field report (with photo upload + GPS) 
- GET /api/reports?shelterId=&status=&urgency=&sort= — search/filter/sort reports 
- GET /api/reports/{id} — single report detail 
- POST /api/reports/{id}/trigger-triage — kicks off the Triage/Planner agent run 
- **Business-specific operation (beyond CRUD): Auto-urgency scoring on report submission (combines people count, need type, and time-since-last-resupply into an initial severity value before the agent runs).** 

3. **React (Admin/Coordinator web app) — screens you build** 

- Shelters overview dashboard (map + list, occupancy %, status filters) 
- Report review queue (sortable by urgency, filter by shelter/need type) 
- Shelter detail page (full report history, timeline of triage decisions) 
- Triage plan viewer (shows the agent's prioritized list and reasoning before hand-off to logistics) 

4. **Flutter (Field/Mobile app) — screens you build** 

- Volunteer login / role-protected home screen 
- New Report form (need type, quantity, notes, validation) 
- Camera capture screen for shelter/damage photos 
- GPS auto-capture + manual pin-adjust map screen 
- My submitted reports list with live status (New -\> Triaged -\> InPlan -\> Resolved) 
- Shelter status view (occupancy, capacity, last update) 
- **Device feature: Camera (photo evidence) + GPS (auto location tagging) on report submission.** 

5. **Your Agent — Agentic AI contribution** 

- **Agent name: Triage / Planner Agent** 
- Responsibility: Reads new/unprocessed reports for a shelter or region, scores urgency, and produces a structured, prioritized multi-step response plan (which needs matter most and in what order). 
- Input contract: One or more Report records + shelter context (capacity, occupancy, recent history) as JSON. 
- Output contract: Structured TriagePlan JSON: ranked list of needs with priority scores and short justification per item, handed to the Logistics Agent (Member B). 
- Tools it may call (allow-listed): Read-only DB query tool (fetch reports/shelter history). No write access, no external APIs. 
- Validation / guardrails it enforces or is subject to: Priority score must fall within a defined 0-100 range; every plan item must reference a real report\_id (schema-checked before hand-off). 

6. **Testing you are responsible for** 

- Unit tests: urgency scoring function (edge cases — zero people, missing fields, extreme values) 
- Controller/API integration tests: report submission with/without photo, invalid GPS, unauthorized shelter update 
- React: report queue component tests, form validation tests, loading/error/empty states 
- Flutter: report form widget tests, camera/GPS permission-denied handling, offline submission queue test 
- Agent evaluation: golden-case test — given a fixed set of reports, does the Triage Agent rank them in the expected order 

7. **Git / individual evidence checklist** 

- Feature branches per screen/endpoint (e.g. feature/report-submission, feature/triage-agent) 
- PRs with at least one peer review comment before merge 
- Issues created and closed for each sub-task below 
- Commits spread across the 9 weeks — not bulk-committed near the deadline 

8. **Viva-readiness — be able to explain, modify, or debug:** 

- Why urgency scoring is deterministic (rule-based) rather than left entirely to the LLM 
- How a report flows from Flutter submission to appearing in the Triage Plan 
- How you'd modify the urgency formula to weight medical needs higher 
- What happens if the agent produces a plan referencing a report that was deleted mid-run 

## Member B — Inventory & Depot Management + Logistics/Matching Agent 

_Owns the supply side: depots, stock levels, reservations, and the Logistics Agent that matches a prioritized needs list against real, available inventory._ 

1. **Database — tables you own (PostgreSQL / EF Core)** 

- **Depots** 

    - id (PK) 
    - name 
    - latitude, longitude 
    - manager\_id (FK -\> Users) 
    - created\_at 

- **InventoryItems** 

    - id (PK) 
    - depot\_id (FK) 
    - item\_name 
    - unit (e.g. liters, kg, units) 
    - quantity\_available 
    - quantity\_reserved 
    - reorder\_threshold 
    - updated\_at 

- **AllocationProposals** 

    - id (PK) 
    - workflow\_run\_id (FK -\> Member C's WorkflowRuns) 
    - depot\_id (FK) 
    - shelter\_id (FK) 
    - item\_name 
    - quantity 
    - status (Proposed/Validated/Rejected) 
    - created\_at 

2. **Backend API — endpoints you build (ASP.NET Core Web API)** 

- POST /api/depots — register a new depot 
- GET /api/depots?search=&page= — list/search/paginate depots 
- POST /api/inventory — add/adjust stock item 
- GET /api/inventory?depotId=&lowStock=true&sort= — filter/sort inventory, low-stock report view 
- PUT /api/inventory/{id}/reserve — reserve stock against a pending allocation (atomic, transaction-wrapped) 
- PUT /api/inventory/{id}/release — release a reservation if a plan is rejected 
- GET /api/allocations?status=&shelterId= — search/filter proposed allocations 
- POST /api/allocations/{id}/trigger-matching — kicks off the Logistics/Matching agent run 
- **Business-specific operation (beyond CRUD): Stock reservation with transaction safety — reserving inventory for a proposed allocation must be atomic so two simultaneous plans can never over-allocate the same stock (use a DB transaction / row lock).** 

3. **React (Admin/Coordinator web app) — screens you build** 

- Inventory dashboard (per-depot stock levels, low-stock warnings, filters/sort/pagination) 
- Depot management screen (add/edit depot, assign manager) 
- Allocation proposals review list (what the Logistics Agent has matched, pending validation) 
- Stock reservation history / audit view 

4. **Flutter (Field/Mobile app) — screens you build** 

- Depot staff login / role-protected home 
- Stock check-in/adjust screen (receive new supplies, quick quantity update) 
- Low-stock alert list for the staff member's depot 
- Reservation confirmation screen (confirm physical stock matches a system reservation before dispatch) 
- **Device feature: Barcode/QR scanning for quick stock item lookup and check-in (or camera-based item photo logging if QR is out of scope).** 

5. **Your Agent — Agentic AI contribution** 

- **Agent name: Logistics / Matching Agent** 
- Responsibility: Takes the Triage Agent's prioritized needs list and proposes concrete allocations — which depot supplies which item/quantity to which shelter — based on current real inventory. 
- Input contract: TriagePlan JSON (from Member A's agent) + live inventory snapshot query. 
- Output contract: Structured AllocationProposal list (depot -\> shelter -\> item -\> quantity), passed to the Route/ETA Agent (Member C). 
- Tools it may call (allow-listed): Inventory read tool (query available stock per depot), a matching/optimization tool call — no direct write access to inventory (writes happen only after human approval, via the API). 
- Validation / guardrails it enforces or is subject to: Every proposed item/quantity must not exceed quantity\_available - quantity\_reserved at query time; unmatched needs must be explicitly flagged as unfulfillable rather than silently dropped. 

6. **Testing you are responsible for** 

- Unit tests: allocation matching logic (partial stock, zero stock, multiple depots for one item) 
- Database integration tests: reservation transaction — concurrent reservation attempts must not double-allocate 
- React: inventory table filter/sort tests, low-stock warning rendering, error state on failed reservation 
- Flutter: stock adjustment form validation, QR/camera permission handling 
- Agent evaluation: golden-case test — given fixed inventory + a fixed needs list, does the agent produce a mathematically valid (non-over-allocating) proposal 

7. **Git / individual evidence checklist** 

- Feature branches per screen/endpoint (e.g. feature/inventory-reserve, feature/logistics-agent) 
- PRs reviewed by at least one teammate before merge 
- Issues tracked for each sub-task below, closed with reference commits 
- Regular commits across the project timeline 

8. **Viva-readiness — be able to explain, modify, or debug:** 

- Why the reservation update must be a database transaction, and what breaks without one 
- How the Logistics Agent decides between two depots that both have partial stock 
- What the system does when a need cannot be fully matched 
- How you'd add a new item type without changing the agent's core logic 

## Member C — Agent Orchestration, Workflow Engine & Route/ETA Agent 

_Owns the backbone that ties all four agents together: the workflow state machine, shared persisted state, execution logging, and the Route/ETA agent that calls the external maps API._ 

1. **Database — tables you own (PostgreSQL / EF Core)** 

- **WorkflowRuns** 

    - id (PK) 
    - objective (text) 
    - current\_stage (Triage/Matching/Routing/Validating/PendingApproval/Approved/Rejected/Failed) 
    - plan\_json 
    - created\_at 
    - updated\_at 

- **AgentExecutionLog** 

    - id (PK) 
    - workflow\_run\_id (FK) 
    - agent\_name 
    - input\_json 
    - output\_json 
    - tool\_calls\_json 
    - duration\_ms 
    - status (Success/Error/Retried) 
    - created\_at 

- **Routes** 

    - id (PK) 
    - workflow\_run\_id (FK -\> WorkflowRuns) 
    - distance\_meters (int) 
    - estimated\_duration\_seconds (int) 
    - polyline\_string (text) 
    - created\_at 

2. **Backend API — endpoints you build (ASP.NET Core Web API)** 

- POST /api/workflows — start a new workflow run for a shelter/region objective 
- GET /api/workflows?status=&page= — list/search/filter/paginate workflow runs 
- GET /api/workflows/{id} — full workflow detail incl. every agent's input/output (observability) 
- GET /api/workflows/{id}/logs — execution log timeline for one run 
- POST /api/routes/calculate — trigger the Route/ETA agent for a given allocation proposal 
- GET /api/routes/{id} — route + ETA detail 
- GET /api/workflows/{id}/status — lightweight polling endpoint for Flutter status updates 
- POST /api/workflows/{id}/retry — retry a failed stage with retry-limit enforcement 
- **Business-specific operation (beyond CRUD): Stage-transition state machine enforcement — a workflow can only move Triage -\> Matching -\> Routing -\> Validating -\> PendingApproval -\> Approved/Rejected in that order; invalid transitions are rejected server-side.** 

3. **React (Admin/Coordinator web app) — screens you build** 

- Live workflow monitor (all active runs, current stage, progress indicator) 
- Workflow detail / execution trace viewer (each agent's input, output, timing — the audit trail) 
- Failed/retried workflows screen with error reasons 
- Route/ETA map preview embedded in the workflow detail view 

4. **Flutter (Field/Mobile app) — screens you build** 

- Workflow status tracker screen for volunteers ("Your report is being processed / matched / routed / awaiting approval / dispatched") 
- Push-style status update screen (polls /status endpoint, or notification on stage change) 
- Simple map view showing incoming supply route once approved 
- **Device feature: Local push notifications when a workflow status changes (e.g. "Supplies approved for dispatch — ETA 47 min").** 

5. **Your Agent — Agentic AI contribution** 

- **Agent name: Route / ETA Agent** 
- Responsibility: Given an allocation proposal (depot + shelter coordinates), calls the routing API to compute distance, route, and estimated arrival time, and attaches it to the plan before validation. 
- Input contract: AllocationProposal (depot lat/lng, shelter lat/lng) from Member B's agent output. 
- Output contract: Structured Route object (distance\_km, eta\_minutes, polyline) merged into the WorkflowRun's plan\_json, passed to the Validation Agent (Member D). 
- Tools it may call (allow-listed): External maps/routing API (OpenRouteService or Mapbox Directions) — allow-listed, called only with validated coordinate inputs, with timeout and retry-limit handling. 
- Validation / guardrails it enforces or is subject to: Reject/flag any route where distance or ETA is implausible (e.g. negative, zero, or exceeding a sane maximum) or where the API call fails after retries — routes to a safe-failure state rather than blocking silently. 

6. **Testing you are responsible for** 

- Unit tests: workflow stage-transition state machine (valid and invalid transitions) 
- API integration tests: routing endpoint with mocked maps API responses (success, timeout, invalid response) 
- React: execution trace rendering tests, loading/error states for long-running workflows 
- Flutter: status polling widget tests, notification trigger tests 
- Performance tests: concurrent workflow runs, response time under load, agent latency measurement 
- Agent evaluation: golden-case test for route calculation accuracy against known depot-shelter pairs; failure-injection test for maps API downtime (must produce a safe-failure state, not a crash) 

7. **Git / individual evidence checklist** 

- Feature branches per screen/endpoint (e.g. feature/workflow-state-machine, feature/route-agent) 
- PRs reviewed before merge, GitHub Actions CI workflow configured and passing (this student typically owns/co-owns the CI pipeline given the orchestration role) 
- Issues tracked and closed for each sub-task below 
- Regular commits across the project timeline, not concentrated near the deadline 

8. **Viva-readiness — be able to explain, modify, or debug:** 

- Why the workflow is modeled as an explicit state machine rather than implicit status flags 
- What happens end-to-end if the maps API is down during a demo 
- How the execution log supports the 'auditable result' requirement 
- How you'd add a fifth stage to the workflow without breaking existing runs 

## Member D — Validation, Approval & Dispatch Audit + Validation/Safety Agent 

_Owns the highest-stakes part of the system: deterministic safety checks, the coordinator approval workflow, and the audit trail proving supplies were actually dispatched correctly._ 

1. **Database — tables you own (PostgreSQL / EF Core)** 

- **Dispatches** 

    - id (PK) 
    - workflow\_run\_id (FK) 
    - allocation\_proposal\_id (FK) 
    - approved\_by (FK -\> Users) 
    - approval\_decision (Approved/Rejected/RevisionRequested) 
    - approval\_notes 
    - dispatched\_at 
    - created\_at 

- **ValidationResults** 

    - id (PK) 
    - workflow\_run\_id (FK) 
    - check\_name 
    - passed (bool) 
    - violation\_detail 
    - created\_at 

- **AuditTrail** 

    - id (PK) 
    - dispatch\_id (FK) 
    - event\_type 
    - event\_detail\_json 
    - actor\_id (FK -\> Users, nullable for system events) 
    - created\_at 

2. **Backend API — endpoints you build (ASP.NET Core Web API)** 

- POST /api/validations/run — trigger the Validation/Safety agent on a completed plan 
- GET /api/validations/{workflowRunId} — view validation results for a run 
- POST /api/dispatches/{workflowRunId}/approve — coordinator approves dispatch (role-protected) 
- POST /api/dispatches/{workflowRunId}/reject — coordinator rejects with reason 
- POST /api/dispatches/{workflowRunId}/request-revision — sends plan back a stage 
- GET /api/dispatches?status=&dateRange= — search/filter/paginate dispatch history 
- GET /api/audit/{dispatchId} — full audit trail for one dispatch (compliance/reporting view) 
- GET /api/dispatches/summary — reporting/analytics endpoint (totals dispatched, avg approval time, etc.) 
- **Business-specific operation (beyond CRUD): Approval enforcement — inventory is only decremented and a dispatch record only created after an authorized coordinator's explicit approval; the API must reject any attempt to dispatch a plan that hasn't passed validation or approval.** 

3. **React (Admin/Coordinator web app) — screens you build** 

- Coordinator approval queue (plans awaiting decision, with full plan/route/validation summary) 
- Approve / Reject / Request-Revision action screen with mandatory notes on rejection 
- Dispatch history + audit trail viewer (compliance reporting view) 
- Analytics dashboard (dispatch counts, approval turnaround time, rejection reasons) 

4. **Flutter (Field/Mobile app) — screens you build** 

- Volunteer-facing dispatch confirmation screen ("Approved — supplies en route") 
- Rejection/revision notice screen with coordinator's reason shown to the field team 
- Delivery confirmation screen (volunteer marks supplies received, optional photo proof) 
- **Device feature: Photo upload for delivery confirmation (proof-of-delivery evidence at the shelter end).** 

5. **Your Agent — Agentic AI contribution** 

- **Agent name: Validation / Safety Agent** 
- Responsibility: Performs the final deterministic checks on a complete plan (triage + allocation + route) before it is allowed to reach a human for approval — the last line of defense against unsafe or infeasible dispatches. 
- Input contract: Full WorkflowRun plan\_json (triage output + allocation proposal + route/ETA), current live inventory and depot capacity limits. 
- Output contract: Structured ValidationResults list (each check: pass/fail + detail); overall pass -\> status becomes PendingApproval, overall fail -\> status becomes Failed with a clear reason. 
- Tools it may call (allow-listed): Rule-based/schema validator (deterministic code, not an LLM call) — checks stock availability, truck/vehicle capacity limits, reserve-minimum thresholds, and coordinate sanity. 
- Validation / guardrails it enforces or is subject to: This agent IS the validation layer — it is intentionally rule-based rather than LLM-based, satisfying the spec's requirement that LLM-as-judge is not the sole evaluation method. 

6. **Testing you are responsible for** 

- Unit tests: every individual validation rule (stock check, capacity check, reserve-threshold check) with pass/fail cases 
- API integration tests: approval endpoint role-authorization (only Coordinator role can approve), reject-without-notes rejected by server 
- React: approval queue tests, action-confirmation flow tests, loading/error/empty states 
- Flutter: dispatch confirmation screen tests, photo upload widget test 
- Agent evaluation: golden-case test set including at least one deliberately unsafe plan (e.g. over-allocated stock) to confirm it is correctly blocked; prompt-injection resistance test (a malicious report description cannot cause the agent to bypass a validation rule) 
- End-to-end test: full Flutter -\> API -\> PostgreSQL -\> all four agents -\> React approval -\> status back to Flutter 

7. **Git / individual evidence checklist** 

- Feature branches per screen/endpoint (e.g. feature/approval-flow, feature/validation-agent) 
- PRs reviewed before merge; this student typically coordinates the end-to-end integration test given ownership of the final stage 
- Issues tracked and closed for each sub-task below 
- Regular commits across the project timeline, not concentrated near the deadline 

8. **Viva-readiness — be able to explain, modify, or debug:** 

- Why validation is rule-based rather than left to the LLM, and what risk that avoids 
- What exactly happens in the system if a coordinator rejects a plan 
- How the audit trail proves a dispatch was authorized and by whom 
- How you'd add a new safety rule (e.g. a maximum items-per-truck limit) without touching the other three agents 

# 5. Cross-Cutting / Shared Responsibilities 

These tasks don't belong to one member alone — agree who leads each, but everyone contributes. 

- GitHub repository setup, branch protection rules, and project board (week 1) — suggested lead: Member C (workflow/CI owner). 
- GitHub Actions CI workflow (restore, build, run backend tests on every PR) — suggested lead: Member C. 
- Shared DTO / contract definitions between agents (TriagePlan, AllocationProposal, Route, ValidationResults JSON shapes) — whole team, week 2. 
- ER diagram and full database schema review — whole team, week 2-3, before migrations are finalized. 
- Architecture Decision Record (ADR) — state management (React + Flutter), Agentic AI framework/orchestration choice, DB schema strategy for workflow state, cloud deployment platform. Suggested: each member drafts the ADR entry for the decision closest to their component, reviewed by the whole team. 
- End-to-end integration test (Flutter -\> API -\> PostgreSQL -\> all 4 agents -\> React approval -\> status back to Flutter) — suggested lead: Member D, with support from all. 
- Deployment: ASP.NET Core API + PostgreSQL to a cloud platform, React to a static host, Flutter APK build — assign one owner per target but verify together before submission. 
- Consolidated report assembly (Group Report + all Individual Reports + ADRs + AI usage declarations) — group leader coordinates, everyone submits their own section on time. 
- Individual AI usage logs — each member maintains their own throughout, not retroactively. 

# 6. Suggested 9-Week Timeline 

| **Week**  | **Focus**  | **Key Milestone**  |
| ---: | --- | --- |
| 1  | Setup  | Repo, CI skeleton, domain/roles finalized, ER diagram draft, auth scaffolding  |
| 2  | Foundations  | DB schema + migrations agreed, shared agent I/O contracts defined, JWT auth working  |
| 3-4  | Core CRUD + screens  | Each member's tables, endpoints, and basic React/Flutter screens functional  |
| 5  | Agent development starts  | Each member builds their agent in isolation against mocked inputs  |
| 6  | Pipeline integration  | Agents wired together end-to-end (Triage -\> Matching -\> Routing -\> Validation)  |
| 7  | Approval flow + third-party API  | Coordinator approval UI working, maps API integrated, human-in-the-loop tested  |
| 8  | Testing + deployment  | Full test suites, CI green, cloud deployment, APK build  |
| 9  | Polish + submission  | End-to-end demo rehearsal, ADRs, reports, AI logs, final consolidated PDF, submission  |

# 7. Definition of Done (per member, before week 9) 

- All your endpoints return correct status codes and are documented in Swagger. 
- Your React and Flutter screens handle loading, empty, success, and error states. 
- Your agent runs reliably against at least 3 golden-case scenarios, including one failure case. 
- Your tests pass in CI, not just locally. 
- You can explain, modify, and debug every part of your component without notes. 
- Your Git history shows regular, real commits across the 9 weeks — not a final-week dump. 
