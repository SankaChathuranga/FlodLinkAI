# FloodLink AI — Full Build Specification & Task Plan

**Purpose of this document:** This is a self-contained build spec for an AI-coordinated disaster/flood relief resource platform (SLIIT SE3090 Assignment 1). Give this whole document to an AI assistant (Claude, etc.) at the start of a work session so it has full project context without needing anything else. It contains: the shared architecture and contracts every component must follow, and a day-by-day, week- by-week checklist of what to build, broken down to the smallest topic.

0. **Instructions for the AI reading this document**

- This is a 4-person team project. Each person owns one of four components: **Member A** (Field Intake & Triage Agent), **Member B** (Inventory & Logistics Agent), **Member C** (Orchestration & Route Agent), **Member D** (Validation, Approval & Audit).
- Before writing any code for a given member's component, read **Section 3 (Shared Contracts)** in full — table schemas, API conventions, and agent JSON shapes are fixed and must not be changed unilaterally. If a change seems necessary, flag it explicitly rather than silently deviating.
- Work through the relevant member's section in **Section 5–8** in order (Week 1 → Week 9). Each task is a checklist item ( `- [ ]` ). Mark items `- [x]` as they're completed, and keep this file updated so it reflects real progress — treat it as the source of truth for "what's done" across sessions.
- Tasks are intentionally small and specific. Don't skip ahead or batch multiple tasks into one unless the person explicitly asks for that.
- When a task involves a decision not fully specified here (e.g. exact validation rule thresholds, specific UI copy), make a reasonable choice, state the assumption made, and move on — don't block on it.
- Mandatory tech stack (do not substitute): **ASP.NET Core Web API (C#), PostgreSQL + Entity** **Framework Core, React (functional components/hooks), Flutter/Dart, LangGraph (or** **an equivalent agent orchestration framework).**

1. **Project Summary**

**FloodLink AI** is a disaster relief resource coordination platform for Sri Lanka. Field volunteers report shelter needs (supplies, medical, capacity) through a Flutter mobile app, with GPS and photo evidence. A four-agent Agentic AI pipeline plans, matches, and routes supplies to shelters based on real inventory. A human coordinator reviews and approves every dispatch through a React web dashboard before any supplies are actually allocated or a truck is sent. The system closes the loop back to the field: volunteers see live status updates as their report moves from submission to approved dispatch.

**Core end-to-end workflow:**

    1. Volunteer submits a shelter needs report via Flutter (GPS + photo).

2. **Triage/Planner Agent** (Member A) scores urgency and produces a prioritized plan.
3. **Logistics/Matching Agent** (Member B) matches needs against real inventory, proposes allocations.
4. **Route/ETA Agent** (Member C) calls a maps API to compute distance/ETA for the proposal.
5. **Validation/Safety Agent** (Member D) runs deterministic checks (stock, capacity, thresholds).
6. A coordinator reviews the full plan in React and **approves / rejects / requests revision**.
7. On approval: inventory is decremented, a dispatch record is created, status updates flow back to the originating Flutter user.

<div></div>

2. **Technology Stack**

| **Layer** | **Technology** |
| --- | --- |
| Backend | C#, ASP.NET Core Web API |
| Database | PostgreSQL, Entity Framework Core (Npgsql provider) |
| Web App | React (functional components, hooks, React Router) |
| Mobile App | Flutter, Dart |
| Agentic AI | LangGraph (or justified equivalent) + LLM with structured/JSON output |
| Auth | JWT, role-based authorization |
| Maps/Routing | OpenRouteService or Mapbox Directions API (free tier) |
| CI/CD | GitHub Actions |

**Roles:** Field Volunteer, Coordinator, Depot/Warehouse Manager, Admin.

3. **Shared Contracts (fixed — do not change without whole-**

## team agreement)

### 3.1 Database Schema (all tables, across all 4 components)

Users

 id (PK), name, role, phone, hashed\_password, created\_at

Shelters \[owned by Member A\]

 id (PK), name, latitude, longitude, capacity, current\_occupancy,

 contact\_volunteer\_id (FK -\> Users), status (Active/Closed), created\_at, updated\_at

Reports \[owned by Member A\]

 id (PK), shelter\_id (FK), reported\_by (FK -\> Users),

 need\_type (Water/Food/Medical/Shelter-Repair/Other), quantity\_needed,

 urgency\_level (auto-set), photo\_url, gps\_lat, gps\_lng,

 status (New/Triaged/InPlan/Resolved), created\_at

TriagePlans \[owned by Member A\]

 id (PK), generated\_from\_report\_ids (JSON array), priority\_rank,

 plan\_summary\_json, created\_by\_agent\_run\_id (FK), created\_at

Depots \[owned by Member B\]

 id (PK), name, latitude, longitude, manager\_id (FK -\> Users), created\_at

InventoryItems \[owned by Member B\]

 id (PK), depot\_id (FK), item\_name, unit, quantity\_available,

 quantity\_reserved, reorder\_threshold, updated\_at

AllocationProposals \[owned by Member B\]

 id (PK), workflow\_run\_id (FK), depot\_id (FK), shelter\_id (FK),

 item\_name, quantity, status (Proposed/Validated/Rejected), created\_at

WorkflowRuns \[owned by Member C\]

 id (PK), objective, current\_stage

 (Triage/Matching/Routing/Validating/PendingApproval/Approved/Rejected/Failed),

 plan\_json, created\_at, updated\_at

AgentExecutionLog \[owned by Member C\]

 id (PK), workflow\_run\_id (FK), agent\_name, input\_json, output\_json,

 tool\_calls\_json, duration\_ms, status (Success/Error/Retried), created\_at

Routes \[owned by Member C\]

 id (PK), allocation\_proposal\_id (FK), origin\_lat, origin\_lng, dest\_lat, dest\_lng, distance\_km, eta\_minutes, route\_polyline, created\_at

Dispatches \[owned by Member D\] id (PK), workflow\_run\_id (FK), allocation\_proposal\_id (FK),

 approved\_by (FK -\> Users), approval\_decision (Approved/Rejected/RevisionRequested), approval\_notes, dispatched\_at, created\_at

ValidationResults \[owned by Member D\] id (PK), workflow\_run\_id (FK), check\_name, passed (bool),

 violation\_detail, created\_at

AuditTrail \[owned by Member D\] id (PK), dispatch\_id (FK), event\_type, event\_detail\_json,

 actor\_id (FK -\> Users, nullable), created\_at

### 3.2 API Conventions

- Base route pattern: /api/{resource}
- Auth: JWT bearer token on every protected endpoint; \[Authorize(Roles = "...")\] per role requirement.
- List endpoints support ?search=&filter=&sort=&page=&pageSize= query params where applicable.
- Standard status codes: 200 OK, 201 Created, 400 validation error, 401 unauthenticated, 403 unauthorized role, 404 not found, 500 unhandled (via global exception middleware).
- All write endpoints validate input server-side (DataAnnotations or FluentValidation) regardless of client-side validation.

### 3.3 Agent JSON I/O Contracts (chain: A → B → C → D)

// Output of Triage/Planner Agent (Member A) — input to Member B TriagePlan {

 "workflowRunId": "guid", "priorityItems": \[

 { "reportId": "int", "shelterId": "int", "needType": "string", "quantity": "number", "priorityScore": "0-100", "justification": "string" } \]

}

// Output of Logistics/Matching Agent (Member B) — input to Member C AllocationProposal {

 "workflowRunId": "guid", "allocations": \[

 { "depotId": "int", "shelterId": "int", "itemName": "string", "quantity": "numbe r" }

 \], "unfulfillable": \[ { "shelterId": "int", "itemName": "string", "reason": "string" } \]

}

