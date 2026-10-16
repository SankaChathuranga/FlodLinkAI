import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:floodlink_mobile/api/workflow_api.dart';
import 'package:floodlink_mobile/main.dart';
import 'package:floodlink_mobile/screens/workflow_status_screen.dart';
import 'package:http/http.dart' as http;

import 'helpers/test_app.dart';

const runId = '741984fa-7614-4107-9871-1f77126cdc5d';

String status(String state, {String? decision, String? note, double? eta, bool finished = false, bool delivered = false}) =>
    jsonEncode({
      'id': runId,
      'state': state,
      'stageLabel': 'Stage: $state',
      'progressPercent': finished ? 100 : 55,
      'isFinished': finished,
      'awaitingApproval': state == 'PendingApproval',
      'isDelivered': delivered,
      'updatedAt': '2026-10-15T06:00:00Z',
      'decision': decision,
      'coordinatorNote': note,
      'distanceKm': eta == null ? null : 12.5,
      'etaMinutes': eta,
    });

Future<void> pumpTracker(WidgetTester tester, TestBackend backend) async {
  await backend.auth.restore();
  await tester.pumpWidget(MaterialApp(
    home: WorkflowStatusScreen(
      workflowRunId: runId,
      pollInterval: const Duration(seconds: 1),
      api: WorkflowApi(backend.api),
    ),
  ));
  await tester.pump(); // first poll
  await tester.pump();
}

void main() {
  testWidgets('polls the status endpoint and shows the new stage', (tester) async {
    final responses = [status('Routing'), status('PendingApproval')];
    final backend = TestBackend(
      storedSession: sessionJson(),
      handler: (_) async => http.Response(responses.length > 1 ? responses.removeAt(0) : responses.first, 200),
    );
    await pumpTracker(tester, backend);

    expect(find.text('Stage: Routing'), findsOneWidget);
    expect(backend.requests.single.url.path, '/api/workflows/$runId/status');
    expect(backend.requests.single.headers['Authorization'], 'Bearer token-Volunteer');

    await tester.pump(const Duration(seconds: 1));
    await tester.pump();
    expect(find.text('Stage: PendingApproval'), findsOneWidget);
    expect(find.text('Awaiting coordinator approval'), findsOneWidget);
  });

  testWidgets('a rejection shows the coordinator note and polling stops', (tester) async {
    final backend = TestBackend(
      storedSession: sessionJson(),
      handler: (_) async => http.Response(status('Rejected', decision: 'Rejected', note: 'Road to the shelter is closed.', finished: true), 200),
    );
    await pumpTracker(tester, backend);

    expect(find.byKey(const Key('outcome')), findsOneWidget);
    expect(find.text('Rejected'), findsOneWidget);
    expect(find.text('Road to the shelter is closed.'), findsOneWidget);

    await tester.pump(const Duration(seconds: 3));
    expect(backend.requests, hasLength(1)); // settled: no more polling
  });

  testWidgets('an approval shows the ETA and a delivery button', (tester) async {
    final backend = TestBackend(
      storedSession: sessionJson(),
      handler: (_) async => http.Response(status('Approved', decision: 'Approved', note: 'Truck leaves now.', eta: 47, finished: true), 200),
    );
    await pumpTracker(tester, backend);

    expect(find.text('Supplies on the way — ETA about 47 min'), findsOneWidget);
    expect(find.text('Confirm delivery'), findsOneWidget);
    expect(find.text('Truck leaves now.'), findsOneWidget);
  });

  testWidgets('a run the volunteer cannot see shows a clear message', (tester) async {
    final backend = TestBackend(storedSession: sessionJson(), handler: (_) async => http.Response('', 404));
    await pumpTracker(tester, backend);

    expect(find.textContaining('not found'), findsOneWidget);
  });

  testWidgets('tapping a report in the list opens its status tracker', (tester) async {
    final backend = TestBackend(
      storedSession: sessionJson(),
      handler: (request) async {
        if (request.url.path == '/api/reports/mine') {
          return http.Response(jsonEncode([
            {'id': 9, 'shelterId': 2, 'needType': 'Water', 'quantityNeeded': 60, 'urgencyLevel': 95, 'status': 'Triaged',
             'workflowRunId': runId, 'workflowState': 'PendingApproval', 'decision': null},
          ]), 200);
        }
        if (request.url.path.endsWith('/status')) return http.Response(status('PendingApproval'), 200);
        return http.Response('[]', 200);
      },
    );
    await tester.pumpWidget(FloodLinkProviders(auth: backend.auth, api: backend.api, child: const FloodLinkApp()));
    await backend.auth.restore();
    await tester.pumpAndSettle();

    expect(find.text('Awaiting coordinator approval'), findsOneWidget);
    await tester.tap(find.byKey(const Key('report-status-9')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Supply request status'), findsOneWidget);
    expect(find.text('Stage: PendingApproval'), findsOneWidget);
    await tester.pumpWidget(const SizedBox()); // dispose the polling timer
  });
}
