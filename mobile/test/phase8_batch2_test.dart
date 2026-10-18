import 'dart:convert';
import 'dart:typed_data';

import 'package:floodlink_mobile/api/api_client.dart';
import 'package:floodlink_mobile/api/workflow_api.dart';
import 'package:floodlink_mobile/maps/polyline.dart';
import 'package:floodlink_mobile/notifications/status_notifier.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:floodlink_mobile/screens/delivery_confirmation_screen.dart';
import 'package:floodlink_mobile/screens/gps_capture_screen.dart';
import 'package:floodlink_mobile/screens/route_map_screen.dart';
import 'package:floodlink_mobile/screens/workflow_status_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import 'helpers/test_app.dart';

// 8.11 route map, 8.08 map pin, 8.12 status notifications, 8.16 delivery photo.

const runId = '741984fa-7614-4107-9871-1f77126cdc5d';

Map<String, Object?> leg() => {
      'depotId': 2, 'depotName': 'Kaduwela Supply Depot', 'depotLatitude': 6.9061, 'depotLongitude': 79.9580,
      'shelterId': 2, 'shelterName': 'Kaduwela Community Hall', 'shelterLatitude': 6.9344, 'shelterLongitude': 79.9841,
      'distanceKm': 6.5, 'etaMinutes': 15, 'polyline': 'czci@ox_gN{oDcbD',
    };

String status(String state, {double? eta, String? note, bool delivered = false, bool withLegs = false}) => jsonEncode({
      'id': runId, 'state': state, 'stageLabel': state, 'progressPercent': 90,
      'isFinished': ['Approved', 'Rejected', 'Failed'].contains(state), 'awaitingApproval': state == 'PendingApproval',
      'isDelivered': delivered, 'updatedAt': '2026-10-18T06:00:00Z', 'coordinatorNote': note, 'etaMinutes': eta,
      'distanceKm': eta == null ? null : 6.5, 'routeLegs': withLegs ? [leg()] : [],
    });

WorkflowStatus parsed(String json) => WorkflowStatus.fromJson(jsonDecode(json) as Map<String, dynamic>);

class RecordingNotifier implements StatusNotifier {
  final sent = <String>[];

  @override
  Future<void> notify(String title, String body) async => sent.add('$title | $body');
}

