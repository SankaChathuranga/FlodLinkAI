# Progress Tracker

Update this file after every meaningful implementation
change.

## Current Phase

- Members A, B, C and D — **ALL MERGED TO MAIN** (PR *#1 merged 2026-10-06). Backend: 116 tests pass.

## Current Goal

- Verify the full pipeline end to end and replace the Development-only auth fallback with real login.
- Deadline extended to 2026-10-12. Follow `FloodLink_Finish_Plan.md` (pipeline fix, auth, Gemini-backed
  Planner/Triage/Matching agents, status tracker, evals, deployment, documents). Review of 2026-10-08 found:
  nothing advances a run past Triage, Validation cannot read the plan the orchestrator stores, approve does not
  touch stock, and there is no login/Users table.

## Completed

- Member C (orchestration, state machine, logger, Route/ETA agent via Mapbox, JWT validation)
  merged into Member A's branch; conflicts resolved, solution builds, 69 tests pass.
- Member B inventory scope completed: `Depot`, `InventoryItem`, and persisted
  `AllocationProposalEntity` tables; CRUD/check-in endpoints; seed stock; EF Core
  migration; greedy matching agent; and matching/orchestrator tests.
- `AllocationProposal` now carries a real persisted `AllocationProposalId`; the
  Route Agent uses database depot/shelter coordinates and this ID instead of
  placeholders.
- React Carbon inventory dashboard and Flutter Provider stock check-in screen added.

- Member D (Validation/Safety Agent with SafetyRules, Dispatch/ValidationResult/AuditTrail tables,
  Dispatch/Audit/Validation controllers, WorkflowStateService, web Approval Queue / Dispatch History /
  Analytics tabs, mobile dispatch-status and delivery-confirmation screens at `/dispatch`) merged with
  A, B and C; migration `AddMemberDTables` applied to the shared dev database.

## In Progress

- None.

## Next Up

- Run one real workflow end to end (needs a Mapbox key or a seeded PendingApproval run) and approve/reject it.
- Replace the Development-only `JwtOrDev` auth fallback with a real login flow; D's endpoints return 401
  in non-Development environments until then.
- Confirm CI runs on PRs and provides Postgres for the integration tests.
- Run the mobile dispatch screens on an emulator/device.

## Open Questions

- `/context` is in .gitignore (from main); existing files stay tracked but new files there are ignored.

## Architecture Decisions

- [Decisions made that affect the system design or
  data model — include why the decision was made]

## Session Notes

- Local-development CORS now permits the React server on port 5173 and
  Flutter Web on the fixed port 5174. Run Flutter with
  `flutter run -d chrome --web-port 5174`.

- Routes table already matches the agreed minimal schema (Id, WorkflowRunId, DistanceMeters,
  EstimatedDurationSeconds, PolylineString, CreatedAt); no migration needed. Build Spec 3.1 and
  Development Plan updated to match. Id and CreatedAt are kept deliberately (PK and audit timestamp).
- Merge: Program.cs now registers A's services/CORS plus C's JWT, Mapbox, orchestrator.
  Added AgentInvokerAdapters.cs (Api) bridging ITriageAgent/IMatchingAgent/IValidationAgent to the
  orchestrator invoker interfaces; Validation is still a stub (run -> Failed).
- Member B added an `IDesignTimeDbContextFactory`, so EF Core migrations use
  `ConnectionStrings__DefaultConnection` and no longer need the API host or JWT configuration.
- Merge (Member D): Validation agent is now real (SafetyRules), plus Dispatch/ValidationResult/AuditTrail
  entities, Dispatch/Audit/Validation controllers, WorkflowStateService, and the web Approval Queue /
  Dispatch History / Analytics tabs and mobile dispatch-status/delivery screens (route `/dispatch`).
  Auth: JWT stays the default; in Development only, requests with no Authorization header fall back to
  a dev Coordinator principal (`JwtOrDev` policy scheme) so D's endpoints are demoable.
  JSON enums now serialize as strings API-wide. D's migration was regenerated as `AddMemberDTables`
  on top of A/B/C's snapshot. Tests: TestAppFactory supplies a test `Jwt:SigningKey`.
- Mapbox: the API token now lives in the gitignored `backend/src/FloodLink.Api/appsettings.Development.json`
  (`Mapbox:ApiKey`). The committed `appsettings.json` keeps the placeholder. For hosted deployments, set
  the `Mapbox__ApiKey` environment variable. Mapbox is only used by the backend; web and mobile have no token.
- Mobile API base URL is now set at build time through `--dart-define=FLOODLINK_API_BASE_URL=...`
  (`mobile/lib/config/api_config.dart`; defaults to `http://localhost:5000`). AppState, ReportProvider and
  InventoryProvider all read it. The main AndroidManifest now declares INTERNET, which release builds need.
  `.github/workflows/release-apk.yml` builds a release APK and publishes it as a GitHub Release when a `v*`
  tag is pushed or the workflow is run manually. It needs the `FLOODLINK_API_BASE_URL` repo secret.

- 2026-10-09 Assignment 2 (testing): added EndToEndWorkflowTests (real agents + Postgres, Mapbox stub),
  SecurityTests (Testing env, real JWTs), DatabaseTests, PostgresCollection (serialises DB-resetting test
  classes), React Vitest/RTL setup + ApprovalQueue tests, k6 scripts in testing/performance. Fixed:
  DEF-001 ValidationAgent now builds PlanDocument from the orchestrator's keyed PlanJson (default vehicle
  capacity 1000, reserve floor 0 until modelled); DEF-002 approve commits proposed stock (409 if short);
  DEF-003 [Authorize] on inventory create/check-in, depot create, Coordinator on POST /api/workflows;
  DEF-004 duplicate inventory item returns 409. Backend 142/142, web 7/7, Flutter 6/6.
  Report: docs/QM-SE3110-Software-Testing-Report.md; evidence in testing-evidence/. ZAP scan still to run.
