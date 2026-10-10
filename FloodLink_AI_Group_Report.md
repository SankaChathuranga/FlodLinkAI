# FloodLink AI — Group Assignment Report

---

## Title Page

**[CONFIRM: exact SLIIT department naming — e.g. Department of Software Engineering]**
**[CONFIRM: exact faculty naming — e.g. Faculty of Computing]**
**Sri Lanka Institute of Information Technology**

**SE3090 — Software Engineering Frameworks**
Year 3, Semester 1, 2026

**Group Assignment**
**FloodLink AI — AI-Coordinated Disaster/Flood Relief Resource Platform for Sri Lanka**

| Item | Link |
| --- | --- |
| GitHub repository | [CONFIRM: submission repository URL — the local git remote is `https://github.com/SankaChathuranga/FlodLinkAI`] |
| Live demo URL (web) | [CONFIRM: live demo URL] |
| Flutter APK | [CONFIRM: APK download URL — APKs are published as GitHub Releases by `release-apk.yml`] |
| Group demo video | [CONFIRM: demo video URL] |
| Swagger URL | [CONFIRM: Swagger URL — note that Swagger is only enabled when the API runs in the `Development` environment] |
| Health endpoint | [CONFIRM: deployed URL of `GET /health`] |

**Team Members**

| Member | Component | Name and Student ID |
| --- | --- | --- |
| Member A | Field Intake, Shelter Reporting, Triage/Planner Agent | [CONFIRM: full name, student ID] |
| Member B | Inventory and Depot Management, Logistics/Matching Agent | [CONFIRM: full name, student ID] |
| Member C | Orchestration, Workflow Engine, Route/ETA Agent | [CONFIRM: full name, student ID] |
| Member D | Validation, Approval and Dispatch Audit, Validation/Safety Agent | [CONFIRM: full name, student ID] |

---

## Table of Contents

1. Executive Summary
2. Project Overview and Scope
3. Requirements and User Roles
4. Full-Stack Architecture
5. Agentic AI Architecture
6. Database Design and ER Model
7. API Design
8. React Web Application Design
9. Flutter Mobile Application Design
10. Technical Implementation Report
11. Software Testing Report
12. Agentic AI Evaluation Report
13. Performance Report
14. Deployment Report
15. Architecture Decision Records
16. Security and Privacy Considerations
17. Consolidated Group AI Usage Declaration
PART B — Individual Reports
18. References

[CONFIRM: page numbers once the document is laid out]

---

## 1. Executive Summary

FloodLink AI is a relief-resource coordination platform for flood response in Sri Lanka. It is intended for two groups: field volunteers, who report shelter needs from the ground, and relief coordinators, who decide which supplies are sent where. The problem it addresses is that, during a flood, reports of need, depot stock levels, and dispatch decisions are held in separate places and handled informally, so a coordinator cannot easily see which shelter needs what most urgently, whether stock exists to meet it, how far away that stock is, and whether a proposed dispatch is safe — and there is no reliable record of who approved what.

The system is implemented as a monorepo containing an ASP.NET Core Web API (.NET 10) backed by PostgreSQL through Entity Framework Core, a React and TypeScript coordinator dashboard, and a Flutter field application. Volunteers submit reports with a need type, quantity, GPS coordinates and an optional photo; the API scores each report's urgency with a rule-based function. Coordinators review shelters, reports, inventory, an approval queue, dispatch history and summary analytics in the web dashboard.

FloodLink AI's Agentic AI contribution is a supervised four-agent workflow. A Triage Agent ranks open needs, a Matching Agent allocates real depot stock against them, a Route/ETA Agent obtains road distance and travel time from the Mapbox Directions API, and a Validation Agent applies deterministic safety rules to the combined plan. A workflow engine moves each run through an explicit state machine (Triage → Matching → Routing → Validating → PendingApproval), records every agent invocation in an execution log, and routes any agent failure to a `Failed` state that records the stage where it failed. No dispatch is recorded until a coordinator explicitly approves, rejects, or requests revision of a plan in `PendingApproval`. All four agents are deterministic C# services; the system does not call a large language model.

The pipeline is fully exercised in automated tests. In the running API, however, the orchestrator is currently invoked only once per run (Triage → Matching), so an end-to-end run from a submitted report to the approval queue is not yet available outside the test suite. This and the other gaps are documented in Sections 2.4, 12.3 and 13.

This report covers the project scope, requirements, architecture, data model, API and client designs, implementation, testing, Agentic AI evaluation, performance, deployment, architecture decisions, security, the group AI declaration, and one individual section per member.

---

## 2. Project Overview and Scope

### 2.1 Problem Statement

Flood relief depends on a sequence of related decisions. Volunteers at shelters know what is needed but have no structured way to report it. Depot staff know what stock is on hand but not where it is most needed. Coordinators must decide which depot sends what to which shelter, but without a combined view they cannot easily confirm that the stock exists, that a delivery is physically plausible, or that a single dispatch will not empty a depot's reserve. When a decision is made, there is often no durable record of the reasoning or of who authorised it.

FloodLink AI provides one data model and one workflow for these activities. Field reports, shelters, depots and stock are held in a single PostgreSQL database. A supervised agent pipeline turns open reports into a proposed, validated dispatch plan, and a human coordinator must make the final decision before any dispatch is recorded.

### 2.2 Project Objectives

1. Accept field reports of shelter need with need type, quantity, GPS position and optional photo evidence.
2. Score and rank reports by urgency using a deterministic, explainable rule.
3. Match ranked needs against real depot inventory and explicitly list any need that cannot be met.
4. Compute road distance and estimated travel time for a proposed delivery using an external routing service.
5. Validate the combined plan against deterministic safety rules (stock, reserve floor, vehicle capacity, coordinate and route plausibility).
6. Require an explicit human coordinator decision before a dispatch is recorded.
7. Persist workflow state, agent inputs and outputs, validation results, dispatch decisions and audit events.
8. Deploy the API, database, web dashboard and Android application to accessible platforms.

### 2.3 In-Scope Functionality

The table below lists functionality present in the repository on `main`. Items marked "partial" are described in Section 2.4.

| Area | Included capability |
| --- | --- |
| Identity | JWT bearer-token validation (issuer, audience, lifetime, signing key) and role-based authorization on coordinator endpoints. A Development-only fallback authenticates token-less requests as a Coordinator. No login or token-issuing endpoint exists (partial). |
| Field intake | Shelter create, list (search, status filter, pagination), detail with report history, update. Report create (multipart, with photo file or URL and GPS), list (filter by shelter, status, minimum urgency; sort), detail, update. Rule-based urgency scoring on submission. |
| Inventory and depots | Depot list and create. Inventory list (optionally by depot), item create, stock check-in (quantity received). Seeded depots and stock. |
| Agentic AI | Four deterministic agents behind a typed `AgentResult<T>` contract; a guarded state machine; a sequential orchestrator; a per-invocation execution log; safe-failure transitions that record the failed stage. |
| Routing | Mapbox Directions client with per-request timeout and bounded retry; Route/ETA agent that persists a `Routes` row and returns distance (km), ETA (minutes) and an encoded polyline. |
| Validation | Deterministic safety rules (stock availability, reserve floor, vehicle capacity, coordinate/route sanity) persisted as `ValidationResults` rows. |
| Approval and dispatch | Coordinator approval queue; approve (optional notes), reject (mandatory reason), request revision (mandatory notes); `Dispatches` and `AuditTrail` records; delivery confirmation; dispatch history with filters and pagination; summary analytics. |
| Web dashboard | React + TypeScript tabs for Shelters, Reports, Triage Plans, Inventory, Approvals, Dispatch History and Analytics. |
| Mobile | Flutter app: report list, new report form, camera capture, GPS capture with manual adjustment, "My submitted reports" with 10-second polling, stock check-in, dispatch status lookup and delivery confirmation. |
| CI/CD | GitHub Actions CI (backend build and test against a Postgres service, web build, Flutter analyze) and an APK release workflow. Dockerfile for the API. |

### 2.4 Out-of-Scope or Deferred Functionality

The following are **not** claimed as completed functionality.

- **End-to-end pipeline execution through the API.** `POST /api/workflows` advances a new run by one step only (Triage → Matching). No endpoint or background worker advances a run from Matching through Routing and Validating to `PendingApproval`. The full chain is exercised only by automated tests that call the orchestrator repeatedly (Section 11).
- **Report-to-workflow linkage.** A submitted report does not start a workflow run. `POST /api/reports/{id}/trigger-triage` runs the Triage Agent directly, outside the orchestrator, and leaves the created run in the `Triage` state. [CONFIRM: describe the "trigger-triage fix" referenced by the team, if one has been applied — the code on `main` still runs the agent outside the orchestrator.]
- **Orchestrated validation of a real plan.** The orchestrator stores agent outputs in `PlanJson` as a keyed object (`triagePlan`, `allocationProposal`, `route`), whereas the Validation Agent expects `PlanJson` in the `PlanDocument` shape (including vehicle capacity, reserved quantities and reorder thresholds). No component currently produces a `PlanDocument`, so a real run reaching `Validating` would fail with `INVALID_PLAN`. Validation is tested against seeded `PlanDocument` data.
- **Real authentication.** There is no login, registration, token issuance, or password hashing. Seeded users have placeholder password values. Outside the Development environment, coordinator endpoints return 401 because no client can obtain a token.
- **Stock reservation and decrement.** Approval records a dispatch and an audit event but does not reserve or decrement inventory. The `quantity_reserved` and `reorder_threshold` columns in the original specification were not implemented on `InventoryItems`.
- **LLM-based planning.** The build specification called for LangGraph (or an equivalent) with an LLM producing structured output. No LLM or agent framework is used (ADR-004).
- **Offline sync.** The mobile app has no local queue or storage for reports created without connectivity.
- **Map display.** Mapbox is used only by the backend for routing. The web dashboard's shelter map is a schematic SVG plot of coordinates; neither client renders a route or polyline.
- **Workflow observability screens.** The planned workflow list, execution-log timeline, status-polling, retry and route endpoints (`GET /api/workflows`, `/logs`, `/status`, `/retry`, `/api/routes/*`) and the React workflow monitor and trace viewer were not built.
- **Push notifications** to volunteers on status change.
- **Photo serving and proof-of-delivery upload.** Report photos are written to `wwwroot/uploads/reports`, but static-file serving is not enabled, so the stored URLs are not served. Delivery confirmation accepts a text photo reference, not an upload.
- **Multi-depot routing.** The Route/ETA Agent routes only the first allocation in a proposal.
- **Performance and load testing.** No load test or Lighthouse audit was run (Section 13).

### 2.5 Success Criteria

The target demonstration is a continuous scenario in which a volunteer submits a field report from Flutter, a workflow run moves through Triage → Matching → Routing → Validating → PendingApproval, a coordinator approves it in the React approval queue, and the field user sees the dispatch status. A separate negative scenario must show a safe failure — for example, no stock available for the requested need, or the Mapbox API unreachable — with the run ending in `Failed`, the failed stage recorded, and no dispatch created.

Current status against these criteria:

| Criterion | Status |
| --- | --- |
| Report submitted from Flutter with GPS and photo | Demonstrable |
| Run progresses Triage → … → PendingApproval | Demonstrated in automated tests only; through the API a run stops at Matching (Section 2.4) |
| Coordinator approves / rejects / requests revision in React | Demonstrable against a run already in `PendingApproval` (e.g. seeded) |
| Field user sees dispatch status | Demonstrable via manual workflow-run ID entry on the mobile dispatch screen |
| Negative scenario: no stock → `Failed` at Matching | Demonstrated in automated tests; the Matching step is reachable through `POST /api/workflows` only if a second advance is triggered |
| Negative scenario: Mapbox failure → `Failed` at Routing | Demonstrated in automated tests (Route agent returns `MAPBOX_TIMEOUT` / `MAPBOX_HTTP_ERROR`) |

[CONFIRM: final demo scenario and data, once the remaining pipeline wiring is complete]

---

## 3. Requirements and User Roles

### 3.1 Stakeholders

| Stakeholder | Primary concern |
| --- | --- |
| Field Volunteer | Report shelter needs quickly with location and evidence; see whether the report has been acted on |
| Coordinator | See urgent needs, available stock and proposed dispatches; approve, reject or send back plans; review history |
| Depot staff | Record incoming stock so that matching uses current quantities |
| [CONFIRM: any additional implemented role, e.g. Admin] | The build specification lists Depot/Warehouse Manager and Admin roles; neither is enforced by any endpoint in the current code |

### 3.2 User-Role Model

Only one role is enforced server-side: `Coordinator`, required by the Workflow decision, Validation, Dispatch and Audit endpoints. The `Users` table stores a free-text `Role` value (seeded as `Volunteer` and `Coordinator`), but no endpoint reads it. All shelter, report, depot, inventory and workflow-create/read endpoints are currently unauthenticated. Neither client has a sign-in screen; the Flutter app submits reports as user ID 1.

| Capability | Field Volunteer | Depot staff | Coordinator |
| --- | --- | --- | --- |
| Submit and view field reports | Yes (no auth enforced) | Yes (no auth enforced) | Yes |
| Manage shelters | Not restricted | Not restricted | Yes |
| Check in stock / manage depots | Not restricted | Yes (no auth enforced) | Yes |
| Start a workflow run | Not restricted | Not restricted | Yes |
| Run validation | No (Coordinator role) | No | Yes |
| Approve / reject / request revision | No (Coordinator role) | No | Yes |
| View dispatch history, audit, analytics | No (Coordinator role) | No | Yes |
| Confirm delivery | Intended; currently requires Coordinator role | No | Yes |

"Not restricted" means the endpoint has no `[Authorize]` attribute. The delivery-confirmation endpoint is used by the Flutter field app but sits on the Coordinator-only `DispatchController`; it works in Development only because of the development authentication fallback.

### 3.3 Key Functional Requirements

**FR-01: Identity and access**

- Coordinator-only endpoints reject requests without a valid JWT carrying the `Coordinator` role claim. (Implemented: validation only.)
- JWT issuer, audience, lifetime and signature are validated on every protected request. (Implemented.)
- A user can sign in and receive a token. (Not implemented.)

**FR-02: Field intake**

- A volunteer can submit a report for an existing shelter with need type (Water, Food, Medical, Shelter-Repair, Other), quantity ≥ 1, optional photo file or URL, and optional GPS coordinates within valid ranges.
- If GPS is omitted, the report takes the shelter's coordinates.
- An urgency score (0–100) is computed on submission from shelter occupancy, need type and time since the shelter's last resolved report, unless the client supplies one.
- Shelters can be created, listed (search, status filter, pagination), viewed with report history, and updated.

**FR-03: Inventory and depots**

- Depots can be listed and created with validated coordinates.
- Inventory items can be listed (optionally by depot), created (unique per depot and item name), and have stock checked in.

**FR-04: Matching**

- Given a triage plan, the Matching Agent allocates stock from depots whose item name matches the need type, in priority order, without exceeding the quantity available in its read snapshot.
- Unmet or partially met needs are listed as unfulfillable with a reason; they are never silently dropped.
- If nothing can be allocated, the agent returns a `NO_STOCK_AVAILABLE` failure.

**FR-05: Routing**

- Given an allocation proposal, the Route/ETA Agent loads depot and shelter coordinates from the database and requests a driving route from Mapbox.
- The external call has a configurable timeout and is retried a bounded number of times on timeout, network error or 5xx response.
- The result (distance in metres, duration in seconds, polyline) is persisted to `Routes` and returned as distance in km and ETA in minutes.

**FR-06: Agentic workflow (triage → matching → routing → validation → approval)**

- A workflow run is created with an objective and starts in `Triage`.
- The orchestrator invokes exactly one agent per step, chosen by the run's current state, and transitions only through the guarded state machine.
- Every agent invocation is logged with agent name, input snapshot, output, duration, status and error message.
- Any agent failure, anticipated or unexpected, moves the run to `Failed` and records the stage in `FailedAtState`.
- Validation runs a fixed set of deterministic checks; the run reaches `PendingApproval` only if all pass.
- Only a run in `PendingApproval` can be approved, rejected or sent for revision, and only by a Coordinator.
- Rejection requires a reason; revision requires notes. Each decision creates a `Dispatches` row and an `AuditTrail` event.
- A revision request returns the plan to `Matching`.
- Delivery can be confirmed only for an `Approved` run with an approved dispatch.

### 3.4 Non-Functional Requirements

| ID | Requirement | Implemented response |
| --- | --- | --- |
| NFR-01 | Security | JWT validation with zero clock skew; role check on coordinator endpoints; DataAnnotations validation on DTOs; signing key and Mapbox key kept out of committed configuration. Gaps: no login, most endpoints unauthenticated, 500 responses include the exception message (Section 16). |
| NFR-02 | Reliability | Workflow state persisted in PostgreSQL after every step; invalid transitions rejected without side effects; agents return `AgentResult<T>` instead of throwing; unexpected agent exceptions caught and converted to `Failed`; bounded Mapbox retries. |
| NFR-03 | Usability | Role-oriented web tabs; mandatory-notes prompts on reject/revision; loading, empty and error states in web and mobile screens; large touch targets and GPS/camera-first reporting on mobile. |
| NFR-04 | Maintainability | Layered solution (Contracts, Domain, Infrastructure, four Agent projects, Api); agents depend only on `FloodLink.Contracts` records; the orchestrator depends only on thin invoker interfaces; central package version management. |
| NFR-05 | Auditability | `AgentExecutionLog` per agent call; `WorkflowRuns.FailedAtState`; `ValidationResults` per check; `Dispatches` and `AuditTrail` per coordinator decision and delivery. Gap: the approving user is not recorded (Section 12.3). |
| NFR-06 | Performance | Asynchronous EF Core and HTTP calls throughout; `AsNoTracking` on read queries; pagination on shelter and dispatch lists; indexes on lookup columns. No measured results (Section 13). |
| NFR-07 | Portability | PostgreSQL in Docker for local development; multi-stage Dockerfile for the API with `PORT` binding and optional migrate-on-startup; API base URLs injected at build time for web (`VITE_API_BASE_URL`) and mobile (`--dart-define`). |
| NFR-08 | Accessibility | Shared colour tokens with documented contrast rules (no white text on the warning colour); IBM Plex typography; Material 3 on mobile. No formal accessibility audit was performed. |

---

## 4. Full-Stack Architecture

### 4.1 Architectural Style

FloodLink AI is a monorepo with three top-level application folders:

- `backend/` — ASP.NET Core Web API and test project, organised as a layered modular monolith:
  - `FloodLink.Contracts` — agent input/output records (`TriagePlan`, `AllocationProposal`, `Route`, `ValidationResults`, `PlanDocument`) and `AgentResult<T>`. The integration boundary between agents.
  - `FloodLink.Domain` — entities, enums, the `WorkflowEngine` state machine, the `WorkflowOrchestrator`, and the interfaces the orchestrator depends on.
  - `FloodLink.Infrastructure` — `AppDbContext`, migrations, repositories, the execution logger, the Mapbox client, and services (urgency scoring, photo storage, workflow state).
  - `FloodLink.Agents.Triage`, `.Matching`, `.Routing`, `.Validation` — one project per agent.
  - `FloodLink.Api` — controllers, DTOs, middleware, authentication, and the composition root, including adapters that bind each agent to the orchestrator's invoker interfaces.
- `web/` — React + TypeScript coordinator dashboard (Vite).
- `mobile/` — Flutter field application.

The API is the only boundary to data and agents: React and Flutter communicate only with the API, never with each other, the database, or the agents. Agents run in-process inside the API and are reachable only through the orchestrator or specific controllers. This keeps one transactional database and one deployment unit while separating ownership by project.

### 4.2 System Context Diagram

[DIAGRAM: System context — Field Volunteer → Flutter mobile app; Coordinator → React web dashboard; both apps → ASP.NET Core Web API (HTTPS/JSON); API → PostgreSQL; API → Mapbox Directions API]

Coordinators use the React dashboard and field volunteers use the Flutter app. Both clients call only the ASP.NET Core Web API, which enforces validation and authorization, persists all state to PostgreSQL, and calls the Mapbox Directions API for route distance and travel time.

### 4.3 Container Architecture

[DIAGRAM: Container architecture — Clients (Flutter + Material 3 + Provider; React 18 + TypeScript + Vite + Carbon) → API container (REST controllers and DTO validation; JWT authentication; WorkflowOrchestrator + WorkflowEngine; four agent services; AgentExecutionLogger; MapboxClient; EF Core AppDbContext) → PostgreSQL; MapboxClient → Mapbox Directions API]

Clients send JSON over HTTP(S); the Flutter app sends report photos as multipart form data. Inside the API, controllers validate input before calling either domain services directly (shelters, reports, inventory, dispatch) or the `WorkflowOrchestrator`, which sequences the four agents. Agents read and write through the same scoped `AppDbContext`. The Route/ETA Agent is the only component that calls an external service.

### 4.4 Third-Party Integrations

| Service | Business purpose | Protection and failure handling |
| --- | --- | --- |
| Mapbox Directions API (`driving` profile) | Road distance, travel time and route geometry between a depot and a shelter, used to validate plan plausibility and to inform the coordinator | API key read from configuration (`Mapbox:ApiKey`), kept in a gitignored `appsettings.Development.json` locally or the `Mapbox__ApiKey` environment variable when hosted; never sent to web or mobile clients. Per-request timeout from `Mapbox:TimeoutSeconds` (10 s in committed configuration). Up to 2 retries (3 attempts) on timeout, `HttpRequestException` or 5xx; 4xx responses are not retried. An empty route list returns no result. The Route/ETA Agent converts each outcome to a failure code (`NO_ROUTE`, `MAPBOX_TIMEOUT`, `MAPBOX_HTTP_ERROR`, `ROUTING_UNEXPECTED_ERROR`), and the orchestrator moves the run to `Failed` with `FailedAtState = Routing`. |

No other external service is integrated. [CONFIRM: any other external service used in the deployed system]

---

## 5. Agentic AI Architecture

### 5.1 Domain Contribution

The Agentic AI subsystem turns open shelter needs into a dispatch plan that a coordinator can approve. It is narrower than a general assistant and broader than a single scoring function: it ranks needs, allocates real stock, obtains an external route estimate, checks the combined plan against safety rules, records every step, and stops for a human decision. If any step cannot produce a usable result, the run ends in a recorded `Failed` state and no dispatch is created.

