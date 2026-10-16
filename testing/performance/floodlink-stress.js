// PERF-004: stress test — ramp report submissions with no think time until latency degrades.
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5055';

// Every endpoint needs a token (Phase 2): sign the demo accounts in once.
function login(email) {
  const res = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email, password: __ENV.DEMO_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } });
  return res.json('token');
}

export function setup() {
  return { volunteer: login('volunteer@floodlink.lk'), coordinator: login('coordinator@floodlink.lk') };
}

export const options = {
  stages: [
    { duration: '20s', target: 100 },
    { duration: '20s', target: 200 },
    { duration: '20s', target: 400 },
    { duration: '20s', target: 0 },
  ],
  thresholds: { http_req_failed: ['rate<0.05'], http_req_duration: ['p(95)<2000'] },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function (tokens) {
  const res = http.post(`${BASE}/api/reports`, {
    ShelterId: '1', NeedType: 'Water', QuantityNeeded: '25',
  }, { headers: { Authorization: `Bearer ${tokens.volunteer}` } });
  check(res, { 'report created (201)': (r) => r.status === 201 });
}
