import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import 'token_store.dart';

/// Roles that may use the mobile app. Coordinators use the web portal.
const mobileRoles = {'Volunteer', 'DepotManager', 'Admin'};

class AuthUser {
  const AuthUser({
    required this.id,
    required this.name,
    required this.email,
    required this.role,
  });

  final int id;
  final String name;
  final String email;
  final String role;

  bool get isDepotManager => role == 'DepotManager';
  bool get isAdmin => role == 'Admin';

  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
        id: json['id'] as int,
        name: json['name'] as String? ?? '',
        email: json['email'] as String? ?? '',
        role: json['role'] as String? ?? '',
      );

  Map<String, dynamic> toJson() => {'id': id, 'name': name, 'email': email, 'role': role};
}

/// Error shown on the login / registration forms.
class AuthException implements Exception {
  const AuthException(this.message);
  final String message;

  @override
  String toString() => message;
}

enum AuthStatus { unknown, signedOut, signedIn }

/// Who is signed in. The session is persisted through [TokenStore] (secure storage),
/// restored on start-up with [restore], and cleared by [logout] or any 401 from the API.
class AuthProvider extends ChangeNotifier {
  AuthProvider({
    required this.baseUrl,
    required TokenStore store,
    http.Client? client,
  })  : _store = store,
        _client = client ?? http.Client();

  final String baseUrl;
  final TokenStore _store;
  final http.Client _client;

  AuthStatus _status = AuthStatus.unknown;
  String? _token;
  DateTime? _expiresAt;
  AuthUser? _user;

  AuthStatus get status => _status;
  String? get token => _token;
  AuthUser? get user => _user;
  bool get isSignedIn => _status == AuthStatus.signedIn;

  /// Loads a saved, unexpired session. Call once at start-up.
  Future<void> restore() async {
    try {
      final raw = await _store.read();
      if (raw != null) {
        final json = jsonDecode(raw) as Map<String, dynamic>;
        final expiresAt = DateTime.parse(json['expiresAt'] as String);
        if (expiresAt.isAfter(DateTime.now())) {
          _apply(json);
          return;
        }
      }
    } catch (_) {
      // Corrupt or unreadable storage: start signed out.
    }
    await _store.clear();
    _setSignedOut();
  }

  Future<void> login(String email, String password) async {
    final response = await _post('/api/auth/login', {'email': email.trim(), 'password': password});
    if (response.statusCode == 401) {
      throw const AuthException('Incorrect email or password.');
    }
    await _acceptSession(response);
  }

  /// Self-registration always creates a Volunteer account.
  Future<void> register({
    required String name,
    required String email,
    required String phone,
    required String password,
  }) async {
    final response = await _post('/api/auth/register', {
      'name': name.trim(),
      'email': email.trim(),
      'phone': phone.trim(),
      'password': password,
    });
    if (response.statusCode == 409) {
      throw const AuthException('An account with this email already exists.');
    }
    if (response.statusCode == 400) {
      throw AuthException(_validationMessage(response.body) ?? 'Check the details and try again.');
    }
    await _acceptSession(response);
  }

  Future<void> logout() async {
    await _store.clear();
    _setSignedOut();
  }

  /// Called by the API client when the server rejects the token.
  Future<void> handleUnauthorized() async {
    if (_status == AuthStatus.signedIn) {
      await logout();
    }
  }

  Future<http.Response> _post(String path, Map<String, dynamic> body) async {
    try {
      return await _client
          .post(
            Uri.parse('$baseUrl$path'),
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode(body),
          )
          .timeout(const Duration(seconds: 15));
    } catch (_) {
      throw const AuthException('Cannot reach the FloodLink server. Check your connection.');
    }
  }

  Future<void> _acceptSession(http.Response response) async {
    if (response.statusCode == 429) {
      throw const AuthException('Too many attempts. Wait a minute and try again.');
    }
    if (response.statusCode != 200 && response.statusCode != 201) {
      throw AuthException('Sign-in failed (HTTP ${response.statusCode}). Try again.');
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    final role = (json['user'] as Map<String, dynamic>)['role'] as String?;
    if (!mobileRoles.contains(role)) {
      throw const AuthException('Coordinator accounts use the FloodLink web portal.');
    }
    await _store.write(jsonEncode(json));
    _apply(json);
  }

  void _apply(Map<String, dynamic> json) {
    _token = json['token'] as String;
    _expiresAt = DateTime.parse(json['expiresAt'] as String);
    _user = AuthUser.fromJson(json['user'] as Map<String, dynamic>);
    _status = AuthStatus.signedIn;
    notifyListeners();
  }

  void _setSignedOut() {
    _token = null;
    _expiresAt = null;
    _user = null;
    _status = AuthStatus.signedOut;
    notifyListeners();
  }

  /// True when the stored token has passed its expiry time.
  bool get isExpired => _expiresAt != null && !_expiresAt!.isAfter(DateTime.now());

  static String? _validationMessage(String body) {
    try {
      final json = jsonDecode(body) as Map<String, dynamic>;
      final errors = json['errors'] as Map<String, dynamic>?;
      final first = errors?.values.whereType<List>().expand((e) => e).firstOrNull;
      return first?.toString() ?? json['message'] as String?;
    } catch (_) {
      return null;
    }
  }
}
