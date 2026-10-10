// PERF-004: stress test — ramp report submissions with no think time until latency degrades.
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5055';

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

export default function () {
  const res = http.post(`${BASE}/api/reports`, {
    ShelterId: '1', ReportedBy: '1', NeedType: 'Water', QuantityNeeded: '25',
  });
  check(res, { 'report created (201)': (r) => r.status === 201 });
}
