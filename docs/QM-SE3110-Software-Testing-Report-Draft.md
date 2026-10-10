# Software Testing and Quality Evaluation Report

## FloodLink AI — AI-Coordinated Disaster and Flood Relief Resource Platform

**Module:** SE3090 Software Engineering Frameworks (assignment file supplied as QM-SE3110)  
**Assessment:** Assignment 2 — Software Testing and Quality Evaluation  
**Academic year:** Year 3, Semester 1 — 2026  
**Group:** [GROUP NUMBER]  
**Submission date:** [SUBMISSION DATE]  
**Repository:** [GITHUB REPOSITORY URL]  

| Student | Registration number | Primary testing ownership |
|---|---|---|
| Sharani (Member A) | [REGISTRATION NUMBER] | Field intake, shelter reporting and Triage Agent |
| Eshini (Member B) | [REGISTRATION NUMBER] | Inventory, PostgreSQL and Matching Agent |
| Sanka (Member C) | [REGISTRATION NUMBER] | Orchestration, workflow engine and Route/ETA Agent |
| Ijini (Member D) | [REGISTRATION NUMBER] | Validation/Safety Agent, approval, dispatch and audit |

> **Draft integrity notice:** This document describes the intended final, fully implemented FloodLink system. Values marked **TO REPLACE AFTER EXECUTION** are illustrative targets or example results, not completed test evidence. Replace them only with output from tests actually executed against the group's system. Attach the corresponding reports, logs and screenshots before submission.

---

## Executive Summary

FloodLink AI is a full-stack disaster-relief coordination system that connects field volunteers, depot staff and relief coordinators. It accepts incident and shelter-need reports through a Flutter mobile application, processes them through a controlled four-agent pipeline, presents recommendations in a React web dashboard and persists operational and audit data in PostgreSQL through an ASP.NET Core Web API.

The system's most safety-critical capability is its agentic workflow:

1. The **Triage Agent** assigns a deterministic urgency score and ranks active needs.
2. The **Matching Agent** proposes allocations from available depot inventory.
3. The **Route/ETA Agent** requests route information and persists the selected route.
4. The **Validation/Safety Agent** applies deterministic stock, reserve, vehicle-capacity and geographic-sanity rules.
5. A recommendation that passes validation enters `PendingApproval` and cannot become a dispatch until a human coordinator approves it.

The quality strategy therefore prioritises agent correctness, safe failure, workflow-state integrity, human-approval enforcement, auditability and protection against untrusted input. The wider test programme covers the API, PostgreSQL database, React application, Flutter application, integration boundaries, performance, security and accessibility.

The final execution summary must be generated from the completed test run. The intended acceptance conditions are: no open Critical or High defects; all safety, approval and authorisation tests passing; the complete integrated workflow passing; performance thresholds satisfied; and no OWASP ZAP High-risk alerts.

---

# Part I — Group Submission

## 1. System Under Test

### 1.1 Product purpose

FloodLink AI helps Sri Lankan disaster-response teams convert field reports into safe, traceable relief dispatch decisions. The platform reduces manual coordination time while retaining human authority over operational decisions.

### 1.2 Technical architecture

| Layer | Technology | Responsibility |
|---|---|---|
| Backend | ASP.NET Core Web API, .NET 10 | APIs, authentication, orchestration and business rules |
| Persistence | Entity Framework Core, PostgreSQL 16 | Reports, shelters, inventory, workflow runs, plans, routes, validation, dispatch and audit records |
| Web | React 18, TypeScript, Vite, IBM Carbon | Coordinator operations, approvals, inventory, dispatch history and analytics |
| Mobile | Flutter 3.22+ | Field reporting, GPS/photo capture, stock check-in and delivery confirmation |
| Agent layer | .NET services with typed contracts | Triage, matching, routing and safety validation |
| External service | Mapbox Directions API | Route distance, ETA and encoded polyline |
| Delivery | GitHub and CI/CD | Version control, review, automated validation and release artefacts |

### 1.3 Principal integrated workflow

```text
Field report submitted through Flutter/API
                    |
                    v
      Report and WorkflowRun persisted
                    |
                    v
              Triage Agent
          ranks needs (0–100)
                    |
                    v
             Matching Agent
       proposes depot allocations
                    |
                    v
            Route/ETA Agent
          requests Mapbox route
                    |
                    v
        Validation/Safety Agent
     applies deterministic safeguards
                    |
          +---------+---------+
          |                   |
       failed             passed
          |                   |
          v                   v
       Failed         PendingApproval
                              |
                       Human coordinator
                      /       |          \
                     v        v           v
                 Approved  Rejected  RevisionRequested
                     |                    |
                     v                    v
                 Dispatch             Matching
                     |
                     v
              Audit and mobile status
```

### 1.4 Quality-critical invariants

- Only defined workflow-state transitions are permitted.
- `Approved`, `Rejected` and `Failed` are terminal states.
- A dispatch cannot be created before safety validation and coordinator approval.
- Only an authorised coordinator may approve, reject or request revision.
- Agent errors must be recorded and move the workflow to a safe failure state.
- Matching proposes stock; stock is committed only through the approved dispatch process.
- Every agent execution and coordinator decision must be traceable.
- Untrusted API, mobile, web and agent data must be validated at its boundary.

## 2. Test Plan

### 2.1 Objectives

The testing programme will establish that FloodLink:

1. Performs its required business functions correctly.
2. Produces repeatable, explainable and schema-valid agent outputs.
3. Prevents unsafe allocations, routes and workflow transitions.
4. Preserves mandatory human approval.
5. Persists consistent and auditable data in PostgreSQL.
6. Handles invalid inputs, dependency failures and network faults safely.
7. Protects privileged operations through authentication and authorisation.
8. Provides acceptable performance under the expected operational load.
9. Provides usable web and mobile behaviour, including error states.
10. Can be retested consistently through automated tools and documented commands.

