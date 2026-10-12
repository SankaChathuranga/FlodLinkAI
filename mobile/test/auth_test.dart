import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:floodlink_mobile/auth/auth_provider.dart';
import 'package:floodlink_mobile/auth/route_guard.dart';
import 'package:floodlink_mobile/main.dart';
import 'package:floodlink_mobile/providers/report_provider.dart';
import 'package:http/http.dart' as http;

import 'helpers/test_app.dart';

Future<void> pumpApp(WidgetTester tester, TestBackend backend) async {
  await tester.pumpWidget(FloodLinkProviders(auth: backend.auth, api: backend.api, child: const FloodLinkApp()));
  await backend.auth.restore();
  await tester.pumpAndSettle();
}

http.Response loginOk(String role) => http.Response(sessionJson(role: role), 200);

void main() {
  group('Login form', () {
    testWidgets('validates empty and malformed fields without calling the API', (tester) async {
      final backend = TestBackend();
      await pumpApp(tester, backend);

      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pump();
      expect(find.text('Enter your email address.'), findsOneWidget);
      expect(find.text('Enter your password.'), findsOneWidget);

      await tester.enterText(find.byKey(const Key('login-email')), 'not-an-email');
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pump();
      expect(find.text('Enter a valid email address.'), findsOneWidget);
      expect(backend.requests, isEmpty);
    });

    testWidgets('wrong password shows an error and stays on sign-in', (tester) async {
      final backend = TestBackend(handler: (_) async => http.Response('{"title":"Invalid email or password."}', 401));
      await pumpApp(tester, backend);

      await tester.enterText(find.byKey(const Key('login-email')), 'volunteer@floodlink.lk');
      await tester.enterText(find.byKey(const Key('login-password')), 'wrong-password');
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();

      expect(find.text('Incorrect email or password.'), findsOneWidget);
      expect(backend.auth.isSignedIn, isFalse);
    });

    testWidgets('a depot manager lands on stock check-in', (tester) async {
      final backend = TestBackend(handler: (request) async =>
          request.url.path == '/api/auth/login' ? loginOk('DepotManager') : http.Response('[]', 200));
      await pumpApp(tester, backend);

      await tester.enterText(find.byKey(const Key('login-email')), 'depot@floodlink.lk');
      await tester.enterText(find.byKey(const Key('login-password')), 'right-password');
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();

      expect(find.text('Stock Check-in'), findsOneWidget);
      expect(backend.store.value, isNotNull);
    });

    testWidgets('a coordinator is told to use the web portal', (tester) async {
      final backend = TestBackend(handler: (_) async => loginOk('Coordinator'));
      await pumpApp(tester, backend);

      await tester.enterText(find.byKey(const Key('login-email')), 'coordinator@floodlink.lk');
      await tester.enterText(find.byKey(const Key('login-password')), 'right-password');
      await tester.tap(find.byKey(const Key('login-submit')));
      await tester.pumpAndSettle();

      expect(find.textContaining('web portal'), findsOneWidget);
      expect(backend.store.value, isNull);
    });
  });

  group('Registration form', () {
    testWidgets('validates every field before calling the API', (tester) async {
      final backend = TestBackend();
      await pumpApp(tester, backend);
      await tester.tap(find.text('New volunteer? Create an account'));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('register-phone')), '12');
      await tester.enterText(find.byKey(const Key('register-password')), 'short');
      await tester.enterText(find.byKey(const Key('register-confirm')), 'different');
      await tester.ensureVisible(find.byKey(const Key('register-submit')));
      await tester.tap(find.byKey(const Key('register-submit')));
      await tester.pump();

      expect(find.text('Enter your name.'), findsOneWidget);
      expect(find.text('Enter your email address.'), findsOneWidget);
      expect(find.textContaining('Phone must be'), findsOneWidget);
      expect(find.textContaining('at least 8 characters'), findsOneWidget);
      expect(find.text('Passwords do not match.'), findsOneWidget);
      expect(backend.requests, isEmpty);
    });
  });

  group('Route guard', () {
    const volunteer = AuthUser(id: 1, name: 'V', email: 'v@x.lk', role: 'Volunteer');

    test('signed out → login; public pages stay open', () {
      final auth = TestBackend().auth;
      return auth.restore().then((_) {
        expect(authRedirect(auth, '/'), '/login');
        expect(authRedirect(auth, '/my-submitted-reports'), '/login');
        expect(authRedirect(auth, '/register'), isNull);
      });
    });

    test('roles stay on their own screens', () async {
      final v = TestBackend(storedSession: sessionJson(role: 'Volunteer')).auth;
      final d = TestBackend(storedSession: sessionJson(role: 'DepotManager')).auth;
      final a = TestBackend(storedSession: sessionJson(role: 'Admin')).auth;
      await Future.wait([v.restore(), d.restore(), a.restore()]);

      expect(authRedirect(v, '/stock-check-in'), '/');
      expect(authRedirect(v, '/login'), homeFor(volunteer));
      expect(authRedirect(d, '/'), '/stock-check-in');
      expect(authRedirect(d, '/stock-check-in'), isNull);
      expect(authRedirect(a, '/stock-check-in'), isNull);
      expect(authRedirect(a, '/'), isNull);
    });

    test('an expired stored session starts signed out', () async {
      final backend = TestBackend(storedSession: sessionJson(validFor: const Duration(minutes: -1)));
      await backend.auth.restore();
      expect(backend.auth.isSignedIn, isFalse);
      expect(backend.store.value, isNull);
    });

    testWidgets('logout clears storage and returns to sign-in', (tester) async {
      final backend = TestBackend(storedSession: sessionJson());
      await pumpApp(tester, backend);
      expect(find.text('FloodLink Field Reports'), findsOneWidget);

      await tester.tap(find.byTooltip('Sign out'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('login-submit')), findsOneWidget);
      expect(backend.store.value, isNull);
    });
  });

  group('API client', () {
    test('providers send the bearer token, and a 401 signs the user out', () async {
      final backend = TestBackend(
        storedSession: sessionJson(),
        handler: (request) async => request.url.path == '/api/reports/mine'
            ? http.Response(jsonEncode([
                {'id': 7, 'shelterId': 1, 'needType': 'Water', 'quantityNeeded': 5, 'urgencyLevel': 60,
                 'status': 'Triaged', 'workflowState': 'PendingApproval', 'decision': null}
              ]), 200)
            : http.Response('', 401),
      );
      await backend.auth.restore();
      final reports = ReportProvider(backend.api);

      await reports.fetchReports();
      expect(backend.requests.single.headers['Authorization'], 'Bearer token-Volunteer');
      expect(reports.reports.single.workflowState, 'PendingApproval');

      await reports.fetchShelters(); // answered with 401
      expect(backend.auth.isSignedIn, isFalse);
      expect(backend.store.value, isNull);
    });
  });
}
