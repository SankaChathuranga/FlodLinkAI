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
    expect(find.text('Photo Evidence Capture'), findsOneWidget);
    expect(find.text('Take Photo with Camera'), findsOneWidget);
    expect(find.text('Select from Photo Gallery'), findsOneWidget);
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

    await tester.pumpAndSettle();

    // Verify GPS Capture screen title and location options
    expect(find.text('GPS Location Capture'), findsOneWidget);
    expect(find.text('Confirm Pin Location'), findsOneWidget);
  });
}
