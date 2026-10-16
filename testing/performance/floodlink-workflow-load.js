// PERF-005 (FloodLink_Project_Finalization.md 6.28): authenticated load on the API.
//  - read_endpoints: 20 concurrent users (coordinator / depot staff dashboards)
//  - workflow_starts: 5 workflows started at the same moment, each followed to PendingApproval/Failed
// Records p50/p95 per group, success rate, database-backed read latency, end-to-end workflow time and the
// Agentic AI latency per agent (from each step's duration_ms in GET /api/workflows/{id}/logs).
//
// Run:  docker run --rm --network host -v "$PWD/testing:/testing" -v "$PWD/testing-evidence:/evidence" \
//         -e BASE_URL=http://localhost:5000 -e DEMO_PASSWORD=... grafana/k6:0.54.0 \
//         run --summary-export /evidence/performance/k6-workflow-summary.json /testing/performance/floodlink-workflow-load.js
// Env:  BASE_URL (default http://localhost:5000), DEMO_PASSWORD (Seed:DemoPassword of the target API)
import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const PASSWORD = __ENV.DEMO_PASSWORD;

const dbReadMs = new Trend('db_read_ms', true);           // list endpoints that are one DB query each
const workflowE2eMs = new Trend('workflow_e2e_ms', true); // start → PendingApproval/Failed
const agentMs = new Trend('agent_duration_ms', true);     // per agent step, tagged by agent
const workflowOk = new Rate('workflow_reached_pending_approval');
const workflowsStarted = new Counter('workflows_started');

export const options = {
  scenarios: {
    read_endpoints: {
      executor: 'constant-vus', exec: 'readEndpoints', vus: 20, duration: '1m',
    },
    workflow_starts: {
      executor: 'per-vu-iterations', exec: 'startWorkflow', vus: 5, iterations: 1, maxDuration: '2m', startTime: '10s',
    },
  },
  thresholds: {
    'http_req_failed{scenario:read_endpoints}': ['rate<0.01'],
    'http_req_duration{scenario:read_endpoints}': ['p(95)<750'],
    'workflow_reached_pending_approval': ['rate>0.99'],
    checks: ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'max'],
};

function login(email) {
  const res = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } });
  if (res.status !== 200) fail(`login ${email} → ${res.status}: set DEMO_PASSWORD to the API's Seed:DemoPassword`);
  return res.json('token');
}

export function setup() {
  if (!PASSWORD) fail('DEMO_PASSWORD is required');
  return {
    coordinator: login('coordinator@floodlink.lk'),
    depot: login('depot@floodlink.lk'),
    volunteer: login('volunteer@floodlink.lk'),
    admin: login('admin@floodlink.lk'),
  };
}

const auth = (token, extra = {}) => ({ headers: { Authorization: `Bearer ${token}`, ...extra } });

export function readEndpoints(tokens) {
  const token = __VU % 2 === 0 ? tokens.coordinator : tokens.depot;
  const reads = [
    ['inventory', `${BASE}/api/inventory?pageSize=20`],
    ['shelters', `${BASE}/api/shelters?pageSize=20`],
    ['depots', `${BASE}/api/depots`],
  ];
  if (token === tokens.coordinator) {
    reads.push(['reports', `${BASE}/api/reports?sort=urgency_desc&pageSize=10`]);
    reads.push(['workflows', `${BASE}/api/workflows?pageSize=10`]);
    reads.push(['approval-queue', `${BASE}/api/dispatches/approval-queue`]);
  }
  const responses = http.batch(reads.map(([name, url]) => ['GET', url, null, { ...auth(token), tags: { endpoint: name } }]));
  responses.forEach((r, i) => {
    check(r, { 'read ok (200)': (x) => x.status === 200 });
    dbReadMs.add(r.timings.duration, { endpoint: reads[i][0] });
  });
  sleep(1);
}

export function startWorkflow(tokens) {
  // Each VU plans for its own new report, so the five runs don't compete for the same report.
  const report = http.post(`${BASE}/api/reports`, {
    ShelterId: String(1 + (__VU % 3)), NeedType: 'Water', QuantityNeeded: '10', Notes: `k6 load test VU ${__VU}`,
  }, auth(tokens.volunteer));
  if (!check(report, { 'report created (201)': (r) => r.status === 201 })) return;

  const started = Date.now();
  const create = http.post(`${BASE}/api/workflows`,
    JSON.stringify({ objective: `k6 load test run ${__VU}`, reportIds: [report.json('id')] }),
    auth(tokens.coordinator, { 'Content-Type': 'application/json' }));
  if (!check(create, { 'workflow accepted (202)': (r) => r.status === 202 })) return;
  workflowsStarted.add(1);
  const runId = create.json('id');

  let state = 'Triage';
  while (!['PendingApproval', 'Failed'].includes(state) && Date.now() - started < 90_000) {
    sleep(0.5);
    const status = http.get(`${BASE}/api/workflows/${runId}/status`, auth(tokens.admin, {}));
    if (status.status === 200) state = status.json('state');
  }
  workflowE2eMs.add(Date.now() - started);
  workflowOk.add(state === 'PendingApproval');

  const logs = http.get(`${BASE}/api/workflows/${runId}/logs`, auth(tokens.coordinator));
  if (check(logs, { 'logs ok (200)': (r) => r.status === 200 })) {
    for (const step of logs.json()) agentMs.add(step.durationMs, { agent: step.agentName });
  }
}