// Output of Route/ETA Agent (Member C) — input to Member D Route {

 "workflowRunId": "guid", "allocationProposalId": "int",

 "distanceKm": "number", "etaMinutes": "number", "polyline": "string" }

// Output of Validation/Safety Agent (Member D) — feeds coordinator approval UI ValidationResults {

 "workflowRunId": "guid", "checks": \[ { "checkName": "string", "passed": "bool", "violationDetail": "string\|n ull" } \],

 "overallPassed": "bool" }

### 3.4 Workflow State Machine (owned by Member C, used by all)

```
Triage -> Matching -> Routing -> Validating -> PendingApproval -> Approved | Rejected                                                                 -> RevisionRequested (loops back)
Any stage -> Failed (on unrecoverable error, with a recorded reason)
```

Invalid transitions must be rejected server-side.

4. **Architecture Notes**

- React and Flutter **only** talk to the ASP.NET Core Web API — never to each other, the database, or agents directly.
- If Agentic AI is implemented as a Python service (e.g. LangGraph via FastAPI), it is called internally by ASP.NET Core only — never directly by React or Flutter.
- All agent/workflow state is persisted in PostgreSQL so the process survives restarts and is fully auditable.
- The Validation/Safety Agent (Member D) is **deterministic, rule-based code** — not an LLM call — per the assignment's requirement that LLM-as-judge cannot be the sole evaluation method.