| Agent | Responsibility | Main structured output | Prohibited action |
| --- | --- | --- | --- |
| Triage Agent (Member A) | Score and rank open reports by urgency using shelter occupancy, need type and time since last resupply | `TriagePlan`: `WorkflowRunId`, `PriorityItems[]` of `ReportId`, `ShelterId`, `NeedType`, `Quantity`, `PriorityScore` (0–100), `Justification` | Cannot match stock, create allocations, change a workflow state, or create a dispatch |
| Matching Agent (Member B) | Allocate depot stock to ranked needs in priority order; list unmet needs | `AllocationProposal`: `AllocationProposalId`, `WorkflowRunId`, `Allocations[]` of `DepotId`, `ShelterId`, `ItemName`, `Quantity`; `Unfulfillable[]` of `ShelterId`, `ItemName`, `Reason` | Cannot change inventory quantities, reserve stock, or create a dispatch |
| Route/ETA Agent (Member C) | Compute road distance, travel time and polyline from depot to shelter via Mapbox | `Route`: `WorkflowRunId`, `AllocationProposalId`, `DistanceKm`, `EtaMinutes`, `Polyline` | Cannot change allocations, inventory, or workflow state; calls only the Mapbox Directions endpoint |
| Validation Agent (Member D) | Apply deterministic safety rules to the full plan | `ValidationResults`: `WorkflowRunId`, `Checks[]` of `CheckName`, `Passed`, `ViolationDetail`; `OverallPassed` | Cannot modify the plan, approve, or dispatch; performs no I/O beyond reading the workflow run |

Each agent has its own project, input and output contract, data access and decision logic. The agents are not prompts with different instructions; they are separate C# services with different inputs and rules.

Note on write access: the agents are not strictly read-only. The Triage Agent writes a `TriagePlans` row, updates each report's urgency and status, and sets the run's `PlanJson`; the Matching Agent writes `AllocationProposals` rows; the Route/ETA Agent writes a `Routes` row. None of them changes inventory, creates a dispatch, or changes the workflow state — state changes are made only by the orchestrator through `WorkflowEngine`, and dispatch records only by the coordinator decision endpoints.

### 5.2 Orchestrator

Orchestration is implemented in C# without an agent framework or language model. `WorkflowOrchestrator` (in `FloodLink.Domain`) exposes one entry point, `AdvanceAsync(workflowRunId)`: load the run, select the agent for its current state, invoke it, log the invocation, merge the output into `PlanJson`, and transition the run. It depends only on four thin invoker interfaces (`ITriageAgentInvoker`, `IMatchingAgentInvoker`, `IRoutingAgentInvoker`, `IValidationAgentInvoker`), `IAgentExecutionLogger` and `IWorkflowRunRepository`, so it has no knowledge of any agent's internals and can be tested with fakes. The API project supplies adapters that bind each member's agent to these interfaces.

The order of agents is fixed by the state machine, not chosen at runtime. There is no planning step: the "plan" is the fixed sequence Triage → Matching → Routing → Validating. The system does not use an LLM anywhere; the Triage Agent's justification text is generated from a template. The build specification's mandate for LangGraph plus an LLM was not met; the rationale is recorded in ADR-004.

State transitions are enforced by `WorkflowEngine.TryTransition(run, target)`, a static method implemented as an enum switch over (current, target) pairs. It returns `false` and leaves the run unchanged for any pair not in the table below; it never throws for an invalid transition. On a transition to `Failed`, it first copies the current state into `FailedAtState`.

| From | To | Trigger |
| --- | --- | --- |
| Triage | Matching | Agent success |
| Triage | Failed | Agent failure |
| Matching | Routing | Agent success |
| Matching | Failed | Agent failure |
| Routing | Validating | Agent success |
| Routing | Failed | Agent failure |
| Validating | PendingApproval | Agent success, all checks passed |
| Validating | Failed | Agent failure or any check failed |
| PendingApproval | Approved | Coordinator action |
| PendingApproval | Rejected | Coordinator action |
| PendingApproval | RevisionRequested | Coordinator action |
| RevisionRequested | Matching | Orchestrator re-queue |

`Approved`, `Rejected` and `Failed` are terminal.

A second, table-based transition check (`WorkflowStateTransitions.IsAllowed`) is used by Member D's `WorkflowStateService` for the dispatch and validation endpoints. It allows the same forward path plus `PendingApproval → Failed` and `RevisionRequested → Failed`, and it does not set `FailedAtState`. Consolidating the two into `WorkflowEngine` is outstanding.

### 5.3 Structured Plan

Each agent's output is a typed C# record from `FloodLink.Contracts`, serialized with `System.Text.Json`. As a run progresses, the orchestrator merges each output into the run's `PlanJson` (a `jsonb` column) under a fixed key — `triagePlan`, `allocationProposal`, `route`, `validationResults` — and reads the previous agent's output back from that key as the next agent's input. Contract changes require approval from all four members (CONTRIBUTING.md).

Two integration gaps affect the plan document (Section 2.4): the Validation Agent expects a different `PlanDocument` shape that no agent currently produces, and the Triage Agent also writes its plan directly to `PlanJson` before the orchestrator merges it, so the stored object contains the triage fields at the top level as well as under `triagePlan`.

### 5.4 Workflow Sequence

1. A client calls `POST /api/workflows` with an objective. The API validates that the objective is non-empty, creates a `WorkflowRun` in `Triage`, and persists it.
2. The controller calls `AdvanceAsync` once. The Triage Agent loads all non-resolved reports, scores each one, ranks them, persists a `TriagePlans` row, and returns a `TriagePlan`. The orchestrator logs the call and transitions to `Matching` (or `Failed`).
3. *(Test harness only — no API trigger exists yet.)* `AdvanceAsync` on a `Matching` run passes the stored `TriagePlan` to the Matching Agent, which allocates stock, persists `AllocationProposals` rows and returns an `AllocationProposal`. Transition to `Routing` (or `Failed`).
4. *(Test harness only.)* `AdvanceAsync` on a `Routing` run passes the proposal to the Route/ETA Agent, which calls Mapbox, persists a `Routes` row and returns a `Route`. Transition to `Validating` (or `Failed`).
5. *(Test harness only.)* `AdvanceAsync` on a `Validating` run passes the route to the Validation Agent. If every check passes, transition to `PendingApproval`; otherwise to `Failed`. Alternatively, `POST /api/validations/run` runs validation on a `Validating` run whose `PlanJson` is a `PlanDocument`, persists one `ValidationResults` row per check, writes a `PlanValidated` audit event, and transitions the run.
6. The run appears in the coordinator approval queue (`GET /api/dispatches/approval-queue`).
7. The coordinator approves (optional notes), rejects (reason required) or requests revision (notes required). The API checks the run is in `PendingApproval`, creates a `Dispatches` row and an `AuditTrail` event, and transitions the run to `Approved`, `Rejected` or `RevisionRequested`.
8. On `Approved`, a field user can look up the dispatch status by run ID and confirm delivery, which writes a `DeliveryConfirmed` audit event.

Every agent invocation in steps 2–5 writes one `AgentExecutionLog` row.

### 5.5 Controlled Execution

- **`AgentResult<T>` convention.** Every agent returns `AgentResult<T>` with `Success`, `Data`, `ErrorCode` and `ErrorMessage`. Anticipated failures (no reports, empty plan, no stock, missing depot or shelter, no route, Mapbox timeout or HTTP error, invalid plan) are returned as `Fail(code, message)`, not thrown.
- **Unexpected exceptions.** The orchestrator's step wrapper catches any exception thrown by an agent, logs it as an `Error` row with the exception message, and transitions the run to `Failed`. A run therefore cannot be left in an intermediate state by an agent crash.
- **External call bounds.** The Mapbox client is a typed `HttpClient` with a configurable timeout (10 s by default in configuration). It makes at most 3 attempts, retrying immediately (no backoff delay) on timeout, network error or 5xx; 4xx responses fail immediately. Worst-case routing latency is therefore about three timeouts.
- **Single step per call.** `AdvanceAsync` runs one agent and saves; it does not loop. A run cannot run away, and each step is persisted before the next starts.
- **Guarded state changes.** No component sets `CurrentState` directly on the orchestrator path; all changes go through `WorkflowEngine.TryTransition`.
- **No tool layer.** There is no tool gateway or per-agent allow-list. Each agent's capabilities are limited by what its code does and by the dependencies injected into it; for example, only the Route/ETA Agent receives the Mapbox client. The `ToolCallsJson` log column exists but is never populated.

### 5.6 Human Approval Boundary

Recording a dispatch is treated as the high-impact action, because it commits scarce supplies and a vehicle to one shelter. Agents only propose; the transition out of `PendingApproval` is never made automatically.

- Only `PendingApproval → Approved | Rejected | RevisionRequested` is a coordinator transition, and it is reachable only through endpoints marked `[Authorize(Roles = "Coordinator")]`.
- Both decision paths check the run's state server-side: the dispatch endpoints return 409 for any other state, and the orchestrator path returns 400.
- Rejection requires a reason and revision requires notes; both are enforced by the server (400) as well as by the web form.
- There are two decision paths: `POST /api/workflows/{id}/decision` (orchestrator; changes state only, and re-queues a revision to `Matching`) and `POST /api/dispatches/{id}/approve | reject | request-revision` (Member D; creates `Dispatches` and `AuditTrail` rows and leaves a revised run in `RevisionRequested`). The web dashboard uses the dispatch path. Unifying them is outstanding.
- Approval does not re-run validation against current data, and does not reserve or decrement stock.

### 5.7 Durable State and Observability