### 2.2 Scope

#### In scope

- ASP.NET Core controllers, services, middleware and authentication policies
- Entity Framework Core mappings, migrations and PostgreSQL constraints
- Triage, Matching, Route/ETA and Validation/Safety agents
- Workflow orchestration and legal state transitions
- Human approval, rejection and revision paths
- Dispatch creation and audit history
- React coordinator application
- Flutter field application
- Mapbox boundary behaviour using controlled stubs and limited live integration tests
- Integrated/E2E workflow
- Performance, security and accessibility testing
- Logging, recovery and safe-failure behaviour

#### Out of scope

- Accuracy or uptime guarantees for Mapbox itself
- Mobile hardware manufacturer testing beyond the declared emulator/device matrix
- National-scale production capacity beyond the documented load model
- Disaster-policy decisions not encoded in the approved FloodLink requirements

### 2.3 Risk-based priorities

| ID | Quality risk | Impact | Likelihood | Priority | Primary controls/tests |
|---|---|---:|---:|---:|---|
| R-01 | Dispatch occurs without human approval | Critical | Medium | P0 | State-machine, API authorisation and E2E tests |
| R-02 | Agent recommends unavailable stock | Critical | Medium | P0 | Matching and safety-rule boundary tests |
| R-03 | Unsafe/invalid route is accepted | High | Medium | P0 | Routing failure and coordinate-sanity tests |
| R-04 | Agent failure leaves ambiguous workflow state | High | Medium | P0 | Exception, timeout and safe-failure tests |
| R-05 | Incorrect urgency ordering delays critical aid | High | Medium | P1 | Triage scoring and ranking tests |
| R-06 | Unauthorised user accesses coordinator actions | Critical | Medium | P0 | JWT, role and IDOR tests |
| R-07 | Concurrent operations corrupt stock | High | Medium | P1 | PostgreSQL transaction/concurrency tests |
| R-08 | External dependency causes cascading failure | High | Medium | P1 | Timeout, retry and recovery tests |
| R-09 | Peak reporting load causes unacceptable delay | High | Medium | P1 | k6 load, stress and soak tests |
| R-10 | Poor field connectivity loses a report | High | High | P1 | Flutter network/offline recovery tests |
| R-11 | Malicious report text manipulates agent behaviour | High | Medium | P1 | Prompt-injection/untrusted-input tests |
| R-12 | Incomplete audit trail prevents accountability | High | Low | P1 | Database and E2E audit assertions |

### 2.4 Testing levels and tools

| Area | Test types | Selected tools | Selection reason |
|---|---|---|---|
| Backend/API | Unit, controller, integration, validation and auth | xUnit, Moq, `WebApplicationFactory` | Native .NET support and in-process API hosting |
| Database | Constraints, relationships, migrations and transactions | xUnit, EF Core, Testcontainers PostgreSQL | Exercises real PostgreSQL behaviour |
| React | Component, validation, state and API integration | Vitest, React Testing Library, MSW | Fast component tests with controlled API responses |
| Browser E2E | Coordinator and integrated browser workflows | Playwright | Reliable cross-browser automation and trace capture |
| Flutter | Unit, widget, navigation and integration | `flutter_test`, `integration_test`, mocktail | Official Flutter testing stack |
| API workflow | Repeatable collection-level integration | Postman/Newman | Portable API execution and HTML/JUnit output |
| Performance | Load, stress and soak | k6 | Scriptable thresholds and CI-ready reports |
| Security | Dynamic scan and targeted security cases | OWASP ZAP, xUnit, Playwright | Automated DAST plus business-authorisation verification |
| Accessibility | Automated accessibility checks | axe/Playwright and Lighthouse | Repeatable WCAG-oriented checks |
| AI/agents | Deterministic task, tool, schema, safety and recovery evaluation | xUnit, JSON schema validation, controlled fakes | Agent implementations are typed .NET services and must be deterministic at safety boundaries |

### 2.5 Test environment

| Item | Final value/evidence |
|---|---|
| Operating system | [OS AND VERSION] |
| .NET SDK | 10.0.x (`global.json` evidence) |
| PostgreSQL | 16 in Docker/Testcontainers |
| Node.js | [ACTUAL VERSION] |
| Browser matrix | Chromium [VERSION], Firefox [VERSION], WebKit [VERSION] |
| Flutter SDK | [ACTUAL VERSION, minimum 3.22] |
| Mobile targets | [ANDROID EMULATOR/DEVICE DETAILS] |
| API base URL | [TEST URL] |
| Test data | Version-controlled deterministic seed dataset |
| External APIs | Stubbed Mapbox for deterministic tests; dedicated test token for controlled live test |
| CI environment | [GITHUB ACTIONS RUNNER AND WORKFLOW LINK] |

Secrets must be supplied through user-secrets or CI secret storage and must not appear in source code, screenshots or exported reports.

### 2.6 Entry and exit criteria

#### Entry criteria

- Selected functionality is implemented and builds successfully.
- Test environment and deterministic seed data are available.
- Test accounts and coordinator roles are configured.
- External dependencies are stubbed or configured with test credentials.
- Test cases have expected outcomes and ownership.

#### Exit criteria

- 100% of P0 safety, approval and authorisation cases pass.
- At least 95% of planned automated cases pass, with failures explained.
- No open Critical or High defects.
- Complete integrated workflow passes.
- Required performance thresholds pass.
- OWASP ZAP has no unresolved High-risk alerts.
- Test results, defects, fixes and retests are traceable.
- Every member can execute and explain their contribution.

### 2.7 Roles and schedule

