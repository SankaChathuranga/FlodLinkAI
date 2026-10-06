import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:floodlink_mobile/main.dart';
import 'package:floodlink_mobile/state/app_state.dart';
import 'package:floodlink_mobile/providers/inventory_provider.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';

void main() {
  testWidgets('field reports home screen renders', (WidgetTester tester) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => AppState()),
          ChangeNotifierProvider(create: (_) => ReportProvider()),
          ChangeNotifierProvider(create: (_) => InventoryProvider()),
        ],
        child: const FloodLinkApp(),
      ),
    );
    await tester.pump();

    expect(find.text('FloodLink Field Reports'), findsOneWidget);
  });
}
