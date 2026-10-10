// PERF-001..PERF-003: FloodLink API load test (k6).
// Run:  docker run --rm --network host -v "$PWD/testing:/testing" grafana/k6 run /testing/performance/floodlink-load.js
// Env:  BASE_URL (default http://localhost:5055)
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5055';
const NEEDS = ['Water', 'Food', 'Medical'];

export const options = {
  scenarios: {
    // PERF-001: field volunteers submitting reports during a flood surge.
    report_submission: {
      executor: 'ramping-vus', exec: 'submitReport',
      stages: [{ duration: '30s', target: 50 }, { duration: '1m', target: 50 }, { duration: '15s', target: 0 }],
    },
    // PERF-002: coordinators and depot staff reading dashboards.
    dashboard_reads: {
      executor: 'constant-vus', exec: 'readDashboards', vus: 30, duration: '1m45s',
    },
  },
  thresholds: {
    'http_req_failed': ['rate<0.01'],
    'http_req_duration{scenario:report_submission}': ['p(95)<1000'],
    'http_req_duration{scenario:dashboard_reads}': ['p(95)<750'],
    'checks': ['rate>0.99'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function submitReport() {
  const res = http.post(`${BASE}/api/reports`, {
    ShelterId: '1',
    ReportedBy: '1',
    NeedType: NEEDS[Math.floor(Math.random() * NEEDS.length)],
    QuantityNeeded: String(10 + Math.floor(Math.random() * 90)),
  });
  check(res, { 'report created (201)': (r) => r.status === 201 });
  sleep(1);
}

export function readDashboards() {
  const responses = http.batch([
    ['GET', `${BASE}/api/inventory`],
    ['GET', `${BASE}/api/shelters`],
    ['GET', `${BASE}/api/dispatches/approval-queue`],
  ]);
  responses.forEach((r) => check(r, { 'dashboard read ok (200)': (x) => x.status === 200 }));
  sleep(1);
}