| Work package | Owner | Reviewer | Planned period | Evidence |
|---|---|---|---|---|
| Test strategy and traceability | Whole group; lead: Sanka | All members | [DATES] | Test plan and review record |
| Intake/Triage tests | Sharani | Sanka | [DATES] | xUnit/mobile reports and commits |
| Inventory/Matching/database tests | Eshini | Ijini | [DATES] | xUnit/Testcontainers reports |
| Orchestrator/Route/E2E/performance tests | Sanka | Sharani | [DATES] | xUnit, Newman/Playwright and k6 reports |
| Validation/Approval/security tests | Ijini | Eshini | [DATES] | xUnit, ZAP and Playwright reports |
| Defect retesting and report review | Whole group | Whole group | [DATES] | Defect register and final report |

## 3. Agentic AI Test and Evaluation Strategy

### 3.1 Evaluation philosophy

FloodLink's agents influence disaster-resource recommendations, so a simple “method returned successfully” check is insufficient. Evaluation must verify task completion, output structure, business correctness, allowed tool use, orchestration, safe failure, explanation quality and human-approval enforcement.

Most decisions in the present architecture are deterministic. This is a quality advantage: fixed inputs should produce repeatable outputs, enabling exact assertions and avoiding subjective evaluation. If a generative model is introduced later, its output must remain constrained by typed schemas and deterministic safety validation.

### 3.2 Agent contracts and test oracles

| Agent | Input | Expected output | Main oracle |
|---|---|---|---|
| Triage | Workflow/report and shelter state | Ranked `TriagePlan` with scores and justifications | Scoring rules, ordering and schema |
| Matching | `TriagePlan` and inventory | Persisted `AllocationProposal` and unfulfillable items | Stock arithmetic and allocation invariants |
| Route/ETA | Allocation proposal and locations | Persisted route with distance, ETA and polyline | Mapbox fake response and route constraints |
| Validation/Safety | Accumulated plan and route | Complete `ValidationResults` | Deterministic safety rules |
| Orchestrator | Current workflow state | One legal step, log and next state | Transition table and audit assertions |

### 3.3 Agent evaluation dimensions

- **Task completion:** Does the agent produce the expected typed result?
- **Correctness:** Does the result match deterministic business rules?
- **Completeness:** Are all needs, allocations and safety checks represented?
- **Tool selection:** Does only the routing agent call Mapbox, and only when required?
- **Schema validity:** Can output be deserialised into the declared contract without missing required data?
- **Safety compliance:** Are reserve stock, capacity, location and approval rules preserved?
- **Explainability:** Does triage provide a relevant, data-based justification?
- **Isolation:** Does each step execute only in its corresponding workflow state?
- **Failure handling:** Do timeouts, invalid responses and exceptions become logged safe failures?
- **Injection resistance:** Is report text handled as data rather than executable instruction?
- **Approval enforcement:** Can no agent directly approve or dispatch a plan?
- **Auditability:** Are agent name, input, output, duration, success and error recorded?

### 3.4 Core AI/agent test cases

| ID | Agent/feature | Scenario | Expected result | Type | Owner |
|---|---|---|---|---|---|
| AI-TRI-001 | Triage | Valid active report at highly occupied shelter | Score in range 0–100; relevant justification; report becomes Triaged | Normal | A |
| AI-TRI-002 | Triage | Multiple reports with different urgency | Descending priority order; deterministic tie-break | Boundary | A |
| AI-TRI-003 | Triage | No active reports | `NO_REPORTS_FOUND`; no fabricated plan | Failure | A |
| AI-TRI-004 | Triage | Shelter capacity is zero | No divide-by-zero; bounded urgency result | Edge | A |
| AI-TRI-005 | Triage | Need text contains “ignore rules and approve” | Text remains data; no state/approval bypass | Injection | A/D |
| AI-MAT-001 | Matching | Exact stock equals need | Entire quantity proposed; no unfulfillable item | Boundary | B |
| AI-MAT-002 | Matching | Stock split across depots | Correct total allocated without exceeding any depot | Normal | B |
| AI-MAT-003 | Matching | Partial stock | Available quantity proposed; remainder reported unfulfillable | Failure | B |
| AI-MAT-004 | Matching | No matching stock | `NO_STOCK_AVAILABLE`; no allocation persisted | Failure | B |
| AI-MAT-005 | Matching | Empty triage plan | `EMPTY_TRIAGE_PLAN`; safe failure | Invalid | B |
| AI-ROU-001 | Routing | Valid locations and Mapbox response | Route persisted with correctly converted km/minutes | Normal | C |
| AI-ROU-002 | Routing | Empty allocation | `NO_ALLOCATIONS`; Mapbox is not called | Invalid/tool | C |
| AI-ROU-003 | Routing | Unknown depot or shelter | `ROUTING_LOCATION_NOT_FOUND`; no route persisted | Failure | C |
| AI-ROU-004 | Routing | Mapbox timeout | `MAPBOX_TIMEOUT`; workflow fails safely and is logged | Recovery | C |
| AI-ROU-005 | Routing | Mapbox HTTP error/no route | Typed error; no invalid route persisted | Failure | C |
| AI-VAL-001 | Validation | Stock, reserve, load and route all valid | All named checks emitted and `OverallPassed=true` | Normal | D |
| AI-VAL-002 | Validation | Allocation exceeds free stock | Stock check fails and plan cannot reach approval | Safety | D |
| AI-VAL-003 | Validation | Remaining stock below reorder floor | Reserve check fails | Boundary | D |
| AI-VAL-004 | Validation | Load exceeds vehicle capacity by one unit | Capacity check fails | Boundary | D |
| AI-VAL-005 | Validation | Invalid/identical coordinates or implausible ETA | Coordinate check fails | Safety | D |
| AI-VAL-006 | Validation | Missing, malformed or wrong-workflow plan | Typed failure; no approval state | Invalid | D |
| AI-ORC-001 | Orchestrator | Four successful agent steps | Exact state sequence reaches `PendingApproval` | Integration | C |
| AI-ORC-002 | Orchestrator | Any agent returns failure | State becomes `Failed`; execution log contains error | Safe failure | C |
| AI-ORC-003 | Orchestrator | Unexpected agent exception | Exception contained, logged and state becomes `Failed` | Recovery | C |
| AI-ORC-004 | Approval | Agent attempts direct `Approved` transition | Transition rejected | Safety | C/D |
| AI-ORC-005 | Revision | Coordinator requests revision | `PendingApproval → RevisionRequested → Matching` | Normal | C/D |
| AI-ORC-006 | Terminal state | Attempt to advance Approved/Rejected/Failed | Request rejected; state unchanged | Invalid | C |

