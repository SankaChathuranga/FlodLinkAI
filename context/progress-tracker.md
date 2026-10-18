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
- Review 2026-10-10: Validation now builds its plan input and approve now commits stock. Still open: runtime
  stops after Triage, no LLM, no login endpoint or screens, no workflow monitor/status screens, nothing deployed,
  `Dispatch.ApprovedById` never set (and Guid vs int User.Id). Plan revised in `FloodLink_Finish_Plan.md`
  with per-member gaps; two teammate branches from 2026-10-08 are still unmerged.
- 2026-10-10: `FloodLink_Project_Finalization.md` is now the master task list (Phases 0–9, task IDs per
  member, gates). It supersedes `FloodLink_Finish_Plan.md`.

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

- 2026-10-10 — **Phase 1 done** (`FloodLink_Project_Finalization.md` 1.01–1.20). Background runner
  (`Api/Workflows`) advances queued runs to PendingApproval/Failed and re-queues unfinished runs at startup.
  `POST /api/workflows` takes `reportIds`, returns 202; new `GET /api/workflows` (filter/search/page),
  `/{id}/logs`, `/{id}/status`, `POST /{id}/retry` (limit `Workflow:MaxRetries`, default 3). Agents return
  `ToolCall`s, logged in `AgentExecutionLog.ToolCallsJson` (jsonb) with `IsRetry`. Route agent routes every
  depot→shelter pair (`Route.Legs`, one `Routes` row per leg); Validation checks each leg plus
  `RouteCompleteness` and uses real `QuantityReserved`/`ReorderThreshold`. Stock lifecycle in
  `StockReservationService` (reserve after validation, commit on approve, release on reject/revision; `xmin`
  tokens on `InventoryItem` and `WorkflowRun`). All coordinator decisions go through `DispatchService`; the
  duplicate `/api/workflows/{id}/decision` endpoint, `ApplyCoordinatorDecisionAsync` and `WorkflowStateService`
  were removed (`WorkflowEngine` is the only transition table, incl. retry Failed → FailedAtState).
  `Dispatch.ApprovedById` / `AuditTrail.ActorId` are now `int?` FKs to Users. Reports move
  New → Triaged → InPlan → Resolved. Swagger is on in every environment; 500 detail only in Development;
  Mapbox retries 429 with backoff. `trigger-triage` is now a run-free preview. Seed depots moved off the
  shelter coordinates (they produced 0 km routes). Migrations `WorkflowRunnerAndStockLifecycle`,
  `SeparateSeedDepotsFromShelters`. Backend tests: 174 pass.

- 2026-10-12 — **Phase 2 (auth + roles) built** (`FloodLink_Project_Finalization.md` 2.01–2.24). Backend:
  `User.Email` (unique, lower-case) + `PasswordHasher<User>` hashes; `Domain/Security/Roles`; policies in
  `Api/Auth/Policies.cs` (every policy also admits Admin); `POST /api/auth/register` (always Volunteer),
  `POST /api/auth/login` (8 h JWT with sub/name/email/role, same 401 for wrong email or password, rate-limited
  by `Auth:LoginPermitLimit`/`Auth:LoginWindowSeconds`), `GET /api/auth/me`, Admin `GET/POST /api/users`
  (Coordinator/DepotManager only). Access matrix applied to every controller; ownership via
  `Api/Auth/Ownership.cs` (Volunteer: own reports, own run status, own by-workflow/confirm-delivery, 404 for
  others). `GET /api/reports/mine`; report reporter comes from the token. `DevCoordinatorHandler`/`JwtOrDev`
  removed — no token-less access in any environment. Four demo accounts (`Infrastructure/DemoAccounts.cs`,
  ids 1–4) are seeded locked and get their hash from `Seed:DemoPassword` at startup. Swagger has a Bearer
  "Authorize" button. `User.HashedPassword` is `[JsonIgnore]`. Migration `AddUserEmailAndDemoAccounts` (also
  moves the Users id sequence past the seeded ids). Tests: `TestAppFactory.TokenFor/CreateClientAs`,
  `AuthTests`, `AccessMatrixTests` (37 endpoint rows); backend 232 pass. React: `AuthContext` +
  `auth/session.ts` (sessionStorage), `api/http.ts` `apiFetch` (Bearer header, 401 → /login), React Router
  routes from `routes.ts`, `ProtectedRoute`, role-filtered Navbar, login page (Volunteers refused), Admin
  `/users` page, basic `/workflows` list; web tests 15 pass. Flutter: `AuthProvider` + `SecureTokenStore`
  (flutter_secure_storage), one `ApiClient` used by ReportProvider/InventoryProvider/DispatchApi, login and
  register screens, GoRouter guard (`auth/route_guard.dart`: Volunteer → `/`, DepotManager →
  `/stock-check-in`, Coordinators refused), sign-out buttons.