5. **Build Plan — Member A: Field Intake & Shelter Reporting +** **Triage/Planner Agent**

**Owns:** `Shelters` , `Reports` , `TriagePlans` tables. **Agent:** Triage/Planner. **Endpoints to build:**

- `POST /api/shelters` — create shelter
- `GET /api/shelters?status=&search=&page=` — list/search/filter/paginate
- `GET /api/shelters/{id}` — detail incl. report history
- `PUT /api/shelters/{id}` — update capacity/occupancy/status
- `POST /api/reports` — submit report (photo upload + GPS)
- `GET /api/reports?shelterId=&status=&urgency=&sort=` — search/filter/sort
- `GET /api/reports/{id}` — detail
- `POST /api/reports/{id}/trigger-triage` — kicks off Triage Agent run

### Week 1 — Foundations & Learning

- Learn C# fundamentals: variables, classes, methods, properties
- Learn C# async/await and LINQ basics
- Learn ASP.NET Core project structure: `Program.cs` , controllers, DI container
- Draft `Shelters` and `Reports` schema in detail (confirm against Section 3.1)
- Learn EF Core basics: `DbContext` , models, migrations workflow
- Learn Dart basics: syntax, null safety, async/await, futures
- Learn Flutter basics: widget tree, StatelessWidget vs StatefulWidget, hot reload

### Week 2 — Schema & Scaffolding

- Finalize `Shelters` / `Reports` schema with team; confirm no drift from Section 3.1
- Agree `TriagePlan` JSON contract with Member B (confirm against Section 3.3)
- Implement `Shelters` table (EF Core model + migration)
- Implement `Reports` table (EF Core model + migration) + seed data
- Study JWT authentication concepts (shared team task — auth setup usually led by one member)
- Scaffold `SheltersController` (empty action methods, routing)
- Scaffold `ReportsController` (empty action methods, routing)

### Week 3 — Backend CRUD

- Implement `POST /api/shelters`
- Implement `GET /api/shelters` (list, search, filter, pagination)
- Implement `GET /api/shelters/{id}` and `PUT /api/shelters/{id}`
- Implement `POST /api/reports` (with photo upload handling)
- Implement `GET /api/reports` (filter by shelter/status/urgency, sort)
- Implement `GET /api/reports/{id}`
- Implement auto-urgency scoring business logic (rule-based, runs on report submission — combines people count, need type, time-since-last-resupply)

### Week 4 — React & Flutter Screens

- Build Shelters overview dashboard (map + list, occupancy %, status filters) — React
- Build Report review queue screen (sortable by urgency, filterable) — React
- Learn Flutter navigation/routing ( `go_router` package)
- Build New Report form screen (need type, quantity, notes, validation) — Flutter
- Build camera capture screen for photo evidence ( `image_picker` ) — Flutter
- Build GPS auto-capture + manual pin-adjust map screen ( `geolocator` ) — Flutter

### Week 5 — Triage/Planner Agent

- Learn agentic AI fundamentals: planning, delegation, structured output
- Learn LangGraph basics: nodes, edges, state graphs
- Design Triage/Planner agent prompt + confirm I/O matches Section 3.3 `TriagePlan`
- Implement rule-based urgency scoring component of the agent
- Implement LLM call for plan generation with structured JSON output parsing
- Test Triage Agent against 3+ sample report sets (golden cases)

### Week 6 — Pipeline Integration

- Build `POST /api/reports/{id}/trigger-triage` endpoint
- Connect Triage Agent output to `WorkflowRun` creation (coordinate with Member C)
- Build Triage plan viewer screen (prioritized list + reasoning) — React
- Integration test: report submission produces a triage plan
- Refine/debug triage scoring logic based on test results
- Build "My submitted reports" status list screen (live status) — Flutter

### Week 7 — Full Pipeline & Polish