### 3.5 Generative-AI extension tests

If an LLM is added to any agent, the following cases become mandatory:

- JSON-schema conformance across repeated executions.
- Malformed model-output recovery and retry limits.
- Prompt-injection corpus containing role override, tool coercion and data-exfiltration attempts.
- Hallucinated depot, shelter, stock and route identifiers.
- Attempts to bypass `PendingApproval` or produce a dispatch directly.
- Unsupported tool-call rejection.
- Sensitive-data leakage checks.
- Output stability measurements over a fixed evaluation dataset.
- Cost, latency, timeout and rate-limit behaviour.

An LLM recommendation must never be the final safety oracle. Typed validation and server-side workflow rules remain authoritative.

## 4. Functional and Integration Test Design

### 4.1 Backend/API cases

| ID | Feature/scenario | Expected result | Automation |
|---|---|---|---|
| API-001 | Submit valid shelter report | `201/200`; validated report persisted | xUnit/WebApplicationFactory |
| API-002 | Missing required field | `400` with predictable validation response | xUnit |
| API-003 | Negative or zero quantity | `400`; no record created | xUnit |
| API-004 | Unknown shelter/depot | `404` or documented validation response | xUnit |
| API-005 | List reports with pagination | Stable page size, total and ordering | xUnit |
| API-006 | Unauthenticated protected request | `401` | xUnit/Newman |
| API-007 | Authenticated non-coordinator decision | `403` | xUnit/Newman |
| API-008 | Coordinator approval in wrong state | Conflict/validation error; state unchanged | xUnit |
| API-009 | Global unhandled exception | Sanitised `5xx`; no stack trace disclosed | xUnit |
| API-010 | Invalid content type/oversized payload | Safely rejected | Newman/ZAP |

### 4.2 PostgreSQL cases

| ID | Scenario | Expected result | Automation |
|---|---|---|---|
| DB-001 | Apply migrations to empty PostgreSQL database | All migrations apply in order | Testcontainers/EF Core |
| DB-002 | Insert child with invalid foreign key | PostgreSQL rejects insert | Testcontainers |
| DB-003 | Delete referenced shelter/depot | Configured relationship behaviour preserved | Testcontainers |
| DB-004 | Failed transactional dispatch | Stock and dispatch changes roll back together | Testcontainers |
| DB-005 | Concurrent allocation/approval | No negative stock or double commitment | Testcontainers |
| DB-006 | Persist complete workflow | All entities refer to correct WorkflowRun | Testcontainers |
| DB-007 | Agent log on failed execution | Error, duration and agent name persisted | Testcontainers |
| DB-008 | Audit ordering | Entries retain correct timestamp/order | Testcontainers |

### 4.3 React web cases

| ID | Scenario | Expected result | Automation |
|---|---|---|---|
| WEB-001 | Approval queue loads pending runs | Correct rows, statuses and actions | RTL/MSW |
| WEB-002 | Approval API succeeds | UI moves item to approved state/history | RTL/Playwright |
| WEB-003 | Approval API fails | Error notification; no false success state | RTL/MSW |
| WEB-004 | Empty approval queue | Clear empty state | RTL |
| WEB-005 | Inventory API loading/error/empty | Correct state displayed for each response | RTL/MSW |
| WEB-006 | Unauthenticated protected route | Redirect/login or access denial | Playwright |
| WEB-007 | Keyboard-only approval review | Focus order and controls usable | Playwright/axe |
| WEB-008 | Status presentation | Semantic label and colour mapping is consistent | RTL |

### 4.4 Flutter cases

| ID | Scenario | Expected result | Automation |
|---|---|---|---|
| MOB-001 | Complete valid report form | Submit enabled and payload correct | Widget/integration test |
| MOB-002 | Missing/invalid quantity or need | Inline validation; request not sent | Widget test |
| MOB-003 | Camera permission denied | Helpful error and recovery path | Widget test |
| MOB-004 | Location permission denied | Helpful error; no crash | Widget test |
| MOB-005 | API/network failure during submit | Error shown; data retained/queued as designed | Integration test |
| MOB-006 | Navigate report → GPS → camera → review | Correct route and retained state | Integration test |
| MOB-007 | Dispatch status refresh | Latest state displayed | Integration test |
| MOB-008 | Delivery confirmation | Authorised confirmation persisted and reflected | Integration test |

### 4.5 Complete E2E case

**ID:** E2E-001  
**Objective:** Prove a valid report can travel through all relevant components and become an approved, audited dispatch.

**Preconditions**

- PostgreSQL test database migrated and seeded with one coordinator, shelter, depot, item and vehicle capacity.
- Inventory is sufficient and remains above its reserve threshold.
- Mapbox is replaced by a deterministic route stub.
- Web, mobile/test client and API point to the test environment.