- 2026-10-15 — **Phase 3 (LLM agents) built** (3.01–3.10). `Domain/Llm/ILlmClient` + `LlmData` (fences
  untrusted text) + `LlmToolCall` (log entry: prompt version, model, latency, tokens, `usedFallback`, reason);
  `Infrastructure/Llm/GeminiClient` (REST generateContent, JSON schema, temp 0, 20 s timeout, 2 retries,
  429 `Retry-After`, key in header; no key → LLM_NOT_CONFIGURED, no request) and `PromptLibrary` (embedded
  `Prompts/<name>.v<n>.md`). New project `FloodLink.Agents.Planner` (`planner.v1`): stored under `plan`,
  runs once before Triage as its own logged step (no new state). Triage (`triage.v1`): rule score + LLM
  adjustment −15..+15, checked, final clamped 0–100; `TriagePlan.ScoringMethod`, item `RuleScore`/
  `LlmAdjustment`. Matching (`matching.v1`): LLM proposal from the free-stock snapshot, checked, else greedy;
  coordinator revision notes passed as `<revision_notes>` data. Tools stay agent-run (model can't call them).
  `Report.Notes` (≤ 500) in API, React report cards and the Flutter form. Route and Validation stay
  deterministic — `docs/adr/ADR-010-gemini-backed-agents.md` (supersedes ADR-004). Migration
  `AddReportNotes`. Tests: `FakeLlmClient`, `LlmAgentTests`, `GeminiClientTests`, `LlmWorkflowEndToEndTests`;
  backend 261 pass, web 15, Flutter 17. Live check 15 Oct: `gemini-2.5-flash` returns 404 "no longer
  available to new users", so the default is now `gemini-3.8-flash` with `Gemini:ThinkingLevel` `low` (~3 s
  instead of ~8 s per call) and a 30 s timeout; Planner and Triage used Gemini, Matching fell back on 503s.

