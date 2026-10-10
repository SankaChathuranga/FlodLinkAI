# FloodLink AI — Finish Plan (revised Sat 10 October 2026)

> **Superseded by [FloodLink_Project_Finalization.md](FloodLink_Project_Finalization.md)**, which contains
> everything here plus the full task list in phases. Use that file.

**Deadline: Monday 12 October 2026.** Submit Monday by early evening, not at 11:50 PM.
Repository, demo video and deployed services must stay live until **21 October** (spec §15).

This replaces the 8 October plan. Only two items from that plan have landed (Validation now reads
the orchestrator's plan; approval now takes stock out of the depot). About 2.5 days remain, so the
plan is cut to what the spec and rubric need. Anything under "Cut" is not to be started.

---

## 1. Where we are (verified in code, 10 Oct)

### What works
- Database, migrations, seed data, CRUD for shelters, reports, depots, inventory.
- State machine, orchestrator, execution logger, Mapbox Route agent, rule-based Validation agent.
- Validation builds its input from the orchestrator's keyed plan (`ValidationAgent.TryBuildFromOrchestratorPlanAsync`).
- Approve takes the proposed stock out of the depot and refuses if stock has dropped.
- Approve / reject / request-revision / history / summary / audit endpoints; React approval queue,
  dispatch history and analytics; Flutter dispatch status and delivery confirmation.
- Tests: backend (20 test files incl. end-to-end on Postgres, security, database), 1 React test file,
  3 Flutter test files, k6 performance runs, ZAP scan, coverage report (`testing-evidence/`).
- CI, Dockerfile, release-APK workflow.

### What blocks the demo
1. **The pipeline still stops after Triage at runtime.** `POST /api/workflows` calls `AdvanceAsync`
   once. The end-to-end test works only because it calls `AdvanceAsync` itself four times.
2. **No LLM anywhere.** The group report's ADR-004 currently says so.
3. **No login.** A `Users` table exists (with `HashedPassword` placeholders), but there is no login or
   register endpoint, no password hashing, and no login screen in React or Flutter.
4. **No workflow monitor in React and no workflow status in Flutter**, so the Flutter → React →
   Flutter loop (spec Figure 2) can't be shown.
5. **Nothing is deployed** (the report still has `[CONFIRM]` placeholders for URLs).

### Unmerged work
- `feature/Field-Intake-&-Shelter-Reporting` (Sharani, 8 Oct): urgency-scoring fix + test, placeholder test removed.
- `inventory-management` (Eshini, 8 Oct): inventory and depot controller tests.
Merge both tonight; they are small.

---

## 2. Gaps by member

Legend: **MUST** = demo or rubric fails without it. **SHOULD** = loses marks if missing. **CUT** = not this week.

### Member A — Sharani · Field Intake & Shelter Reporting + Triage Agent

| Gap | Priority |
|---|---|
| Triage Agent has no LLM: score is rules + template text. Needs Gemini adjustment (−15..+15) + justification, schema check, rule fallback | MUST |
| `Report` has no free-text `Notes` field (needed for the LLM and the prompt-injection test). Add it (max 500 chars) in API, React and Flutter form | MUST |
| Flutter login, logout, secure token storage (`flutter_secure_storage`), token on every request | MUST |
| `POST /api/reports` should take `ReportedBy` from the token, not the body | SHOULD |
| React "trigger triage" button runs Triage outside the orchestrator; replace with "Start workflow" (C's endpoint) | SHOULD |
| Triage viewer text says "LLM/rule-based" — make it true, then keep it | SHOULD |
| Golden-case tests for Triage (ranking, bounded adjustment, unknown report id rejected, LLM failure → fallback) | MUST |
| Merge the 8 Oct branch | MUST |
| Flutter shelter status view, offline queue | CUT |

### Member B — Eshini · Inventory & Depot Management + Matching Agent

| Gap | Priority |
|---|---|
| Matching Agent has no LLM: greedy loop. Needs Gemini proposal with read-only `get_inventory` tool or inventory snapshot, deterministic stock check, greedy fallback | MUST |
| Stock commit is not concurrency-safe: two approvals can both read the same quantity (lost update). Add a concurrency token (`xmin`) or row lock + retry, and a concurrent-approval test | MUST (this is B's business operation) |
| `QuantityReserved` is never used. Reserve on PendingApproval, commit on approve, release on reject/fail; `PUT /api/inventory/{id}/reserve` and `/release` per the dev plan | SHOULD |
| `GET /api/allocations?status=&shelterId=` missing | SHOULD |
| Validation passes `ReorderThreshold = 0` and `QuantityReserved = 0`, so the reserve-floor rule never fires. Supply the real values (with D) | SHOULD |
| Golden-case tests for Matching (no over-allocation, unmatched needs listed, LLM over-allocation → fallback) | MUST |
| Merge the 8 Oct branch | MUST |
| React depot management + allocation review list, Flutter low-stock list, reservation confirmation, QR scanning | CUT |

### Member C — Sanka · Orchestration & Workflow Engine + Route/ETA Agent

| Gap | Priority |
|---|---|
| Runtime runs only one step. Add a run-to-completion loop (background runner, `POST` returns 202) until PendingApproval or Failed | MUST, first |
| Shared `ILlmClient` + `GeminiClient` (HttpClient, JSON schema output, temperature 0, 20 s timeout, 2 retries, key from `Gemini__ApiKey`) + fake for tests | MUST, first |
| Planner step: Gemini turns the objective into a structured plan; validated (only the 4 agents, fixed order, max 4 steps); default plan on failure | MUST (spec §9 planning and delegation) |
| Endpoints: `GET /api/workflows` (list/filter/page), `GET /{id}/logs` (execution summary), `GET /{id}/status`, `POST /{id}/retry` | MUST (spec §5 "viewing execution summaries") |
| `POST /api/workflows` should take `reportIds`, and Triage should use them | MUST |
| React workflow monitor + execution trace (agent, duration, status, tool calls, fallback flag) + "Start workflow" | MUST |
| Flutter workflow status tracker (poll `/status`, show stage and coordinator note) | MUST |
| Revision re-queues to Matching but nothing runs it; the runner must pick it up | SHOULD |
| Route agent routes only the first allocation | SHOULD |
| Local notifications, route map preview | CUT |

### Member D — Ijini · Validation, Approval & Dispatch Audit + Validation Agent

| Gap | Priority |
|---|---|
| Auth backend: `POST /api/auth/register` (Volunteer), `POST /api/auth/login` (JWT), `GET /api/auth/me`; `PasswordHasher<User>`; seed 4 test accounts; add `Email` to `User`; remove the Development-only fake coordinator | MUST |
| React login page, `AuthContext`, React Router, `ProtectedRoute`, role-based nav, token on requests | MUST (spec §7) |
| Approve doesn't record who approved. `Dispatch.ApprovedById` is never set, and it is `Guid?` while `User.Id` is `int`, so it can't be a foreign key. Change to `int?` + FK, set from the token, show in audit | MUST (audit requirement) |
| Validation uses only the first allocation's coordinates; reserve values hard-coded to 0 (with B) | SHOULD |
| Role attributes on all write endpoints (shelters, reports, depots, inventory still partly open) | SHOULD |
| Update `SEC008` prompt-injection test once the LLM agents exist (hostile note + hostile LLM output must not change stock or skip validation) | MUST |
| LLM-written explanation of validation results | CUT |

### Shared
| Gap | Owner |
|---|---|
| Gemini API key from Google AI Studio (the AI Pro subscription is not API access); in user-secrets / env only, never pasted into an AI tool | Sanka, tonight |
| Deploy API + Postgres, React, publish APK | Eshini (CI owner) with Sanka |
| Rewrite report: ADR-004, §2.4, §5, §16 security, test and eval sections, live URLs, `[CONFIRM]` items | All, Monday |
| Individual sections ×4: contribution, commits/PRs/tests, challenges, AI usage log, own ~1-page reflection, signed declaration | Each member |
| Demo video (10 min, "anyone with link") | Sanka records, all appear |

---

## 2b. Spec audit — items not in the earlier plan (checked in code, 10 Oct)

### Backend (spec §5, §6, §9, §11)
| Finding | Spec | Fix | Owner | Priority |
|---|---|---|---|---|
| Swagger is enabled only in Development; the deployed API will have no Swagger URL | §14 | Enable Swagger in all environments | Sanka | MUST |
| 500 responses return `exception.Message` in `detail` | §5 security | Return `detail` only in Development | Ijini | MUST |
| Execution log never records tool calls or retries: `toolCallsJson` and `isRetry` are never passed by the orchestrator, and there is no retry endpoint | §9 observability | Pass tool calls (Mapbox, inventory, LLM) and retry flag; add `POST /workflows/{id}/retry` with a limit | Sanka | MUST |
| Workflows controller has 3 endpoints; each member needs ≥ 4 | §5 | List / logs / status / retry (already planned) | Sanka | MUST |
| Pagination / search / sort missing: Reports (no paging), Inventory, Depots, Workflows, Audit | §4.1, §5 | Add `page`, `pageSize`, `search`, `sort` to each member's main list endpoint | A: Reports · B: Inventory + Depots · C: Workflows | MUST |
| 7 of 8 controllers use `AppDbContext` directly; no service layer for most business operations | §5 architecture | Move each member's business operation into a service (A: report submission/triage, B: stock commit/reserve, C: workflow service, D: approval/dispatch). No full refactor | Each member | SHOULD |
| `UpdatedAt` missing on Report, Depot, Dispatch, User, AllocationProposal | §6 audit fields | Add + migration (one PR to avoid migration conflicts) | Eshini | SHOULD |
| Only 4 explicit indexes (EF adds FK indexes) | §6 | Add on `Reports.Status`, `AllocationProposals.Status`, `WorkflowRuns.CurrentState`, unique `Users.Email` (same migration) | Eshini | SHOULD |
| Mapbox treats HTTP 429 as a non-retryable 4xx | §11 rate limits | Retry 429 with backoff; same for Gemini | Sanka | SHOULD |
| Logging only in the exception middleware and Mapbox client | §5 structured logging | `ILogger` in orchestrator, agents, auth and approval (structured fields: runId, agent, durationMs) | Each member | SHOULD |
| LLM output storage | §6 "no hidden reasoning, passwords, tokens" | Store structured output + short justification only; never raw model reasoning, keys or tokens | Sanka, Sharani, Eshini | MUST (design rule) |

### Clients (spec §7, §8)
| Finding | Fix | Owner | Priority |
|---|---|---|---|
| React has 1 test file (ApprovalQueue). Spec asks for component, form-validation, protected-route, API-integration and error-state tests | 2 tests per member on their own screens (login/protected route: Ijini) | Each member | MUST |
| Flutter has 3 test files; no navigation or API-integration test | Navigation guard test + one provider test with a mocked HTTP client | Sharani, Sanka | MUST |
| Flutter needs registration as well as login | Already in §3a | Sharani | MUST |

### Agent evaluation (spec §12)
Needs evidence for: planning and delegation, tool selection, structured outputs, deterministic validation,
business rules, approval enforcement, prompt-injection resistance, failure recovery, safe failure.
One golden-case test file per agent plus one end-to-end golden case. Performance re-run must include
**Agentic AI latency** (from `AgentExecutionLog.duration_ms`).

### Git and process (spec §13) — evaluators read the Git history
| Finding | What to do |
|---|---|
| **Zero GitHub issues** and (likely) no project board | Create one issue per remaining task in this plan **now**, assign owners, add a project board, close issues from PRs (`Closes #n`) |
| PRs #1–#6 have no human reviews | Every remaining PR gets a real review comment from a teammate before merge |
| Recent work goes straight to `main` with messages like "updates" | Feature branches + PRs for everything left; descriptive commit messages |
| First commit is 21 Sep (spec: repo from the start of the project) | Cannot be changed. **Do not back-fill or rewrite history** (spec §18.2). State it honestly in the report |
| Sharani has the fewest commits (11) | Her remaining work (Triage LLM, Flutter auth, notes field, tests) must be committed under her own account |

### Documents and submission (spec §14, §15, §18)
| Finding | Fix | Owner |
|---|---|---|
| README (134 lines) lacks: architecture, agent architecture, env var names, test accounts, test instructions, deployment, live URLs, contributions, security, AI declaration | Extend README to spec §14.1 | Ijini |
| No diagram files; report has no rendered diagrams (§4.2, §5.8, §6.4) | Mermaid diagrams: system context, agent workflow, ER diagram (exported to PNG for the PDF) | Eshini (ER), Sanka (architecture, workflow) |
| ADRs: no cloud-deployment-platform ADR; agent workflow-state schema only partly covered; ADR-004 says "no LLM" | Rewrite ADR-004; add deployment ADR; expand the workflow-state ADR | Sanka |
| Individual sections have `[CONFIRM]` placeholders; no AI usage logs exist | Each member writes their log from real records (chat history, dates, tools, what changed, how verified). **No invented entries** (§18.2) | Each member |
| Reflection (~1 page, own words) and signed declaration per member | Each member writes their own; an AI-written reflection gets no credit (§18.3) | Each member |
| Group number, repo URL, live URLs still placeholders on the title page | Fill in Monday | Sanka |
| Services must stay up until 21 Oct | Check the free tier won't sleep permanently or expire before then (free Postgres tiers can expire); note cold-start delay for evaluators | Eshini |
| Postgres "restricted credentials" | App connects as a non-superuser role; document init steps | Eshini |

### Already fine (no action)
CI runs backend, web and mobile jobs on push and PR. Mapbox has a timeout and bounded retries. No real
Mapbox key found in the committed config (only the placeholder). k6, ZAP and coverage evidence exists.

---

## 3. Design (unchanged from 8 Oct, shortened)

**LLM.** One `ILlmClient` in Infrastructure; `GeminiClient` calls REST `generateContent` with a response
schema. Model in `Gemini:Model` (a Flash-class model your key lists). CI uses a fake. Live runs are a
separate test category, excluded from CI. Send only ids, need types, quantities and short notes, no
names or phones (spec §11; free-tier prompts may be used by Google).

| Agent | LLM | Tool (allow-listed, read-only) | Deterministic guardrail | Fallback |
|---|---|---|---|---|
| Planner (orchestrator step) | Yes | none | only the 4 agents, fixed order, ≤ 4 steps | default plan |
| Triage | Yes | shelter context | report id must be in input; adjustment −15..+15; score 0–100 | rule score |
| Matching | Yes | inventory | qty ≤ free stock; depot/item exist; every need allocated or listed unfulfillable | greedy |
| Route/ETA | No | Mapbox | plausibility, timeout, retries | safe Failed |
| Validation | No (by design) | none | it is the guardrail | — |

Every LLM call is logged in `AgentExecutionLog.tool_calls_json`: prompt version, model, latency,
`usedFallback`. The trace shows `usedFallback`; don't hide it.

**Prompt injection.** Notes are untrusted data inside delimiters; outputs are schema-checked and
bounded; Validation re-checks stock whatever the LLM says.

**Auth.** JWT login, `PasswordHasher<User>`, roles Volunteer / Coordinator / DepotManager / Admin.
Test accounts listed in the README (spec §14.1).

---

## 3a. Authentication and role-based access (owner: Ijini, clients: Ijini + Sharani)

### Current state (10 Oct)
- The API validates JWTs, but nothing issues them: no login, register or token endpoint.
- Passwords are placeholder strings (`hashed_password_1`); no hashing.
- In Development every token-less request is silently a Coordinator (`DevCoordinatorHandler`).
- Neither React nor Flutter sends an `Authorization` header, so outside Development every
  Coordinator-only endpoint returns 401 and the deployed demo cannot approve anything.
- Shelters, reports and depot/inventory reads have no `[Authorize]`; depot/inventory writes accept any logged-in role.
- `confirm-delivery` is Coordinator-only, but the volunteer at the shelter is the one who confirms delivery.
- Users have no email; seeded users are a Volunteer and a Coordinator only.

### Backend
- `User`: add `Email` (unique). Hash passwords with `PasswordHasher<User>` (PBKDF2).
- Role constants in one class: `Volunteer`, `Coordinator`, `DepotManager`, `Admin`. Admin is included in every staff role check.
- `POST /api/auth/register` → always creates a **Volunteer** (the client can't choose the role).
- `POST /api/auth/login` → JWT with `sub` (user id), `name`, `role`, 8-hour expiry; wrong email and wrong password return the same 401.
- `GET /api/auth/me`.
- `POST /api/users` (Admin) creates Coordinator / DepotManager accounts. (SHOULD)
- Rate-limit `login` with the built-in ASP.NET rate limiter. (SHOULD)
- Seed four demo accounts at startup from `Seed:DemoPassword` (hashed then, so no hash is committed). List them in the README.
- Remove `DevCoordinatorHandler` once both clients send tokens.
- Ownership check: a Volunteer sees only their own reports and their own workflow status, filtered by the `sub` claim. `ReportedBy` comes from the token.

### Endpoint access

| Endpoint group | Volunteer | Coordinator | DepotManager | Admin |
|---|---|---|---|---|
| `auth/login`, `auth/register`, `/health` | public | public | public | public |
| `GET shelters` | ✓ | ✓ | ✓ | ✓ |
| `POST/PUT shelters` | | ✓ | | ✓ |
| `POST reports`, `GET reports/mine`, own workflow `status` | ✓ | | | ✓ |
| `GET reports` (all), `PUT reports`, triage plans | | ✓ | | ✓ |
| `GET depots`, `GET inventory` | | ✓ | ✓ | ✓ |
| `POST depots`, `POST inventory`, `check-in`, `reserve/release` | | | ✓ | ✓ |
| `POST workflows`, list, logs, retry | | ✓ | | ✓ |
| `approve / reject / request-revision`, validations, audit, dispatch history and summary | | ✓ | | ✓ |
| `confirm-delivery`, `dispatches/by-workflow` for own report | ✓ | ✓ | | ✓ |
| `POST users` | | | | ✓ |

### React (Coordinator, DepotManager, Admin)
- `AuthContext` holds token, user and role; `login()` / `logout()`.
- The API client adds `Authorization: Bearer`; a 401 logs out and goes to `/login`.
- React Router: `/login`, `/shelters`, `/reports`, `/workflows`, `/inventory`, `/approvals`, `/dispatches`, `/analytics`.
- `ProtectedRoute roles={[...]}`; Navbar shows only the tabs for the role. A Volunteer login is refused with "use the mobile app".
- Token in `sessionStorage` (ADR note: simpler than cookies, XSS risk accepted for the assignment).

### Flutter (Volunteer, DepotManager)
- Login and register screens with validation; `AuthProvider` (`ChangeNotifier`).
- Token in `flutter_secure_storage`; one HTTP helper adds the header for all providers.
- Route guard; home screen by role: Volunteer → reports and status, DepotManager → stock check-in.
- Logout clears secure storage.

### Tests
- Login success / wrong password / unknown email; register ignores a role in the body.
- 401 without token and 403 with the wrong role for each endpoint group above (one `[Theory]` table).
- A Volunteer cannot read another volunteer's report.
- React: `ProtectedRoute` redirects; login form validation. Flutter: login form validation.

### Order
1. Backend auth, with the dev fallback still on locally (Saturday night).
2. React and Flutter send tokens (Sunday morning).
3. Remove the dev fallback, run all tests, deploy (Sunday afternoon).

---

## 4. Schedule

Merge into `main` through small PRs, each reviewed by one teammate. Pull `main` before starting each block.

### Saturday night (today) — unblock everyone
| Who | Task | Done when |
|---|---|---|
| Sanka | Merge both 8 Oct branches. Land `ILlmClient` interface + fake + `GeminiClient`. Land run-to-completion runner | A run started through the API reaches PendingApproval with rule-based agents |
| Ijini | Auth backend + seeded test accounts; fix `ApprovedById` (migration) | Login returns a JWT; approve records the approver |
| Sharani | Add `Notes` to `Report`; write the Triage prompt + schema against the fake client | PR open |
| Eshini | Write the Matching prompt + schema against the fake client; concurrency token on stock commit | PR open |

### Sunday morning — agents and screens
| Who | Task |
|---|---|
| Sanka | Planner step; workflow list/logs/status/retry endpoints; `POST` takes `reportIds` |
| Sharani | Triage LLM live with fallback; Flutter login + secure storage |
| Eshini | Matching LLM live with fallback; reserve/release if time |
| Ijini | React login, router, protected routes, role nav |

**Gate (Sunday 1 PM):** React login as Coordinator → start a workflow → real Gemini → PendingApproval → approve → stock changes, approver recorded.
If the LLM agents aren't working by then, ship them with fallback on and keep going; don't stall the rest.

### Sunday afternoon — close the loop and deploy
| Who | Task |
|---|---|
| Sanka | React workflow monitor + trace; Flutter status tracker |
| Sharani | Triage golden tests; React "Start workflow" replaces "trigger triage" |
| Eshini | Matching golden tests + concurrent-approval test; deploy API + Postgres + React; trigger APK release |
| Ijini | Update prompt-injection test; role attributes on write endpoints; React tests for login and protected route |

**Gate (Sunday 8 PM):** APK on a phone logs in against the deployed API, submits a report; React (deployed) approves; the phone shows the new status. **Feature freeze.**

### Sunday night — evidence
- Run the live Gemini golden cases once and save the real output in `testing-evidence/agents/`.
- Re-run k6 against the deployed API including workflow starts, to get real agent latency.
- CI green on `main`.

### Monday — documents and submission
| Time | Task |
|---|---|
| Morning | Update the group report: ADR-004 (custom C# orchestrator + Gemini; Validation stays rule-based), agent architecture, security, testing and eval sections with real results, live URLs, test accounts. Each member writes their individual section, AI usage log and own reflection |
| Midday | Record the 10-minute demo video following the checklist below |
| Afternoon | Export one PDF, check every link in a private window, name items `SE3090_G<number>`, group leader submits |

---

## 5. Demo checklist (spec §17.1)

1. Log in with different roles: Volunteer in Flutter, Coordinator and DepotManager in React; show a 403.
2. Volunteer submits a report with GPS (and notes) in Flutter.
3. Coordinator starts a workflow in React. The trace shows Planner → Triage → Matching → Routing → Validation with timings, tool calls and fallback flags.
4. Show one unsafe plan blocked by Validation.
5. Coordinator approves; show stock change in Postgres and the audit entry with the approver.
6. Flutter shows the updated status.
7. Show Swagger, CI green, tests, deployed URLs and GitHub history.

## 6. Cut (do not build)

Photo work beyond what exists, notifications, route map preview, QR scanning, depot management
and allocation review screens, low-stock and reservation-confirmation Flutter screens, offline queue,
LLM explanation of validation, analytics polish.

## 7. Risks

| Risk | Mitigation |
|---|---|
| Gemini rate limit or outage during the demo | Fallbacks on; `usedFallback` visible; keep one recorded successful run |
| Auth breaks existing tests and screens | Land auth backend tonight; fix `TestAppFactory` to issue tokens in the same PR |
| Deployment surprises | Start Sunday afternoon, not Monday; health check first |
| Merge conflicts | Small PRs, one owner per file area, pull before each block |
| Report claims not matching the code | Every claim checked against `main` on Monday; only real test and eval results |
