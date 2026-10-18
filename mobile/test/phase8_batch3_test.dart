import 'dart:convert';

import 'package:floodlink_mobile/offline/report_queue.dart';
import 'package:floodlink_mobile/providers/inventory_provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:floodlink_mobile/screens/reservation_confirmation_screen.dart';
import 'package:floodlink_mobile/screens/stock_check_in_screen.dart';
import 'package:floodlink_mobile/stock/stock_label.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'helpers/test_app.dart';

// 8.05 reserved-stock check, 8.06 stock labels, 8.09 offline report queue.

String page(List<Map<String, Object?>> items) =>
    jsonEncode({'items': items, 'page': 1, 'pageSize': 100, 'totalItems': items.length, 'totalPages': 1});

/// The multipart form fields of a captured report submission.
Map<String, String> formFields(http.Request request) {
  final fields = <String, String>{};
  final body = request.body;
  for (final m in RegExp(r'name="([^"]+)"\r\n\r\n([^\r]*)\r\n').allMatches(body)) {
    fields[m.group(1)!] = m.group(2)!;
  }
  return fields;
}

void main() {
  test('stock label codes', () {
    expect(parseStockLabel('FLI:42'), 42);
    expect(parseStockLabel(' FLI:7 '), 7);
    expect(parseStockLabel('FLI:'), isNull);
    expect(parseStockLabel('https://example.com'), isNull);
    expect(parseStockLabel(null), isNull);
  });

  group('offline report queue', () {
    test('a report without a connection is queued, then sent once with the same reference', () async {
      var online = false;
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (request) async {
          if (!online) throw http.ClientException('Failed host lookup');
          if (request.url.path == '/api/reports/mine') return http.Response('[]', 200);
          return http.Response(jsonEncode({'id': 9}), 201);
        },
      );
      await backend.auth.restore();
      final store = MemoryQueueStore();
      final provider = ReportProvider(backend.api, queue: ReportQueue(store))..setSelectedGps(6.93, 79.98);
      addTearDown(provider.dispose);

      final outcome = await provider.submitReport(shelterId: 2, needType: 'Water', quantityNeeded: 12, notes: 'Tank cracked');

      expect(outcome, SubmitOutcome.queued);
      expect(provider.queuedReports, hasLength(1));
      expect(provider.selectedLat, isNull); // the form is cleared: the report is safely on the phone
      final saved = jsonDecode((await store.read())!) as List<dynamic>;
      expect(saved.single['needType'], 'Water');
      expect(saved.single['gpsLat'], 6.93);
      final reference = provider.queuedReports.single.clientReference;
      expect(reference, matches(RegExp(r'^[0-9a-f]{32}$')));

      online = true;
      await provider.sendQueuedReports();

      expect(provider.queuedReports, isEmpty);
      final sends = backend.requests.where((r) => r.url.path == '/api/reports').toList();
      expect(sends.map((r) => formFields(r)['ClientReference']).toSet(), {reference}); // same id every time
      expect(formFields(sends.last)['Notes'], 'Tank cracked');
    });

    test('a re-send the server already had (200) also leaves the queue', () async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (request) async => request.url.path == '/api/reports'
            ? http.Response(jsonEncode({'id': 9}), 200)
            : http.Response('[]', 200),
      );
      await backend.auth.restore();
      final queue = ReportQueue.memory();
      await queue.add(QueuedReport(clientReference: 'ab' * 16, shelterId: 1, needType: 'Food', quantityNeeded: 5, notes: '', queuedAt: DateTime.now()));
      final provider = ReportProvider(backend.api, queue: queue);
      addTearDown(provider.dispose);

      await provider.sendQueuedReports();

      expect(provider.queuedReports, isEmpty);
    });

    test('a report the server refuses is kept with the reason until discarded', () async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (_) async => http.Response(jsonEncode({'errors': {'ShelterId': ['Shelter with ID 99 does not exist.']}}), 400),
      );
      await backend.auth.restore();
      final queue = ReportQueue.memory();
      await queue.add(QueuedReport(clientReference: 'cd' * 16, shelterId: 99, needType: 'Food', quantityNeeded: 5, notes: '', queuedAt: DateTime.now()));
      final provider = ReportProvider(backend.api, queue: queue);
      addTearDown(provider.dispose);

      await provider.sendQueuedReports();
      expect(provider.queuedReports.single.lastError, 'Shelter with ID 99 does not exist.');

      final before = backend.requests.length;
      await provider.sendQueuedReports();
      expect(backend.requests.length, before); // refused reports are not retried

      await provider.discardQueuedReport('cd' * 16);
      expect(provider.queuedReports, isEmpty);
    });

    test('the queue survives a restart', () async {
      final store = MemoryQueueStore();
      await ReportQueue(store).add(QueuedReport(clientReference: 'ef' * 16, shelterId: 1, needType: 'Medical', quantityNeeded: 2, notes: 'n', queuedAt: DateTime.utc(2026, 10, 18)));

      final reloaded = await ReportQueue(store).load();

      expect(reloaded.single.needType, 'Medical');
      expect(reloaded.single.queuedAt.toUtc(), DateTime.utc(2026, 10, 18));
    });
  });

  group('reserved stock check', () {
    Map<String, Object?> line({double? counted}) => {
          'id': 7, 'workflowRunId': 'run-1', 'depotId': 1, 'depotName': 'Colombo Central Depot', 'shelterId': 1,
          'shelterName': 'Central Colombo Relief Center', 'itemName': 'Water', 'quantity': 40, 'status': 'Reserved',
          'countedQuantity': counted, 'confirmedBy': counted == null ? null : 'Kamal Perera',
        };

    Future<TestBackend> pumpChecks(WidgetTester tester) async {
      final backend = TestBackend(
        storedSession: sessionJson(role: 'DepotManager', id: 3),
        handler: (request) async {
          if (request.method == 'POST') {
            final counted = (jsonDecode(request.body)['countedQuantity'] as num).toDouble();
            return http.Response(jsonEncode(line(counted: counted)), 200);
          }
          return http.Response(page([line()]), 200);
        },
      );
      await backend.auth.restore();
      await tester.pumpWidget(ChangeNotifierProvider(
        create: (_) => InventoryProvider(backend.api),
        child: const MaterialApp(home: ReservationConfirmationScreen()),
      ));
      await tester.pumpAndSettle();
      return backend;
    }

    testWidgets('lists reserved lines at the manager\'s depots and confirms one', (tester) async {
      final backend = await pumpChecks(tester);

      expect(backend.requests.first.url.queryParameters, {'status': 'Reserved', 'managedByMe': 'true', 'pageSize': '100'});
      expect(find.text('Water × 40'), findsOneWidget);
      await tester.tap(find.text('All on the shelf'));
      await tester.pumpAndSettle();

      expect(jsonDecode(backend.requests.last.body), {'countedQuantity': 40.0, 'note': null});
      expect(find.text('Confirmed on the shelf by Kamal Perera'), findsOneWidget);
    });

    testWidgets('a short count is reported with a note', (tester) async {
      final backend = await pumpChecks(tester);

      await tester.tap(find.text('Report a different count'));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextFormField, 'Counted on the shelf'), '25');
      await tester.enterText(find.widgetWithText(TextFormField, 'Note (optional)'), '15 damaged');
      await tester.tap(find.text('Save count'));
      await tester.pumpAndSettle();

      expect(jsonDecode(backend.requests.last.body), {'countedQuantity': 25.0, 'note': '15 damaged'});
      expect(find.text('Shortfall reported: 25 of 40 on the shelf'), findsOneWidget);
    });

    testWidgets('a negative count is rejected before sending', (tester) async {
      final backend = await pumpChecks(tester);

      await tester.tap(find.text('Report a different count'));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextFormField, 'Counted on the shelf'), '-3');
      await tester.tap(find.text('Save count'));
      await tester.pumpAndSettle();

      expect(find.text('Enter the number you counted (0 or more).'), findsOneWidget);
      expect(backend.requests.where((r) => r.method == 'POST'), isEmpty);
    });
  });

  testWidgets('scanning a shelf label picks the depot and stock line', (tester) async {
    final backend = TestBackend(
      storedSession: sessionJson(role: 'DepotManager', id: 3),
      handler: (request) async {
        final path = request.url.path;
        if (path == '/api/depots') return http.Response(page([{'id': 1, 'name': 'Colombo Central Depot'}, {'id': 2, 'name': 'Kaduwela Supply Depot'}]), 200);
        if (path == '/api/inventory/4') {
          return http.Response(jsonEncode({'id': 4, 'depotId': 2, 'depot': {'id': 2, 'name': 'Kaduwela Supply Depot'}, 'itemName': 'Medical', 'unit': 'kits', 'quantityAvailable': 50}), 200);
        }
        if (path == '/api/inventory') {
          return http.Response(page([{'id': 4, 'depotId': 2, 'itemName': 'Medical', 'unit': 'kits', 'quantityAvailable': 50}]), 200);
        }
        return http.Response('{}', 404);
      },
    );
    await backend.auth.restore();
    await tester.pumpWidget(ChangeNotifierProvider(
      create: (_) => InventoryProvider(backend.api),
      child: MaterialApp(home: StockCheckInScreen(scanLabel: (_) async => parseStockLabel('FLI:4'))),
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Scan shelf label'));
    await tester.pumpAndSettle();

    expect(find.text('Scanned Medical at Kaduwela Supply Depot.'), findsOneWidget);
    expect(find.text('Kaduwela Supply Depot'), findsWidgets); // depot selected
    expect(find.text('Medical (50.0 kits)'), findsOneWidget); // stock line selected
    expect(find.text('Quantity received (kits)'), findsOneWidget);
  });
}