- 2026-10-15 — **Phase 4 (client screens) built** (4.01–4.08). React: `/workflows` is now `WorkflowMonitor`
  (stage + progress bar, stage filter, search, paging, 5 s auto-refresh, "Failed runs" tab with Retry up to
  the run's `retryLimit`); `/workflows/:id` is `WorkflowTrace` (plan, timeline with tool calls and LLM calls
  incl. model/prompt/tokens/usedFallback, triage scores, allocations, route legs, safety checks, decisions
  with approver, audit; polls every 3 s while active). Reports page: select New/Triaged reports + objective
  → Start workflow → opens the trace (the Triage viewer's preview button was removed). Approval Queue: plan
  summary per run, "View trace", notice names the approver. API: approval-queue items carry a plan summary;
  `by-workflow` returns every decision with `decidedBy` (name); workflow list carries `retryLimit`. Flutter:
  `WorkflowStatusScreen` (`/status/:runId`, polls `/status` every 5 s, stops when settled; stages, coordinator
  note, ETA, Confirm delivery) and `ReportWorkflowLink` on both report lists. Typed client `web/src/api/workflows.ts`.
  Tests: backend 261, web 22 (`Workflows.test.tsx`), Flutter 22 (`workflow_status_test.dart`).

- 2026-10-15 — **Phase 5 (backend quality) built** (5.01–5.10). Shared paging helper `Api/DTOs/PaginatedResult.cs`
  (`Paging.Normalize`, `ToPageAsync`; page size 1–100, default 20). `GET /api/reports` now pages and takes
  `search` (need type, notes, shelter name, `#R-id`) and `quantity_*` sorts; `GET /api/inventory` takes `search`,
  `lowStock` (free ≤ reorder threshold), `sort`, paging; `GET /api/depots` pages/searches and returns `DepotDto`
  (manager name, item count); new `GET/PUT /api/depots/{id}` (edit, assign a DepotManager via `Depots.ManagerId`);
  new `GET /api/audit` (event type / actor / dispatch / run / date filters, paged) and `GET /api/audit/event-types`.
  Service layer: `Api/Services/ReportService` (submit + urgency, edit, queries), `Api/Workflows/WorkflowService`
  (start, list, logs, status, retry), stock check-in moved into `StockReservationService`; `ReportsController`,
  `WorkflowsController` and the stock endpoints no longer use `AppDbContext`. `IHasUpdatedAt` + `AppDbContext`
  stamps `UpdatedAt` on every modified row (Report, Depot, Dispatch, User, AllocationProposal, plus Shelter,
  InventoryItem, WorkflowRun). Structured `ILogger` logging in orchestrator (one line per agent step: run, agent,
  duration, tool/LLM calls, used fallback), runner (scope with `WorkflowRunId`), stock, dispatch, report, depot
  and workflow services; JSON console logs outside Development. DTO validation added for dispatch notes,
  workflow report ids, depot manager ids, shelter contact ids. Migration `UpdatedAtIndexesAndDepotManager`
  (applied to dev DB). React: Reports queue search + paging (selections kept across pages), Inventory search,
  low-stock filter and tag, sort, Carbon pagination. Flutter inventory provider reads paged depots/inventory.
  Tests: `BackendQualityTests`, access-matrix rows for the new endpoints, orchestrator log-field test,
  web `Lists.test.tsx`; backend 278, web 28, Flutter 22.

- 2026-10-15 — **Phase 6 backend tests and agent evaluation built** (6.01–6.16). New `AgentEvaluation/` test
  folder: `GoldenCases.cs` (fixed "flood night" world: insulin vs routine medical with equal rule scores, a hostile
  note, a need larger than stock; `GoldenChecks` safety invariants for Planner/Triage/Matching/Route output),
  `AgentGoldenCaseTests` (CI, scripted LLM), `AgentSafetyApiTests` (real API + runner + Postgres: Mapbox failure →
  safe Failed with nothing reserved; prompt injection end to end; no dispatch before PendingApproval; golden
  report → approval → delivery) and `LiveAgentEvaluationTests` (`Category=Live`, real Gemini, writes
  `testing-evidence/agents/`). Also `ReportApiTests`, AUTH016/017, SEC008b, E2E007 (concurrent approval),
  E2E008 (retry limit). CI runs `--filter "Category!=Live"`. Fixes: uploaded report photos were saved but never
  served — now `UseStaticFiles` on `wwwroot/uploads` (gitignored), JPEG/PNG/WebP only, ≤ 10 MB;
  `SafetyRules.CoordinateSanity` rejects (0, 0) points and road distances under 0.9 × straight line (two test
  fixtures had impossible distances and were corrected). Backend 337 tests pass. Live run: Planner/Triage used
  Gemini (insulin +15, routine −5, hostile note 0); Matching got 429 (free-tier quota) and used greedy.

- 2026-10-15 — **Phase 6 finished** (6.17–6.29; 6.28 deployed run waits for Phase 7). Web: `Lists.test.tsx`
  extended (report filters/sort/error, inventory sort/failed hold/release), long-running monitor/trace tests,
  `api/client.test.ts`; `test/setup.ts` stubs ResizeObserver. React inventory: Hold / Release actions next to
  Check in, one stock-action modal that shows the API's refusal message. Flutter: `stock_check_in_test.dart`,
  `providers_test.dart`, `dispatch_screens_test.dart`. k6: new `testing/performance/floodlink-workflow-load.js`
  (login in setup, 20 read users + 5 simultaneous workflows, agent latency from logs), old scripts now log in;
  results in `testing-evidence/performance/k6-workflow-results.md` (local, fresh DB, Gemini off). Fix: workflow
  list selected full rows including `PlanJson` (22 MB on old runs) — now summary columns only. Note: local DB
  `floodlink_perf` holds Oct 9 ZAP-scan junk (52k reports); don't use it for measurements. Tests: backend 337,
  web 41, Flutter 40.

- 2026-10-18 — **Phase 8 batch 1** (8.01, 8.02, 8.04, 8.07, 8.13, 8.14, 8.17). API: `RoutesController`
  (calculate via Mapbox without storing; read stored legs), `POST /api/allocations/{id}/trigger-matching` (→
  `WorkflowService.RetryAsync`), `GET /api/depots/managers` (DepotManager picker; `/api/users` stays Admin-only),
  inventory `managedByMe`, dispatch summary `medianApprovalMinutes` + `dispatchesPerShelter`, and turnaround fixed
  (run start → approval). React: `DepotsPage` (`/depots`), `AllocationsPage` (`/allocations`), `api/stock.ts`,
  Analytics per-shelter table. Flutter: `LowStockScreen` (`/low-stock`, depot-manager side of the guard),
  `ShelterStatusScreen` (`/shelters`); `InventoryItem` gains reserved/threshold/depot name, `Shelter` gains
  `updatedAt`. Tests: `Phase8ApiTests`, access-matrix rows, `Phase8.test.tsx`, `phase8_screens_test.dart`;
  backend 352, web 50, Flutter 46. 8.08 deferred to 8.11 (map pin needs a map package).

- 2026-10-18 — **Phase 8 batch 2** (8.08, 8.10, 8.11, 8.12, 8.15, 8.16). API: `ValidationExplainer`
  (Agents.Validation, prompt `validation-explainer.v1`, template fallback, pass/fail copied from rules) behind
  `POST /api/validations/{id}/explain` (cached under plan key `validationExplanation`, logged as a step);
  `POST /api/dispatches/{id}/delivery-photo` (Approved runs, run-access rules of confirm-delivery,
  `PhotoStorageService` folder `deliveries`); `WorkflowStatusResponse.RouteLegs`. React: `lib/polyline.ts`,
  `RouteMap` (Mapbox or SVG fallback, also when WebGL is missing), trace map + names from `/api/routes`,
  `ValidationExplainer` panel, delivery photo. Flutter: packages `flutter_map`, `latlong2`,
  `flutter_local_notifications`; `maps/map_tiles.dart` (OSM tiles off under `flutter test`), `maps/polyline.dart`,
  `RouteMapScreen`, GPS map pin, `notifications/status_notifier.dart` (+ `Provider<StatusNotifier>`),
  delivery photo pick/upload. Android: desugaring (`desugar_jdk_libs` 2.1.4), ACCESS_FINE/COARSE_LOCATION and
  POST_NOTIFICATIONS permissions — the manifest had no location permission, so GPS would have failed on phones.
  ADR-010 addendum. Tests: backend 366, web 56, Flutter 54.

- 2026-10-18 — **Phase 8 batch 3** (8.03, 8.05, 8.06, 8.09). Domain: `StockMovement` + `StockMovementKinds`;
  `AllocationProposalEntity` gains `CountedQuantity`/`ConfirmedAt`/`ConfirmedById`/`ConfirmationNote`; `Report.ClientReference`
  (filtered unique index with `ReportedBy`; plain `ReportedBy` index kept explicitly). `StockReservationService` records a
  movement for every change (approver/rejecter passed from `DispatchService`; added movements detached on a concurrency
  conflict) and gains `ConfirmReservedStockAsync`. API: `GET /api/inventory/{id}`, `GET /api/inventory/movements`,
  `POST /api/allocations/{id}/confirm-stock`, allocations `managedByMe` + confirmation fields, approval-queue `stockCheck`,
  `ReportService.SubmitOnceAsync` (re-send → 200 with the saved report). React: `StockHistoryPage` (`/stock-history`),
  Inventory History link + QR label (`qrcode` 1.5.4), approval-queue stock badge. Flutter: `mobile_scanner`,
  `shared_preferences`; `ScanLabelScreen`, `stock/stock_label.dart`, check-in "Scan shelf label",
  `ReservationConfirmationScreen` (`/reservations`), `offline/report_queue.dart` + `QueuedReportsBanner`,
  `ReportProvider.submitReport` now returns `SubmitOutcome`. iOS usage descriptions added. Migration
  `StockMovementsAndConfirmations` (applied to dev DB). Tests: `Phase8Batch3Tests`, `Batch3.test.tsx`,
  `phase8_batch3_test.dart`; backend 379, web 62, Flutter 63.

- 2026-10-18 — **Phase 8 polish** (8.18 Carbon consistency, 8.19 accessibility/responsiveness). React: `Navbar` is a
  Carbon UI Shell (`routes.ts` pages carry a `group`; menus Operations/Stock/Insights/Admin; side nav on small
  screens; skip link to `#main-content`); Login, Users, Reports, Shelters (+ detail `Modal`), Triage (`RadioTile`
  run list), Workflows (`ContentSwitcher`), Trace (`ProgressIndicator` pipeline, Carbon tables), Approvals
  (`TextArea` with label, `aria-pressed` decision buttons), Dispatches, Analytics, ProtectedRoute, RouteMap legend
  converted; `stateTagType` replaces `stateBadgeClass`; legacy `fl-*` component CSS removed (layout `fl-*` kept).
  Tests: `test/a11y.ts` (axe-core 4.10.2), `Accessibility.test.tsx`, `test/contrast.test.ts`; `test/setup.ts` stubs
  `matchMedia`. ADR-011; `context/ui-context.md` documents components, tag colours and a11y rules. Web tests 95.
  Phase 8 complete except the manual phone/narrow-screen check.

## In Progress

- None.

## Next Up

- Set `Seed:DemoPassword` and `Jwt:SigningKey` in user-secrets (or env vars) for local runs; the demo accounts
  stay locked without a demo password.
- Gate 4 by hand: Flutter report → React start + trace → approve → Flutter shows Approved with ETA.
- Phase 7 (deployment and APK). Deployed 6.28 run done 16 Oct: set `Gemini__ApiKey` and
  `ASPNETCORE_ENVIRONMENT=Production` on Render; consider putting Neon and Render in the same region
  (~90 ms per DB round trip); reads p95 2.5 s vs 750 ms target.
- Re-run the live evaluation when the Gemini quota resets so Matching is evaluated on model output.
- Confirm CI runs on PRs and provides Postgres for the integration tests.
- Run the mobile dispatch screens on an emulator/device.

## Open Questions

- Access matrix gives `GET /api/workflows/{id}/status` to the owning Volunteer and Admin only, not
  Coordinators (they use the list and logs endpoints). Revisit if the React monitor (4.01) needs it.
- `web` has no ESLint config, so `npm run lint` fails before linting anything (pre-existing).

- `/context` is in .gitignore (from main); existing files stay tracked but new files there are ignored.

## Architecture Decisions

- [Decisions made that affect the system design or
  data model — include why the decision was made]

## Session Notes

- 2026-10-18 — Added `docs/USER-GUIDE.md` and linked it from README. The guide covers implemented mobile/web workflows by role, status meanings, stock lifecycle, troubleshooting, and current limitations (including no persistent offline report queue). Verified against source screens and services; no application behavior changed.

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

- 2026-10-15 CORS: in Development the API now allows any loopback origin (Flutter web via
  `flutter run -d chrome` uses a random port). Production still uses the fixed list + `Cors__AllowedOrigins`.
  `start_floodlink.sh` now uses `docker compose` (falls back to `docker-compose`) and has the shebang on line 1.

- Shelters map: `ShelterMap` now renders a real Mapbox GL map (outdoors-v12, colored occupancy pins, popups, auto-fit) when `VITE_MAPBOX_TOKEN` is set in `web/.env`; otherwise it falls back to the old simulated grid map. Added `mapbox-gl` dependency.

- 2026-10-18 Shelters: Coordinators (and Admins) can add and edit shelters from the web Shelters dashboard (`ShelterFormModal`, `api/shelters.ts`) using the existing `POST/PUT /api/shelters`. The contact volunteer is kept as-is on edit; there is no picker yet.
