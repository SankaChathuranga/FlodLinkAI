import 'dart:convert';

import 'package:floodlink_mobile/providers/inventory_provider.dart';
import 'package:floodlink_mobile/screens/stock_check_in_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'helpers/test_app.dart';

// 6.25: the depot manager's stock check-in form validates before it sends anything,
// then sends the received quantity for the chosen item.

String page(List<Map<String, Object?>> items) =>
    jsonEncode({'items': items, 'page': 1, 'pageSize': 100, 'totalItems': items.length, 'totalPages': 1});

final depots = page([
  {'id': 1, 'name': 'Colombo Central Depot'},
  {'id': 2, 'name': 'Kaduwela Supply Depot'},
]);

final colomboStock = page([
  {'id': 1, 'depotId': 1, 'itemName': 'Water', 'unit': 'bottles', 'quantityAvailable': 600},
  {'id': 2, 'depotId': 1, 'itemName': 'Food', 'unit': 'packs', 'quantityAvailable': 250},
]);

TestBackend stockBackend({int checkInStatus = 200}) => TestBackend(
      storedSession: sessionJson(role: 'DepotManager', id: 3),
      handler: (request) async {
        final path = request.url.path;
        if (path == '/api/depots') return http.Response(depots, 200);
        if (path == '/api/inventory') return http.Response(colomboStock, 200);
        if (path.endsWith('/check-in')) {
          return http.Response(jsonEncode({'id': 1, 'quantityAvailable': 625}), checkInStatus);
        }
        return http.Response('{}', 404);
      },
    );

Future<void> pumpCheckIn(WidgetTester tester, TestBackend backend) async {
  await backend.auth.restore();
  await tester.pumpWidget(ChangeNotifierProvider(
    create: (_) => InventoryProvider(backend.api),
    child: const MaterialApp(home: StockCheckInScreen()),
  ));
  await tester.pumpAndSettle(); // depots load after the first frame
}

Future<void> choose(WidgetTester tester, String label, String option) async {
  await tester.tap(find.widgetWithText(DropdownButtonFormField<int>, label));
  await tester.pumpAndSettle();
  await tester.tap(find.text(option).last);
  await tester.pumpAndSettle();
}

List<http.Request> checkIns(TestBackend backend) =>
    backend.requests.where((r) => r.url.path.endsWith('/check-in')).toList();

void main() {
  testWidgets('loads depots from the paged API', (tester) async {
    final backend = stockBackend();
    await pumpCheckIn(tester, backend);

    expect(backend.requests.first.url.path, '/api/depots');
    expect(backend.requests.first.headers['Authorization'], 'Bearer token-DepotManager');
    await tester.tap(find.widgetWithText(DropdownButtonFormField<int>, 'Depot'));
    await tester.pumpAndSettle();
    expect(find.text('Kaduwela Supply Depot'), findsWidgets);
  });

  testWidgets('submitting an empty form shows every error and sends nothing', (tester) async {
    final backend = stockBackend();
    await pumpCheckIn(tester, backend);

    await tester.tap(find.text('Record check-in'));
    await tester.pumpAndSettle();

    expect(find.text('Select a depot.'), findsOneWidget);
    expect(find.text('Select an item.'), findsOneWidget);
    expect(find.text('Enter a quantity greater than zero.'), findsOneWidget);
    expect(checkIns(backend), isEmpty);
  });

  for (final bad in ['0', '-5', 'ten']) {
    testWidgets('quantity "$bad" is rejected', (tester) async {
      final backend = stockBackend();
      await pumpCheckIn(tester, backend);
      await choose(tester, 'Depot', 'Colombo Central Depot');
      await choose(tester, 'Stock item', 'Food (250.0 packs)');

      await tester.enterText(find.widgetWithText(TextFormField, 'Quantity received (packs)'), bad);
      await tester.tap(find.text('Record check-in'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a quantity greater than zero.'), findsOneWidget);
      expect(checkIns(backend), isEmpty);
    });
  }

  testWidgets('a valid check-in sends the quantity for the chosen item', (tester) async {
    final backend = stockBackend();
    await pumpCheckIn(tester, backend);
    await choose(tester, 'Depot', 'Colombo Central Depot');
    await choose(tester, 'Stock item', 'Water (600.0 bottles)');

    await tester.enterText(find.widgetWithText(TextFormField, 'Quantity received (bottles)'), '25');
    await tester.tap(find.text('Record check-in'));
    await tester.pumpAndSettle();

    final request = checkIns(backend).single;
    expect(request.url.path, '/api/inventory/1/check-in');
    expect(jsonDecode(request.body), {'quantityReceived': 25.0});
    expect(find.text('Stock check-in recorded.'), findsOneWidget);
  });

  testWidgets('a refused check-in shows the error', (tester) async {
    final backend = stockBackend(checkInStatus: 409);
    await pumpCheckIn(tester, backend);
    await choose(tester, 'Depot', 'Colombo Central Depot');
    await choose(tester, 'Stock item', 'Water (600.0 bottles)');

    await tester.enterText(find.widgetWithText(TextFormField, 'Quantity received (bottles)'), '25');
    await tester.tap(find.text('Record check-in'));
    await tester.pumpAndSettle();

    expect(find.text('Stock update failed (HTTP 409).'), findsOneWidget);
    expect(find.text('Stock check-in recorded.'), findsNothing);
  });
}