- Support full 4-agent pipeline integration testing
- Refine shelter detail page (full report history timeline) — React
- Add loading/empty/error states to Shelters & Reports screens (React + Flutter)
- Cross-review teammates' PRs touching Reports/Shelters data
- Write unit tests for urgency scoring function (edge cases: zero people, missing fields)
- Write API integration tests for report submission validation

### Week 8 — Testing & Deployment

- Complete backend test suite for your component; confirm CI green
- Write React component/form validation tests
- Write Flutter widget tests (form validation, camera/GPS permission denial)
- Build Triage Agent golden-case evaluation suite (include one failure case)
- Verify your endpoints work correctly against the deployed API
- Fix any deployment-related bugs in your component

### Week 9 — Finalization

- Full end-to-end demo rehearsal — your part
- Write Individual Report section (contribution, evidence, challenges)
- Write AI usage log entries and one-page reflection
- Review/contribute to ADR entries related to your component
- Final bug bash and UI polish
- Submission day support — verify links, final checks

6. **Build Plan — Member B: Inventory & Depot Management +** **Logistics/Matching Agent**

**Owns:** `Depots` , `InventoryItems` , `AllocationProposals` tables. **Agent:** Logistics/Matching. **Endpoints to build:**

- `POST /api/depots` — register depot
- `GET /api/depots?search=&page=` — list/search/paginate
- `POST /api/inventory` — add/adjust stock item
- `GET /api/inventory?depotId=&lowStock=true&sort=` — filter/sort, low-stock view
- `PUT /api/inventory/{id}/reserve` — reserve stock (atomic, transaction-wrapped)
- `PUT /api/inventory/{id}/release` — release a reservation
- `GET /api/allocations?status=&shelterId=` — search/filter proposed allocations
- `POST /api/allocations/{id}/trigger-matching` — kicks off Logistics Agent run

### Week 1 — Foundations & Learning

- Learn C# fundamentals: classes, LINQ, async/await
- Learn ASP.NET Core controllers and dependency injection
- Draft `Depots` and `InventoryItems` schema in detail (confirm against Section 3.1)
- Learn EF Core relationships (1:many) and migrations
- Learn Dart basics: syntax, null safety, async/await
- Learn Flutter basics: widgets, state, layout

### Week 2 — Schema & Scaffolding

- Finalize `Depots` / `InventoryItems` schema with team; confirm no drift from Section 3.1
- Agree `AllocationProposal` JSON contract with Members A and C (confirm against Section 3.3)
- Implement `Depots` table (EF Core model + migration)
- Implement `InventoryItems` table (EF Core model + migration) + seed data
- Study JWT authentication concepts (shared team task)
- Scaffold `DepotsController` (empty action methods, routing)
- Scaffold `InventoryController` (empty action methods, routing)

### Week 3 — Backend CRUD

- Implement `POST /api/depots` and `GET /api/depots` (search, paginate)
- Implement `POST /api/inventory` (add/adjust stock item)
- Implement `GET /api/inventory` (filter/sort, low-stock report view)
- Learn database transactions and row-locking in EF Core
- Implement `PUT /api/inventory/{id}/reserve` (atomic, transaction-wrapped — must prevent double-allocation under concurrent requests)
- Implement `PUT /api/inventory/{id}/release` (undo a reservation)
- Implement `GET /api/allocations` (search/filter proposed allocations)

### Week 4 — React & Flutter Screens

- Build Inventory dashboard (per-depot stock, low-stock warnings, filters) — React
- Build Depot management screen (add/edit depot, assign manager) — React
- Learn Flutter forms and validation patterns
- Build stock check-in/adjust screen — Flutter
- Learn QR/barcode scanning package ( `mobile_scanner` ) or camera-based logging
- Build low-stock alert list screen for depot staff — Flutter

### Week 5 — Logistics/Matching Agent

- Review agentic AI fundamentals + review Triage Agent's output contract (Section 3.3)
- Learn LangGraph tool-calling / structured-output patterns
- Design Logistics/Matching agent; confirm I/O matches Section 3.3 `AllocationProposal`
- Implement rule-based allocation matching logic (depot → shelter → item/qty)
- Implement LLM-assisted allocation reasoning + structured output parsing
- Test Logistics Agent against sample inventory + needs list (include partial-stock and zero-stock cases)

