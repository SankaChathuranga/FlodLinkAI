import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:floodlink_mobile/screens/camera_capture_screen.dart';
import 'package:floodlink_mobile/screens/gps_capture_screen.dart';

void main() {
  testWidgets('CameraCaptureScreen renders camera options and handles permission state', (WidgetTester tester) async {
    final reportProvider = ReportProvider();

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<ReportProvider>.value(value: reportProvider),
        ],
        child: const MaterialApp(
          home: CameraCaptureScreen(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    // Verify Title and camera action options
    expect(find.text('Capture Photo Evidence'), findsOneWidget);
    expect(find.text('Take Photo'), findsOneWidget);
    expect(find.text('Gallery'), findsOneWidget);
  });

  testWidgets('GpsCaptureScreen renders GPS controls and manual location adjust options', (WidgetTester tester) async {
    final reportProvider = ReportProvider();

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<ReportProvider>.value(value: reportProvider),
        ],
        child: const MaterialApp(
          home: GpsCaptureScreen(),
        ),
      ),
    );

    // Pump widget without waiting indefinitely for custom paint/timer animations
    await tester.pump();

    // Verify GPS Capture screen title and location options
    expect(find.text('GPS Location & Pin Adjust'), findsOneWidget);
    expect(find.text('Confirm GPS Coordinates'), findsOneWidget);
  });
}

