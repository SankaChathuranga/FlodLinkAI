import 'dart:convert';

import 'package:floodlink_mobile/providers/inventory_provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'helpers/test_app.dart';

// 6.24: providers against a mocked HTTP client — what they request, how they parse the API's
// responses (lists and pages), and the error they surface when a call fails.

void main() {
  group('InventoryProvider', () {
    test('reads depots and stock from paged responses', () async {
      final backend = TestBackend(
        storedSession: sessionJson(role: 'DepotManager', id: 3),
        handler: (request) async => http.Response(
          jsonEncode({
            'items': request.url.path == '/api/depots'
                ? [
                    {'id': 2, 'name': 'Kaduwela Supply Depot'}
                  ]
                : [
                    {'id': 4, 'depotId': 2, 'itemName': 'Medical', 'unit': 'kits', 'quantityAvailable': 50}
                  ],
            'page': 1,
            'pageSize': 100,
            'totalItems': 1,
            'totalPages': 1,
          }),
          200,
        ),
      );
      await backend.auth.restore();
      final provider = InventoryProvider(backend.api);

      await provider.fetchDepots();
      await provider.fetchItems(2);

      expect(provider.depots.single.name, 'Kaduwela Supply Depot');
      expect(provider.items.single.itemName, 'Medical');
      expect(provider.items.single.quantityAvailable, 50);
      expect(provider.error, isNull);
      expect(backend.requests[1].url.queryParameters, {'depotId': '2', 'sort': 'item', 'pageSize': '100'});
    });

    test('a forbidden depot list gives a clear error', () async {
      final backend = TestBackend(
        storedSession: sessionJson(role: 'DepotManager', id: 3),
        handler: (_) async => http.Response('{}', 403),
      );
      await backend.auth.restore();
      final provider = InventoryProvider(backend.api);

      await provider.fetchDepots();

      expect(provider.depots, isEmpty);
      expect(provider.error, 'Your account cannot view depots.');
      expect(provider.isLoadingDepots, isFalse);
    });
  });

  group('ReportProvider', () {
    test('reads the volunteer\'s own reports with their workflow stage', () async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (_) async => http.Response(
          jsonEncode([
            {
              'id': 7,
              'shelterId': 2,
              'shelterName': 'Kaduwela Community Hall',
              'needType': 'Medical',
              'quantityNeeded': 5,
              'urgencyLevel': 85,
              'notes': 'Insulin needed.',
              'status': 'InPlan',
              'createdAt': '2026-10-15T06:00:00Z',
              'workflowRunId': '741984fa-7614-4107-9871-1f77126cdc5d',
              'workflowState': 'Approved',
              'decision': 'Approved',
            }
          ]),
          200,
        ),
      );
      await backend.auth.restore();
      final provider = ReportProvider(backend.api);

      await provider.fetchReports();

      expect(backend.requests.single.url.path, '/api/reports/mine');
      expect(backend.requests.single.headers['Authorization'], 'Bearer token-Volunteer');
      final report = provider.reports.single;
      expect(report.id, 7);
      expect(report.needType, 'Medical');
      expect(report.workflowRunId, '741984fa-7614-4107-9871-1f77126cdc5d');
      expect(provider.reportsError, isNull);
    });

    test('a server error is reported, not shown as an empty list', () async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (_) async => http.Response('{}', 500),
      );
      await backend.auth.restore();
      final provider = ReportProvider(backend.api);

      await provider.fetchReports();

      expect(provider.reports, isEmpty);
      expect(provider.reportsError, 'Failed to load reports (HTTP 500)');
    });
  });
}