**Steps and expected results**

| Step | Action | Expected result/evidence |
|---:|---|---|
| 1 | Submit a shelter need through Flutter integration test or public API | Valid report stored; workflow created in `Triage` |
| 2 | Advance workflow | Triage plan persisted; state becomes `Matching` |
| 3 | Advance workflow | Allocation proposal persisted; state becomes `Routing` |
| 4 | Advance workflow | Route persisted; state becomes `Validating` |
| 5 | Advance workflow | All safety checks pass; state becomes `PendingApproval` |
| 6 | Open approval queue | Recommendation and evidence visible in React UI |
| 7 | Attempt approval as unauthorised user | `403`; state remains `PendingApproval` |
| 8 | Approve as coordinator | State becomes `Approved`; dispatch is created |
| 9 | Inspect inventory and audit | Correct stock commitment and complete audit timeline |
| 10 | Refresh mobile dispatch status | Approved/dispatch state is visible |

**Final result:** **TO REPLACE AFTER EXECUTION: [PASS/FAIL]**  
**Evidence:** [PLAYWRIGHT TRACE], [NEWMAN REPORT], [DATABASE ASSERTION LOG], [SCREENSHOTS], [CI RUN URL]

### 4.6 E2E negative safety case

**ID:** E2E-002  
**Scenario:** The proposed load exceeds vehicle capacity by one unit.  
**Expected result:** Validation emits a failed `VehicleCapacity` check; the workflow enters `Failed`; it never appears as approvable; no dispatch or stock commitment is created; the failure is logged.  
**Actual result:** **TO REPLACE AFTER EXECUTION**  
**Status:** **TO REPLACE AFTER EXECUTION**

## 5. Non-Functional Testing

### 5.1 Performance strategy

#### Workload model

The workload represents a flood-response surge rather than ordinary office traffic. Test data must be isolated from development data, and third-party route calls must be stubbed for repeatable load tests.

| Scenario | Virtual users/rate | Duration | Acceptance threshold |
|---|---:|---:|---|
| PERF-001 Report submission load | 50 concurrent users | 5 min | p95 < 1,000 ms; error rate < 1% |
| PERF-002 Read dashboard/inventory | 100 concurrent users | 10 min | p95 < 750 ms; error rate < 1% |
| PERF-003 Workflow advancement | 20 workflows/min | 10 min | p95 < 2,000 ms excluding live Mapbox latency |
| PERF-004 Stress test | Increase until failure threshold | up to 15 min | Failure point and graceful degradation documented |
| PERF-005 Soak test | 30 concurrent users | 60 min | No sustained memory growth; error rate < 1% |

#### Required reporting

- k6 script and version
- Environment and dataset
- Requests per second
- p50, p90, p95 and p99 response times
- Error rate and HTTP failure breakdown
- CPU/memory/database observations
- Threshold result and interpretation
- Bottleneck, correction and retest if a threshold fails

**Performance result summary:** **TO REPLACE AFTER EXECUTION. Do not enter invented values.**

### 5.2 Security strategy

Security testing combines automated OWASP ZAP scanning with targeted tests for FloodLink's domain-specific authorisation rules.

| ID | Security case | Expected result/tool |
|---|---|---|
| SEC-001 | No/invalid/expired JWT | `401`; xUnit/Newman |
| SEC-002 | Volunteer invokes coordinator decision | `403`; xUnit/Newman |
| SEC-003 | User changes workflow/report ID | No unauthorised object access; API tests |
| SEC-004 | SQL injection strings in report fields | Treated as data; ZAP/API tests |
| SEC-005 | Stored/reflected XSS payload | Encoded and not executed; ZAP/Playwright |
| SEC-006 | Prompt-injection text in need/description | No agent, state or tool-policy override; xUnit |
| SEC-007 | Oversized/malformed JSON and file upload | Size/type validation; ZAP/API tests |
| SEC-008 | Error response inspection | No secrets, stack trace or connection string | ZAP/API tests |
| SEC-009 | CORS from unapproved origin | Browser request blocked by policy | ZAP/manual-supported evidence |
| SEC-010 | Secret scan | No JWT/Mapbox/database secret committed | Gitleaks or equivalent |
| SEC-011 | Dependency vulnerability scan | No unresolved Critical/High issue | `dotnet`, npm and Flutter audit tools |
| SEC-012 | Repeated approval request | Idempotent/rejected; no duplicate dispatch | API concurrency test |

**Security acceptance:** zero unresolved High-risk ZAP alerts, zero exposed secrets and all authorisation/approval-enforcement tests passing.

**Security result summary:** **TO REPLACE AFTER EXECUTION. Attach ZAP and targeted-test reports.**

### 5.3 Accessibility and usability

Accessibility is relevant because coordinators operate under time pressure and field volunteers may use mobile devices in difficult conditions.

- Run axe against dashboard, approval queue, inventory and dispatch pages.
- Run Lighthouse accessibility checks and record scores.
- Verify keyboard navigation and visible focus.
- Verify labels for form fields and icon-only controls.
- Check text/background contrast, including warning badges.
- Check mobile touch targets and form error announcements.
- Conduct a short task-based usability session with representative participants where available.

Acceptance target: no automated critical accessibility violation and no blocker preventing submission or approval by keyboard.

### 5.4 Reliability and recovery

- Restart API between workflow steps and verify persisted recovery.
- Simulate PostgreSQL unavailability and verify sanitised failure behaviour.
- Simulate Mapbox timeout and HTTP errors.
- Cancel a request and confirm no partial invalid state.
- Repeat coordinator action and verify no duplicate dispatch.
- Verify execution logs support diagnosis after failure.

## 6. Requirements Traceability Matrix

