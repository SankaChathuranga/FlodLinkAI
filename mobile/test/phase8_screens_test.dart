import 'dart:convert';

import 'package:floodlink_mobile/auth/route_guard.dart';
import 'package:floodlink_mobile/providers/inventory_provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:floodlink_mobile/screens/low_stock_screen.dart';
import 'package:floodlink_mobile/screens/shelter_status_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'helpers/test_app.dart';

// 8.04 low-stock alerts for the manager's depots; 8.07 shelter status for volunteers.

String page(List<Map<String, Object?>> items) =>
    jsonEncode({'items': items, 'page': 1, 'pageSize': 100, 'totalItems': items.length, 'totalPages': 1});

void main() {
  group('LowStockScreen', () {
    Future<TestBackend> pumpLowStock(WidgetTester tester, Future<http.Response> Function(http.Request) handler) async {
      final backend = TestBackend(storedSession: sessionJson(role: 'DepotManager', id: 3), handler: handler);
      await backend.auth.restore();
      await tester.pumpWidget(ChangeNotifierProvider(
        create: (_) => InventoryProvider(backend.api),
        child: const MaterialApp(home: LowStockScreen()),
      ));
      await tester.pumpAndSettle();
      return backend;
    }

    testWidgets('asks only for the manager\'s own low stock and lists it', (tester) async {
      final backend = await pumpLowStock(tester, (_) async => http.Response(page([
            {'id': 4, 'depotId': 2, 'depot': {'id': 2, 'name': 'Kaduwela Supply Depot'}, 'itemName': 'Medical', 'unit': 'kits',
             'quantityAvailable': 50, 'quantityReserved': 45, 'reorderThreshold': 10},
            {'id': 2, 'depotId': 1, 'depot': {'id': 1, 'name': 'Colombo Central Depot'}, 'itemName': 'Food', 'unit': 'packs',
             'quantityAvailable': 0, 'quantityReserved': 0, 'reorderThreshold': 50},
          ]), 200));

      expect(backend.requests.single.url.queryParameters,
          {'managedByMe': 'true', 'lowStock': 'true', 'sort': 'quantity_asc', 'pageSize': '100'});
      expect(find.text('2 lines at or below the reorder level'), findsOneWidget);
      expect(find.text('Medical · Kaduwela Supply Depot'), findsOneWidget);
      expect(find.text('5 kits free (reorder level 10) · 45 held'), findsOneWidget);
      expect(find.byIcon(Icons.remove_shopping_cart_outlined), findsOneWidget); // Food has nothing free
    });

    testWidgets('says so when nothing is low', (tester) async {
      await pumpLowStock(tester, (_) async => http.Response(page([]), 200));
      expect(find.text('All stock at your depots is above its reorder level.'), findsOneWidget);
    });

    testWidgets('shows a load failure', (tester) async {
      await pumpLowStock(tester, (_) async => http.Response('{}', 500));
      expect(find.text('Failed to load low stock (HTTP 500).'), findsOneWidget);
    });

    test('the route guard keeps it on the depot manager side', () {
      expect(depotManagerPaths, contains('/low-stock'));
    });
  });

  group('ShelterStatusScreen', () {
    Future<void> pumpShelters(WidgetTester tester, Future<http.Response> Function(http.Request) handler) async {
      final backend = TestBackend(storedSession: sessionJson(), handler: handler);
      await backend.auth.restore();
      await tester.pumpWidget(ChangeNotifierProvider(
        create: (_) => ReportProvider(backend.api),
        child: const MaterialApp(home: ShelterStatusScreen()),
      ));
      await tester.pumpAndSettle();
    }

    testWidgets('lists shelters fullest first with occupancy and last update', (tester) async {
      final recent = DateTime.now().toUtc().subtract(const Duration(minutes: 5)).toIso8601String();
      await pumpShelters(tester, (_) async => http.Response(page([
            {'id': 3, 'name': 'Gampaha Primary School Shelter', 'capacity': 100, 'currentOccupancy': 45, 'status': 'Active', 'updatedAt': recent},
            {'id': 1, 'name': 'Central Colombo Relief Center', 'capacity': 200, 'currentOccupancy': 210, 'status': 'Active', 'updatedAt': recent},
            {'id': 2, 'name': 'Kaduwela Community Hall', 'capacity': 150, 'currentOccupancy': 0, 'status': 'Closed'},
          ]), 200));

      final names = tester.widgetList<Text>(find.byType(Text)).map((t) => t.data).whereType<String>()
          .where((t) => t.contains('Shelter') || t.contains('Center') || t.contains('Hall')).toList();
      expect(names.first, 'Central Colombo Relief Center');
      expect(find.text('210 / 200 people · 105% — over capacity'), findsOneWidget);
      expect(find.text('45 / 100 people · 45%'), findsOneWidget);
      expect(find.text('Updated 5 min ago'), findsNWidgets(2));
      expect(find.text('Last update unknown'), findsOneWidget);
      expect(find.text('Closed'), findsOneWidget);
    });

    testWidgets('shows the error when shelters cannot load', (tester) async {
      await pumpShelters(tester, (_) async => http.Response('{}', 503));
      expect(find.text('Failed to load shelters (HTTP 503)'), findsOneWidget);
    });
  });
}
