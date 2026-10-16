import 'dart:convert';

import 'package:floodlink_mobile/api/api_client.dart';
import 'package:floodlink_mobile/auth/auth_provider.dart';
import 'package:floodlink_mobile/auth/token_store.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const testBaseUrl = 'http://api.test';

/// A stored session for [role], as AuthProvider saves it after login.
String sessionJson({String role = 'Volunteer', int id = 1, Duration validFor = const Duration(hours: 8)}) =>
    jsonEncode({
      'token': 'token-$role',
      'expiresAt': DateTime.now().add(validFor).toUtc().toIso8601String(),
      'user': {'id': id, 'name': 'Test $role', 'email': '${role.toLowerCase()}@floodlink.lk', 'role': role},
    });

/// Auth + API client wired to a fake HTTP backend. [handler] answers every request.
class TestBackend {
  TestBackend({MockClientHandler? handler, String? storedSession})
      : store = MemoryTokenStore(storedSession) {
    final client = MockClient((request) async {
      requests.add(request);
      return handler != null ? handler(request) : http.Response('[]', 200);
    });
    auth = AuthProvider(baseUrl: testBaseUrl, store: store, client: client);
    api = ApiClient(baseUrl: testBaseUrl, auth: auth, client: client);
  }

  final MemoryTokenStore store;
  final List<http.Request> requests = [];
  late final AuthProvider auth;
  late final ApiClient api;
}