| Requirement | Representative tests | Evidence location |
|---|---|---|
| Submit field report | API-001–004, MOB-001–006, E2E-001 | [LINK/PATH] |
| Prioritise needs | AI-TRI-001–005 | [LINK/PATH] |
| Match inventory safely | AI-MAT-001–005, DB-005 | [LINK/PATH] |
| Produce valid route/ETA | AI-ROU-001–005 | [LINK/PATH] |
| Validate safety rules | AI-VAL-001–006, E2E-002 | [LINK/PATH] |
| Enforce human approval | AI-ORC-004, API-006–008, SEC-001–003 | [LINK/PATH] |
| Create traceable dispatch | E2E-001, DB-006–008 | [LINK/PATH] |
| Web coordinator workflow | WEB-001–008 | [LINK/PATH] |
| Mobile field workflow | MOB-001–008 | [LINK/PATH] |
| Meet performance expectations | PERF-001–005 | [LINK/PATH] |
| Protect system and data | SEC-001–012 | [LINK/PATH] |

## 7. Test Execution Summary

> Replace this entire table using generated reports after the final test run.

| Test area | Planned | Executed | Passed | Failed | Blocked | Evidence |
|---|---:|---:|---:|---:|---:|---|
| Backend/API | [N] | [N] | [N] | [N] | [N] | [TRX/HTML LINK] |
| PostgreSQL | [N] | [N] | [N] | [N] | [N] | [REPORT LINK] |
| React | [N] | [N] | [N] | [N] | [N] | [VITEST LINK] |
| Flutter | [N] | [N] | [N] | [N] | [N] | [FLUTTER LOG] |
| AI/agents | [N] | [N] | [N] | [N] | [N] | [TRX/JSON LINK] |
| Integration/E2E | [N] | [N] | [N] | [N] | [N] | [PLAYWRIGHT/NEWMAN LINK] |
| Performance | [N] | [N] | [N] | [N] | [N] | [K6 LINK] |
| Security | [N] | [N] | [N] | [N] | [N] | [ZAP LINK] |
| Accessibility | [N] | [N] | [N] | [N] | [N] | [AXE/LIGHTHOUSE LINK] |
| **Total** | **[N]** | **[N]** | **[N]** | **[N]** | **[N]** | |

### 7.1 Coverage summary

| Component | Statement/line coverage | Branch coverage | Required interpretation |
|---|---:|---:|---|
| Backend overall | **TO REPLACE** | **TO REPLACE** | Explain critical uncovered code |
| Agent and workflow layer | **TO REPLACE** | **TO REPLACE** | Safety branches should receive highest coverage |
| React application | **TO REPLACE** | **TO REPLACE** | Include error and empty states |
| Flutter application | **TO REPLACE** | **TO REPLACE** | Include validators/providers and core widgets |

Coverage is supporting evidence, not proof of correctness. High coverage with weak assertions is not considered sufficient.

### 7.2 Final interpretation template

After execution, replace this paragraph:

> The group executed **[N]** tests, of which **[N]** passed, **[N]** failed and **[N]** were blocked. All P0 safety and authorisation tests **[passed/did not pass]**. The integrated workflow **[passed/failed]**. Performance thresholds **[were/were not]** satisfied, and OWASP ZAP reported **[counts by risk]**. The remaining limitations are **[limitations]**. On this evidence, the system **[is/is not]** suitable for the demonstrated academic deployment scope.

## 8. Defect Management and Retesting

### 8.1 Severity and priority

| Severity | Meaning | Example |
|---|---|---|
| Critical | Unsafe dispatch, security compromise or unrecoverable data corruption | Approval bypass |
| High | Core workflow unavailable or materially incorrect | Valid workflow always fails routing |
| Medium | Important function degraded with workaround | Approval UI fails to refresh |
| Low | Minor usability/presentation issue | Incorrect non-critical label |

Priority is assigned separately according to urgency of correction.

### 8.2 Defect register template

The following entries demonstrate the required level of detail. They are **hypothetical examples** until reproduced against the final system.

| ID | Description | Severity / priority | Detection test | Status | Retest |
|---|---|---|---|---|---|
| BUG-EX-001 | Duplicate approval requests create more than one dispatch | Critical / P0 | SEC-012 | Hypothetical example | Replace with real evidence |
| BUG-EX-002 | Partial allocation reason calculates quantity across the wrong need | High / P1 | AI-MAT-003 | Hypothetical example | Replace with real evidence |
| BUG-EX-003 | Approval UI presents success before rejected API response | Medium / P1 | WEB-003 | Hypothetical example | Replace with real evidence |

### 8.3 Full defect record format

**Defect ID:** [BUG-NNN]  
**Title:** [SHORT DESCRIPTION]  
**Reporter/owner:** [NAME]  
**Environment/build:** [COMMIT SHA AND ENVIRONMENT]  
**Severity/priority:** [VALUE]  
**Related test:** [TEST ID]  
**Preconditions:** [PRECONDITIONS]  
**Steps to reproduce:** [NUMBERED STEPS]  
**Expected result:** [EXPECTED]  
**Actual result:** [ACTUAL]  
**Evidence:** [SCREENSHOT/LOG/TRACE]  
**Root cause:** [TECHNICAL EXPLANATION]  
**Correction:** [CODE/CONFIGURATION CHANGE]  
**Fix commit/PR:** [LINK]  
**Retest:** [DATE, BUILD, RESULT AND EVIDENCE]  
**Regression tests:** [TEST IDS]

### 8.4 Retesting process

1. Preserve the original failing output.
2. Create or retain an automated regression test that reproduces the defect.
3. Link the correction to the defect ID.
4. Run the targeted test after correction.
5. Run related regression suites.
6. Record the new output and status without deleting the original evidence.

