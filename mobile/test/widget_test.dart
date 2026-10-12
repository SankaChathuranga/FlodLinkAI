import 'package:flutter_test/flutter_test.dart';

import 'package:floodlink_mobile/main.dart';

import 'helpers/test_app.dart';

void main() {
  testWidgets('signed-out app opens on the sign-in screen', (WidgetTester tester) async {
    final backend = TestBackend();
    await tester.pumpWidget(FloodLinkProviders(auth: backend.auth, api: backend.api, child: const FloodLinkApp()));
    await backend.auth.restore();
    await tester.pumpAndSettle();

    expect(find.text('Sign in'), findsWidgets);
    expect(find.text('FloodLink Field Reports'), findsNothing);
  });

  testWidgets('signed-in volunteer opens on the field reports home', (WidgetTester tester) async {
    final backend = TestBackend(storedSession: sessionJson());
    await tester.pumpWidget(FloodLinkProviders(auth: backend.auth, api: backend.api, child: const FloodLinkApp()));
    await backend.auth.restore();
    await tester.pumpAndSettle();

    expect(find.text('FloodLink Field Reports'), findsOneWidget);
  });
}