### Week 6 — Pipeline Integration

- Build `POST /api/allocations/{id}/trigger-matching` endpoint
- Connect Logistics Agent to `WorkflowRun` pipeline (coordinate with Member C)
- Build Allocation proposals review list screen — React
- Integration test: triage plan produces an allocation proposal
- Refine/debug matching logic based on test results
- Build reservation confirmation screen (confirm physical stock) — Flutter

### Week 7 — Full Pipeline & Polish

- Support full 4-agent pipeline integration testing
- Add stock reservation history / audit view — React
- Add loading/empty/error states to Inventory & Depot screens (React + Flutter)
- Cross-review teammates' PRs touching inventory data
- Write unit tests for allocation matching logic (edge cases)
- Write DB integration tests for reservation transaction (concurrency test — two simultaneous reservations must not over-allocate)

### Week 8 — Testing & Deployment

- Complete backend test suite for your component; confirm CI green
- Write React inventory table filter/sort/error-state tests
- Write Flutter stock form validation + QR/camera permission tests
- Build Logistics Agent golden-case evaluation suite (include one failure case)
- Verify your endpoints work correctly against the deployed API
- Fix any deployment-related bugs in your component

### Week 9 — Finalization

- Full end-to-end demo rehearsal — your part
- Write Individual Report section (contribution, evidence, challenges)
- Write AI usage log entries and one-page reflection
- Review/contribute to ADR entries related to your component
- Final bug bash and UI polish
- Submission day support — verify links, final checks

7. **Build Plan — Member C: Agent Orchestration, Workflow** **Engine + Route/ETA Agent**

**Owns:** `WorkflowRuns` , `AgentExecutionLog` , `Routes` tables. **Agent:** Route/ETA. **Also leads:** GitHub repo/CI setup, overall pipeline wiring, deployment.

**Endpoints to build:**

- `POST /api/workflows` — start workflow run
- `GET /api/workflows?status=&page=` — list/search/filter/paginate
- `GET /api/workflows/{id}` — full detail (all agent input/output — observability)
- `GET /api/workflows/{id}/logs` — execution log timeline
- `POST /api/routes/calculate` — trigger Route/ETA agent
- `GET /api/routes/{id}` — route + ETA detail
- `GET /api/workflows/{id}/status` — lightweight polling endpoint for Flutter
- `POST /api/workflows/{id}/retry` — retry a failed stage with retry-limit enforcement

### Week 1 — Foundations & Learning

- Learn C# fundamentals: classes, interfaces, async/await
- Learn ASP.NET Core middleware pipeline and background services
- Draft `WorkflowRuns` , `AgentExecutionLog` , `Routes` schema (confirm against Section 3.1)
- Learn EF Core migrations + storing JSON columns ( `jsonb` ) in PostgreSQL
- Learn Dart basics: syntax, null safety, async/await
- Learn Flutter basics; skim GitHub Actions YAML syntax (you own CI)

### Week 2 — Schema, Repo & CI Setup

- Lead schema review meeting; finalize `WorkflowRuns` /logging tables against Section 3.1
- Lead agreement on all 4 agents' JSON I/O contracts with the team (confirm against Section 3.3)
- Implement `WorkflowRuns` table (EF Core model + migration)
- Implement `AgentExecutionLog` and `Routes` tables + migration
- Set up GitHub repository, branch protection rules, project board
- Write initial GitHub Actions CI workflow (restore, build, run backend tests on PR)
- Scaffold `WorkflowsController` (empty action methods, routing)

### Week 3 — Backend & State Machine

- Design and implement workflow stage state machine (Section 3.4)
- Implement `POST /api/workflows` and `GET /api/workflows` (list/filter/paginate)
- Implement `GET /api/workflows/{id}` (full detail) and `/logs` endpoint
- Learn `HttpClientFactory` and external API integration patterns in ASP.NET Core
- Implement `POST /api/routes/calculate` skeleton and `GET /api/routes/{id}`
- Implement `GET /api/workflows/{id}/status` (polling) + retry endpoint with retry-limit logic

### Week 4 — React & Flutter Screens

- Build live workflow monitor screen (all active runs, stage, progress) — React
- Build workflow detail / execution trace viewer (audit trail UI) — React
- Learn Flutter local/push notification package
- Build workflow status tracker screen for volunteers — Flutter
- Build status polling logic (calls `/status` endpoint) — Flutter
- Build simple map view showing incoming supply route once approved — Flutter