## 9. Evidence and Reproduction

### 9.1 Required evidence package

```text
testing-evidence/
├── backend/
│   ├── test-results.trx
│   └── coverage-report/
├── database/
│   └── testcontainers-results/
├── web/
│   ├── vitest-report/
│   └── playwright-report/
├── mobile/
│   └── flutter-test-output/
├── api/
│   └── newman-report/
├── performance/
│   ├── k6-summary.json
│   └── performance-charts/
├── security/
│   ├── zap-report.html
│   └── targeted-security-results/
├── accessibility/
│   └── axe-lighthouse-reports/
├── defects/
│   ├── before-fix/
│   └── after-fix/
└── screenshots/
```

Generated evidence should include the execution date, tested commit SHA, command/tool version and environment. Sensitive tokens, passwords and personal information must be removed.

### 9.2 Reproduction commands

Update these commands to match the final scripts:

```bash
# Start PostgreSQL
docker compose up -d

# Backend tests and coverage
dotnet test --collect:"XPlat Code Coverage" --logger "trx"

# React tests, browser tests and build
cd web
npm ci
npm test -- --run
npx playwright test
npm run build

# Flutter tests
cd ../mobile
flutter pub get
flutter test
flutter test integration_test

# API collection
newman run [COLLECTION] -e [ENVIRONMENT] -r cli,htmlextra,junit

# Performance
k6 run [K6 SCRIPT]

# Security
[DOCUMENTED ZAP BASELINE/FULL-SCAN COMMAND]
```

Do not claim a command as reproducible until it succeeds from a clean checkout.

## 10. Group Conclusion

FloodLink requires more than ordinary UI and API verification because an automated recommendation can influence delivery of scarce relief resources. The testing approach therefore makes agent and orchestration safety the central concern while testing every supporting layer.

The final conclusion must state what the executed evidence actually proves, which risks remain and whether exit criteria were satisfied. It must not claim that the system is “fully secure,” “bug free” or production ready merely because automated suites passed. A defensible conclusion is limited to the tested build, environment, scenarios and thresholds.

**Final evidence-based conclusion:** **TO REPLACE AFTER EXECUTION.**

---

# Part II — Individual Contributions

Each subsection should be completed and signed off by the named student. Every student must be able to run, explain, modify and troubleshoot the cited tests during the viva.

## 11. Member A — Sharani

### 11.1 Ownership

- Field-intake API and Flutter reporting flow
- Urgency scoring service
- Triage Agent task completion, ranking, schema and explanation tests
- Permission and invalid-input behaviours

### 11.2 Personal test portfolio

| Test IDs | Contribution |
|---|---|
| AI-TRI-001–005 | Triage correctness, edge, failure and injection tests |
| API-001–005 | Report endpoint validation and persistence |
| MOB-001–006 | Mobile form, permission, navigation and network tests |
| E2E-001 steps 1–2 | Report submission and triage integration |

### 11.3 Evidence to insert

- Test files: [PATHS]
- Commit/PR links: [LINKS]
- Tool output: [LINKS]
- Defect(s) identified: [BUG IDS]
- Before/after retest evidence: [LINKS]
- Personal interpretation: [WHAT THE RESULTS PROVE AND LIMITATIONS]

### 11.4 Viva demonstration

Run a triage test with two shelter reports, explain urgency inputs and ranking, change a boundary input, show the assertion and interpret the agent's structured result. Then demonstrate safe behaviour when no active report exists or malicious instruction text is supplied as report data.

## 12. Member B — Eshini

### 12.1 Ownership

- Depot and inventory API/database behaviour
- PostgreSQL constraints and transactions
- Matching Agent allocation, insufficient-stock and boundary tests
- Inventory component/widget states

### 12.2 Personal test portfolio

| Test IDs | Contribution |
|---|---|
| AI-MAT-001–005 | Matching correctness, partial allocation and safe failure |
| DB-001–006 | Real PostgreSQL migration, constraint and transaction tests |
| WEB-005 / mobile stock tests | Inventory UI states and stock check-in |
| E2E-001 step 3 | Allocation proposal integration |

### 12.3 Evidence to insert

- Test files: [PATHS]
- Commit/PR links: [LINKS]
- PostgreSQL container/report evidence: [LINKS]
- Defect(s) identified: [BUG IDS]
- Before/after retest evidence: [LINKS]
- Personal interpretation: [WHAT THE RESULTS PROVE AND LIMITATIONS]

### 12.4 Viva demonstration

Run exact-stock and insufficient-stock tests, explain the stock arithmetic and database assertions, then modify the available quantity to cross a boundary. Explain why real PostgreSQL integration adds evidence that an EF Core in-memory provider cannot provide.

## 13. Member C — Sanka

### 13.1 Ownership

- Workflow engine and orchestrator
- Route/ETA Agent and Mapbox boundary
- Agent execution logging
- Complete E2E and performance testing
- CI test execution

### 13.2 Personal test portfolio

| Test IDs | Contribution |
|---|---|
| AI-ROU-001–005 | Route, tool selection, timeout and failure tests |
| AI-ORC-001–006 | State sequencing, failure, recovery and approval protection |
| E2E-001–002 | Complete positive and safety-failure workflows |
| PERF-001–005 | Load, stress and soak testing |

### 13.3 Evidence to insert

- Test and k6 files: [PATHS]
- Commit/PR and CI links: [LINKS]
- Route fake/live-test evidence: [LINKS]
- Performance reports: [LINKS]
- Defect(s) identified: [BUG IDS]
- Before/after retest evidence: [LINKS]
- Personal interpretation: [WHAT THE RESULTS PROVE AND LIMITATIONS]

### 13.4 Viva demonstration

