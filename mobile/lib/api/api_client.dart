import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/auth_provider.dart';

/// The one HTTP helper every provider and API class uses. Adds
/// `Authorization: Bearer <token>` from [AuthProvider] and signs the user out on a 401.
class ApiClient {
  ApiClient({required this.baseUrl, required this.auth, http.Client? client})
      : _client = client ?? http.Client();

  final String baseUrl;
  final AuthProvider auth;
  final http.Client _client;

  static const _timeout = Duration(seconds: 15);

  Uri uri(String path) => Uri.parse('$baseUrl$path');

  Map<String, String> _headers({bool json = false}) => {
        if (json) 'Content-Type': 'application/json',
        if (auth.token != null) 'Authorization': 'Bearer ${auth.token}',
      };

  Future<http.Response> get(String path) =>
      _check(_client.get(uri(path), headers: _headers()).timeout(_timeout));

  Future<http.Response> postJson(String path, Object? body) => _check(
      _client.post(uri(path), headers: _headers(json: true), body: jsonEncode(body)).timeout(_timeout));

  Future<http.Response> putJson(String path, Object? body) => _check(
      _client.put(uri(path), headers: _headers(json: true), body: jsonEncode(body)).timeout(_timeout));

  /// Sends a multipart request (e.g. a report with a photo) with the auth header added.
  Future<http.Response> sendMultipart(http.MultipartRequest request) async {
    request.headers.addAll(_headers());
    final streamed = await _client.send(request).timeout(_timeout);
    return _check(http.Response.fromStream(streamed));
  }

  Future<http.Response> _check(Future<http.Response> pending) async {
    final response = await pending;
    if (response.statusCode == 401) {
      await auth.handleUnauthorized();
    }
    return response;
  }
}