### Week 5 — Route/ETA Agent

- Study multi-agent orchestration patterns and shared-state design in depth
- Learn LangGraph state graph implementation in depth (you own orchestration)
- Sign up for maps/routing API (OpenRouteService/Mapbox); study docs
- Design Route/ETA agent; confirm I/O matches Section 3.3 `Route`
- Implement Route/ETA agent: API call wrapper with timeout + retry handling
- Implement structured output (distance/ETA/polyline), merge into `plan_json`
- Test Route Agent against known depot-shelter pairs + a failure-injection test (maps API down)

### Week 6 — Full Pipeline Wiring

- Wire full 4-agent pipeline together end-to-end (sequential orchestration: Triage → Matching → Routing → Validation)
- Implement stage-transition enforcement in the pipeline orchestrator
- Build failed/retried workflows screen with error reasons — React
- Integration test: full pipeline run from report to validation stage
- Refine/debug orchestration logic based on test results
- Embed Route/ETA map preview in the workflow detail view — React

### Week 7 — Full Pipeline Testing & Performance

- Lead full 4-agent pipeline integration testing session
- Set up performance testing: concurrent workflow runs, response time measurement
- Add loading/error states to workflow monitor screens
- Cross-review teammates' agent integrations
- Write unit tests for state machine transitions (valid/invalid)
- Write API integration tests with mocked maps API (success/timeout/invalid response)

### Week 8 — Testing & Deployment

- Complete backend test suite; confirm CI green across whole repo
- Write React execution trace rendering tests
- Write Flutter status polling / notification tests
- Run performance tests (concurrent requests, agent latency); document results
- Lead deployment of ASP.NET Core API + PostgreSQL to a free-tier cloud platform
- Fix deployment/CI issues surfaced by the team's merged code

### Week 9 — Finalization

- Full end-to-end demo rehearsal — lead the technical run-through
- Write Individual Report section (contribution, evidence, challenges)
- Write AI usage log entries and one-page reflection
- Finalize ADR entries: AI framework, orchestration method, deployment platform
- Final bug bash and polish
- Submission day support — verify all deployed links in incognito browser

8. **Build Plan — Member D: Validation, Approval & Dispatch** **Audit + Validation/Safety Agent**

**Owns:** `Dispatches` , `ValidationResults` , `AuditTrail` tables. **Agent:** Validation/Safety (deterministic, rule-based).

**Endpoints to build:**

- `POST /api/validations/run` — trigger Validation/Safety agent
- `GET /api/validations/{workflowRunId}` — view validation results
- `POST /api/dispatches/{workflowRunId}/approve` — coordinator approves (role-protected)
- `POST /api/dispatches/{workflowRunId}/reject` — reject with reason
- `POST /api/dispatches/{workflowRunId}/request-revision` — sends plan back a stage
- `GET /api/dispatches?status=&dateRange=` — search/filter/paginate dispatch history
- `GET /api/audit/{dispatchId}` — full audit trail for one dispatch
- `GET /api/dispatches/summary` — analytics endpoint

### Week 1 — Foundations & Learning

- Learn C# fundamentals: classes, interfaces, async/await
- Learn ASP.NET Core authorization policies and role-based access in depth
- Draft `Dispatches` , `ValidationResults` , `AuditTrail` schema (confirm against Section 3.1)
- Learn EF Core migrations + audit-field patterns ( `CreatedAt` / `UpdatedAt` )
- Learn Dart basics: syntax, null safety, async/await
- Learn Flutter basics: widgets, state, layout

### Week 2 — Schema & Scaffolding

- Finalize `Dispatches` / `ValidationResults` / `AuditTrail` schema with team
- Agree `ValidationResults` JSON contract with Member C (confirm against Section 3.3)
- Implement `Dispatches` table (EF Core model + migration)
- Implement `ValidationResults` and `AuditTrail` tables + migration
- Study JWT + role-based authorization implementation in depth (you own approval security)
- Scaffold `ValidationsController` (empty action methods, routing)
- Scaffold `DispatchesController` (empty action methods, routing)

### Week 3 — Backend CRUD & Approval Logic