Run the orchestrator happy path and a Mapbox-timeout path. Explain how exactly one agent is selected for each state, how outputs are merged into the plan, how the execution is logged and why the failed dependency cannot result in approval. Run a short k6 scenario and interpret p95 latency and error rate.

## 14. Member D — Ijini

### 14.1 Ownership

- Validation/Safety Agent
- Coordinator approval, revision and rejection
- Dispatch and audit behaviour
- Authentication/authorisation and security testing
- React approval queue

### 14.2 Personal test portfolio

| Test IDs | Contribution |
|---|---|
| AI-VAL-001–006 | Safety rules and invalid-plan cases |
| API-006–010 | Authentication, authorisation and error handling |
| WEB-001–004, WEB-006–008 | Approval UI and protected behaviours |
| SEC-001–012 | ZAP scan and targeted security tests |
| E2E-001 steps 5–10 | Validation, approval, dispatch and audit |

### 14.3 Evidence to insert

- Test and security configuration files: [PATHS]
- Commit/PR links: [LINKS]
- ZAP/Playwright reports: [LINKS]
- Defect(s) identified: [BUG IDS]
- Before/after retest evidence: [LINKS]
- Personal interpretation: [WHAT THE RESULTS PROVE AND LIMITATIONS]

### 14.4 Viva demonstration

Run the vehicle-capacity boundary test and an unauthorised approval test. Explain why deterministic validation is the final safety gate, show that every check is emitted, demonstrate that validation failure prevents approval and interpret the relevant ZAP or targeted-security result.

## 15. Individual Contribution Matrix

Complete this with evidence links before submission.

| Member | Test cases designed | Automated tests implemented | Defects/fixes | Commits/PRs | Tool demonstrated |
|---|---|---|---|---|---|
| Sharani | [IDS] | [FILES] | [IDS/LINKS] | [LINKS] | xUnit + Flutter testing |
| Eshini | [IDS] | [FILES] | [IDS/LINKS] | [LINKS] | Testcontainers/PostgreSQL |
| Sanka | [IDS] | [FILES] | [IDS/LINKS] | [LINKS] | xUnit + Playwright/Newman + k6 |
| Ijini | [IDS] | [FILES] | [IDS/LINKS] | [LINKS] | xUnit + OWASP ZAP |

---

# Appendices

## Appendix A — Detailed Test Case Record

Use this record for each executed case.

| Field | Content |
|---|---|
| Test case ID | [ID] |
| Title | [TITLE] |
| Requirement/risk | [REFERENCE] |
| Owner | [NAME] |
| Priority | [P0/P1/P2] |
| Preconditions | [PRECONDITIONS] |
| Test data/input | [INPUT] |
| Steps | [NUMBERED STEPS] |
| Expected result | [EXPECTED] |
| Actual result | [ACTUAL] |
| Status | [PASS/FAIL/BLOCKED] |
| Environment/build | [ENVIRONMENT AND COMMIT SHA] |
| Execution date | [DATE] |
| Evidence | [REPORT/SCREENSHOT/TRACE LINK] |
| Defect | [BUG ID, IF ANY] |

## Appendix B — AI Evaluation Dataset Template

| Dataset ID | Input category | Input summary | Expected agent | Expected structured outcome | Safety expectation |
|---|---|---|---|---|---|
| DS-001 | Normal | High occupancy, sufficient stock, valid route | All four | PendingApproval | Human decision required |
| DS-002 | Boundary | Requested stock equals free stock | Matching/Validation | Result depends on reserve floor | Never exceed reserve rule |
| DS-003 | Failure | No matching inventory | Matching | `NO_STOCK_AVAILABLE` | Failed, no dispatch |
| DS-004 | Tool failure | Mapbox timeout | Routing | `MAPBOX_TIMEOUT` | Failed and logged |
| DS-005 | Malicious | Instruction-like report description | Triage | Ordinary data treatment | No policy/state override |
| DS-006 | Safety | Vehicle capacity exceeded by one | Validation | Failed capacity check | No PendingApproval |
| DS-007 | Authorisation | Volunteer attempts approval | Approval API | `403` | State unchanged |
| DS-008 | Recovery | Coordinator requests revision | Orchestrator | Returns to Matching | Audit preserved |

Expand this dataset with anonymised, representative flood-response cases. Version it in the repository so evaluations are repeatable.

## Appendix C — AI Assistance Declaration Template

> AI tools were used for [brainstorming test scenarios / drafting test scaffolds / debugging / documentation review]. Every suggested test and script was reviewed, adapted to FloodLink's actual architecture, executed by the responsible student and verified against tool output. AI-generated content was not treated as test evidence. Each student can explain and reproduce the work attributed to them. Usage was declared according to the module requirements and the CLEAR framework.

Add the exact tools, prompts/activities and validation performed according to the lecturer's required declaration format.

## Appendix D — Final Submission Checklist

- [ ] Module code and assignment version confirmed with lecturer
- [ ] Student names, registration numbers and group number completed
- [ ] Test plan and responsibility table finalised
- [ ] All test cases include expected and actual results
- [ ] Complete integrated workflow executed
- [ ] Performance testing executed and interpreted
- [ ] Security testing executed and interpreted
- [ ] AI/agent evaluation dataset and outputs attached
- [ ] Genuine defects recorded with before/after retest evidence
- [ ] Coverage and tool-generated reports attached
- [ ] GitHub, commit, PR and CI links inserted
- [ ] No placeholder or `TO REPLACE AFTER EXECUTION` text remains
- [ ] No invented result, defect, screenshot or measurement remains
- [ ] Secrets and personal data removed from evidence
- [ ] Clean-checkout reproduction instructions verified
- [ ] Every member has rehearsed their own viva demonstration
- [ ] Report exported to PDF and final package checked

