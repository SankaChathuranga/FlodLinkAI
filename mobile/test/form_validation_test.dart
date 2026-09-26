import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:floodlink_mobile/screens/new_report_screen.dart';

void main() {
  testWidgets('NewReportScreen renders all form fields and action buttons', (WidgetTester tester) async {
    final reportProvider = ReportProvider();

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<ReportProvider>.value(value: reportProvider),
        ],
        child: const MaterialApp(
          home: NewReportScreen(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    // Verify Title and core form fields exist
    expect(find.text('Submit Field Report'), findsOneWidget);
    expect(find.text('Emergency Need Type'), findsOneWidget);
    expect(find.text('Quantity Required'), findsOneWidget);
    expect(find.text('Submit Report'), findsOneWidget);
  });

  testWidgets('NewReportScreen validates empty quantity field on submit', (WidgetTester tester) async {
    final reportProvider = ReportProvider();

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<ReportProvider>.value(value: reportProvider),
        ],
        child: const MaterialApp(
          home: NewReportScreen(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    // Clear quantity input text box
    final quantityField = find.byType(TextFormField).first;
    await tester.enterText(quantityField, '');
    await tester.pumpAndSettle();

    // Tap submit button
    final submitButton = find.text('Submit Report');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    // Verify validation error message is displayed
    expect(find.text('Please enter quantity needed.'), findsOneWidget);
  });

  testWidgets('NewReportScreen validates non-positive quantity on submit', (WidgetTester tester) async {
    final reportProvider = ReportProvider();

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<ReportProvider>.value(value: reportProvider),
        ],
        child: const MaterialApp(
          home: NewReportScreen(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    // Enter negative quantity
    final quantityField = find.byType(TextFormField).first;
    await tester.enterText(quantityField, '-5');
    await tester.pumpAndSettle();

    // Tap submit button
    final submitButton = find.text('Submit Report');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    // Verify validation error message for positive quantity
    expect(find.text('Quantity must be greater than 0.'), findsOneWidget);
  });
}
