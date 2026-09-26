import 'package:flutter_test/flutter_test.dart';
import 'package:floodlink_mobile/main.dart';
import 'package:floodlink_mobile/state/app_state.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets('Field home renders entry cards', (tester) async {
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => AppState(),
        child: const FloodLinkApp(),
      ),
    );

    expect(find.text('FloodLink Field'), findsOneWidget);
    expect(find.text('Check dispatch status'), findsOneWidget);
    expect(find.text('Confirm delivery'), findsOneWidget);
  });
}