- Implement `POST /api/validations/run` and `GET /api/validations/{workflowRunId}`
- Implement `POST /api/dispatches/{id}/approve` (role-protected)
- Implement `POST /api/dispatches/{id}/reject` and `/request-revision`
- Implement `GET /api/dispatches` (search/filter/paginate) + `GET /api/audit/{dispatchId}`
- Implement `GET /api/dispatches/summary` (analytics endpoint)
- Write approval-enforcement logic (block dispatch without validation + approval — this is the core high-impact-action safeguard)

### Week 4 — React & Flutter Screens

- Build coordinator approval queue screen (plan/route/validation summary) — React
- Build Approve / Reject / Request-Revision action screen with mandatory notes on rejection — React
- Learn Flutter photo upload / proof-of-delivery pattern
- Build dispatch confirmation screen ("Approved — supplies en route") — Flutter
- Build rejection/revision notice screen with coordinator's reason — Flutter
- Build delivery confirmation screen (photo proof of receipt) — Flutter

### Week 5 — Validation/Safety Agent

- Learn deterministic validation / rule-engine design (vs LLM-as-judge)
- Learn prompt-injection risks and basic resistance-testing techniques
- Design Validation/Safety agent rule set (stock check, capacity check, reserve-threshold check); confirm I/O matches Section 3.3 `ValidationResults`
- Implement rule-based validation checks (deterministic code — no LLM call)
- Implement structured pass/fail output with violation detail
- Test Validation Agent against golden cases including one deliberately unsafe plan (e.g. over- allocated stock — must be correctly blocked)

### Week 6 — Pipeline Integration

- Wire Validation Agent into pipeline as the final gate before `PendingApproval`
- Build dispatch history + audit trail viewer — React
- Build analytics dashboard (dispatch counts, approval turnaround time) — React
- Integration test: validated plan correctly appears in approval queue
- Refine/debug validation rules based on test results
- Connect delivery confirmation to dispatch status update — Flutter

### Week 7 — End-to-End Testing & Polish

- Lead full end-to-end integration test: Flutter → API → PostgreSQL → all 4 agents → React approval → status back to Flutter
- Add mandatory rejection-notes enforcement + role-authorization checks
- Add loading/empty/error states to approval & audit screens
- Cross-review teammates' PRs touching approval/dispatch flow
- Write unit tests for every individual validation rule (pass/fail cases)
- Write API integration tests (role authorization, reject-without-notes rejected by server)

### Week 8 — Testing & Deployment

- Complete backend test suite; confirm CI green
- Write React approval queue and action-flow tests
- Write Flutter dispatch confirmation + photo upload widget tests
- Build Validation Agent golden-case suite including a prompt-injection resistance test
- Run/verify full end-to-end test against the deployed system
- Fix any deployment-related bugs in your component

### Week 9 — Finalization

- Full end-to-end demo rehearsal — the approval step is likely the demo centerpiece
- Write Individual Report section (contribution, evidence, challenges)
- Write AI usage log entries and one-page reflection
- Review/contribute to ADR entries related to your component
- Final bug bash and polish
- Submission day support — verify links, final checks

9. **Team / Shared Milestones (not owned by one member)**

- **Week 1:** Repo created; domain, roles & user stories finalized; ER diagram drafted
- **Week 2:** Full DB schema + all 4 agent JSON I/O contracts agreed and frozen (Sections 3.1, 3.3); JWT auth working end-to-end
- **Week 3:** Core backend CRUD functional for all 4 components
- **Week 4:** Base React and Flutter screens functional for all 4 components; state-management approach confirmed consistent
- **Week 5:** Each member's individual agent built and passing its own golden-case tests
- **Week 6:** Full 4-agent pipeline wired together end-to-end
- **Week 7:** Coordinator approval flow + maps API integration complete; full end-to-end test passing
- **Week 8:** All test suites complete, CI green, system deployed (API, DB, React, APK)
- **Week 9:** Demo rehearsed, ADRs/reports/AI logs finalized, submission ready

10. **Definition of Done (per member, before Week 9)**

- All your endpoints return correct status codes and are documented in Swagger
- Your React and Flutter screens handle loading, empty, success, and error states
- Your agent runs reliably against at least 3 golden-case scenarios, including one failure case
- Your tests pass in CI, not just locally
- You can explain, modify, and debug every part of your component without notes
- Your Git history shows regular, real commits across the 9 weeks — not a final-week dump