| Table | What is persisted |
| --- | --- |
| `WorkflowRuns` | ID, objective, current state, `FailedAtState`, accumulated `PlanJson` (jsonb), created and updated timestamps |
| `AgentExecutionLog` | One row per agent invocation: workflow run ID, agent name, input snapshot (the run's `PlanJson` at call time), output JSON, duration in ms, status (`Success` / `Error`; `Retried` is supported by the logger but not emitted), error message, timestamp |
| `Routes` | Workflow run ID, distance (metres), estimated duration (seconds), encoded polyline, timestamp |
| `TriagePlans` | Source report IDs (jsonb), priority rank, plan summary (jsonb), creating run ID |
| `AllocationProposals` | Workflow run ID, depot, shelter, item, quantity, status (`Proposed`) |
| `ValidationResults` | Workflow run ID, check name, pass/fail, violation detail |
| `Dispatches` | Workflow run ID, decision, notes, dispatched-at time; `ApprovedById` exists but is not set |
| `AuditTrail` | Event type (`DispatchApproved`, `DispatchRejected`, `RevisionRequested`, `DeliveryConfirmed`, `PlanValidated`), event detail (jsonb), dispatch ID; `ActorId` exists but is not set |

`GET /api/workflows/{id}` returns the run including `PlanJson` and `FailedAtState`. No endpoint currently exposes `AgentExecutionLog` rows; they are readable only in the database.

### 5.8 Workflow Diagram

[DIAGRAM: Workflow sequence — Report submitted → POST /api/workflows → WorkflowRun (Triage) → Triage Agent → Matching Agent → Route/ETA Agent (Mapbox) → Validation Agent (deterministic safety rules) → PendingApproval → Coordinator decision. Branches: any agent failure → Failed (FailedAtState recorded); validation check failed → Failed; Rejected → recorded rejection, no dispatch; RevisionRequested → back to Matching; Approved → Dispatch record + AuditTrail → delivery confirmation. Each agent box writes one AgentExecutionLog row.]

---

## 6. Database Design and ER Model

### 6.1 Data Strategy

PostgreSQL 16 is the single relational store. Entity Framework Core 10 with the Npgsql provider maps each domain class to a table; schema changes are versioned as committed code-first migrations. Variable-shaped data (the workflow plan, triage plan summaries and source report IDs, audit event details) is stored in `jsonb` columns. Enum-typed state columns (`CurrentState`, `FailedAtState`, `Decision`) are stored as strings. Reference data — two users, three shelters, two depots, four inventory items and five reports — is seeded through `HasData`. Report photos are stored on the API host's file system with only the relative URL in the database.

Migrations, in order:

1. `InitialCreate`
2. `AddMemberAFieldIntakeAndShelterReporting`
3. `AddFailedAtState`
4. `AddAgentExecutionLogErrorMessage`
5. `ReplaceRoutesSchema`
6. `AddMemberBInventoryAndMatching`
7. `AddMemberDTables`

A design-time `DbContext` factory lets migrations run from the connection-string environment variable without starting the API. Hosted deployments can apply pending migrations at startup by setting `Database__MigrateOnStartup=true`, which the Dockerfile sets by default.

### 6.2 Entity Relationships

**Identity and field intake**

- A `User` may be the contact volunteer for many `Shelter` records and the reporter of many `Report` records.
- A `Shelter` has many `Report` records.
- A `TriagePlan` records the IDs of the reports it was generated from (as a jsonb array, not a foreign key) and optionally references the `WorkflowRun` that created it.

**Inventory**

- A `Depot` has many `InventoryItem` records, at most one per item name.
- An `AllocationProposal` row links one `WorkflowRun`, one `Depot` and one `Shelter` with an item name and quantity. A run can have many allocation rows.

**Workflow and routing**

- A `WorkflowRun` has many `AgentExecutionLog` rows (one per agent call) and many `Routes` rows (one per routing call).
- A `WorkflowRun` is not linked by foreign key to the reports it triaged; that relationship exists only through the `TriagePlans.GeneratedFromReportIds` list.

**Validation, approval and audit**

- A `WorkflowRun` has many `ValidationResults` rows (one per check) and at most one `Dispatch` (unique index on `WorkflowRunId`).
- A `Dispatch` has many `AuditTrail` events. Audit events not tied to a dispatch (e.g. `PlanValidated`) have a null dispatch ID.
- `Dispatch.ApprovedById` and `AuditTrail.ActorId` are GUID columns with no foreign key; `Users.Id` is an integer, so they cannot reference it as designed.

The workflow run is the hub: the trigger (reports), the process (agent log, route, allocations, validation results) and the outcome (dispatch, audit) all attach to it.

### 6.3 Constraints

- Inventory item name is unique within a depot (composite unique index on `DepotId`, `ItemName`).
- A workflow run has at most one dispatch (unique index on `Dispatches.WorkflowRunId`).
- Indexes on `ValidationResults.WorkflowRunId` and `AuditTrail.DispatchId`; EF Core adds indexes on all foreign-key columns.
- Required fields and maximum lengths: e.g. `WorkflowRuns.Objective` (1000), `AgentExecutionLog.AgentName` (100) and `Status` (20), `Shelters.Name` (150), `Reports.NeedType` (50), `Reports.PhotoUrl` (500), `Dispatches.ApprovalNotes` (2000), `ValidationResults.ViolationDetail` (2000), `AuditTrail.EventType` (50).
- `jsonb` columns: `WorkflowRuns.PlanJson`, `TriagePlans.GeneratedFromReportIds`, `TriagePlans.PlanSummaryJson`, `AuditTrail.EventDetailJson`. `AgentExecutionLog` input and output are stored as `text`.
- Delete behaviour:
  - Cascade from `WorkflowRuns` to `AgentExecutionLog`, `Routes`, `AllocationProposals`, `Dispatches` and `ValidationResults`.
  - Cascade from `Shelters` to `Reports`, and from `Depots` to `InventoryItems`.
  - Restrict on `Reports → Users` (reporter), `AllocationProposals → Depots` and `→ Shelters`, and `AuditTrail → Dispatches`, so audited or allocated records cannot be orphaned by deletion.
  - Set null on `Shelters → Users` (contact volunteer) and `TriagePlans → WorkflowRuns`.
- Quantities are stored as `double` (inventory, allocations) or `int` (report quantity); no decimal precision is configured.

Because a workflow run's dispatch, validation results and logs cascade on run deletion, deleting a run removes its audit history except for `AuditTrail` events, which are protected by the restrict rule on `Dispatches`. No endpoint deletes runs.

### 6.4 ER Diagram

[DIAGRAM: Full ER diagram — Users, Shelters, Reports, TriagePlans, Depots, InventoryItems, AllocationProposals, WorkflowRuns, AgentExecutionLog, Routes, Dispatches, ValidationResults, AuditTrail, with the relationships and delete behaviours listed in 6.2–6.3]

---

## 7. API Design

### 7.1 API Conventions

- Resource routes use the pattern `/api/{resource}`; the API is not versioned.
- Controllers are asynchronous throughout and accept a `CancellationToken` where the action supports it.
- Enums are serialized as strings API-wide (`JsonStringEnumConverter`); reference cycles are ignored during serialization.
- Request DTOs with DataAnnotations are used for report, shelter, depot, inventory and decision inputs. Several read endpoints return EF entities directly (shelters, reports, depots, inventory items); workflow and dispatch endpoints return response records or anonymous projections.
- Status codes: 201 Created for creates, 200 OK for reads and decisions, 400 for validation failures, 401/403 for authentication/role failures, 404 for missing records, 409 Conflict for state-machine violations on dispatch and validation endpoints, 422 for an unusable plan document, 500 for unhandled errors.
- Error shapes differ by component: Member A's endpoints throw domain exceptions mapped by global middleware to `{ statusCode, message, detail }`; Member C's return RFC 7807 problem details; Member D's return `{ error, message }` with a machine-readable code.
- Swagger UI and the OpenAPI document are enabled in the Development environment. `GET /health` returns `{ status: "Healthy" }`.

### 7.2 Controller Groups

| Controller | Main responsibility |
| --- | --- |
| `SheltersController` | Shelter create, list (search, status, pagination), detail with reports, update |
| `ReportsController` | Report create (multipart, photo, GPS, urgency scoring), list (filter, sort), detail, update, list triage plans, trigger triage for a report |
| `DepotsController` | Depot list and create |
| `InventoryController` | Inventory list (by depot), item create, stock check-in |
| `WorkflowsController` | Start a workflow run, get run detail, apply a coordinator decision |
| `ValidationController` | Run the Validation Agent on a `Validating` run; get validation results |
| `DispatchController` | Approve, reject, request revision; approval queue; dispatch status by run; confirm delivery; dispatch history; summary analytics |
| `AuditController` | Audit trail for one dispatch |

### 7.3 Representative Endpoints

| Method and route | Purpose | Authorization |
| --- | --- | --- |
| `POST /api/reports` | Submit a field report (multipart; photo file or URL, GPS) | None |
| `GET /api/reports?shelterId=&status=&urgency=&sort=` | List and filter reports | None |
| `POST /api/reports/{id}/trigger-triage` | Run the Triage Agent for one report | None |
| `GET /api/shelters?status=&search=&page=&pageSize=` | List shelters (page size ≤ 100) | None |
| `PUT /api/inventory/{id}/check-in` | Add received quantity to an inventory item | None |
| `POST /api/workflows` | Create a workflow run and run the Triage step | None |
| `GET /api/workflows/{id}` | Run state, failed stage and plan | None |
| `POST /api/workflows/{id}/decision` | Apply Approved / Rejected / RevisionRequested via the orchestrator | Coordinator |
| `POST /api/validations/run` | Run deterministic validation on a `Validating` run | Coordinator |
| `GET /api/dispatches/approval-queue` | Runs awaiting a decision | Coordinator |
| `POST /api/dispatches/{workflowRunId}/approve` | Approve and record dispatch and audit event | Coordinator |
| `POST /api/dispatches/{workflowRunId}/reject` | Reject with mandatory reason | Coordinator |
| `POST /api/dispatches/{workflowRunId}/request-revision` | Send back with mandatory notes | Coordinator |
| `POST /api/dispatches/{workflowRunId}/confirm-delivery` | Record delivery for an approved run | Coordinator |
| `GET /api/dispatches?status=&dateFrom=&dateTo=&page=&pageSize=` | Dispatch history | Coordinator |
| `GET /api/dispatches/summary` | Totals, average approval time, top rejection reasons | Coordinator |
| `GET /api/audit/{dispatchId}` | Audit events for one dispatch | Coordinator |
| `GET /health` | Liveness check | None |

In the Development environment, a request with no `Authorization` header is authenticated as a development Coordinator principal; a request with a header is validated as a JWT. This fallback is not registered in any other environment.

### 7.4 Validation and Error Handling

1. **Model binding and DataAnnotations** reject malformed input before controller logic: need type pattern, quantity ≥ 1, latitude/longitude ranges, urgency 0–100, URL format and length, depot name length, positive check-in quantity.
2. **Controller checks** confirm referenced records exist (shelter and reporter on report creation, depot on inventory creation, workflow run on every workflow endpoint) and enforce mandatory reject/revision text.
3. **Domain rules** enforce the workflow state machine (only `PendingApproval` can be decided; only `Validating` can be validated; only `Approved` can be delivered) and agent-level rules (Section 5).
4. **Database constraints** enforce uniqueness, required columns and referential integrity as a final backstop.
5. **Global exception middleware** maps `NotFoundException`, `BadRequestException`, `UnauthorizedException` and `ForbiddenException` to 404, 400, 401 and 403, and any other exception to 500. For 500 responses, the exception message is included in a `detail` field (Section 16.2).

---

## 8. React Web Application Design

### 8.1 Technology and Structure

The coordinator dashboard is built with React 18, TypeScript and Vite 5. The design reference is the IBM Carbon Design System (`@carbon/react`, `@carbon/styles`, `@carbon/icons-react`) with the FloodLink colour and typography tokens from `ui-context.md` defined as CSS custom properties in `index.css`. In practice, Carbon components are used in the Inventory dashboard, while the other screens are built from plain elements styled with Tailwind CSS utility classes and the shared CSS variables; Tailwind remains a dependency, contrary to the `ui-context.md` decision (ADR-006).

```
web/src/
  api/          client.ts, types.ts — typed calls to dispatch, validation and audit endpoints
  components/   one component per screen, plus Navbar, ShelterMap, ShelterDetailModal
  context/      AppContext.tsx — shared state provider
  lib/          apiError.ts — error message extraction
  types/        shared domain types
```

Navigation is a tab bar managed with local state; React Router is not used. The API base URL comes from `VITE_API_BASE_URL`.

### 8.2 Role-Specific Experience

The dashboard is a single coordinator-facing application without sign-in. Its tabs are:

- **Shelters** — list with search, status filter and pagination, a schematic coordinate plot, and a detail modal with report history.
- **Reports** — review queue filterable by shelter, status and urgency, sortable by urgency or date.
- **Triage** — lists persisted triage plans and lets the coordinator trigger triage for a selected report.
- **Inventory** — Carbon `DataTable` of stock per depot with a check-in action.
- **Approvals** — runs in `PendingApproval` with their validation checks and Approve / Reject / Request Revision actions.
- **Dispatches** — dispatch history with decision and date filters, pagination, and the audit trail for a dispatch.
- **Analytics** — totals by decision, average approval time and top rejection reasons.

There is no workflow monitor, execution-trace viewer or route map.

### 8.3 Design Principles

From `ui-context.md`: a calm, light-first, information-dense operations interface inspired by IBM Carbon, avoiding decorative "AI" styling (neon gradients, glows, robot imagery). All colours are CSS variables — page background `#F4F4F4`, surface `#FFFFFF`, primary text `#161616`, muted text `#6F6F6F`, accent `#0F62FE`, border `#C6C6C6`, error `#DA1E28`, warning `#F1C21B`, success `#198038`, info `#0043CE`. The accent colour is reserved for interactive elements; the info colour is reserved for in-progress status badges. Warning yellow is always paired with dark text. Workflow states map consistently to status colours: Triage/Matching/Routing/Validating → info; PendingApproval/RevisionRequested → warning; Approved → success; Rejected/Failed → error. Typography is IBM Plex Sans, with IBM Plex Mono for IDs, coordinates and raw JSON.

### 8.4 State and Data Handling

- Shared state goes through the React Context API only (`AppContext`); third-party state libraries are not permitted (CONTRIBUTING.md). The context currently holds the API base URL.
- Each screen holds its own data, filters, form state and loading/error flags in local `useState`.
- API calls use `fetch`; the approval, dispatch, validation and audit calls go through the typed module in `api/client.ts`, while other screens call `fetch` directly.
- No client-side caching or retry is implemented.

### 8.5 Approval User Experience

The approval queue lists each run awaiting a decision with its objective and creation time. Expanding a run loads its validation checks from `GET /api/validations/{id}` and shows each check with its pass/fail state and violation detail. The coordinator chooses Approve, Reject or Request Revision; the form requires a reason for rejection and notes for revision before it will submit, and the server enforces the same rule. After a decision the queue refreshes. The interface makes clear that the plan is a proposal awaiting the coordinator's decision.

---

## 9. Flutter Mobile Application Design

### 9.1 Scope

The Flutter app serves field users: volunteers reporting shelter needs (Member A), depot staff checking in stock (Member B), and field users checking dispatch status and confirming delivery (Member D). It uses Material 3 with the same colour tokens as the web dashboard, `provider` for shared state, `go_router` for navigation, `image_picker` for photos, `geolocator` for location, and `http` for API calls. The API base URL is set at build time with `--dart-define=FLOODLINK_API_BASE_URL=...`.

### 9.2 Structure

| Path | Responsibility |
| --- | --- |
| `lib/main.dart` | App entry, `MultiProvider`, Material 3 theme, colour tokens, routes |
| `lib/config/` | API base URL configuration |
| `lib/models/` | Typed report, shelter and inventory models |
| `lib/api/` | Dispatch API client and models |
| `lib/providers/` | `ReportProvider`, `InventoryProvider` (`ChangeNotifier`) |
| `lib/state/` | `AppState` |
| `lib/screens/` | Report list, new report, camera capture, GPS capture, my submitted reports, stock check-in, field home, dispatch status, delivery confirmation |

### 9.3 Field Volunteer Flow

1. Open the app at the field reports list.
2. Start a new report: choose shelter, need type and quantity; the form validates that quantity is a positive number.
3. Capture or select a photo (camera or gallery).
4. Capture the current GPS position, or adjust the location manually; permission denial is handled with a message.
5. Submit; the report is sent as multipart form data and the server computes its urgency.
6. Open "My submitted reports" to see each report's status (New → Triaged → InPlan → Resolved), refreshed automatically every 10 seconds.
7. On the dispatch screen, enter a workflow run ID to see its decision, notes and delivery state, then confirm delivery with optional notes and a photo reference.

Depot staff use the stock check-in screen: select a depot and item, enter the quantity received, and submit.

### 9.4 Design Principles

Task-first screens with large touch targets and minimal typing, camera- and GPS-first reporting, inline notifications for success, warning and error, and explicit loading, empty and error states. The app has no sign-in; reports are submitted as user ID 1. It has no offline queue, so a report cannot be submitted without connectivity.

---

## 10. Technical Implementation Report

### 10.1 Backend Implementation

The API uses ASP.NET Core controllers with constructor injection. `Program.cs` registers `AppDbContext` (Npgsql, connection string from configuration), scoped services and agents, the typed Mapbox `HttpClient` with its timeout, the orchestrator and its adapters, controllers with string-enum JSON, Swagger, CORS (React dev server on 5173, Flutter Web on 5174, plus origins from `Cors__AllowedOrigins`), problem details, JWT bearer authentication, and the Development-only coordinator fallback. The pipeline is: optional migrate-on-startup, Swagger (Development), global exception middleware, CORS, HTTPS redirection, authentication, authorization, controllers, and `/health`. If a `PORT` environment variable is present, the API listens on `0.0.0.0:{PORT}`. Startup fails if `Jwt:Issuer`, `Jwt:Audience` or `Jwt:SigningKey` is missing. The solution targets .NET 10 with central package management.

### 10.2 Field Intake Implementation

`POST /api/reports` accepts multipart form data, validates the DTO, checks that the shelter and reporter exist, saves an uploaded photo through `PhotoStorageService` (a GUID-named file under `wwwroot/uploads/reports`, keeping the client's file extension), and defaults GPS to the shelter's coordinates. Urgency is computed by `UrgencyScoringService` unless the client supplies a value:

- Occupancy (5–35 points): ≥ 100% → 35, ≥ 80% → 25, ≥ 50% → 15, otherwise 5; 15 if the shelter is unknown; zero capacity is treated as full.
- Need type (10–40 points): Medical 40, Water 35, Food 25, Shelter-Repair 15, other 10 (case- and whitespace-insensitive).
- Time since last resolved report (5–25 points): ≥ 48 h → 25, ≥ 24 h → 18, ≥ 12 h → 10, otherwise 5; 20 if none.
- The sum is clamped to 0–100.

The Triage Agent reapplies this function to each open report, updates the report's urgency and moves `New` reports to `Triaged`, ranks by score then quantity, builds a template justification ("[Critical/High/Moderate Priority] …"), and persists a `TriagePlans` row. `POST /api/reports/{id}/trigger-triage` runs the agent for one report with a new workflow run ID, which creates a `WorkflowRun` in `Triage` that the orchestrator never advances. [CONFIRM: status of the trigger-triage fix referenced by the team.] Uploaded photos are not served because static-file middleware is not enabled.

### 10.3 Inventory and Matching Implementation

Depots and inventory items are created through validated DTOs; stock check-in adds the received quantity and updates the timestamp. The Matching Agent reads all inventory with `AsNoTracking`, ordered by depot then item ID, and for each triage item in priority order takes stock from depots whose item name matches the need type (case-insensitive) until the quantity is met. It tracks remaining stock in memory so that two needs in one plan cannot both claim the same units. Any shortfall becomes an `Unfulfillable` entry: "No stock is available at any depot" or "Only X of Y could be allocated". If nothing is allocated, the agent returns `NO_STOCK_AVAILABLE`. Allocations are saved as `AllocationProposals` rows with status `Proposed`, and the first row's ID becomes the proposal's `AllocationProposalId`. Stock is not reserved, so two concurrent runs can propose the same units.

### 10.4 Routing Implementation

`MapboxClient` builds a Directions API request for the `driving` profile with `lng,lat` coordinate order, full-overview encoded polyline geometry, and the access token as a query parameter. It reads only `distance`, `duration` and `geometry` from the first route. The retry loop and timeout handling are described in Section 5.5. `RoutingAgentInvoker` takes the first allocation in the proposal, loads that depot and shelter from the database (`ROUTING_LOCATION_NOT_FOUND` if either is missing), calls the client, persists a `RouteEntity` (metres, seconds, polyline), and returns a contract `Route` with kilometres and minutes. Every exception path is converted to an `AgentResult` failure code. The agent itself does not check route plausibility; that is done by the Validation Agent's coordinate rule.

### 10.5 Validation Implementation

`SafetyRules` is a static class with no I/O. `RunAll` produces, for each allocation line, a `StockAvailability` check (quantity ≤ available − reserved) and a `ReserveMinimumThreshold` check (remaining free stock ≥ reorder threshold), followed by one `VehicleCapacity` check (total load ≤ vehicle capacity) and one `CoordinateSanity` check (latitude and longitude in range, origin ≠ destination, distance > 0 and ≤ 400 km, ETA > 0 and ≤ 1440 minutes). Every check is listed, whether it passes or fails. `ValidationAgent` loads the run, deserializes `PlanJson` as a `PlanDocument` (case-insensitive), confirms it belongs to the same run, and returns all checks with `OverallPassed`. The inputs it relies on (`VehicleCapacity`, `QuantityReserved`, `ReorderThreshold`, origin and destination coordinates) are not produced by any upstream agent (Section 2.4).

### 10.6 Orchestration Implementation

`WorkflowEngine` and `WorkflowOrchestrator` are described in Section 5.2. `AgentExecutionLogger` writes one row per call, with status `Success`, `Error` or `Retried`, and saves immediately. `WorkflowRunRepository` and `RouteRepository` wrap `AppDbContext` so the orchestrator and Route/ETA Agent can be unit-tested without a database.

---

## 11. Software Testing Report

### 11.1 Test Strategy

- **Static verification:** C# Release build of the full solution; TypeScript compile (`tsc`) and Vite production build; `flutter analyze --fatal-infos`.
- **Unit tests (xUnit):** state machine transitions, urgency scoring, safety rules, agent behaviour with in-memory EF Core or fakes, execution logger round-trip.
- **Integration tests:** `WebApplicationFactory`-hosted API against a real PostgreSQL database for validation, approval, rejection, revision, delivery, approval queue, summary and history endpoints; controller-level tests for shelters and reports.
- **Pipeline tests:** the orchestrator driven through all four steps with fake or real agents.
- **Flutter widget tests:** report form rendering and validation, camera and GPS screen rendering.
- **Manual end-to-end:** [CONFIRM: whether a manual end-to-end run was performed, and its result]

No React component tests exist; ESLint is configured but not run in CI.

### 11.2 Test Environment

| Layer | Local environment |
| --- | --- |
| Database | PostgreSQL 16 in Docker (`docker-compose.yml`), port 5432; test database `floodlink_test` |
| API | .NET 10, `http://localhost:5000` |
| Web | Vite development server, `http://localhost:5173` |
| Mobile | Flutter Web on port 5174 (`flutter run -d chrome --web-port 5174`); [CONFIRM: emulator / physical device used] |
| Routing | Mapbox Directions API with a developer token (unit tests use a fake client) |

### 11.3 CI and Git Workflow

Two GitHub Actions workflows are defined.

- **`ci.yml` (Continuous Integration)** runs on every push and pull request to `main`, with three jobs:
  - *Backend — Build & Test:* starts a `postgres:16` service container, sets up .NET 10, restores and builds the solution in Release, and runs `dotnet test`.
  - *Web — Install & Build:* Node 20, `npm ci`, `npm run build` (TypeScript check and Vite build).
  - *Mobile — Analyze:* Flutter stable, `flutter pub get`, `flutter analyze --fatal-infos`. Flutter widget tests are not run in CI.
- **`release-apk.yml` (Build & Release Android APK)** runs on a `v*` tag or manually. It sets up Java 17 and Flutter, fails if the `FLOODLINK_API_BASE_URL` repository secret is missing, builds a release APK with that URL, and publishes it as a GitHub Release.

Branch and review rules (CONTRIBUTING.md): feature branches named `feature/<initials>-<description>`, no direct commits to `main`, CI must pass, at least one approval per PR, and all four approvals for changes to `FloodLink.Contracts`.

[SCREENSHOT: GitHub Actions run of `ci.yml` showing the three jobs passing]

### 11.4 Functional Test Matrix

The backend test project declares 116 test methods across 15 files. The progress tracker records all 116 passing after the final merge. [CONFIRM: latest CI run result and date]

| ID | Scenario (test class) | Expected result |
| --- | --- | --- |
| T-01 | Each of the 12 valid transitions (`WorkflowEngineTests`) | Transition applied, `UpdatedAt` set |
| T-02 | Invalid transitions, e.g. Triage → Approved, PendingApproval → Triage, any transition out of a terminal state (`WorkflowEngineTests`, `WorkflowStateTransitionsTests`) | Rejected; run unchanged |
| T-03 | Failure from Routing and from Validating (`WorkflowEngineTests`) | `FailedAtState` records the stage |
| T-04 | Full pipeline with fake agents (`WorkflowOrchestratorTests`) | Run reaches `PendingApproval` with four log entries |
| T-05 | Triage / Matching / Validation failure (`WorkflowOrchestratorTests`) | Run `Failed`, error logged, correct `FailedAtState` |
| T-06 | Coordinator decision on a non-pending run; Approved; RevisionRequested (`WorkflowOrchestratorTests`) | Rejected; `Approved`; auto re-queue to `Matching` |
| T-07 | Pipeline with the real Matching Agent (`MemberBPipelineIntegrationTests`) | Reaches `PendingApproval`; one allocation row persisted |
| T-08 | Execution log write and read-back (`AgentExecutionLoggerTests`) | All fields round-trip |
| T-09 | Route agent success (`RoutingAgentInvokerTests`) | Correct km/minutes; `Routes` row persisted |
| T-10 | Route agent: no allocations, no route, timeout, HTTP error (`RoutingAgentInvokerTests`) | `NO_ALLOCATIONS`, `NO_ROUTE`, `MAPBOX_TIMEOUT`, `MAPBOX_HTTP_ERROR` |
| T-11 | Matching: full, partial and zero stock (`MatchingAgentTests`) | Allocation persisted; partial need listed as unfulfillable; safe failure |
| T-12 | Each safety rule pass/fail, safe plan, over-allocated stock, overloaded truck (`SafetyRulesTests`) | Correct pass/fail and violation detail; unsafe plans blocked |
| T-13 | Urgency scoring edge cases: zero occupancy, zero capacity, null shelter, null need type, case/padding, resupply intervals, clamping (`UrgencyScoringServiceTests`) | Expected scores; no exceptions |
| T-14 | Triage ranking and edge cases (`TriageAgentTests`) | Critical medical need ranked first; descending order; missing data handled |
| T-15 | Report submission validation: unknown shelter, unknown reporter, invalid model; valid report with auto urgency; trigger triage end to end (`ReportSubmissionAndTriageIntegrationTests`) | 400 / created with urgency / persisted triage plan |
| T-16 | Shelter search/filter/pagination and report filter/sort (`ShelterAndReportController*Tests`) | Correct results and response types |
| T-17 | Validate a valid / failing plan; wrong state; missing plan (`WorkflowIntegrationTests`) | `PendingApproval` / `Failed` / 409 / 422 |
| T-18 | Approve pending, wrong state, unknown run (`WorkflowIntegrationTests`) | Dispatch and audit created / 409 / 404 |
| T-19 | Reject or request revision without and with text (`WorkflowIntegrationTests`) | 400 / recorded with audit event |
| T-20 | Approval queue, delivery confirmation, summary, history filter (`WorkflowIntegrationTests`) | Only pending runs; delivery audited or 409; correct counts; unknown status rejected |
| T-21 | Flutter: report form fields and quantity validation; camera and GPS screens render (`mobile/test`, 6 widget tests) | Validation messages shown; screens render |

Not covered: the Mapbox client's own retry loop (tests replace the client), restart recovery, concurrent runs, role enforcement outside Development, and the Validation Agent run through the orchestrator on a real plan.

### 11.5 Agent Acceptance Test

**Positive case:** a run driven through all four orchestrator steps reaches `PendingApproval` with one execution log entry per agent (`FullPipeline_AdvancesFromTriageToPendingApproval_WithFourLogEntries`; `RealMatchingAgent_AllowsPipelineToReachPendingApproval` with the real Matching Agent). A seeded `PendingApproval` run approved through the API produces a dispatch record and a `DispatchApproved` audit event (`Approve_PendingPlan_CommitsDispatchAndAudits`).

**Negative cases:**

- No stock for the requested need: the Matching Agent returns `NO_STOCK_AVAILABLE` (`ExecuteAsync_WhenNoStockMatches_ReturnsSafeFailure`); a Matching failure moves the run to `Failed` with `FailedAtState = Matching` (`MatchingFailure_TransitionsToFailed_AndRecordsMatchingAsFailedStage`).
- Mapbox timeout or HTTP error: the Route/ETA Agent returns `MAPBOX_TIMEOUT` or `MAPBOX_HTTP_ERROR` without throwing (`RoutingAgentInvokerTests`); any agent failure moves the run to `Failed` with the stage recorded (`WorkflowOrchestratorTests`).
- Unsafe plan: over-allocated stock or an overloaded truck fails validation and the run moves to `Failed`, not `PendingApproval` (`SafetyRulesTests`, `Validate_FailingPlan_TransitionsToFailed`).

In every negative case no dispatch row is created.

### 11.6 Defect and Regression Handling

[CONFIRM: defect log — any defect that materially affected schema, authorization, matching, routing, validation or the approval flow, with reproduction steps, fix commit and retest status]

Defects identified while preparing this report, not yet fixed:

- Pipeline cannot advance past Matching through the API (Section 2.4).
- Orchestrator `PlanJson` shape is incompatible with the Validation Agent's `PlanDocument` (Section 2.4).
- Approver identity not recorded on dispatches or audit events (Section 12.3).
- A run that is revised and later approved again would create a second `Dispatches` row against the unique index on `WorkflowRunId`; this path is untested.
- Uploaded report photos are not served (no static-file middleware).

---

## 12. Agentic AI Evaluation Report

### 12.1 Acceptance-Criteria Mapping

| Specification requirement | FloodLink AI implementation | Status |
| --- | --- | --- |
| Domain objective | Run created with a free-text objective; the Triage Agent triages all open reports | Implemented |
| Structured multi-step plan | Fixed four-step sequence; typed outputs merged into `PlanJson` under fixed keys. No model-generated plan. | Partial |
| Distinct agents | Four agents in separate projects with different inputs, rules, data access and outputs | Implemented |
| Controlled tools | No tool gateway or allow-list; only the Route/ETA Agent is given the external client; agents access the database directly | Not implemented |
| Structured outputs | Typed `FloodLink.Contracts` records wrapped in `AgentResult<T>` | Implemented |
| Persisted shared state | Run state, plan, failed stage, agent log, route, allocations, validation results, dispatches and audit trail in PostgreSQL | Implemented |
| Deterministic checks | Four rule types in `SafetyRules`, unit-tested | Implemented (not yet fed by the orchestrated plan) |
| Human approval | `PendingApproval` with Coordinator-only decision endpoints and mandatory reasons | Implemented |
| Auditability | Per-agent execution log; dispatch and audit events. Approver identity not recorded; no log-viewing endpoint | Partial |
| Safe failure | Anticipated and unexpected agent failures → `Failed` with stage recorded; no dispatch created | Implemented |
| End-to-end run via clients | Pipeline driven only by tests beyond the Triage step | Not implemented |
| LLM / agent framework | Not used (ADR-004) | Not implemented |

### 12.2 Strengths

- The state machine is explicit, small and fully tested: every valid transition and a sample of invalid ones have their own test, and invalid transitions leave the run unchanged.
- Failure is recorded rather than hidden: every agent call is logged, and a failed run records the stage where it failed.
- The agents operate on real domain data — shelters, reports, depot stock, real road distances — rather than mock prompts.
- Unmet needs are reported explicitly rather than dropped.
- Validation is deterministic and explainable, and each check states its violation in plain language.
- The orchestrator depends only on interfaces, so each agent can be replaced or faked independently.
- Deterministic agents make results reproducible for a live demonstration, apart from Mapbox's response.

### 12.3 Limitations

- No language model is used; the Triage justification is a template, and natural-language objectives are not interpreted.
- The pipeline does not advance past Matching through the API, and the orchestrated plan cannot be validated by the current Validation Agent (Section 2.4).
- There is no tool gateway or per-agent allow-list, and three agents write to the database.
- Approval does not reserve or decrement stock, and does not re-validate against current data.
- The approving coordinator is not recorded (`ApprovedById` and `ActorId` are never set), so the audit trail shows that a decision was made but not by whom.
- There are two decision paths and two transition implementations with slightly different rules.
- The Route/ETA Agent routes only the first allocation of a multi-depot proposal; Mapbox retries have no backoff.
- `ToolCallsJson` is never populated and the `Retried` status is never emitted.
- No endpoint or screen exposes the execution log.

### 12.4 Evaluation Scenarios

| Scenario | Expected agent behaviour | Final result | Evidence |
| --- | --- | --- | --- |
| Stock available for all needs | Matching allocates; Routing returns a route; Validation passes | `PendingApproval` → coordinator decides | Orchestrator and pipeline tests |
| Partial stock | Matching allocates what exists and lists the remainder as unfulfillable | Proceeds with partial allocation | `MatchingAgentTests` |
| No stock | Matching returns `NO_STOCK_AVAILABLE` | `Failed`, `FailedAtState = Matching` | `MatchingAgentTests`, `WorkflowOrchestratorTests` |
| Mapbox timeout | Up to three attempts, then `MAPBOX_TIMEOUT` | `Failed`, `FailedAtState = Routing` | `RoutingAgentInvokerTests` (agent level) |
| Mapbox returns no route / 4xx | `NO_ROUTE` | `Failed` at Routing | `RoutingAgentInvokerTests` |
| Over-allocated stock or overloaded truck | Validation check fails with violation detail | `Failed`, no approval request | `SafetyRulesTests`, `WorkflowIntegrationTests` |
| Implausible route (zero, negative, > 400 km, > 24 h) | `CoordinateSanity` fails | `Failed` | `SafetyRulesTests` |
| Coordinator rejects without reason | Server rejects request | 400, run unchanged | `WorkflowIntegrationTests` |
| Coordinator rejects with reason | Dispatch and audit recorded | `Rejected`, no supplies committed | `WorkflowIntegrationTests` |
| Coordinator requests revision | Dispatch and audit recorded | `RevisionRequested`; orchestrator path re-queues to `Matching` | `WorkflowIntegrationTests`, `WorkflowOrchestratorTests` |
| Decision on a run not in `PendingApproval` | Rejected by state machine | 409 / 400 | `WorkflowIntegrationTests`, `WorkflowOrchestratorTests` |

---

## 13. Performance Report

### 13.1 Performance Goals

These are targets for a demonstration-scale dataset. None has been measured.

| Area | Target | Measurement method |
| --- | --- | --- |
| Common list API (shelters, reports, inventory) | p95 below 500 ms | [CONFIRM: tool, e.g. k6] |
| Detail API (shelter, report, workflow run) | p95 below 400 ms | [CONFIRM: tool] |
| Report submission (with photo) | p95 below 1 s | [CONFIRM: tool] |
| Single orchestrator step (excluding Mapbox) | Below 1 s | `AgentExecutionLog.DurationMs` |
| Routing step | Below 3 s typical; bounded by 3 × timeout | `AgentExecutionLog.DurationMs` |
| React initial load | LCP below 2.5 s | [CONFIRM: Lighthouse] |

### 13.2 Implemented Performance Controls

- Asynchronous EF Core and `HttpClient` calls throughout; no blocking `.Result` or `.Wait()`.
- `AsNoTracking` on read-only queries (lists, details, matching snapshot, validation, dispatch queries).
- Pagination with an upper bound of 100 on shelter and dispatch lists.
- Indexes on inventory (depot + item), dispatch (workflow run), validation results (workflow run), audit trail (dispatch), and all foreign keys.
- Typed `HttpClient` registered through `IHttpClientFactory`, with a configured timeout and bounded retries on the Mapbox call.
- Vite production bundling for the web app.

Known costs: the report list is not paginated; `GET /api/dispatches/summary` loads all dispatches into memory; the Triage Agent issues one resupply-time query per report.

### 13.3 Results

No load test, Lighthouse audit or agent-latency measurement was performed. [CONFIRM: if any measurements are taken before submission, insert them here]

[SCREENSHOT: load test results, if performed]
[SCREENSHOT: Lighthouse report for the web dashboard, if performed]

---

## 14. Deployment Report

### 14.1 Deployment Architecture

| Component | Platform | Notes |
| --- | --- | --- |
| React web app | [CONFIRM: web hosting platform] | Built with `npm run build`; receives only `VITE_API_BASE_URL` |
| ASP.NET Core API | [CONFIRM: API hosting platform] | Multi-stage Dockerfile (.NET 10 SDK → ASP.NET runtime), binds to `PORT` when set, applies migrations on startup; requires `Jwt__SigningKey`, `Mapbox__ApiKey`, `ConnectionStrings__DefaultConnection`, `Cors__AllowedOrigins` |
| PostgreSQL | [CONFIRM: database hosting platform] | PostgreSQL 16 locally |
| Maps / routing | Mapbox Directions API | Called only by the API |
| Report photos | API host file system | Not served (Section 2.4); not persistent on ephemeral container storage |
| Android app | GitHub Releases | Release APK built by `release-apk.yml` with the API URL from a repository secret |
| Source and CI | GitHub, GitHub Actions | `ci.yml`, `release-apk.yml` |

Hosted environments run outside `Development`, so the coordinator endpoints return 401 until a login flow exists, and Swagger is disabled. [CONFIRM: environment setting used for the demo deployment]

[SCREENSHOT: deployed web dashboard]
[SCREENSHOT: deployed API health endpoint]
[SCREENSHOT: APK release on GitHub]

---

## 15. Architecture Decision Records

**ADR-001 — Monorepo with a layered modular monolith.** *Context:* four members each own a vertical slice (backend, web, mobile, agent), and the domain data is tightly related and transactional. *Decision:* one repository with `backend/`, `web/` and `mobile/`; one ASP.NET Core API split into Contracts, Domain, Infrastructure, four Agent projects and Api. *Rationale:* a single build, database and deployment unit, with project boundaries enforcing ownership and dependency direction. *Consequences:* all agents run in the API process and scale together; the Api project must hold adapter glue because Domain cannot reference the Agent projects.

**ADR-002 — PostgreSQL with EF Core code-first migrations.** *Context:* the system needs relational integrity across reports, stock, allocations, runs and dispatches, plus storage for variable-shaped plan data. *Decision:* PostgreSQL 16 via EF Core and Npgsql, committed migrations, `jsonb` for plan and audit detail. *Rationale:* one store for relational and semi-structured data; migrations make schema changes reviewable. *Consequences:* members' migrations must be sequenced and regenerated on merge, as happened with `AddMemberDTables`.

**ADR-003 — Separate React and Flutter clients that talk only to the API.** *Context:* coordinators need dense review screens; field users need camera, GPS and minimal typing. *Decision:* React for the coordinator dashboard, Flutter for field users; both call only the API. *Rationale:* each client matches its users and devices, and all validation and authorization stay server-side. *Consequences:* shared behaviour is defined only by API contracts; the clients' models are maintained separately.

**ADR-004 — Deterministic C# agents and orchestrator, without an LLM or agent framework.** *Context:* the build specification mandated LangGraph (or an equivalent framework) with an LLM producing structured output, and allowed the Validation Agent to be rule-based. *Decision:* all four agents are deterministic C# services, sequenced by a hand-written orchestrator over an explicit enum-based state machine (chosen over the Stateless library). This differs from the module's expectation of an LLM-based agent. *Rationale:* [CONFIRM: rationale agreed by the team] — the reasons recorded in the code are that a 12-transition switch is dependency-free and fully explainable, that deterministic results are reproducible and testable, and that validation must not rely on an LLM judge. *Consequences:* no natural-language planning or reasoning; the "agentic" character rests on distinct agents, typed contracts, persisted state, safe failure and human approval rather than on model-driven planning. Adding an LLM later would sit behind the existing invoker interfaces and `AgentResult<T>` contract.

**ADR-005 — Mandatory human approval before dispatch.** *Context:* committing supplies and a vehicle to one shelter affects others in need. *Decision:* agents only propose; a run waits in `PendingApproval` until a Coordinator approves, rejects (with reason) or requests revision (with notes); dispatch records are created only by these actions. *Rationale:* accountability and a final human check on an automated plan. *Consequences:* dispatch depends on coordinator availability; approver identity must still be recorded to complete the audit.

**ADR-006 — IBM Carbon Design System rather than Tailwind/shadcn.** *Context:* the dashboard is an operations tool used under pressure. *Decision:* `ui-context.md` adopts Carbon (`@carbon/react`, `g10` theme) with FloodLink tokens, and states that Tailwind and shadcn are not used on web. *Rationale:* Carbon's data tables, tags, tiles and modals map onto runs, statuses and approvals, and its style suits an information-dense interface. *Consequences:* adoption is partial — only the Inventory dashboard uses Carbon components; other screens use Tailwind classes with the shared tokens, and Tailwind remains a dependency.

**ADR-007 — Context API (web) and Provider (mobile) for shared state.** *Context:* a four-person, nine-week project needs one consistent state approach per client. *Decision:* React Context API only on web; `provider` with `ChangeNotifier` only on mobile; no Redux, Zustand, Riverpod, BLoC or similar (CONTRIBUTING.md). *Rationale:* built-in or minimal libraries, easy to explain and review. *Consequences:* no client-side caching or request deduplication; each screen fetches its own data.

**ADR-008 — Offline sync not implemented.** *Context:* `ui-context.md` notes that offline-friendly reporting implies a local queue (e.g. `drift`, `hive`, `sqflite`) and needs its own design decision. *Decision:* offline storage and sync were not built. *Rationale:* [CONFIRM: rationale — e.g. time constraints and priority on the agent pipeline]. *Consequences:* a report cannot be submitted without connectivity, which is a real limitation for flood conditions.

**ADR-009 — Mapbox Directions for routing.** *Context:* the Route/ETA Agent needs road distance and travel time; `ui-context.md` recommended choosing the map renderer and routing API together. *Decision:* Mapbox Directions (`driving` profile), called only from the backend through a typed `HttpClient` with timeout and bounded retries; the token is server-side only. *Rationale:* real road distance and travel time with a free tier, and the option to reuse the provider for map rendering. *Consequences:* routing depends on an external service and token; Mapbox is not yet used for map display in either client.

---

## 16. Security and Privacy Considerations

### 16.1 Authentication and Authorization

The API validates JWT bearer tokens for issuer, audience, lifetime (zero clock skew) and HMAC signature, and reads the role from the standard role claim. Coordinator-only endpoints — workflow decisions, validation, dispatch and audit — require the `Coordinator` role. The decision endpoints additionally require the run to be in `PendingApproval`.

This is not a complete authentication system:

- There is no login, registration or token-issuing endpoint, and no password hashing; seeded users hold placeholder password strings.
- Shelter, report, depot, inventory and workflow create/read endpoints have no authorization, so any client can submit reports, change stock, or start workflows.
- In the Development environment, any request without an `Authorization` header is treated as a Coordinator. This fallback is registered only in Development.
- The delivery-confirmation endpoint used by the field app requires the Coordinator role.
- Neither client sends a token.

Before production use, the system needs a login flow, role assignment, authorization on every write endpoint, and a field-user role for delivery confirmation.

### 16.2 Input and Output Protection

- DTO validation restricts need types, quantities, coordinate ranges, urgency range, URL format and string lengths.
- Referenced records are checked to exist before writes.
- Reject and revision text is required server-side.
- The state machine rejects out-of-order transitions.
- The Validation Agent treats plan data as untrusted: it re-checks quantities, coordinates and route plausibility deterministically and does not interpret free text, so report text cannot influence its decisions.
- Gaps: photo uploads have no file-size, content-type or file-signature validation and keep the client-supplied extension; 500 responses include the exception message in a `detail` field, which can expose internal details; some read endpoints return EF entities directly.

### 16.3 Secret Management

- The JWT signing key must come from user-secrets or the `Jwt__SigningKey` environment variable; startup fails without it.
- The Mapbox token is held in a gitignored `appsettings.Development.json` locally or the `Mapbox__ApiKey` environment variable when hosted; the committed configuration holds a placeholder. Web and mobile clients never receive it.
- The APK's API URL comes from a GitHub repository secret.
- The local development database password (`floodlink_dev_pw`) is committed in `appsettings.json` and `docker-compose.yml`; it is a development-only credential and must be overridden by environment variables in any hosted deployment.

### 16.4 Agent Safety

- Agents never change inventory, create dispatch records, or change workflow state. State changes go through the guarded state machine; dispatch records are created only by Coordinator-authorized endpoints.
- Agents are not read-only: the Triage, Matching and Route/ETA agents write their own output tables and the Triage Agent updates report status and urgency (Section 5.1).
- Each agent receives only the dependencies it needs; only the Route/ETA Agent can make external calls, and only to the Mapbox Directions endpoint.
- External calls have a timeout and a fixed retry limit; the orchestrator runs one step per call.
- Anticipated and unexpected failures end in a recorded `Failed` state rather than a partial plan reaching approval.
- Every agent invocation is logged with input, output, duration and error.
- No LLM is used, so prompt-injection risk does not apply to the current agents.

### 16.5 Privacy Considerations

The system stores user names, roles and phone numbers; shelter locations and contact volunteers; report GPS coordinates, which may correspond to individuals' locations; and report photos, which may show people or homes. A production deployment would need authenticated access to all personal data, a retention policy for photos and locations, consent and privacy notices for volunteers and shelter occupants, access logging, encrypted backups, and private storage for photos with access-controlled URLs. Seed and demonstration data use fictional names and placeholder phone numbers.

---

## 17. Consolidated Group AI Usage Declaration

### 17.1 Group Statement

The group used generative AI tools as development support for planning, architecture discussion, code drafting, debugging, test writing, documentation and report drafting. AI output was not treated as authoritative: each member was responsible for reviewing generated changes, adapting them to the agreed contracts and architecture, running the relevant build and tests, and being able to explain and modify the result.

### 17.2 Where AI Was Used

- Project planning and task breakdown (the build specification and member plans).
- Architecture and contract design, including the state machine and agent JSON contracts.
- Backend, database and migration code drafting and debugging.
- React and Flutter screen scaffolding and styling.
- Test generation for agents, the state machine and API endpoints.
- CI and deployment troubleshooting.
- Report drafting and proofreading, including this document.

[CONFIRM: specific AI tools used by each member]

### 17.3 Group Declaration

We declare that:

1. This report identifies AI-assisted work honestly.
2. We reviewed and take responsibility for the submitted code and writing.
3. We did not knowingly submit secrets, unverified or fabricated evidence, or copied proprietary material.
4. We can each explain our individually owned components and the shared integration.
5. Each member's detailed AI usage log appears in their individual report section.

[CONFIRM: finalize wording with team]

---

## PART B — Individual Reports

Team contribution overview: [CONFIRM: repository contributors URL, e.g. `https://github.com/SankaChathuranga/FlodLinkAI/graphs/contributors`]

### B1. Member A — [CONFIRM: full name and student ID] — Component A: Field Intake, Shelter Reporting and Triage/Planner Agent

GitHub contributions: [CONFIRM: commits URL filtered by author]

Contribution statement: [CONFIRM: fill in]

Owned work:

- Backend and database: [CONFIRM: fill in]
- React: [CONFIRM: fill in]
- Flutter: [CONFIRM: fill in]
- Business operation beyond CRUD: [CONFIRM: fill in]
- Agent — Triage/Planner Agent: [CONFIRM: fill in responsibility and restrictions]

Repository areas attributed to this component (for the member to verify): `SheltersController`, `ReportsController`, `UrgencyScoringService`, `PhotoStorageService`, `FloodLink.Agents.Triage`, migration `AddMemberAFieldIntakeAndShelterReporting`, web `SheltersDashboard`, `ReportsQueue`, `ShelterDetailModal`, `ShelterMap`, `TriagePlanViewer`, mobile report, camera, GPS and "My submitted reports" screens.

AI usage log: [CONFIRM: fill in]

### B2. Member B — [CONFIRM: full name and student ID] — Component B: Inventory and Depot Management and Logistics/Matching Agent

GitHub contributions: [CONFIRM: commits URL filtered by author]

Contribution statement: [CONFIRM: fill in]

Owned work:

- Backend and database: [CONFIRM: fill in]
- React: [CONFIRM: fill in]
- Flutter: [CONFIRM: fill in]
- Business operation beyond CRUD: [CONFIRM: fill in]
- Agent — Logistics/Matching Agent: [CONFIRM: fill in responsibility and restrictions]

Repository areas attributed to this component (for the member to verify): `DepotsController`, `InventoryController`, `Depot`, `InventoryItem` and `AllocationProposalEntity` entities, migration `AddMemberBInventoryAndMatching`, `FloodLink.Agents.Matching`, design-time `DbContext` factory, web `InventoryDashboard`, mobile `StockCheckInScreen` and `InventoryProvider`.

AI usage log: [CONFIRM: fill in]

### B3. Member C — [CONFIRM: full name and student ID] — Component C: Orchestration, Workflow Engine and Route/ETA Agent

GitHub contributions: [CONFIRM: commits URL filtered by author]

**Contribution statement.** I owned Component C: the workflow state machine, the orchestrator that sequences the four agents, the agent execution log, and the Route/ETA Agent with its Mapbox integration. I also set up the repository structure, the shared contracts project, the CI workflow, the APK release workflow and the API container configuration, and led the integration of the four members' branches into `main`.

**Owned work**

- **Backend and database**
  - `WorkflowRuns`, `AgentExecutionLog` and `Routes` tables, with migrations `AddFailedAtState`, `AddAgentExecutionLogErrorMessage` and `ReplaceRoutesSchema`. `WorkflowRuns.PlanJson` is `jsonb`; `CurrentState` and `FailedAtState` are stored as strings.
  - `WorkflowsController`: `POST /api/workflows` (create a run and run the Triage step), `GET /api/workflows/{id}`, and the Coordinator-only `POST /api/workflows/{id}/decision`.
  - JWT bearer validation configuration, with the signing key required from user-secrets or environment variables.
  - Repository interfaces (`IWorkflowRunRepository`, `IRouteRepository`) so the orchestrator and Route/ETA Agent can be tested without a database.
- **Business operation beyond CRUD — guarded state machine.** `WorkflowEngine.TryTransition` implements the 12-transition table in Section 5.2 as an enum switch. It returns `false` and leaves the run unchanged for any other transition, and records `FailedAtState` on failure. I chose an enum switch over the Stateless library because 12 transitions fit in one readable expression, it adds no dependency, and every rule is visible in one place.
- **Orchestration.** `WorkflowOrchestrator.AdvanceAsync` runs exactly one agent per call, chosen by the run's state, logs it through `IAgentExecutionLogger`, merges the output into `PlanJson`, and transitions. Unexpected exceptions are caught, logged and converted to `Failed`. `ApplyCoordinatorDecisionAsync` reuses `TryTransition` and re-queues a revision to `Matching`. The orchestrator depends only on four invoker interfaces; I added the adapters in the Api project that bind each member's agent to them.
- **Agent — Route/ETA Agent.** `RoutingAgentInvoker` loads the depot and shelter coordinates for the first allocation from the database (this lookup replaced my original placeholder coordinates when Member B's persisted allocation IDs became available), calls `MapboxClient`, persists a `Routes` row and returns a `Route` contract (km, minutes, polyline). `MapboxClient` uses a typed `HttpClient` with a configurable timeout and up to two retries on timeout, network error or 5xx, and does not retry 4xx. Every failure is returned as an `AgentResult` code (`NO_ALLOCATIONS`, `ROUTING_LOCATION_NOT_FOUND`, `NO_ROUTE`, `MAPBOX_TIMEOUT`, `MAPBOX_HTTP_ERROR`, `ROUTING_UNEXPECTED_ERROR`), and the orchestrator moves the run to `Failed` at `Routing`. The agent cannot modify allocations, inventory or workflow state, and its only external call is to the Mapbox Directions endpoint. The Mapbox token is server-side only.
- **Tests.** `WorkflowEngineTests` (24), `WorkflowOrchestratorTests` (8), `RoutingAgentInvokerTests` (6), `AgentExecutionLoggerTests` (1), and the contract checks in `PlaceholderTests` (3).
- **CI/CD and deployment.** The initial `ci.yml` with backend (Release build, tests), web (build) and mobile (analyze) jobs — Member D later added the Postgres 16 service container and path fixes; `release-apk.yml` for APK releases with the API URL from a repository secret; the API Dockerfile, `PORT` binding, configurable CORS origins and migrate-on-startup.
- **Integration.** Merged Members A, B and D into `main`, resolving conflicts in `Program.cs` and the migration snapshot, and regenerating Member D's migration on top of the merged snapshot.

**Planned but not completed (Component C)**

- Endpoints `GET /api/workflows` (list), `/logs`, `/status`, `/retry`, `POST /api/routes/calculate` and `GET /api/routes/{id}`.
- A trigger that advances a run beyond the Triage step in the running API.
- React workflow monitor, execution trace viewer and route map preview; Flutter workflow status tracker, route map and local notifications.
- Retry backoff, a test of the Mapbox client's own retry loop, restart-recovery and concurrent-run tests, and performance measurements.
- Consolidation of the two transition implementations and the two coordinator-decision paths, and alignment of the orchestrator's `PlanJson` with the Validation Agent's `PlanDocument`.

**Challenges.** [CONFIRM: fill in — e.g. merging four branches with overlapping migrations and `Program.cs` registrations; agreeing the `Routes` schema; keeping the orchestrator independent of agent projects]

**AI usage log.** [CONFIRM: fill in tools, tasks and how output was verified]

### B4. Member D — [CONFIRM: full name and student ID] — Component D: Validation, Approval and Dispatch Audit and Validation/Safety Agent

GitHub contributions: [CONFIRM: commits URL filtered by author]

Contribution statement: [CONFIRM: fill in]

Owned work:

- Backend and database: [CONFIRM: fill in]
- React: [CONFIRM: fill in]
- Flutter: [CONFIRM: fill in]
- Business operation beyond CRUD: [CONFIRM: fill in]
- Agent — Validation/Safety Agent: [CONFIRM: fill in responsibility and restrictions]

Repository areas attributed to this component (for the member to verify): `ValidationController`, `DispatchController`, `AuditController`, `WorkflowStateService`, `WorkflowStateTransitions`, `DevCoordinatorHandler`, `Dispatch`, `ValidationResult` and `AuditTrail` entities, migration `AddMemberDTables`, `PlanDocument` contract, `FloodLink.Agents.Validation` (`SafetyRules`, `ValidationAgent`), web `ApprovalQueue`, `DispatchHistory`, `Analytics`, mobile dispatch status and delivery confirmation screens.

AI usage log: [CONFIRM: fill in]

---

## 18. References

1. Microsoft, "ASP.NET Core documentation," https://learn.microsoft.com/aspnet/core/
2. Microsoft, "Entity Framework Core documentation," https://learn.microsoft.com/ef/core/
3. Npgsql, "Npgsql Entity Framework Core provider," https://www.npgsql.org/efcore/
4. PostgreSQL Global Development Group, "PostgreSQL documentation," https://www.postgresql.org/docs/
5. React Team, "React documentation," https://react.dev/
6. Vite Team, "Vite guide," https://vite.dev/guide/
7. IBM, "Carbon Design System," https://carbondesignsystem.com/
8. Flutter Team, "Flutter documentation," https://docs.flutter.dev/
9. Mapbox, "Directions API documentation," https://docs.mapbox.com/api/navigation/directions/
10. GitHub, "GitHub Actions documentation," https://docs.github.com/actions
11. xUnit.net, "xUnit.net documentation," https://xunit.net/
12. OWASP Foundation, "OWASP Application Security Verification Standard," https://owasp.org/www-project-application-security-verification-standard/
13. [CONFIRM: any other specific service used, e.g. the hosting platforms]
14. SLIIT, "SE3090 Software Engineering Frameworks Assignment 1 Specification 2026," module material supplied to students.
