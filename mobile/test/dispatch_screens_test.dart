import 'dart:convert';

import 'package:floodlink_mobile/api/api_client.dart';
import 'package:floodlink_mobile/screens/delivery_confirmation_screen.dart';
import 'package:floodlink_mobile/screens/dispatch_status_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'helpers/test_app.dart';

// 6.27: the dispatch status lookup and the delivery confirmation screens.

const runId = '741984fa-7614-4107-9871-1f77126cdc5d';

Future<TestBackend> pump(WidgetTester tester, Widget screen, MockHandler handler) async {
  final backend = TestBackend(storedSession: sessionJson(), handler: handler);
  await backend.auth.restore();
  await tester.pumpWidget(Provider<ApiClient>.value(
    value: backend.api,
    child: MaterialApp(home: screen),
  ));
  return backend;
}

typedef MockHandler = Future<http.Response> Function(http.Request request);

String dispatch({String? decision, String? notes, bool delivered = false}) => jsonEncode({
      'workflowRunId': runId,
      'state': decision ?? 'PendingApproval',
      'decision': decision,
      'notes': notes,
      'dispatchedAt': decision == 'Approved' ? '2026-10-15T06:10:00Z' : null,
      'isDelivered': delivered,
      'deliveredAt': delivered ? '2026-10-15T08:00:00Z' : null,
    });

void main() {
  group('DispatchStatusScreen', () {
    testWidgets('asks for a run id before calling the API', (tester) async {
      final backend = await pump(tester, const DispatchStatusScreen(), (_) async => http.Response('{}', 200));

      await tester.tap(find.text('Look up'));
      await tester.pump();

      expect(find.text('Enter a workflow run id first.'), findsOneWidget);
      expect(backend.requests, isEmpty);
    });

    testWidgets('a rejected run shows the coordinator\'s reason', (tester) async {
      final backend = await pump(tester, const DispatchStatusScreen(),
          (_) async => http.Response(dispatch(decision: 'Rejected', notes: 'Road to the shelter is closed.'), 200));

      await tester.enterText(find.byType(TextField), runId);
      await tester.tap(find.text('Look up'));
      await tester.pumpAndSettle();

      expect(backend.requests.single.url.path, '/api/dispatches/by-workflow/$runId');
      expect(find.text('Rejected'), findsOneWidget);
      expect(find.text('Road to the shelter is closed.'), findsOneWidget);
    });

    testWidgets('a delivered run is marked delivered', (tester) async {
      await pump(tester, const DispatchStatusScreen(),
          (_) async => http.Response(dispatch(decision: 'Approved', notes: 'Go.', delivered: true), 200));

      await tester.enterText(find.byType(TextField), runId);
      await tester.tap(find.text('Look up'));
      await tester.pumpAndSettle();

      expect(find.text('Delivered ✓'), findsOneWidget);
    });

    testWidgets('a run the volunteer cannot see shows the API message', (tester) async {
      await pump(tester, const DispatchStatusScreen(), (_) async => http.Response(
          jsonEncode({'error': 'WORKFLOW_NOT_FOUND', 'message': 'No workflow run with id $runId.'}), 404));

      await tester.enterText(find.byType(TextField), runId);
      await tester.tap(find.text('Look up'));
      await tester.pumpAndSettle();

      expect(find.text('No workflow run with id $runId.'), findsOneWidget);
    });
  });

  group('DeliveryConfirmationScreen', () {
    testWidgets('asks for a run id before calling the API', (tester) async {
      final backend = await pump(tester, const DeliveryConfirmationScreen(), (_) async => http.Response('{}', 200));

      await tester.tap(find.byIcon(Icons.task_alt));
      await tester.pump();

      expect(find.text('Enter the workflow run id you delivered to.'), findsOneWidget);
      expect(backend.requests, isEmpty);
    });

    testWidgets('confirms the delivery with the notes and shows success', (tester) async {
      final backend = await pump(
        tester,
        const DeliveryConfirmationScreen(workflowRunId: runId),
        (_) async => http.Response(
            jsonEncode({'workflowRunId': runId, 'state': 'Approved', 'isDelivered': true, 'notes': 'All received.'}), 200),
      );

      await tester.enterText(find.widgetWithText(TextField, 'Notes (optional)'), 'All received.');
      await tester.tap(find.byIcon(Icons.task_alt));
      await tester.pumpAndSettle();

      final request = backend.requests.single;
      expect(request.method, 'POST');
      expect(request.url.path, '/api/dispatches/$runId/confirm-delivery');
      expect(jsonDecode(request.body), {'notes': 'All received.', 'photoReference': null});
      expect(find.text('Delivery confirmed'), findsOneWidget);
    });

    testWidgets('a run that is not approved yet shows the API error', (tester) async {
      await pump(
        tester,
        const DeliveryConfirmationScreen(workflowRunId: runId),
        (_) async => http.Response(
            jsonEncode({'error': 'INVALID_STATE', 'message': 'Delivery confirmation requires state Approved.'}), 409),
      );

      await tester.tap(find.byIcon(Icons.task_alt));
      await tester.pumpAndSettle();

      expect(find.text('Delivery confirmation requires state Approved.'), findsOneWidget);
      expect(find.text('Delivery confirmed'), findsNothing);
    });
  });
}