void main() {
  test('decodes the reference polyline', () {
    final points = decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
    expect(points.map((p) => [p.latitude, p.longitude]), [
      [38.5, -120.2],
      [40.7, -120.95],
      [43.252, -126.453],
    ]);
    expect(decodePolyline('_p~iF'), isEmpty);
  });

  group('status change messages', () {
    test('an approval names the ETA and the coordinator note', () {
      final message = statusChangeMessage(parsed(status('PendingApproval')), parsed(status('Approved', eta: 47, note: 'Truck leaves now.')));
      expect(message?.title, 'Supplies approved — ETA 47 min');
      expect(message?.body, 'The coordinator approved the delivery. “Truck leaves now.”');
    });

    test('no message on the first poll, an unchanged stage, or an internal step', () {
      expect(statusChangeMessage(null, parsed(status('Approved'))), isNull);
      expect(statusChangeMessage(parsed(status('Routing')), parsed(status('Routing'))), isNull);
      expect(statusChangeMessage(parsed(status('Matching')), parsed(status('Routing'))), isNull);
    });

    test('a confirmed delivery is announced', () {
      expect(statusChangeMessage(parsed(status('Approved')), parsed(status('Approved', delivered: true)))?.title, 'Delivery confirmed');
    });
  });

  testWidgets('the tracker notifies on a stage change and opens the route map', (tester) async {
    final responses = [status('PendingApproval'), status('Approved', eta: 15, withLegs: true)];
    final backend = TestBackend(
      storedSession: sessionJson(),
      handler: (_) async => http.Response(responses.length > 1 ? responses.removeAt(0) : responses.first, 200),
    );
    await backend.auth.restore();
    final notifier = RecordingNotifier();
    await tester.pumpWidget(MaterialApp(
      home: WorkflowStatusScreen(
        workflowRunId: runId,
        pollInterval: const Duration(seconds: 1),
        api: WorkflowApi(backend.api),
        notifier: notifier,
      ),
    ));
    await tester.pump();
    await tester.pump();
    expect(notifier.sent, isEmpty); // first status is not a change

    await tester.pump(const Duration(seconds: 1));
    await tester.pump();
    expect(notifier.sent, ['Supplies approved — ETA 15 min | The coordinator approved the delivery.']);

    await tester.tap(find.text('View route on map'));
    await tester.pumpAndSettle();
    expect(find.byType(RouteMapScreen), findsOneWidget);
    expect(find.text('Kaduwela Supply Depot → Kaduwela Community Hall'), findsOneWidget);
    expect(find.text('6.5 km · about 15 min'), findsOneWidget);
    expect(find.byTooltip('Depot: Kaduwela Supply Depot'), findsOneWidget);
    expect(find.byTooltip('Shelter: Kaduwela Community Hall'), findsOneWidget);
  });

  testWidgets('tapping the GPS map moves the pin', (tester) async {
    await tester.binding.setSurfaceSize(const Size(500, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final provider = ReportProvider(TestBackend().api)..setSelectedGps(6.9271, 79.8612);
    await tester.pumpWidget(ChangeNotifierProvider<ReportProvider>.value(
      value: provider,
      child: const MaterialApp(home: GpsCaptureScreen()),
    ));
    await tester.pump();
    expect(find.text('Lat: 6.92710, Lng: 79.86120'), findsOneWidget);

    final map = tester.getCenter(find.byType(FlutterMap));
    await tester.tapAt(map + const Offset(60, 0)); // east of the centre
    await tester.pump(const Duration(milliseconds: 400)); // past the double-tap window

    final label = tester.widgetList<Text>(find.byType(Text)).map((t) => t.data ?? '').firstWhere((t) => t.startsWith('Lat:'));
    final lng = double.parse(label.split('Lng: ').last);
    expect(lng, greaterThan(79.8612));
  });

  group('delivery photo', () {
    Future<TestBackend> pumpDelivery(WidgetTester tester, {int uploadStatus = 201}) async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (request) async {
          if (request.url.path.endsWith('/delivery-photo')) {
            return http.Response(
                uploadStatus == 201
                    ? jsonEncode({'workflowRunId': runId, 'photoUrl': '/uploads/deliveries/abc.jpg'})
                    : jsonEncode({'error': 'INVALID_STATE', 'message': 'A delivery photo needs an Approved run.'}),
                uploadStatus);
          }
          return http.Response(jsonEncode({'workflowRunId': runId, 'state': 'Approved', 'isDelivered': true}), 200);
        },
      );
      await backend.auth.restore();
      await tester.pumpWidget(Provider<ApiClient>.value(
        value: backend.api,
        child: MaterialApp(
          home: DeliveryConfirmationScreen(
            workflowRunId: runId,
            pickPhoto: (_) async => XFile.fromData(Uint8List.fromList([0xFF, 0xD8, 0xFF]), mimeType: 'image/jpeg'),
          ),
        ),
      ));
      return backend;
    }

    testWidgets('uploads the photo, then confirms with its URL', (tester) async {
      final backend = await pumpDelivery(tester);

      await tester.tap(find.text('Take photo'));
      await tester.pump();
      expect(find.text('Photo attached: delivery.jpg'), findsOneWidget);

      await tester.tap(find.byIcon(Icons.task_alt));
      await tester.pumpAndSettle();

      expect(backend.requests.map((r) => r.url.path), ['/api/dispatches/$runId/delivery-photo', '/api/dispatches/$runId/confirm-delivery']);
      expect(jsonDecode(backend.requests.last.body), {'notes': null, 'photoReference': '/uploads/deliveries/abc.jpg'});
      expect(find.text('Delivery confirmed'), findsOneWidget);
    });

    testWidgets('a refused upload shows why and does not confirm', (tester) async {
      final backend = await pumpDelivery(tester, uploadStatus: 409);

      await tester.tap(find.text('Choose photo'));
      await tester.pump();
      await tester.tap(find.byIcon(Icons.task_alt));
      await tester.pumpAndSettle();

      expect(find.text('A delivery photo needs an Approved run.'), findsOneWidget);
      expect(backend.requests, hasLength(1));
    });
  });
}
